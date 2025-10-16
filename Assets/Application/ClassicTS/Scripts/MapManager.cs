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
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private int[] indices;
		private int[] offsets;
		private Tile[] tiles;
		private DatabaseSerializer.Tiles mapTiles; // Store only the tiles data for updating
		private DatabaseSerializer.MapTileDef[] mapDefs; // Store defs for tile updates

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
		}

		private void Initialise(DatabaseSerializer.Map map)
		{
			offsets = map?.mixed?.TileData?.bytes;
			Width = map?.tiles.nWidth ?? 0;
			Height = map?.tiles.nHeight ?? 0;
			mapTiles = map?.tiles; // Store tiles for updating
			mapDefs = map?.defs; // Store defs for tile updates

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
					if (tileDefIndex < 0 || tileDefIndex >= map.defs.Length)
					{
						Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex}");
						continue;
					}

					var szType = map.defs[tileDefIndex].szType;
					if (string.IsNullOrEmpty(szType)) Debug.LogWarning($"Null szType at tileDefIndex {tileDefIndex}");
					var szTheme = map.defs[tileDefIndex].szTheme;
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

		public void UpdateTileAt(int x, int z, int newTileDefIndex)
		{
			if (x < 0 || x >= Width || z < 0 || z >= Height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds (Width={Width}, Height={Height})");
				return;
			}

			if (mapTiles == null || mapTiles.TileData?.bytes == null)
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

			// Update the map tile data
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

			// Update the GameObject name and position
			if (tiles[index].GameObject != null)
			{
				var position = TileWorldPosition(index);
				tiles[index].GameObject.transform.position = position;
				var namePosition = position - tile_origin;
#if DEBUG
				tiles[index].GameObject.name = $"{tiles[index].GameObject.GetComponent<RTTI>()?.tileDef.szType ?? "Empty"} ({namePosition.x},{namePosition.z})";
#endif
			}

			Debug.Log($"Updated tile at ({x}, {z}) to tileDefIndex={newTileDefIndex}");
		}

		public void SetupWaypoints(DatabaseSerializer.Map map)
		{
			waypoints = map.waypoints?.Where(w => w != null).ToArray();
			if (null == waypoints || 0 == waypoints.Length)
				waypoints = GenerateWaypoints();

			DatabaseSerializer.Waypoint[] GenerateWaypoints()
			{
				var generatedWaypoints = new List<DatabaseSerializer.Waypoint>();
				if (0 == Count)
				{
					Debug.LogWarning("Cannot setup waypoints: invalid tile data");
					return generatedWaypoints.ToArray();
				}

				var startTile = GetStartTile();
				var endTile = GetEndTile();

				if (-1 == startTile || -1 == endTile)
				{
					Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
					return generatedWaypoints.ToArray();
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

				Debug.Log($"Generated {generatedWaypoints.Count} waypoints: [{string.Join(", ", generatedWaypoints.Select(w => w.nTile))}]");
				return generatedWaypoints.ToArray();
			}
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