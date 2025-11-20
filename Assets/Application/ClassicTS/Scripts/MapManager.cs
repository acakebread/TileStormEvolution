using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;
using System;

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
		Map CurrentMap { get; } // optional – useful for editor tools
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		// ------------------------------------------------------------------
		// Runtime-only mutable state
		// ------------------------------------------------------------------
		private Map currentMap;
		private int[] indices;                    // Scrambled/solved visual indices
		private Tile[] tiles;                     // Instantiated runtime Tile objects
		private string[] definitions;             // Cached 'id' string per map index (for fast lookup & saving)

		// ------------------------------------------------------------------
		// IMapData / IMapManager forwarded properties
		// ------------------------------------------------------------------
		public Map CurrentMap => currentMap;

		public int Width => currentMap?.width ?? 0;
		public int Height => currentMap?.height ?? 0;
		public int Count => Width * Height;

		public int[] Indices => indices;

		public Waypoint[] Waypoints => currentMap?.waypoints ?? Array.Empty<Waypoint>();

		private void Awake()
		{
			indices = null;
			tiles = null;
			definitions = null;
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

			if (PreviewSettings.Scrambled) Scramble();
			else Solve();

			InitializeWindController();
			SetupWaypoints();
		}

		private void LoadTileData(int[] tileMap)
		{
			if (tileMap == null || tileMap.Length != Count)
			{
				Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={Count}");
				return;
			}

			tiles = new Tile[Count];
			definitions = new string[Count];

			for (int n = 0; n < tileMap.Length; ++n)
			{
				int definitionIndex = tileMap[n];
				string id = (definitionIndex >= 0 && definitionIndex < currentMap.table?.Length)
					? currentMap.table[definitionIndex]
					: "tile_empty";

				if (string.IsNullOrEmpty(id))
					id = "tile_empty";

				definitions[n] = id;
				var definition = ResourceManager.Definitions.FirstOrDefault(td => td.id == id);
				tiles[n] = new Tile(definition);

				if (id == "tile_empty") continue;
				if (definition == null)
				{
					Debug.LogError($"Definition not found for id={id} at tile {n}");
					continue;
				}

				tiles[n].GameObject = GeometryManager.InstantiateTile(
					definition, transform, TileWorldPosition(n), tiles[n].Interactive);
			}
		}

		public int GetOrAddMapDefIndex(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				Debug.LogError($"Invalid tile definition: id={id}");
				return -1;
			}

			if (currentMap.table != null)
			{
				int existing = Array.IndexOf(currentMap.table, id);
				if (existing != -1) return existing;
			}

			// Grow defs array
			var oldDefs = currentMap.table ?? Array.Empty<string>();
			var newDefs = new string[oldDefs.Length + 1];
			oldDefs.CopyTo(newDefs, 0);
			newDefs[oldDefs.Length] = id;
			currentMap.table = newDefs;

			Debug.Log($"Added new mapDef: id={id}, new index={oldDefs.Length}");
			return oldDefs.Length;
		}

		public void UpdateTileAt(int x, int z, int newDeinitionfIndex)
		{
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds ({Width}x{Height})");
				return;
			}

			int index = z * Width + x;
			if (newDeinitionfIndex < 0 || newDeinitionfIndex >= currentMap.table.Length)
			{
				Debug.LogError($"Invalid newDefinitionIndex={newDeinitionfIndex}");
				return;
			}

			string id = currentMap.table[newDeinitionfIndex];
			if (string.IsNullOrEmpty(id))
			{
				Debug.LogError($"Empty id at index {newDeinitionfIndex}");
				return;
			}

			if (tiles[index].GameObject != null)
				Destroy(tiles[index].GameObject);

			definitions[index] = id;
			var def = ResourceManager.Definitions.FirstOrDefault(td => td.id == id);
			tiles[index] = new Tile(def);

			if (id != "tile_empty")
			{
				var definition = ResourceManager.GetDefinition(id);
				if (definition != null)
				{
					tiles[index].GameObject = GeometryManager.InstantiateTile(
						definition, transform, TileWorldPosition(index), tiles[index].Interactive);
				}
			}

			UpdateChanges();
		}

		// -----------------------------------------------------------------------
		// Database update
		// -----------------------------------------------------------------------
		private void ApplyCurrentMapChanges()
		{
			if (currentMap == null) return;

			// Step 1: Count frequency of each ID
			var consolidatedTableMap = new Dictionary<string, int>();
			for (int i = 0; i < definitions.Length; i++)
			{
				string id = definitions[i];
				if (string.IsNullOrEmpty(id))
				{
					Debug.LogError("invalid id");
					continue;
				}

				if (!consolidatedTableMap.ContainsKey(id))
					consolidatedTableMap[id] = 0;
				consolidatedTableMap[id]++;
			}

			// Step 2: Build list of IDs sorted by frequency (highest first)
			var consolidatedTableList = consolidatedTableMap
				.OrderByDescending(kvp => kvp.Value) // sort by frequency
				.Select(kvp => kvp.Key)              // extract keys in that order
				.ToList();

			// Step 3: Convert to array
			var consolidatedTable = consolidatedTableList.ToArray();

			// Step 4: Remap every tile index to the new compact table
			var logicalTiles = new int[Count];
			for (int i = 0; i < definitions.Length; i++)
			{
				string id = definitions[i];
				logicalTiles[i] = Array.IndexOf(consolidatedTable, id); // guaranteed valid
			}

			// Step 5: Apply changes
			currentMap.table = consolidatedTable;
			currentMap.tiles = logicalTiles;

			ResourceManager.ApplyMapChanges(currentMap);
		}

		public void UpdateChanges()
		{
			ApplyCurrentMapChanges();
			// No extra call — DatabaseSerializer is stateless, no sync needed
		}

		public string GetDefinitionAtIndex(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= Count) return null;
			return definitions[mapIndex];
		}

		public int GetStartTile()
		{
			if (Waypoints.Length > 0) return Waypoints[0].nTile;

			for (int i = 0; i < Count; ++i)
				if (GetTile(i).IsStart) return i;

			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (Waypoints.Length > 0) return Waypoints.Last().nTile;

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

		private void InitializeWindController()
		{
			var windController = gameObject.AddComponent<WindController>();
			var swayComponents = new List<(MorphGeomSway sway, Vector3 position)>();

			for (int n = 0; n < tiles.Length; ++n)
			{
				if (tiles[n].GameObject == null) continue;
				var sway = tiles[n].GameObject.GetComponent<MorphGeomSway>();
				if (sway != null)
					swayComponents.Add((sway, TileWorldPosition(n)));
			}

			windController.Initialize(swayComponents);
			Debug.Log($"WindController initialized with {swayComponents.Count} sway components.");
		}

		public void Scramble()
		{
			indices = Enumerable.Range(0, Count).Select(n => n + (currentMap.mixed?[n] ?? 0)).ToArray();
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			indices = Enumerable.Range(0, Count).ToArray();
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			Debug.AssertFormat(indices.Length > 0 && indices.Length == tiles.Length, "mismatched tiles and indices");

			for (var n = 0; n < indices.Length; ++n)
			{
				var gameObject = tiles[indices[n]].GameObject;
				if (null == gameObject) continue;

				var position = TileWorldPosition(n);
				gameObject.transform.position = position;
				position -= tile_origin;
#if DEBUG
				gameObject.name = $"{gameObject.GetComponent<RTTI>()?.definition.id ?? "Empty"} ({position.x},{position.z})";
#endif
			}
		}

#if UNITY_EDITOR
		public static readonly Vector3 tile_origin = new(0.5f, 0f, 0.5f);
		public Vector3 TileWorldPosition(int index) => new Vector3(index % Width, 0f, index / Width) + tile_origin;
		public int WorldToMapIndex(Vector3 vec) => vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1;
#else
        public static readonly Vector3 tile_origin = Vector3.zero;
        public Vector3 TileWorldPosition(int index) => new(index % Width, 0f, index / Width);
        public int WorldToMapIndex(Vector3 vec) { vec += new Vector3(0.5f, 0f, 0.5f); return vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1; }
#endif

		public Tile GetTile(int index)
		{
			if (index < 0 || index >= indices.Length || Width <= 0) return default;
			int dataIndex = indices[index];
			return dataIndex >= 0 && dataIndex < tiles.Length ? tiles[dataIndex] : default;
		}

		public static Vector3 ScreenToWorld(Camera camera, Vector3 screenPos)
		{
			var ray = camera.ScreenPointToRay(screenPos);
			var plane = new Plane(Vector3.up, Vector3.zero);
			return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : Vector3.zero;
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

			generated.Add(new Waypoint { nTile = start });

			int cur = start;
			int dir = Navigation.NavToDest(this, cur, end);
			if (dir != 0)
			{
				while (cur != end)
				{
					if (FindAdjacentConsole(cur) != -1)
						generated.Add(new Waypoint { nTile = cur });

					int next = Navigation.GetAdjacentTile(this, cur, dir);
					if (next == -1 || next == start) break;

					var nextTile = GetTile(next);
					if (nextTile.Nav == 0) break;

					dir = Navigation.CalculateNav(dir, nextTile.Nav);
					if (dir == 0) break;

					cur = next;
				}
			}

			generated.Add(new Waypoint { nTile = end });
			currentMap.waypoints = generated.ToArray();
			Debug.Log($"Generated {currentMap.waypoints.Length} waypoints.");
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