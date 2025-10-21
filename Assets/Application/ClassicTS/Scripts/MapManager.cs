using UnityEngine;
using System.Linq;
using System.Collections.Generic;
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
		DatabaseSerializer.Waypoint[] Waypoints { get; }
		void SetupWaypoints(DatabaseSerializer.Map map);
		int GetStartTile();
		int GetEndTile();
		int FindAdjacentConsole(int nTile);
		int GetOrAddMapDefIndex(string szType, string szTheme);
		void SaveChanges();
		int GetTileDefIndexAt(int mapIndex);
		DatabaseSerializer.MapTileDef[] GetMapDefs(); // Added method
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private int[] indices;
		private int[] offsets;
		private Tile[] tiles;
		private DatabaseSerializer.Tiles mapTiles;
		private DatabaseSerializer.MapTileDef[] mapDefs;
		private DatabaseSerializer.Map currentMap;
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
			mapTiles = null;
			mapDefs = null;
			waypoints = null;
			currentMap = null;
		}

		private void Initialise(DatabaseSerializer.Map map)
		{
			currentMap = map;
			offsets = map?.mixed?.TileData?.bytes;
			Width = map?.tiles.nWidth ?? 0;
			Height = map?.tiles.nHeight ?? 0;
			mapTiles = map?.tiles;
			mapDefs = map?.defs;

			void LoadTileData(DatabaseSerializer.Tiles dbTiles)
			{
				var tileMap = dbTiles.TileData.bytes;
				if (tileMap == null || tileMap.Length != dbTiles.nWidth * dbTiles.nHeight)
				{
					Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={dbTiles.nWidth * dbTiles.nHeight}");
					return;
				}

				tiles = new Tile[dbTiles.nWidth * dbTiles.nHeight];
				for (var n = 0; n < tileMap.Length; ++n)
				{
					var tileDefIndex = tileMap[n];
					if (tileDefIndex < 0 || tileDefIndex >= mapDefs.Length)
					{
						Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex}");
						continue;
					}

					var szType = mapDefs[tileDefIndex].szType;
					if (string.IsNullOrEmpty(szType)) Debug.LogWarning($"Null szType at tileDefIndex {tileDefIndex}");
					var szTheme = mapDefs[tileDefIndex].szTheme;
					if (string.IsNullOrEmpty(szTheme)) Debug.LogWarning($"Null szTheme at tileDefIndex {tileDefIndex}");
					tiles[n] = new Tile(szType, szTheme);
					if (szType == "tile_empty") continue;

					var tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					tiles[n].GameObject = GeometryManager.InstantiateTile(tileDef, transform, TileWorldPosition(n), tiles[n].Interactive);
				}
			}

			LoadTileData(map.tiles);

			if (PreviewSettings.Scrambled) Scramble(); else Solve();

			Debug.AssertFormat(null != indices && null != offsets, "invalid map tile indices or offsets data");

			InitializeWindController();

			SetupWaypoints(map);
		}

		public int GetOrAddMapDefIndex(string szType, string szTheme)
		{
			if (string.IsNullOrEmpty(szType) || string.IsNullOrEmpty(szTheme))
			{
				Debug.LogError($"Invalid tile definition: szType={szType}, szTheme={szTheme}");
				return -1;
			}

			// Find existing MapTileDef
			for (int i = 0; i < mapDefs.Length; i++)
			{
				if (mapDefs[i].szType == szType && mapDefs[i].szTheme == szTheme)
					return i;
			}

			// Verify TileDef exists
			var tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
			if (tileDef == null)
			{
				Debug.LogError($"TileDef not found for szType={szType}, szTheme={szTheme}");
				return -1;
			}

			// Add new MapTileDef
			var newDef = new DatabaseSerializer.MapTileDef { szType = szType, szTheme = szTheme };
			mapDefs = mapDefs.Concat(new[] { newDef }).ToArray();
			Debug.Log($"Added new MapTileDef: szType={szType}, szTheme={szTheme}, new index={mapDefs.Length - 1}");

			return mapDefs.Length - 1;
		}

		public void UpdateTileAt(int x, int z, int newTileDefIndex)
		{
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds (Width={Width}, Height={Height})");
				return;
			}

			if (mapTiles == null || mapTiles.TileData == null)
			{
				Debug.LogError("Map tiles or tile data is null");
				return;
			}

			int index = z * Width + x;
			if (index >= mapTiles.TileData.bytes.Length)
			{
				Debug.LogError($"Calculated index {index} is out of bounds for tile data array (length={mapTiles.TileData.bytes.Length})");
				return;
			}

			if (newTileDefIndex < 0 || newTileDefIndex >= mapDefs.Length)
			{
				Debug.LogError($"Invalid newTileDefIndex={newTileDefIndex}, must be between 0 and {mapDefs.Length - 1}");
				return;
			}

			// Update tile data immediately
			mapTiles.TileData.bytes[index] = newTileDefIndex;

			// Update the tile at the specified index
			var szType = mapDefs[newTileDefIndex].szType;
			var szTheme = mapDefs[newTileDefIndex].szTheme;
			if (string.IsNullOrEmpty(szType)) Debug.LogWarning($"Null szType at tileDefIndex {newTileDefIndex}");
			if (string.IsNullOrEmpty(szTheme)) Debug.LogWarning($"Null szTheme at tileDefIndex {newTileDefIndex}");

			// Destroy the existing GameObject, if any
			if (tiles[index].GameObject != null)
			{
				Destroy(tiles[index].GameObject);
			}

			// Create new tile
			tiles[index] = new Tile(szType, szTheme);
			if (szType != "tile_empty")
			{
				var tileDef = DatabaseSerializer.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				tiles[index].GameObject = GeometryManager.InstantiateTile(tileDef, transform, TileWorldPosition(index), tiles[index].Interactive);
			}
		}

		public void SaveChanges()
		{
			if (currentMap == null)
			{
				Debug.LogError("Cannot save changes: map is null");
				return;
			}

			// Save to DatabaseSerializer
			DatabaseSerializer.SaveDatabase(new DatabaseSerializer.DatabaseData
			{
				maps = DatabaseSerializer.Maps.ToArray(),
				themes = DatabaseSerializer.Themes.ToArray(),
				tiledefs = DatabaseSerializer.TileDefs.ToArray(),
				buttons = DatabaseSerializer.Buttons.ToArray(),
				texture_set = DatabaseSerializer.TextureSets.ToArray()
			});
			Debug.Log("Database saved to disk");
		}

		public int GetTileDefIndexAt(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= mapTiles.TileData.bytes.Length)
			{
				Debug.LogWarning($"Invalid mapIndex={mapIndex}, must be between 0 and {mapTiles.TileData.bytes.Length - 1}");
				return -1;
			}
			return mapTiles.TileData.bytes[mapIndex];
		}

		public DatabaseSerializer.MapTileDef[] GetMapDefs()
		{
			return mapDefs;
		}

		public void SetupWaypoints(DatabaseSerializer.Map map)
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

		public static Vector3 ScreenToWorld(Vector3 screenPos)
		{
			var ray = Camera.main.ScreenPointToRay(screenPos);
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