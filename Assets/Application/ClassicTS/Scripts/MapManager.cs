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

		int CameraHitTile(Camera camera, Vector3 position);

		bool UpdateTileAt(int x, int z, string id, bool expand = true);
		void RefreshAttachmentInstance(MapAttachment attachment);
		void DestroyAttachmentInstance(MapAttachment attachment);

		void AddAttachment(MapAttachment attachment);
		bool RemoveAttachment(MapAttachment attachment);
		void RemoveAllAttachmentsOnTile(int tileIndex);

		Action<IMapManager, bool, Vector3> OnMapEdited { get; set; }
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		// ------------------------------------------------------------------
		// Runtime-only mutable state
		// ------------------------------------------------------------------
		private Map currentMap;
		private int[] indices;// Scrambled/solved visual indices

		// ONE source of truth per map slot
		private MapTile[] mapTiles;

		private struct MapTile
		{
			public string definitionId;// e.g. "tile_grass", "tile_empty"
			public Tile tile;// Runtime Tile with flags + GameObject

			public MapTile(string id, Tile t)
			{
				definitionId = id;
				tile = t;
			}
		}

		// ------------------------------------------------------------------
		// IMapData / IMapManager forwarded properties
		// ------------------------------------------------------------------
		public Map CurrentMap => currentMap;
		public Transform CurrentTransform => transform;

		// Replace the field
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

		public int[] Waypoints { get => currentMap?.waypoints; set { if (null != currentMap) currentMap.waypoints = value; } }

		// -----------------------------------------------------------------------
		// Attachment runtime instances
		// -----------------------------------------------------------------------
		private readonly Dictionary<MapAttachment, GameObject> attachmentGameObjects = new();

		public int GetWaypoint(int index) => (index >= 0 && null != currentMap?.waypoints) ? index < currentMap.waypoints.Length ? currentMap.waypoints[index] : -1 : -1;

#if UNITY_EDITOR
		public static readonly Vector3 tile_origin = new(0.5f, 0f, 0.5f);
		public Vector3 TileWorldPosition(int index) => new Vector3(index % Width, 0f, index / Width) + tile_origin;
		public int WorldToMapIndex(Vector3 vec) => vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1;
		public static Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.FloorToInt(vec.x), 0f, Mathf.FloorToInt(vec.z)) + tile_origin;
#else
        public static readonly Vector3 tile_origin = Vector3.zero;
        public Vector3 TileWorldPosition(int index) => new(index % Width, 0f, index / Width);
        public int WorldToMapIndex(Vector3 vec) { vec += new Vector3(0.5f, 0f, 0.5f); return vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1; }
		public static Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.FloorToInt(vec.x + 0.5f), 0f, Mathf.FloorToInt(vec.z + 0.5f));
#endif

		//statci helpers rely on instance - work out how to remove these later
		private static MapManager instance;
		public static Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;//just pass through
		public static Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;//just pass through

		public static Vector3 LocalPosition(int tileIndex, Vector3 worldPosition) => instance == null || tileIndex < 0 ? worldPosition : worldPosition - instance.TileWorldPosition(tileIndex);
		public static Vector3 WorldPosition(int tileIndex, Vector3 localPosition) => instance == null || tileIndex < 0 ? localPosition : localPosition + instance.TileWorldPosition(tileIndex);

		public int CameraHitTile(Camera camera, Vector3 position) => WorldToMapIndex(ScreenToWorld(camera, position));

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
			if (index < 0 || index >= indices.Length || Width <= 0 || mapTiles == null) return default;
			int dataIndex = indices[index];
			return dataIndex >= 0 && dataIndex < mapTiles.Length ? mapTiles[dataIndex].tile : default;
		}

		public static bool RayToWorld(Ray ray, out Vector3 point)
		{
			point = Vector3.zero;
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (plane.Raycast(ray, out float d))
			{
				point = ray.GetPoint(d);
				return true;
			}
			return false;
		}

		public static Vector3 ScreenToWorld(Camera camera, Vector3 screenPos)
		{
			if (null == camera) return Vector3.negativeInfinity;
			RayToWorld(camera.ScreenPointToRay(screenPos), out Vector3 result);
			return result;
		}

		public static Vector3 ScreenToWorldSnapped(Camera camera, Vector3 screenPos) => SnappedMapPosition(ScreenToWorld(camera, Input.mousePosition));

		private void Awake()
		{
			indices = null;
			mapTiles = null;
		}

		private void OnDestroy()
		{
			CleanupAttachmentInstances();
			if (ReferenceEquals(MapAttachmentExtensions.CurrentMapManager, this))
				MapAttachmentExtensions.ClearActiveMapManager();
			instance = null;
		}

		private void Initialise(Map map)
		{
			CleanupAttachmentInstances();

			currentMap = map ?? throw new ArgumentNullException(nameof(map));

			MapAttachmentExtensions.SetActiveMapManager(this);

			if (string.IsNullOrEmpty(currentMap.name))
			{
				Debug.LogError("Map name is null or empty during initialization.");
				return;
			}

			LoadTileData(currentMap.tiles);

			if (PreviewSettings.Scrambled) Preset();
			else Solve();

			InitializeWindController();

			// Refresh ALL visual attachments on load
			foreach (var att in CurrentMap.attachments)
				RefreshAttachmentInstance(att);

			SetupWaypoints();
		}

		private void DestroyAllTiles()
		{
			for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
		}

		private void LoadTileData(int[] tileMap)
		{
			if (tileMap == null || tileMap.Length != Count)
			{
				Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={Count}");
				return;
			}

			mapTiles = new MapTile[Count];

			for (int n = 0; n < tileMap.Length; ++n)
			{
				int definitionIndex = tileMap[n];
				string id = (definitionIndex >= 0 && definitionIndex < currentMap.table?.Length) ? currentMap.table[definitionIndex] : "tile_empty";

				if (string.IsNullOrEmpty(id))
					id = "tile_empty";

				var definition = ResourceManager.Definitions.FirstOrDefault(td => td.id == id);
				var tile = new Tile(definition);

				if (id != "tile_empty" && definition != null)
					tile.gameObject = InstantiateTile(definition, transform, TileWorldPosition(n));

				mapTiles[n] = new MapTile(id, tile);
			}
		}

		// Editor-only – returns the source definition ID at map index (only valid when not scrambled)
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
			const int iterations = 1;//increase for more scrambling per iteration
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
				var go = mapTile.tile.gameObject;
				if (go == null) continue;

				var position = TileWorldPosition(n);
				go.transform.position = position;

