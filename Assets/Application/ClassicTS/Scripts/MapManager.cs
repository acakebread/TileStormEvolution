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

		DatabaseSerializer.Waypoint[] Waypoints { get; }
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private DatabaseSerializer.Map currentMap;//this is going to completely replaced with a local copy of the map state
		private int[] indices;
		private int[] offsets;
		private Tile[] tiles;
		private string[] mapDefs;//all unique defs
		private string[] tileDefs;//map sized array of defs
		private DatabaseSerializer.Waypoint[] waypoints;
		public DatabaseSerializer.Waypoint[] Waypoints => waypoints;

		public int[] Indices { get => indices; private set => indices = value; }
		public Tile[] Tiles { get => tiles; private set => tiles = value; }

		public int Width { get; private set; }
		public int Height { get; private set; }
		public int Count { get => Width * Height; }

		private void Awake()
		{
			indices = null;
			offsets = null;
			tiles = null;
			mapDefs = null;
			tileDefs = null;
			waypoints = null;
			currentMap = null;
		}

		private void Initialise(DatabaseSerializer.Map map)
		{
			currentMap = map;
			offsets = map?.mixed;
			Width = map?.nWidth ?? 0;
			Height = map?.nHeight ?? 0;
			mapDefs = map?.defs ?? new string[0];
			tileDefs = new string[Width * Height];

			void LoadTileData(int[] tileMap)
			{
				if (tileMap == null || tileMap.Length != Width * Height)
				{
					Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={Width * Height}");
					return;
				}

				tiles = new Tile[Width * Height];
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

			if (currentMap == null || currentMap.tiles == null)
			{
				Debug.LogError("Map or tiles array is null");
				return;
			}

			int index = z * Width + x;
			if (index >= currentMap.tiles.Length)
			{
				Debug.LogError($"Calculated index {index} is out of bounds for tiles array (length={currentMap.tiles.Length})");
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

		public void UpdateChanges()
		{
			if (currentMap == null)
			{
				Debug.LogError("Cannot update changes: map is null");
				return;
			}

			var logicalTiles = new int[Count];
			for (int i = 0; i < Count; i++)
			{
				var szType = tileDefs[indices[i]];

				logicalTiles[i] = Array.IndexOf(mapDefs, szType);
				if (logicalTiles[i] == -1)
				{
					Debug.LogWarning($"Tile type '{szType}' not in mapDefs, defaulting to 0");
					logicalTiles[i] = 0;
				}
			}

			currentMap.tiles = logicalTiles;
			currentMap.defs = mapDefs;

			var updatedData = new DatabaseSerializer.DatabaseData
			{
				maps = DatabaseSerializer.Maps.Select(m => m.name == currentMap.name ? currentMap : m).ToArray(),
				themes = DatabaseSerializer.Themes.ToArray(),
				tiledefs = DatabaseSerializer.TileDefs.ToArray(),
				buttons = DatabaseSerializer.Buttons.ToArray(),
				texture_set = DatabaseSerializer.TextureSets.ToArray()
			};

			DatabaseSerializer.UpdateDatabase(updatedData);
			Debug.Log($"Map '{currentMap.name}' changes updated in memory.");
		}

		public void SaveChanges()
		{
			if (currentMap == null)
			{
				Debug.LogError("Cannot save changes: map is null");
				return;
			}

			currentMap.defs = mapDefs;

			DatabaseSerializer.SaveDatabase(new DatabaseSerializer.DatabaseData
			{
				maps = DatabaseSerializer.Maps.ToArray(),
				themes = DatabaseSerializer.Themes.ToArray(),
				tiledefs = DatabaseSerializer.TileDefs.ToArray(),
				buttons = DatabaseSerializer.Buttons.ToArray(),
				texture_set = DatabaseSerializer.TextureSets.ToArray()
			});
			Debug.Log("Database saved to disk with updated mapDefs and tile data");
		}

		public int GetTileDefIndexAt(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= currentMap.tiles.Length)
			{
				Debug.LogWarning($"Invalid mapIndex={mapIndex}, must be between 0 and {currentMap.tiles.Length - 1}");
				return -1;
			}
			return currentMap.tiles[mapIndex];
		}

		public string[] GetMapDefs() => mapDefs;

		private void SetupWaypoints(DatabaseSerializer.Map map)
		{
			waypoints = map?.waypoints;
			if (null != waypoints && waypoints.Length > 0)
			{
				Debug.Log($"Using {waypoints.Length} waypoints from map data: [{string.Join(", ", waypoints.Select(w => w.nTile))}]");
				return;
			}

			var generatedWaypoints = new List<DatabaseSerializer.Waypoint>();
			var startTile = GetStartTile();
			var endTile = GetEndTile();

			if (-1 == startTile || -1 == endTile)
			{
				Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
				waypoints = generatedWaypoints.ToArray();
				return;
			}

			generatedWaypoints.Add(new DatabaseSerializer.Waypoint { nTile = startTile });

			var currentTile = startTile;
			var currentDir = Navigation.NavToDest(this, currentTile, endTile);
			if (0 != currentDir)
			{
				while (currentTile != endTile)
				{
					if (FindAdjacentConsole(currentTile) != -1)
						generatedWaypoints.Add(new DatabaseSerializer.Waypoint { nTile = currentTile });

					var nextTileIndex = Navigation.GetAdjacentTile(this, currentTile, currentDir);
					if (-1 == nextTileIndex || nextTileIndex == startTile) break;

					var nextTile = GetTile(nextTileIndex);
					if (0 == nextTile.Nav) break;

					currentDir = Navigation.CalculateNav(currentDir, nextTile.Nav);
					if (0 == currentDir) break;

					currentTile = nextTileIndex;
				}
			}

			generatedWaypoints.Add(new DatabaseSerializer.Waypoint { nTile = endTile });

			waypoints = generatedWaypoints.ToArray();
			Debug.Log($"Generated {waypoints.Length} waypoints: [{string.Join(", ", waypoints.Select(w => w.nTile))}]");
		}

		public int GetStartTile()
		{
			if (null != waypoints && 0 != waypoints.Length)
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
			if (null != waypoints && 0 != waypoints.Length)
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
				if (-1 == consoleTileIndex)
					continue;

				var consoleTile = GetTile(consoleTileIndex);
				if (true != consoleTile.IsConsole)
					continue;

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

		private static readonly Vector3 tile_origin = new(0.5f, 0f, 0.5f);

		public Vector3 TileWorldPosition(int index) => new Vector3(index % Width, 0f, index / Width) + tile_origin;

		public int WorldToMapIndex(Vector3 vec) => vec.x >= 0 && vec.x < Width && vec.z >= 0 && vec.z < Height ? (int)vec.z * Width + (int)vec.x : -1;

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
			var container = new GameObject($"Map: {map.name}");
			if (null != parent) container.transform.SetParent(parent, false);
			var mapManager = container.AddComponent<MapManager>();
			mapManager.Initialise(map);
			return mapManager;
		}
	}
}