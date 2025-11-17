// MapManager.cs
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
		// Authoritative source of truth
		// ------------------------------------------------------------------
		[SerializeField, HideInInspector] private Map currentMap;

		// ------------------------------------------------------------------
		// Runtime-only mutable state
		// ------------------------------------------------------------------
		private int[] indices;                    // Scrambled/solved visual indices
		private Tile[] tiles;                     // Instantiated runtime Tile objects
		private string[] tileDefs;                // Cached szType string per map index (for fast lookup & saving)
		private Waypoint[] waypoints;             // May be generated or overridden at runtime

		// ------------------------------------------------------------------
		// IMapData / IMapManager forwarded properties
		// ------------------------------------------------------------------
		public Map CurrentMap => currentMap;

		public int Width => currentMap?.nWidth ?? 0;
		public int Height => currentMap?.nHeight ?? 0;
		public int Count => Width * Height;

		public int[] Indices => indices;

		public Waypoint[] Waypoints => waypoints ?? currentMap?.waypoints ?? System.Array.Empty<Waypoint>();

		private void Awake()
		{
			indices = null;
			tiles = null;
			tileDefs = null;
			waypoints = null;
		}

		private void Initialise(Map map)
		{
			currentMap = map ?? throw new System.ArgumentNullException(nameof(map));

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
			tileDefs = new string[Count];

			for (int n = 0; n < tileMap.Length; ++n)
			{
				int tileDefIndex = tileMap[n];
				string szType = (tileDefIndex >= 0 && tileDefIndex < currentMap.defs?.Length)
					? currentMap.defs[tileDefIndex]
					: "tile_empty";

				if (string.IsNullOrEmpty(szType))
					szType = "tile_empty";

				tileDefs[n] = szType;
				tiles[n] = new Tile(szType);

				if (szType == "tile_empty") continue;

				var tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType);
				if (tileDef == null)
				{
					Debug.LogError($"TileDef not found for szType={szType} at tile {n}");
					continue;
				}

				tiles[n].GameObject = GeometryManager.InstantiateTile(
					tileDef, transform, TileWorldPosition(n), tiles[n].Interactive);
			}
		}

		public int GetOrAddMapDefIndex(string szType)
		{
			if (string.IsNullOrEmpty(szType))
			{
				Debug.LogError($"Invalid tile definition: szType={szType}");
				return -1;
			}

			if (currentMap.defs != null)
			{
				int existing = Array.IndexOf(currentMap.defs, szType);
				if (existing != -1) return existing;
			}

			// Grow defs array
			var oldDefs = currentMap.defs ?? Array.Empty<string>();
			var newDefs = new string[oldDefs.Length + 1];
			oldDefs.CopyTo(newDefs, 0);
			newDefs[oldDefs.Length] = szType;
			currentMap.defs = newDefs;

			Debug.Log($"Added new mapDef: szType={szType}, new index={oldDefs.Length}");
			UpdateChanges();
			return oldDefs.Length;
		}

		public void UpdateTileAt(int x, int z, int newTileDefIndex)
		{
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds ({Width}x{Height})");
				return;
			}

			int index = z * Width + x;
			if (newTileDefIndex < 0 || newTileDefIndex >= currentMap.defs.Length)
			{
				Debug.LogError($"Invalid newTileDefIndex={newTileDefIndex}");
				return;
			}

			string szType = currentMap.defs[newTileDefIndex];
			if (string.IsNullOrEmpty(szType))
			{
				Debug.LogError($"Empty szType at index {newTileDefIndex}");
				return;
			}

			if (tiles[index].GameObject != null)
				Destroy(tiles[index].GameObject);

			tileDefs[index] = szType;
			tiles[index] = new Tile(szType);

			if (szType != "tile_empty")
			{
				var tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType);
				if (tileDef != null)
				{
					tiles[index].GameObject = GeometryManager.InstantiateTile(
						tileDef, transform, TileWorldPosition(index), tiles[index].Interactive);
				}
			}

			UpdateChanges();
		}

		// -----------------------------------------------------------------------
		// Database update / save logic
		// -----------------------------------------------------------------------
		private DatabaseSerializer.DatabaseData CreateUpdatedDatabaseData()
		{
			if (currentMap == null)
				throw new InvalidOperationException("No current map set.");

			// Convert tileDefs back to logical indices
			var logicalTiles = new int[Count];
			for (int i = 0; i < Count; i++)
			{
				string szType = tileDefs[i];
				logicalTiles[i] = Array.IndexOf(currentMap.defs, szType);
				if (logicalTiles[i] == -1)
				{
					Debug.LogWarning($"Tile type '{szType}' not in mapDefs, defaulting to 0");
					logicalTiles[i] = 0;
				}
			}

			var updatedMap = new Map
			{
				name = currentMap.name,
				szEggbotCostume = currentMap.szEggbotCostume,
				szMusic = currentMap.szMusic,
				Pickups = currentMap.Pickups,
				szButtonID = currentMap.szButtonID,
				waypoints = waypoints ?? currentMap.waypoints,
				defs = currentMap.defs,
				nWidth = currentMap.nWidth,
				nHeight = currentMap.nHeight,
				tiles = logicalTiles,
				mixed = currentMap.mixed
			};

			return new DatabaseSerializer.DatabaseData
			{
				maps = DatabaseSerializer.Maps
					.Select(m => m.name == currentMap.name ? updatedMap : m)
					.ToArray(),
				themes = DatabaseSerializer.Themes.ToArray(),
				tiledefs = DatabaseSerializer.TileDefs.ToArray(),
				buttons = DatabaseSerializer.Buttons.ToArray(),
				texture_set = DatabaseSerializer.TextureSets.ToArray()
			};
		}

		public void UpdateChanges()
		{
			if (currentMap == null) return;
			var data = CreateUpdatedDatabaseData();
			DatabaseSerializer.UpdateDatabase(data);
			Debug.Log($"Map '{currentMap.name}' changes updated in memory.");
		}

		public void SaveChanges()
		{
			if (currentMap == null) return;
			var data = CreateUpdatedDatabaseData();
			DatabaseSerializer.SaveDatabase(data);
			Debug.Log($"Database saved to disk with updated map '{currentMap.name}'.");
		}

		public string GetTileDefAtIndex(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= Count) return null;
			return tileDefs[mapIndex];
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
			indices = Enumerable.Range(0, Count)
				.Select(n => n + (currentMap.mixed?[n] ?? 0))
				.ToArray();
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
				gameObject.name = $"{gameObject.GetComponent<RTTI>()?.tileDef.szType ?? "Empty"} ({position.x},{position.z})";
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
				waypoints = currentMap.waypoints;
				Debug.Log($"Using {waypoints.Length} predefined waypoints.");
				return;
			}

			var generated = new List<Waypoint>();
			int start = GetStartTile();
			int end = GetEndTile();

			if (start == -1 || end == -1)
			{
				waypoints = generated.ToArray();
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
			waypoints = generated.ToArray();
			Debug.Log($"Generated {waypoints.Length} waypoints.");
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