#if DEBUG
				position -= tile_origin;
				var id = string.IsNullOrEmpty(mapTile.definitionId) ? "Empty" : mapTile.definitionId;
				go.name = $"{id} ({position.x},{position.z})";
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

		// -----------------------------------------------------------------------
		// Map editing
		// -----------------------------------------------------------------------

		public bool UpdateTileAt(int x, int z, string id, bool expand = true)
		{
			bool result;
			if (expand)
				result = UpdateTileAtSmart(x, z, id);
			else
				result = UpdateTileAtRestricted(x, z, id);

			//RebuildEmitterVisuals();//we need to deal with this because emitters vanish if map resized
			return result;
		}

		private bool UpdateTileAtRestricted(int x, int z, string id)
		{
			if (string.IsNullOrEmpty(id))
				id = "tile_empty";

			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds ({Width}x{Height})");
				return false;
			}

			int index = z * Width + x;

			// Destroy old visual
			if (mapTiles[index].tile.gameObject != null)
				Destroy(mapTiles[index].tile.gameObject);

			var def = ResourceManager.GetDefinition(id);
			var newTile = new Tile(def);

			if (id != "tile_empty" && def != null)
				newTile.gameObject = InstantiateTile(def, transform, TileWorldPosition(index));

			mapTiles[index] = new MapTile(id, newTile);

			RefreshAttachmentsOnTile(index);

			// Update string table
			if (currentMap.table == null || !Array.Exists(currentMap.table, s => s == id))
			{
				var list = currentMap.table != null ? new List<string>(currentMap.table) : new List<string>();
				if (!list.Contains(id)) list.Add(id);
				currentMap.table = list.ToArray();
			}

			currentMap.tiles[index] = Array.IndexOf(currentMap.table, id);
			currentMap.Consolidate();

			// No resize possible in restricted mode
			OnMapEdited?.Invoke(this, false, Vector3.zero);
			return true; // Success!
		}

		private const int MAP_MAX_SIZE = 64;

		/// <summary>
		/// Places a tile at any (x,z). Expands/crops intelligently with hard size limits.
		/// Returns true if tile was successfully placed/removed.
		/// Calls onEdited only on success, with (resized, originWorldDelta).
		/// </summary>
		private bool UpdateTileAtSmart(int x, int z, string id, Action<bool, Vector3> onEdited = null)
		{
			if (string.IsNullOrEmpty(id))
				id = "tile_empty";

			int oldWidth = Width;
			int oldHeight = Height;

			var oldBounds = currentMap.GetContentBounds();

			Vector3 originDelta = Vector3.zero;
			bool sizeChanged = false;

			// === 1. Expand — only negative axes shift origin ===
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				int minX = Mathf.Min(0, x);
				int minZ = Mathf.Min(0, z);
				int maxX = Mathf.Max(Width - 1, x);
				int maxZ = Mathf.Max(Height - 1, z);

				int newWidth = maxX - minX + 1;
				int newHeight = maxZ - minZ + 1;

				// HARD LIMIT: Prevent gargantuan maps
				if (newWidth > MAP_MAX_SIZE || newHeight > MAP_MAX_SIZE)
				{
					Debug.LogWarning($"Map placement rejected: would exceed max size ({MAP_MAX_SIZE}x{MAP_MAX_SIZE})");
					onEdited?.Invoke(false, Vector3.zero);
					return false; // Fail silently (or play sound, show toast, etc.)
				}

				int offsetX = -minX;
				int offsetZ = -minZ;

				currentMap.RepositionAndResize(newWidth, newHeight, offsetX, offsetZ);

				// Only apply delta for axes that were negative
				if (x < 0) originDelta.x = offsetX;
				if (z < 0) originDelta.z = offsetZ;

				x += offsetX;
				z += offsetZ;

				sizeChanged = true;
			}

			// === 2. Place tile ===
			int index = z * Width + x;

			if (currentMap.table == null || !Array.Exists(currentMap.table, s => s == id))
			{
				var list = currentMap.table != null ? new List<string>(currentMap.table) : new List<string>();
				if (!list.Contains(id)) list.Add(id);
				currentMap.table = list.ToArray();
			}

			currentMap.tiles[index] = Array.IndexOf(currentMap.table, id);

			// === 3. Crop — add delta only if left/top content was removed ===
			if (id == "tile_empty" || sizeChanged)
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

			// === 4. Rebuild visuals ===
			bool boundsChanged = sizeChanged || Width != oldWidth || Height != oldHeight;

			if (boundsChanged)
			{
				DestroyAllTiles();
				LoadTileData(currentMap.tiles);

				RefreshAllAttachmentInstances();
			}
			else
			{
				// Fast single-tile update
				var def = ResourceManager.GetDefinition(id);
				var newTile = new Tile(def);

				if (mapTiles[index].tile.gameObject != null)
					Destroy(mapTiles[index].tile.gameObject);

				if (id != "tile_empty" && def != null)
					newTile.gameObject = InstantiateTile(def, transform, TileWorldPosition(index));

				mapTiles[index] = new MapTile(id, newTile);

				RefreshAttachmentsOnTile(index);
			}

			currentMap.Consolidate();

			OnMapEdited?.Invoke(this, boundsChanged, originDelta);
			return true;
		}

		// -----------------------------------------------------------------------
		// Environmental effects
		// -----------------------------------------------------------------------
		private void InitializeWindController()
		{
			WindController windController = null;
			for (int n = 0; n < mapTiles.Length; ++n)
			{
				var go = mapTiles[n].tile.gameObject;
				if (go == null) continue;

				var sway = go.GetComponent<MorphGeomSway>();
				if (sway == null) continue;

				windController = windController ?? gameObject.AddComponent<WindController>();
				windController.AddSway(sway, TileWorldPosition(n));
			}

			if (windController != null)
				Debug.Log($"WindController initialized with {windController.SwayComponents.Count} sway components.");
		}

		private static GameObject InstantiateTile(Definition definition, Transform parent, Vector3 position)
		{
			if (null == definition || string.IsNullOrEmpty(definition.model))
			{
				if (definition?.id == "tile_invisible")
					return PreviewSettings.ShowHiddenTiles ? GeometryFactory.CreateDebugTile(parent, position) : null;

				Debug.LogWarning("GeometryManager: Invalid Definition or geometry name." + definition.id);
				return GeometryFactory.CreateFallbackTile(parent, position);
			}

			//temporary special placeholder flag setting for special properties in absence of definition editor 
			if (definition.model.Contains("tree"))
				definition.bSway = true;//ToDo implement sway in definition editor - hard set to trees for now

			//temporary special placeholder flag setting for special properties in absence of definition editor 
			if (definition.model.Equals("jun_tile_ns") || 
				definition.model.Equals("jun_tile_ew") ||
				definition.model.Equals("jun_tile_ne_corner") ||
				definition.model.Equals("jun_tile_nw_corner") ||
				definition.model.Equals("jun_tile_se_corner") ||
				definition.model.Equals("jun_tile_sw_corner") ||
				definition.model.Equals("jun_tile_nsew"))
				definition.bWash= true;//ToDo implement sway in definition editor - hard set to jungle drag tiles for now

			//temporary special placeholder material override for special properties in absence of definition editor 
			if ("Caustic" == definition.texture)
				definition.material = "toxic";

			return DefinitionFactory.Instantiate(definition, position, Quaternion.identity, parent);
		}

		//// -----------------------------------------------------------------------
		//// Attachment runtime instance management
		//// -----------------------------------------------------------------------

		private void RefreshAllAttachmentInstances()
		{
			foreach (var att in CurrentMap.attachments)
				RefreshAttachmentInstance(att);
		}

		private void RefreshAttachmentsOnTile(int tileIndex)
		{
			foreach (var att in CurrentMap.attachments)
			{
				if (att.tile == tileIndex)
					RefreshAttachmentInstance(att);
			}
		}

		public void RefreshAttachmentInstance(MapAttachment attachment)
		{
			if (attachment == null) return;

			// Let each type decide its prefab and behavior
			string prefabName = attachment switch
			{
				Emitter e => e.variant switch
				{
					"flame" => "flame",
					"spark" => "spark",
					_ => null
				},
				Pickup => null,// null for now p => "pickup", // example — you can make this dynamic later
				View => null,// Views have no runtime GO — only editor helpers
				_ => null
			};

			if (string.IsNullOrEmpty(prefabName))
			{
				DestroyAttachmentInstance(attachment);
				return;
			}

			//Vector3 worldPos = TileWorldPosition(attachment.tile) + GetAttachmentLocalPosition(attachment);
			//Quaternion rotation = GetAttachmentRotation(attachment);

			Vector3 localPos = attachment switch
			{
				Emitter e => e.Position,
				Pickup => Vector3.up * 0.5f,// floating above ground
				View v => v.Position,
				_ => Vector3.zero
			};

			Quaternion rotation = attachment switch
			{
				Emitter e => e.Rotation,
				Pickup => Quaternion.identity,
				View v => v.Rotation,
				_ => Quaternion.identity
			};

			Vector3 worldPos = TileWorldPosition(attachment.tile) + localPos;

			if (attachmentGameObjects.TryGetValue(attachment, out GameObject go) && go != null)
			{
				go.transform.position = worldPos;
				go.transform.rotation = rotation;
				return;
			}

			// Instantiate new
			string prefabPath = $"{AssetPath.PrefabPath}{prefabName}";
			GameObject prefab = Resources.Load<GameObject>(prefabPath);
			if (prefab == null)
			{
				Debug.LogWarning($"Attachment prefab not found: {prefabPath}");
				return;
			}

			go = Instantiate(prefab, transform);
			go.transform.position = worldPos;
			go.transform.rotation = rotation;
			go.name = $"{attachment.TypeName}_{prefabName}_tile{attachment.tile}";

			attachmentGameObjects[attachment] = go;
		}

		public void DestroyAttachmentInstance(MapAttachment attachment)
		{
			if (attachment == null) return;

			if (attachmentGameObjects.TryGetValue(attachment, out GameObject go) && go != null)
			{
				if (Application.isPlaying)
					Destroy(go);
				else
					DestroyImmediate(go);
			}

			attachmentGameObjects.Remove(attachment);
		}

		private void CleanupAttachmentInstances()
		{
			foreach (var att in attachmentGameObjects.Keys.ToList())
				DestroyAttachmentInstance(att);
			attachmentGameObjects.Clear();
		}

		public void AddAttachment(MapAttachment attachment)
		{
			currentMap.AddAttachment(attachment);
			RefreshAttachmentInstance(attachment);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public bool RemoveAttachment(MapAttachment attachment)
		{
			var result = currentMap.RemoveAttachment(attachment);
			DestroyAttachmentInstance(attachment);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
			return result;
		}

		public void RemoveAllAttachmentsOnTile(int tileIndex)
		{
			var attsOnTile = currentMap.GetAttachmentsOnTile(tileIndex);
			foreach (var att in attsOnTile)
			{
				currentMap.RemoveAttachment(att); 
				DestroyAttachmentInstance(att);
			}
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		/// <summary>
		/// Returns the world-space bounds of the highest rendered geometry that is centered within the given tile.
		/// Uses a tight horizontal threshold to avoid considering geometry from adjacent tiles or edge decorations (e.g. battlements).
		/// Returns a default 1x1x1 bounds centered 0.5 units above the tile if no suitable renderer is found.
		/// </summary>
		public Bounds GetTileGeometryBounds(int tileIndex)
		{
			if (tileIndex < 0 || tileIndex >= Count)
			{
				Vector3 center = TileWorldPosition(tileIndex);
				return new Bounds(center + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
			}

			Vector3 tileCenter = TileWorldPosition(tileIndex);
			const float horizontalThreshold = 0.7f; // Safe within 1x1 tile, avoids parapets/towers edges

			Bounds bestBounds = default;
			float bestTopY = tileCenter.y;
			bool foundAny = false;

			foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
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

			return foundAny ? bestBounds : new Bounds(tileCenter + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f));
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
			instance = go.GetComponent<MapManager>();
			return manager;
		}
	}
}