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
		Map CurrentMap { get; }

		int WorldToMapIndex(Vector3 vec);
		Tile GetTile(int index);
		int GetStartTile();
		int GetEndTile();

		int FindAdjacentConsole(int nTile);
		Transform CurrentTransform { get; }

		string GetDefinitionAtIndex(int mapIndex);
		Bounds GetTileGeometryBounds(int tileIndex);

		bool UpdateTileAt(int x, int z, string id, bool expand = true);
		void RefreshAttachmentInstance(MapAttachment attachment);
		void DestroyAttachmentInstance(MapAttachment attachment);

		void AddAttachment(MapAttachment attachment);
		bool RemoveAttachment(MapAttachment attachment);
		bool RemoveAttachments(MapAttachment[] attachmentArray);
		void RemoveAllAttachmentsOnTile(int tileIndex);

		void RefreshGeometry();

		Action<IMapManager, bool, Vector3> OnMapEdited { get; set; }
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private Map currentMap;

		public Map CurrentMap => currentMap;
		public Transform CurrentTransform => transform;

		public Action<IMapManager, bool, Vector3> OnMapEdited { get; set; }

		public int Width => currentMap?.width ?? 0;
		public int Height => currentMap?.height ?? 0;
		public int Count => Width * Height;

		private static MapManager instance;

		public static Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public static Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public static Vector3 LocalPosition(int tileIndex, Vector3 worldPosition)
			=> instance == null || tileIndex < 0 ? worldPosition : worldPosition - instance.currentMap.TileWorldPosition(tileIndex);

		public static Vector3 WorldPosition(int tileIndex, Vector3 localPosition)
			=> instance == null || tileIndex < 0 ? localPosition : localPosition + instance.currentMap.TileWorldPosition(tileIndex);

		public int WorldToMapIndex(Vector3 vec) => currentMap?.WorldToMapIndex(vec) ?? -1;

		public Tile GetTile(int index)
		{
			if (index < 0 || index >= Count || Width <= 0 || currentMap == null)
				return default;

			int dataIndex = currentMap?.indices?[index] ?? index;

			return currentMap.GetRuntimeTile(dataIndex);
		}

		public int[] Indices => currentMap?.indices;

		private void Awake()
		{
			// no need for mapTiles = null anymore
		}

		private void OnDestroy()
		{
			currentMap?.CleanupAttachmentInstances();
			currentMap?.DestroyRuntimeTiles();
			if (ReferenceEquals(MapAttachmentExtensions.CurrentMapManager, this))
				MapAttachmentExtensions.ClearActiveMapManager();
			instance = null;
		}

		private void Initialise(Map map)
		{
			currentMap?.CleanupAttachmentInstances();
			currentMap?.DestroyRuntimeTiles();

			currentMap = map ?? throw new ArgumentNullException(nameof(map));

			MapAttachmentExtensions.SetActiveMapManager(this);

			if (string.IsNullOrEmpty(currentMap.name))
			{
				Debug.LogError("Map name is null or empty during initialization.");
				return;
			}

			currentMap.CreateOrGetRuntimeTiles(transform);

			if (currentMap.RuntimeTileCount == 0)
			{
				Debug.LogError("Failed to create runtime tiles — map data invalid.");
				return;
			}

			if (ApplicationSettings.Scrambled) Preset();
			else Solve();

			InitializeWindController();

			currentMap.RefreshAllAttachmentInstances();

			SetupWaypoints();
		}

		public void RefreshGeometry()
		{
			DestroyAllTiles();

			currentMap?.DestroyRuntimeTiles();
			currentMap?.CreateOrGetRuntimeTiles(transform);

			if (currentMap?.RuntimeTileCount == 0)
			{
				Debug.LogError("RefreshGeometry failed — could not recreate tiles.");
				return;
			}

			currentMap.RefreshAllAttachmentInstances();
		}

		private void DestroyAllTiles()
		{
			for (int i = transform.childCount - 1; i >= 0; i--)
				Destroy(transform.GetChild(i).gameObject);
		}

		public string GetDefinitionAtIndex(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= Count)
				return null;

			return currentMap?.GetRuntimeTile(mapIndex).definitionId;
		}

		public int GetStartTile()
		{
			if (currentMap?.waypoints?.Length > 0) return currentMap.waypoints[0];

			for (int i = 0; i < Count; ++i)
				if (GetTile(i).IsStart) return i;

			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (currentMap?.waypoints?.Length > 0) return currentMap.waypoints.Last();

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
			currentMap.indices = Enumerable.Range(0, Count).ToArray();
			UpdateTileObjectNamesAndPositions();
		}

		public void Scramble()
		{
			if (currentMap.indices == null)
				currentMap.indices = Enumerable.Range(0, Count).ToArray();

			const int iterations = 1;
			for (var n = 0; n < currentMap.indices.Length * iterations; ++n)
			{
				var stride = (UnityEngine.Random.value > 0.5f ? Width : 1) *
							 (UnityEngine.Random.value > 0.5f ? 1 : -1);

				var tileStrip = TileStripHelper.GetTileStrip(this, n % currentMap.indices.Length, stride, true);
				TileStripHelper.RollStrip(this, tileStrip);
			}
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			currentMap.indices = Enumerable.Range(0, Count)
				.Select(n => n + (currentMap.solve?[n] ?? 0))
				.ToArray();

			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			var perm = currentMap?.indices;
			if (perm == null || currentMap?.RuntimeTileCount != perm.Length)
			{
				Debug.Assert(false, "mismatched indices and runtime tiles");
				return;
			}

			for (int n = 0; n < perm.Length; ++n)
			{
				var mapTile = currentMap.GetRuntimeTile(perm[n]);
				var go = mapTile.gameObject;
				if (go == null) continue;

				var position = currentMap.TileWorldPosition(n);
				go.transform.position = position;

#if DEBUG
				position -= Map.tile_origin;
				var id = string.IsNullOrEmpty(mapTile.definitionId) ? "Empty" : mapTile.definitionId;
				var def = ResourceManager.GetDefinition(mapTile.definitionId);
				go.name = $"{def?.id ?? "??"} ({position.x},{position.z})";
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
				currentMap.waypoints = generated.ToArray();
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

			currentMap.waypoints = generated.ToArray();

			Debug.Log($"Generated {currentMap.waypoints.Length} waypoints.");
		}

		private void InitializeWindController()
		{
			WindController windController = null;

			for (int n = 0; n < currentMap?.RuntimeTileCount; ++n)
			{
				var go = currentMap?.GetRuntimeTile(n).gameObject;
				if (go == null) continue;

				var sway = go.GetComponent<MorphGeomSway>();
				if (sway == null) continue;

				windController = windController ?? gameObject.AddComponent<WindController>();
				windController.AddSway(sway, currentMap.TileWorldPosition(n));
			}

			if (windController != null)
				Debug.Log($"WindController initialized with {windController.SwayComponents.Count} sway components.");
		}

		public bool UpdateTileAt(int x, int z, string id, bool expand = true)
		{
			return expand ? UpdateTileAtSmart(x, z, id) : UpdateTileAtRestricted(x, z, id);
		}

		private bool UpdateTileAtRestricted(int x, int z, string id)
		{
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds ({Width}x{Height})");
				return false;
			}

			int index = z * Width + x;

			var oldTile = currentMap.GetRuntimeTile(index);
			if (oldTile.gameObject != null)
				Destroy(oldTile.gameObject);

			var def = currentMap.ResolveDefinition(id, index);

			// We can't directly set runtimeTiles[index] here anymore — force full refresh
			currentMap.tiles[index] = GetOrAddTableIndex(id);

			currentMap.RefreshAttachmentsOnTile(index);
			currentMap.Consolidate();

			RefreshGeometry();  // rebuilds runtime tiles

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

			currentMap.tiles[index] = GetOrAddTableIndex(id);

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
				currentMap.DestroyRuntimeTiles();
				currentMap.CreateOrGetRuntimeTiles(transform);
				currentMap.RefreshAllAttachmentInstances();
			}
			else
			{
				var oldTile = currentMap.GetRuntimeTile(index);
				if (oldTile.gameObject != null)
					Destroy(oldTile.gameObject);

				var def = currentMap.ResolveDefinition(id, index);

				// Force single tile recreation
				currentMap.runtimeTiles[index] = new Tile(def, transform, currentMap.TileWorldPosition(index));

				currentMap.RefreshAttachmentsOnTile(index);
			}

			currentMap.Consolidate();

			OnMapEdited?.Invoke(this, boundsChanged, originDelta);
			return true;
		}

		private int GetOrAddTableIndex(string id)
		{
			if (currentMap.table == null || !Array.Exists(currentMap.table, s => s == id))
			{
				var list = currentMap.table?.ToList() ?? new List<string>();
				list.Add(id);
				currentMap.table = list.ToArray();
				return currentMap.table.Length - 1;
			}
			return Array.IndexOf(currentMap.table, id);
		}

		public void RefreshAttachmentInstance(MapAttachment attachment)
		{
			currentMap?.RefreshAttachmentInstance(attachment);
		}

		public void DestroyAttachmentInstance(MapAttachment attachment)
		{
			currentMap?.DestroyAttachmentInstance(attachment);
		}

		public void AddAttachment(MapAttachment attachment)
		{
			currentMap?.AddAttachment(attachment);
			currentMap?.RefreshAttachmentInstance(attachment);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public bool RemoveAttachment(MapAttachment attachment)
		{
			bool removed = currentMap?.RemoveAttachment(attachment) ?? false;
			if (removed)
				OnMapEdited?.Invoke(this, false, Vector3.zero);
			return removed;
		}

		public bool RemoveAttachments(MapAttachment[] attachmentArray)
		{
			bool result = false;
			foreach (var att in attachmentArray)
				result |= RemoveAttachment(att);
			return result;
		}

		public void RemoveAllAttachmentsOnTile(int tileIndex)
		{
			currentMap?.RemoveAllAttachmentsOnTile(tileIndex);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public Bounds GetTileGeometryBounds(int tileIndex)
		{
			if (tileIndex < 0 || tileIndex >= Count)
			{
				Vector3 center = currentMap?.TileWorldPosition(tileIndex) ?? Vector3.zero;
				return new Bounds(center + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
			}

			Vector3 tileCenter = currentMap.TileWorldPosition(tileIndex);
			const float horizontalThreshold = 0.7f;

			Bounds bestBounds = default;
			float bestTopY = tileCenter.y;
			bool foundAny = false;

			var tile = currentMap.GetRuntimeTile(tileIndex);
			var tileTransform = tile.gameObject?.transform;

			if (tileTransform != null)
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
			Map.parentTransform = go.transform;

			var manager = go.AddComponent<MapManager>();
			manager.Initialise(map);
			instance = manager;
			return manager;
		}
	}
}