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
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private int[] indices;
		private int[] offsets;
		private Tile[] tiles;
		private string[] mapDefs;
		private string[] tileDefs;
		private Waypoint[] waypoints;
		private string mapName; // Replaces currentMap reference

		public Waypoint[] Waypoints => waypoints;

		public int[] Indices { get => indices; private set => indices = value; }
		public Tile[] Tiles { get => tiles; private set => tiles = value; }

		public int Width { get; private set; }
		public int Height { get; private set; }
		public int Count => Width * Height;

		private void Awake()
		{
			indices = null;
			offsets = null;
			tiles = null;
			mapDefs = null;
			tileDefs = null;
			waypoints = null;
			mapName = null;
		}

		private void Initialise(DatabaseSerializer.Map map)
		{
			mapName = map?.name;
			if (string.IsNullOrEmpty(mapName))
			{
				Debug.LogError("Map name is null or empty during initialization.");
				return;
			}

			offsets = map?.mixed;
			Width = map?.nWidth ?? 0;
			Height = map?.nHeight ?? 0;
			mapDefs = map?.defs ?? new string[0];
			tileDefs = new string[Count];

			void LoadTileData(int[] tileMap)
			{
				if (tileMap == null || tileMap.Length != Count)
				{
					Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={Count}");
					return;
				}

				tiles = new Tile[Count];
				for (var n = 0; n < tileMap.Length; ++n)
				{
					var tileDefIndex = tileMap[n];
					if (tileDefIndex < 0 || tileDefIndex >= mapDefs.Length)
					{
						Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex} at tile {n}, expected range [0, {mapDefs.Length - 1}]");
						continue;
					}

					var szType = mapDefs[tileDefIndex];
					if (string.IsNullOrEmpty(szType))
					{
						Debug.LogWarning($"Null or empty szType at tileDefIndex {tileDefIndex} for tile {n}, using tile_empty");
						szType = "tile_empty";
					}
					tileDefs[n] = szType;
					tiles[n] = new Tile(szType);
					if (szType == "tile_empty") continue;

					var tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType);
					if (tileDef == null)
					{
						Debug.LogError($"TileDef not found for szType={szType} at tileDefIndex={tileDefIndex} for tile {n}");
						continue;
					}
					tiles[n].GameObject = GeometryManager.InstantiateTile(tileDef, transform, TileWorldPosition(n), tiles[n].Interactive);
				}
			}

			LoadTileData(map.tiles);

			if (PreviewSettings.Scrambled) Scramble(); else Solve();

			Debug.AssertFormat(null != indices && null != offsets, "invalid map tile indices or offsets data");

			InitializeWindController();
			SetupWaypoints(map);

			void SetupWaypoints(DatabaseSerializer.Map map)
			{
				if (map?.waypoints != null && map.waypoints.Length > 0)
				{
					waypoints = map.waypoints;
					Debug.Log($"Using {waypoints.Length} waypoints from map data: [{string.Join(", ", waypoints.Select(w => w.nTile))}]");
					return;
				}

				var generated = new List<Waypoint>();
				var startTile = GetStartTile();
				var endTile = GetEndTile();

				if (startTile == -1 || endTile == -1)
				{
					Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
					waypoints = generated.ToArray();
					return;
				}

				generated.Add(new Waypoint { nTile = startTile });

				var cur = startTile;
				var dir = Navigation.NavToDest(this, cur, endTile);
				if (dir != 0)
				{
					while (cur != endTile)
					{
						if (FindAdjacentConsole(cur) != -1)
							generated.Add(new Waypoint { nTile = cur });

						var next = Navigation.GetAdjacentTile(this, cur, dir);
						if (next == -1 || next == startTile) break;

						var nextTile = GetTile(next);
						if (nextTile.Nav == 0) break;

						dir = Navigation.CalculateNav(dir, nextTile.Nav);
						if (dir == 0) break;

						cur = next;
					}
				}

				generated.Add(new Waypoint { nTile = endTile });
				waypoints = generated.ToArray();
				Debug.Log($"Generated {waypoints.Length} waypoints: [{string.Join(", ", waypoints.Select(w => w.nTile))}]");
			}
		}

		public int GetOrAddMapDefIndex(string szType)
		{
			if (string.IsNullOrEmpty(szType))
			{
				Debug.LogError($"Invalid tile definition: szType={szType}");
				return -1;
			}

			for (int i = 0; i < mapDefs.Length; i++)
			{
				if (mapDefs[i] == szType)
					return i;
			}

			mapDefs = mapDefs.Concat(new[] { szType }).ToArray();
			Debug.Log($"Added new mapDef: szType={szType}, new index={mapDefs.Length - 1}");
			UpdateChanges();
			return mapDefs.Length - 1;
		}

		public void UpdateTileAt(int x, int z, int newTileDefIndex)
		{
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds (Width={Width}, Height={Height})");
				return;
			}

			int index = z * Width + x;
			if (index >= Count)
			{
				Debug.LogError($"Calculated index {index} is out of bounds for tiles array (Count={Count})");
				return;
			}

			if (newTileDefIndex < 0 || newTileDefIndex >= mapDefs.Length)
			{
				Debug.LogError($"Invalid newTileDefIndex={newTileDefIndex}, must be between 0 and {mapDefs.Length - 1}");
				return;
			}

			var szType = mapDefs[newTileDefIndex];
			if (string.IsNullOrEmpty(szType))
			{
				Debug.LogError($"Null or empty szType at tileDefIndex {newTileDefIndex} for tile ({x}, {z})");
				return;
			}

			if (tiles[index].GameObject != null)
				Destroy(tiles[index].GameObject);

			tileDefs[index] = szType;
			tiles[index] = new Tile(szType);
			if (szType != "tile_empty")
			{
				var tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType);
				if (tileDef == null)
				{
					Debug.LogError($"TileDef not found for szType={szType} at tileDefIndex={newTileDefIndex} for tile ({x}, {z})");
					return;
				}
				tiles[index].GameObject = GeometryManager.InstantiateTile(tileDef, transform, TileWorldPosition(index), tiles[index].Interactive);
			}

			UpdateChanges();
		}

		// ---------------------------------------------------------------------------
		// Create updated database payload using mapName to locate the map
		// ---------------------------------------------------------------------------
		private DatabaseSerializer.DatabaseData CreateUpdatedDatabaseData()
		{
			if (string.IsNullOrEmpty(mapName))
				throw new InvalidOperationException("Map name is null – cannot create database data.");

			var targetMap = DatabaseSerializer.Maps.FirstOrDefault(m => m.name == mapName);
			if (targetMap == null)
				throw new InvalidOperationException($"Map with name '{mapName}' not found in database.");

			// Sync logical tile indices back into the map
			var logicalTiles = new int[Count];
			for (int i = 0; i < Count; i++)
			{
				var szType = tileDefs[i];
				logicalTiles[i] = Array.IndexOf(mapDefs, szType);
				if (logicalTiles[i] == -1)
				{
					Debug.LogWarning($"Tile type '{szType}' not in mapDefs, defaulting to 0");
					logicalTiles[i] = 0;
				}
			}

			// Clone and update the map
			var updatedMap = new DatabaseSerializer.Map
			{
				name = targetMap.name,
				szEggbotCostume = targetMap.szEggbotCostume,
				szMusic = targetMap.szMusic,
				Pickups = targetMap.Pickups,
				szButtonID = targetMap.szButtonID,
				waypoints = waypoints,
				defs = mapDefs,
				nWidth = Width,
				nHeight = Height,
				tiles = logicalTiles,
				mixed = offsets
			};

			return new DatabaseSerializer.DatabaseData
			{
				maps = DatabaseSerializer.Maps
					.Select(m => m.name == mapName ? updatedMap : m)
					.ToArray(),
				themes = DatabaseSerializer.Themes.ToArray(),
				tiledefs = DatabaseSerializer.TileDefs.ToArray(),
				buttons = DatabaseSerializer.Buttons.ToArray(),
				texture_set = DatabaseSerializer.TextureSets.ToArray()
			};
		}

		public void UpdateChanges()
		{
			if (string.IsNullOrEmpty(mapName))
			{
				Debug.LogError("Cannot update changes: map name is null or empty");
				return;
			}

			var updatedData = CreateUpdatedDatabaseData();
			DatabaseSerializer.UpdateDatabase(updatedData);
			Debug.Log($"Map '{mapName}' changes updated in memory (tiles + waypoints).");
		}

		public void SaveChanges()
		{
			if (string.IsNullOrEmpty(mapName))
			{
				Debug.LogError("Cannot save changes: map name is null or empty");
				return;
			}

			var updatedData = CreateUpdatedDatabaseData();
			DatabaseSerializer.SaveDatabase(updatedData);
			Debug.Log("Database saved to disk with updated mapDefs, tiles, and waypoints");
		}

		public string GetTileDefAtIndex(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= Count)
			{
				Debug.LogWarning($"Invalid mapIndex={mapIndex}, must be between 0 and {Count - 1}");
				return null;
			}
			return tileDefs[mapIndex];
		}

		public int GetStartTile()
		{
			if (waypoints != null && waypoints.Length > 0)
				return waypoints[0].nTile;

			for (var i = 0; i < Count; ++i)
			{
				if (GetTile(i).IsStart)
					return i;
			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (waypoints != null && waypoints.Length > 0)
				return waypoints.Last().nTile;

			for (var i = 0; i < Count; ++i)
			{
				if (GetTile(i).IsEnd)
					return i;
			}
			Debug.LogError("No end tile found!");
			return -1;
		}

		public int FindAdjacentConsole(int nTile)
		{
			var tile = GetTile(nTile);
			if (0 == tile.Nav) return -1;

			foreach (var dirBit in Navigation.Directions)
			{
				var consoleTileIndex = Navigation.GetAdjacentTile(this, nTile, dirBit);
				if (-1 == consoleTileIndex) continue;

				var consoleTile = GetTile(consoleTileIndex);
				if (true != consoleTile.IsConsole) continue;

				if (dirBit == Navigation.GetOppositeDirection(consoleTile.Nav))
					return consoleTileIndex;
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
				{
					var position = TileWorldPosition(n);
					swayComponents.Add((sway, position));
				}
			}

			windController.Initialize(swayComponents);
			Debug.Log($"WindController initialized with {swayComponents.Count} MorphGeomSway components.");
		}

		public void Scramble()
		{
			indices = Enumerable.Range(0, Count).Select(n => n + offsets[n]).ToArray();
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
			if (index < 0 || index >= Indices.Length || Width <= 0) return default;
			var dataIndex = Indices[index];
			return dataIndex >= 0 && dataIndex < tiles.Length ? tiles[dataIndex] : default;
		}

		public static Vector3 ScreenToWorld(Camera camera, Vector3 screenPos)
		{
			var ray = camera.ScreenPointToRay(screenPos);
			var mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return Vector3.zero;
			return ray.GetPoint(distance);
		}

		public static MapManager Instantiate(DatabaseSerializer.Map map, Transform parent = null)
		{
			if (map == null || string.IsNullOrEmpty(map.name))
			{
				Debug.LogError("Cannot instantiate MapManager: map or map.name is null.");
				return null;
			}

			var container = new GameObject($"Map: {map.name}");
			if (null != parent) container.transform.SetParent(parent, false);
			var mapManager = container.AddComponent<MapManager>();
			mapManager.Initialise(map);
			return mapManager;
		}
	}
}