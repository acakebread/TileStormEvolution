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
		bool UpdateTileAt(int x, int z, string id, bool expand = true);
		Vector3 SnappedMapPosition(Vector3 vec);
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

		public Waypoint[] Waypoints => currentMap?.waypoints ?? Array.Empty<Waypoint>();

#if UNITY_EDITOR
		public static readonly Vector3 tile_origin = new(0.5f, 0f, 0.5f);
		public Vector3 TileWorldPosition(int index) => new Vector3(index % Width, 0f, index / Width) + tile_origin;
		public int WorldToMapIndex(Vector3 vec) => vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1;
		public Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.Floor(vec.x), 0f, Mathf.Floor(vec.z)) + tile_origin;
#else
        public static readonly Vector3 tile_origin = Vector3.zero;
        public Vector3 TileWorldPosition(int index) => new(index % Width, 0f, index / Width);
        public int WorldToMapIndex(Vector3 vec) { vec += new Vector3(0.5f, 0f, 0.5f); return vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1; }
		public Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.Floor(vec.x), 0f, Mathf.Floor(vec.z));
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
				currentMap.waypoints = generated.ToArray();
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
			currentMap.waypoints = generated.ToArray();
			Debug.Log($"Generated {currentMap.waypoints.Length} waypoints.");
		}

		// -----------------------------------------------------------------------
		// Map editing
		// -----------------------------------------------------------------------

		public bool UpdateTileAt(int x, int z, string id, bool expand = true)
		{
			var result = expand ? UpdateTileAtSmart(x, z, id) : UpdateTileAtRestricted(x, z, id);

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
			if (mapTiles[index].tile.GameObject != null)
				Destroy(mapTiles[index].tile.GameObject);

			var def = ResourceManager.GetDefinition(id);
			var newTile = new Tile(def);

			if (id != "tile_empty" && def != null)
				newTile.GameObject = GeometryManager.InstantiateTile(def, transform, TileWorldPosition(index), newTile.IsDrag);

			mapTiles[index] = new MapTile(id, newTile);

			// Ensure the string ID exists in the table (for save compatibility)
			if (currentMap.table == null || !Array.Exists(currentMap.table, s => s == id))
			{
				var list = currentMap.table != null ? new List<string>(currentMap.table) : new List<string>();
				if (!list.Contains(id))
					list.Add(id);
				currentMap.table = list.ToArray();
			}

			currentMap.tiles[index] = Array.IndexOf(currentMap.table, id);
			currentMap.Consolidate();// Rebuild compact indices for saving
			return false;// false for now because return value indicates map size change at the moment - will change to whether tile was added or not later
		}

		/// <summary>
		/// Places a tile at any coordinate — automatically expands if needed, crops if appropriate.
		/// Returns true if the map bounds changed (resized or cropped), so caller can reload.
		/// </summary>
		private bool UpdateTileAtSmart(int x, int z, string id)
		{
			if (string.IsNullOrEmpty(id))
				id = "tile_empty";

			bool wasEmpty = id == "tile_empty";
			bool extentsChanged = false;

			// If in bounds → just place normally
			if (x >= 0 && x < Width && z >= 0 && z < Height)
			{
				UpdateTileAtRestricted(x, z, id);
			}
			else
			{
				// Need to expand
				int requiredMinX = Mathf.Min(0, x);
				int requiredMinZ = Mathf.Min(0, z);
				int requiredMaxX = Mathf.Max(Width - 1, x);
				int requiredMaxZ = Mathf.Max(Height - 1, z);

				int newWidth = requiredMaxX - requiredMinX + 1;
				int newHeight = requiredMaxZ - requiredMinZ + 1;

				int offsetX = -requiredMinX;
				int offsetZ = -requiredMinZ;

				currentMap.RepositionAndResize(newWidth, newHeight, offsetX, offsetZ);

				DestroyAllTiles();
				LoadTileData(currentMap.tiles);

				// Coordinates now valid
				int newX = x + offsetX;
				int newZ = z + offsetZ;
				UpdateTileAtRestricted(newX, newZ, id);

				extentsChanged = true;
			}

			// Always try to crop after placing empty (or after expansion — might have empty borders now)
			if (wasEmpty || extentsChanged)
			{
				if (currentMap.CropToContent())
				{
					DestroyAllTiles();
					LoadTileData(currentMap.tiles);
					extentsChanged = true;
				}
			}
			currentMap.Consolidate();

			return extentsChanged;
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