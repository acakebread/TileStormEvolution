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
		Waypoint[] Waypoints { get; }
		Map CurrentMap { get; }
		Transform CurrentTransform { get; }
		string GetDefinitionAtIndex(int mapIndex);
		bool UpdateTileAt(int x, int z, string id, bool expand = true, Action<bool, Vector3> onEdited = null);
		Vector3 SnappedMapPosition(Vector3 vec);

		public void SetWaypointTiles(int[] tiles);//placeholder method
		public void SetWaypoint(int index, int tile);
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

		public int Width => currentMap?.width ?? 0;
		public int Height => currentMap?.height ?? 0;
		public int Count => Width * Height;

		public int[] Indices => indices;

		public Waypoint[] Waypoints
		{
			get
			{
				int[] tiles = currentMap?.waypoints;
				if (tiles == null || tiles.Length == 0)
					return Array.Empty<Waypoint>();

				var list = new Waypoint[tiles.Length];
				for (int i = 0; i < tiles.Length; i++)
					list[i] = GetWaypoint(i);
				return list;
			}
		}


		public int[] WaypointTiles => currentMap?.waypoints ?? Array.Empty<int>();

		public void SetWaypointTiles(int[] tiles)//new placeholder method
		{
			if (currentMap != null)
				currentMap.waypoints = tiles ?? Array.Empty<int>();
		}

		/// <summary>
		/// Returns a fully populated Waypoint for the given index, built from int[] + attachments
		/// This replaces the old _richWaypoints array — no caching, no waste
		/// </summary>
		public Waypoint GetWaypoint(int index)
		{
			var tiles = currentMap?.waypoints;
			if (tiles == null || index < 0 || index >= tiles.Length)
				return new Waypoint { name = $"Invalid_{index}", tile = -1 };

			int tile = tiles[index];

			var wp = new Waypoint
			{
				name = $"WP{index:00}",
				tile = tile
			};

			// Look for a matching Viewpoint attachment
			if (currentMap?.attachments != null && currentMap.attachments.Length > 0)
		    {
				foreach (var att in currentMap.attachments)
				{
					if (att is Viewpoint vp && vp.tile == tile)
					{
						if (!string.IsNullOrEmpty(vp.name))
							wp.name = vp.name;
						wp.vSrc = vp.vSrc;
						wp.vDst = vp.vDst;
						break; // first match wins
					}
				}
			}

			return wp;
		}

		public void SetWaypoint(int index, int tile)
		{
			var tiles = currentMap?.waypoints;
			if (tiles == null || index < 0 || index >= tiles.Length)
			{
				Debug.LogError($"Invalid_{index}");
				return;
			}
			currentMap.waypoints[index] = tile;
		}

#if UNITY_EDITOR
		public static readonly Vector3 tile_origin = new(0.5f, 0f, 0.5f);
		public Vector3 TileWorldPosition(int index) => new Vector3(index % Width, 0f, index / Width) + tile_origin;
		public int WorldToMapIndex(Vector3 vec) => vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1;
		public Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.FloorToInt(vec.x), 0f, Mathf.FloorToInt(vec.z)) + tile_origin;
#else
        public static readonly Vector3 tile_origin = Vector3.zero;
        public Vector3 TileWorldPosition(int index) => new(index % Width, 0f, index / Width);
        public int WorldToMapIndex(Vector3 vec) { vec += new Vector3(0.5f, 0f, 0.5f); return vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1; }
		public Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.FloorToInt(vec.x + 0.5f), 0f, Mathf.FloorToInt(vec.z + 0.5f));
