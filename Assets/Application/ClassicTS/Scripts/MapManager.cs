using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public interface IMapData
	{
		int Width { get; }
		int Height { get; }
		int Count { get; }
		int[] Indices { get; }
	}

	public interface IMapManager : IMapData
	{
		Vector3 TileWorldPosition(int index);
		int WorldToMapIndex(Vector3 vec);
		Tile GetTile(int index);
		int GetStartTile();
		int GetEndTile();

		int FindAdjacentConsole(int nTile);
		Map CurrentMap { get; }
		Transform CurrentTransform { get; }

		int[] Waypoints { set; get; }
		string GetDefinitionAtIndex(int mapIndex);
		int GetWaypoint(int index);
		View GetView(int tile);
		Bounds GetTileGeometryBounds(int tileIndex);

		bool UpdateTileAt(int x, int z, string id, bool expand = true);
		void RefreshAttachmentInstance(MapAttachment attachment);
		void DestroyAttachmentInstance(MapAttachment attachment);

		void AddAttachment(MapAttachment attachment);
		bool RemoveAttachment(MapAttachment attachment);
		bool RemoveAttachments(MapAttachment[] attachmentArray);
		void RemoveAllAttachmentsOnTile(int tileIndex);

		void RefreshGeometry();

		MapAttachment[] attachments { get; set; }
		Waypoint[] waypointAttachments { get; }

		Action<IMapManager, bool, Vector3> OnMapEdited { get; set; }
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private Map currentMap;
		private int[] indices;

		private Tile[] mapTiles;

		public Map CurrentMap => currentMap;
		public Transform CurrentTransform => transform;

		private Action<IMapManager, bool, Vector3> onMapEdited;
		public Action<IMapManager, bool, Vector3> OnMapEdited
		{
			get => onMapEdited;
			set => onMapEdited = value;
		}

		public int Width => currentMap?.width ?? 0;
		public int Height => currentMap?.height ?? 0;
		public int Count => Width * Height;

		public int[] Indices => indices;

		public int[] Waypoints { get => currentMap?.waypoints; set { if (currentMap != null) currentMap.waypoints = value; } }
		public int GetWaypoint(int index) => currentMap?.GetWaypoint(index) ?? -1;

		private static MapManager instance;
		public static Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public static Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public static Vector3 LocalPosition(int tileIndex, Vector3 worldPosition) => instance == null || tileIndex < 0 ? worldPosition : worldPosition - instance.currentMap.TileWorldPosition(tileIndex);
		public static Vector3 WorldPosition(int tileIndex, Vector3 localPosition) => instance == null || tileIndex < 0 ? localPosition : localPosition + instance.currentMap.TileWorldPosition(tileIndex);

		public Vector3 TileWorldPosition(int index) => currentMap.TileWorldPosition(index);
		public int WorldToMapIndex(Vector3 vec) => currentMap.WorldToMapIndex(vec);

		public View GetView(int tile)
		{
			if (currentMap?.attachments == null || tile < 0 || tile >= currentMap.tiles.Length)
				return null;

			foreach (var att in currentMap.attachments)
			{
				if (att is View view && att.tile == tile)
					return view;
			}
			return null;
		}

		public Tile GetTile(int index)
		{
			if (index < 0 || index >= indices.Length || Width <= 0 || mapTiles == null)
				return default;

			int dataIndex = indices[index];
			return dataIndex >= 0 && dataIndex < mapTiles.Length
				? mapTiles[dataIndex]
				: default;
		}

		private void Awake()
		{
			indices = null;
			mapTiles = null;
		}

		private void OnDestroy()
		{
			currentMap?.CleanupAttachmentInstances();
			if (ReferenceEquals(MapAttachmentExtensions.CurrentMapManager, this))
				MapAttachmentExtensions.ClearActiveMapManager();
			instance = null;
		}

		private void Initialise(Map map)
		{
			currentMap?.CleanupAttachmentInstances();

			currentMap = map ?? throw new ArgumentNullException(nameof(map));

			MapAttachmentExtensions.SetActiveMapManager(this);

			if (string.IsNullOrEmpty(currentMap.name))
			{
				Debug.LogError("Map name is null or empty during initialization.");
				return;
			}

			// Changed line:
			mapTiles = currentMap.CreateRuntimeTiles(transform);

			if (mapTiles == null)
			{
				Debug.LogError("Failed to create runtime tiles — map data invalid.");
				return;
			}

			if (ApplicationSettings.Scrambled) Preset();
			else Solve();

			InitializeWindController();

			currentMap.RefreshAllAttachmentInstances(transform);

			SetupWaypoints();
		}

		public void RefreshGeometry()
		{
			DestroyAllTiles();
			mapTiles = currentMap.CreateRuntimeTiles(transform);

			if (mapTiles == null)
			{
				Debug.LogError("RefreshGeometry failed — could not recreate tiles.");
				return;
			}

			currentMap.RefreshAllAttachmentInstances(transform);
		}

		private void DestroyAllTiles()
		{
			for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
		}

		public string GetDefinitionAtIndex(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= Count || mapTiles == null) return null;
			return mapTiles[mapIndex].definitionId;
		}

		public int GetStartTile()
		{
			if (Waypoints.Length > 0) return Waypoints[0];

			for (int i = 0; i < Count; ++i)
				if (GetTile(i).IsStart) return i;

			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (Waypoints.Length > 0) return Waypoints.Last();

			for (int i = 0; i < Count; ++i)
				if (GetTile(i).IsEnd) return i;

			Debug.LogError("No end tile found!");
			return -1;
		}

		public int FindAdjacentConsole(int nTile)
		{
			var tile = GetTile(nTile);
			if (tile.Nav == 0) return -1;

			foreach (var dirBit in Navigation.Directions)
			{
				int consoleIndex = Navigation.GetAdjacentTile(this, nTile, dirBit);
				if (consoleIndex == -1) continue;

				var consoleTile = GetTile(consoleIndex);
				if (consoleTile.IsConsole && dirBit == Navigation.GetOppositeDirection(consoleTile.Nav))
					return consoleIndex;
			}
			return -1;
		}

		public void Preset()
		{
			indices = Enumerable.Range(0, Count).ToArray();
			UpdateTileObjectNamesAndPositions();
		}

		public void Scramble()
		{
			const int iterations = 1;
			for (var n = 0; n < indices.Length * iterations; ++n)
			{
				var stride = (UnityEngine.Random.value > 0.5f ? Width : 1) * (UnityEngine.Random.value > 0.5f ? 1 : -1);
				var tileStrip = TileStripHelper.GetTileStrip(this, n % indices.Length, stride, true);
				TileStripHelper.RollStrip(this, tileStrip);
			}
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			indices = Enumerable.Range(0, Count).Select(n => n + (currentMap.solve?[n] ?? 0)).ToArray();
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			Debug.Assert(indices != null && indices.Length == mapTiles?.Length,
				"mismatched tiles and indices");

			for (int n = 0; n < indices.Length; ++n)
			{
				var mapTile = mapTiles[indices[n]];
				var go = mapTile.gameObject;
				if (go == null) continue;

				var position = TileWorldPosition(n);
				go.transform.position = position;

#if DEBUG
				position -= Map.tile_origin;
				var id = string.IsNullOrEmpty(mapTile.definitionId) ? "Empty" : mapTile.definitionId;
				var def = ResourceManager.GetDefinition(mapTile.definitionId);
				go.name = $"{def.id} ({position.x},{position.z})";
#endif
			}
		}

		private void SetupWaypoints()
		{
			if (currentMap.waypoints != null && currentMap.waypoints.Length > 0)
			{
				Debug.Log($"Using {currentMap.waypoints.Length} predefined waypoints.");
				return;
			}

			var generated = new List<int>();
			int start = GetStartTile();
			int end = GetEndTile();

			if (start == -1 || end == -1)
			{
				Waypoints = generated.ToArray();
				return;
			}

			generated.Add(start);

			int cur = start;
			int dir = Navigation.NavToDest(this, cur, end);
			if (dir != 0)
			{
				while (cur != end)
				{
					if (FindAdjacentConsole(cur) != -1)
						generated.Add(cur);

					int next = Navigation.GetAdjacentTile(this, cur, dir);
					if (next == -1 || next == start) break;

					var nextTile = GetTile(next);
					if (nextTile.Nav == 0) break;

					dir = Navigation.CalculateNav(dir, nextTile.Nav);
					if (dir == 0) break;

					cur = next;
				}
			}

			generated.Add(end);

			Waypoints = generated.ToArray();

			Debug.Log($"Generated {currentMap.waypoints.Length} waypoints.");
		}

		public bool UpdateTileAt(int x, int z, string id, bool expand = true)
		{
			bool result;
			if (expand)
				result = UpdateTileAtSmart(x, z, id);
			else
				result = UpdateTileAtRestricted(x, z, id);

			return result;
		}

		private bool UpdateTileAtRestricted(int x, int z, string id)
		{
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds ({Width}x{Height})");
				return false;
			}

			int index = z * Width + x;

			if (mapTiles[index].gameObject != null)
				Destroy(mapTiles[index].gameObject);

			var def = currentMap.ResolveDefinition(id, index);

			mapTiles[index] = new Tile(def, transform, TileWorldPosition(index));

			currentMap.RefreshAttachmentsOnTile(index, transform);

			int tableIndex;
			if (currentMap.table == null || !Array.Exists(currentMap.table, s => s == id))
			{
				var list = currentMap.table?.ToList() ?? new List<string>();
				list.Add(id);
				currentMap.table = list.ToArray();
				tableIndex = currentMap.table.Length - 1;
			}
			else
			{
				tableIndex = Array.IndexOf(currentMap.table, id);
			}

			currentMap.tiles[index] = tableIndex;

			currentMap.Consolidate();

			OnMapEdited?.Invoke(this, false, Vector3.zero);
			return true;
		}

		private bool UpdateTileAtSmart(int x, int z, string id, Action<bool, Vector3> onEdited = null)
		{
			int oldWidth = Width;
			int oldHeight = Height;

			var oldBounds = currentMap.GetContentBounds();

			Vector3 originDelta = Vector3.zero;
			bool sizeChanged = false;

			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				int minX = Mathf.Min(0, x);
				int minZ = Mathf.Min(0, z);
				int maxX = Mathf.Max(Width - 1, x);
				int maxZ = Mathf.Max(Height - 1, z);

				int newWidth = maxX - minX + 1;
				int newHeight = maxZ - minZ + 1;

				if (newWidth > Map.MAP_MAX_SIZE || newHeight > Map.MAP_MAX_SIZE)
				{
					Debug.LogWarning($"Map placement rejected: would exceed max size ({Map.MAP_MAX_SIZE}x{Map.MAP_MAX_SIZE})");
					onEdited?.Invoke(false, Vector3.zero);
					return false;
				}

				int offsetX = -minX;
				int offsetZ = -minZ;

				currentMap.RepositionAndResize(newWidth, newHeight, offsetX, offsetZ);

				if (x < 0) originDelta.x = offsetX;
				if (z < 0) originDelta.z = offsetZ;

				x += offsetX;
				z += offsetZ;

				sizeChanged = true;
			}

			int index = z * Width + x;

			int tableIndex;
			if (currentMap.table == null || !Array.Exists(currentMap.table, s => s == id))
			{
				var list = currentMap.table?.ToList() ?? new List<string>();
				list.Add(id);
				currentMap.table = list.ToArray();
				tableIndex = currentMap.table.Length - 1;
			}
			else
			{
				tableIndex = Array.IndexOf(currentMap.table, id);
			}

			currentMap.tiles[index] = tableIndex;

			var defForCrop = ResourceManager.GetDefinition(id);
			bool isDefaultTile = defForCrop?.IsDefault() ?? false;

			if (isDefaultTile || sizeChanged)
			{
				var newBounds = currentMap.GetContentBounds();

				if (currentMap.CropToContent())
				{
					originDelta += new Vector3(
						oldBounds.minX - newBounds.minX,
						0,
						oldBounds.minZ - newBounds.minZ
					);
					sizeChanged = true;
				}
			}

			bool boundsChanged = sizeChanged || Width != oldWidth || Height != oldHeight;

			if (boundsChanged)
			{
				DestroyAllTiles();
				mapTiles = currentMap.CreateRuntimeTiles(transform);
				currentMap.RefreshAllAttachmentInstances(transform);
			}
			else
			{
				if (mapTiles[index].gameObject != null)
					Destroy(mapTiles[index].gameObject);

				var def = currentMap.ResolveDefinition(id, index);

				mapTiles[index] = new Tile(def, transform, TileWorldPosition(index));

				currentMap.RefreshAttachmentsOnTile(index, transform);
			}

			currentMap.Consolidate();

			OnMapEdited?.Invoke(this, boundsChanged, originDelta);
			return true;
		}

		private void InitializeWindController()
		{
			WindController windController = null;
			for (int n = 0; n < mapTiles.Length; ++n)
			{
				var go = mapTiles[n].gameObject;
				if (go == null) continue;

				var sway = go.GetComponent<MorphGeomSway>();
				if (sway == null) continue;

				windController = windController ?? gameObject.AddComponent<WindController>();
				windController.AddSway(sway, TileWorldPosition(n));
			}

			if (windController != null)
				Debug.Log($"WindController initialized with {windController.SwayComponents.Count} sway components.");
		}

		public void RefreshAttachmentInstance(MapAttachment attachment)
		{
			currentMap.RefreshAttachmentInstance(attachment, transform);
		}

		public void DestroyAttachmentInstance(MapAttachment attachment)
		{
			currentMap.DestroyAttachmentInstance(attachment);
		}

		public void AddAttachment(MapAttachment attachment)
		{
			currentMap.AddAttachment(attachment);
			currentMap.RefreshAttachmentInstance(attachment, transform);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public bool RemoveAttachments(MapAttachment[] attachmentArray)
		{
			bool result = false;
			foreach (var att in attachmentArray)
				result |= RemoveAttachment(att);
			return result;
		}

		public bool RemoveAttachment(MapAttachment attachment)
		{
			bool removed = currentMap.RemoveAttachment(attachment);
			if (removed)
				OnMapEdited?.Invoke(this, false, Vector3.zero);
			return removed;
		}

		public void RemoveAllAttachmentsOnTile(int tileIndex)
		{
			currentMap.RemoveAllAttachmentsOnTile(tileIndex);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public Waypoint[] waypointAttachments => currentMap?.GetWaypointAttachments() ?? Array.Empty<Waypoint>();

		public MapAttachment[] attachments
		{
			get => currentMap?.GetAllAttachments() ?? Array.Empty<MapAttachment>();
			set
			{
				currentMap?.SetAllAttachments(value);
			}
		}

		public Bounds GetTileGeometryBounds(int tileIndex)
		{
			if (tileIndex < 0 || tileIndex >= Count)
			{
				Vector3 center = TileWorldPosition(tileIndex);
				return new Bounds(center + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
			}

			Vector3 tileCenter = TileWorldPosition(tileIndex);
			const float horizontalThreshold = 0.7f;

			Bounds bestBounds = default;
			float bestTopY = tileCenter.y;
			bool foundAny = false;

			var tileTransform = GetTile(tileIndex).gameObject?.transform;

			if (null != tileTransform)
			{
				foreach (Renderer renderer in tileTransform.GetComponentsInChildren<Renderer>(true))
				{
					if (!renderer.gameObject.activeInHierarchy) continue;

					Bounds bounds = renderer.bounds;
					Vector3 boundsCenterXZ = new Vector3(bounds.center.x, tileCenter.y, bounds.center.z);

					if (Vector3.Distance(boundsCenterXZ, tileCenter) < horizontalThreshold)
					{
						if (bounds.max.y > bestTopY)
						{
							bestTopY = bounds.max.y;
							bestBounds = bounds;
							foundAny = true;
						}
					}
				}
			}

			return foundAny ? bestBounds : new Bounds(tileCenter + Vector3.up * 0.25f, new Vector3(1f, 1f, 1f));
		}

		public static MapManager Instantiate(Map map, Transform parent = null)
		{
			if (map == null || string.IsNullOrEmpty(map.name))
			{
				Debug.LogError("Cannot instantiate MapManager: invalid map or name.");
				return null;
			}

			var go = new GameObject($"Map: {map.name}");
			if (parent != null) go.transform.SetParent(parent, false);

			var manager = go.AddComponent<MapManager>();
			manager.Initialise(map);
			instance = manager;
			return manager;
		}
	}
}