#endif

		public enum Anchor
		{
			TopLeft, TopCenter, TopRight,
			MiddleLeft, Center, MiddleRight,
			BottomLeft, BottomCenter, BottomRight
		}

		public Tile GetTile(int index)
		{
			if (index < 0 || index >= indices.Length || Width <= 0 || mapTiles == null) return default;
			int dataIndex = indices[index];
			return dataIndex >= 0 && dataIndex < mapTiles.Length ? mapTiles[dataIndex].tile : default;
		}

		public static Vector3 ScreenToWorld(Camera camera, Vector3 screenPos)
		{
			var ray = camera.ScreenPointToRay(screenPos);
			var plane = new Plane(Vector3.up, Vector3.zero);
			return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : Vector3.zero;
		}

		private void Awake()
		{
			indices = null;
			mapTiles = null;
		}

		private void Initialise(Map map)
		{
			currentMap = map ?? throw new ArgumentNullException(nameof(map));

			if (string.IsNullOrEmpty(currentMap.name))
			{
				Debug.LogError("Map name is null or empty during initialization.");
				return;
			}

			LoadTileData(currentMap.tiles);

			if (PreviewSettings.Scrambled) Preset();
			else Solve();

			InitializeWindController();
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
					tile.GameObject = GeometryManager.InstantiateTile( definition, transform, TileWorldPosition(n), tile.IsDrag);

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
			if (Waypoints.Length > 0) return Waypoints[0].tile;

			for (int i = 0; i < Count; ++i)
				if (GetTile(i).IsStart) return i;

			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (Waypoints.Length > 0) return Waypoints.Last().tile;

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
				var go = mapTile.tile.GameObject;
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

			var generated = new List<Waypoint>();
			int start = GetStartTile();
			int end = GetEndTile();

			if (start == -1 || end == -1)
			{
				SetWaypointTiles(generated.Select(wp => wp.tile).ToArray());
				return;
			}

			generated.Add(new Waypoint { tile = start });

			int cur = start;
			int dir = Navigation.NavToDest(this, cur, end);
			if (dir != 0)
			{
				while (cur != end)
				{
					if (FindAdjacentConsole(cur) != -1)
						generated.Add(new Waypoint { tile = cur });

					int next = Navigation.GetAdjacentTile(this, cur, dir);
					if (next == -1 || next == start) break;

					var nextTile = GetTile(next);
					if (nextTile.Nav == 0) break;

					dir = Navigation.CalculateNav(dir, nextTile.Nav);
					if (dir == 0) break;

					cur = next;
				}
			}

			generated.Add(new Waypoint { tile = end });

			SetWaypointTiles(generated.Select(wp => wp.tile).ToArray());

			Debug.Log($"Generated {currentMap.waypoints.Length} waypoints.");
		}

		// -----------------------------------------------------------------------
		// Map editing
		// -----------------------------------------------------------------------

		public bool UpdateTileAt(int x, int z, string id, bool expand = true, Action<bool, Vector3> onEdited = null)
		{
			if (expand)
				return UpdateTileAtSmart(x, z, id, onEdited);
			else
				return UpdateTileAtRestricted(x, z, id, onEdited);
		}

		private bool UpdateTileAtRestricted(int x, int z, string id, Action<bool, Vector3> onEdited = null)
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
			if (mapTiles[index].tile.GameObject != null)
				Destroy(mapTiles[index].tile.GameObject);

			var def = ResourceManager.GetDefinition(id);
			var newTile = new Tile(def);

			if (id != "tile_empty" && def != null)
			{
				newTile.GameObject = GeometryManager.InstantiateTile(
					def, transform, TileWorldPosition(index), newTile.IsDrag);
			}

			mapTiles[index] = new MapTile(id, newTile);

			// Update string table
			if (currentMap.table == null || !Array.Exists(currentMap.table, s => s == id))
			{
				var list = currentMap.table != null ? new List<string>(currentMap.table) : new List<string>();
				if (!list.Contains(id)) list.Add(id);
				currentMap.table = list.ToArray();
			}

			currentMap.tiles[index] = Array.IndexOf(currentMap.table, id);
			currentMap.Consolidate();
			//RebuildRichWaypoints();

			// No resize possible in restricted mode
			onEdited?.Invoke(false, Vector3.zero);

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
			}
			else
			{
				// Fast single-tile update
				var def = ResourceManager.GetDefinition(id);
				var newTile = new Tile(def);

				if (mapTiles[index].tile.GameObject != null)
					Destroy(mapTiles[index].tile.GameObject);

				if (id != "tile_empty" && def != null)
				{
					newTile.GameObject = GeometryManager.InstantiateTile(
						def, transform, TileWorldPosition(index), newTile.IsDrag);
				}

				mapTiles[index] = new MapTile(id, newTile);
			}

			currentMap.Consolidate();
			//RebuildRichWaypoints();

			// Success!
			onEdited?.Invoke(boundsChanged, originDelta);
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
				var go = mapTiles[n].tile.GameObject;
				if (go == null) continue;

				var sway = go.GetComponent<MorphGeomSway>();
				if (sway == null) continue;

				windController = windController ?? gameObject.AddComponent<WindController>();
				windController.AddSway(sway, TileWorldPosition(n));
			}

			if (windController != null)
				Debug.Log($"WindController initialized with {windController.SwayComponents.Count} sway components.");
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
			return manager;
		}
	}
}