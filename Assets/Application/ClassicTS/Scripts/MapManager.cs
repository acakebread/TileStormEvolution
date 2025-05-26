using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class MapManager : MonoBehaviour, IMap
	{
		private struct Tile
		{
			public TileProperties Properties;
			public GameObject GameObject;
			//public Vector3 position { get => null != GameObject ? GameObject.transform.position : Vector3.zero; set { if (null != GameObject) GameObject.transform.position = value; } }
		}

		private DatabaseLoader.Map currentMap;
		private Tile[] tileArray;
		private int[] tiles;
		private DatabaseLoader.Waypoint[] waypoints;

		public string CurrentMapName => currentMap?.name;
		public int Width => currentMap?.tiles.nWidth ?? 0;
		public int Height => currentMap?.tiles.nHeight ?? 0;
		public DatabaseLoader.Waypoint[] Waypoints => waypoints;
		public string EggbotCostume => currentMap?.szEggbotCostume;

		private void Awake()
		{
			currentMap = null;
			waypoints = null;
			tileArray = null;
			tiles = null;
		}

		public bool IsValidTileIndex(int tileIndex) => tileIndex >= 0 && tileIndex < tiles?.Length && Width > 0;

		public TileProperties GetTileProperties(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex)) return null;
			var dataIndex = tiles[tileIndex];
			return dataIndex >= 0 && dataIndex < tileArray.Length ? tileArray[dataIndex].Properties : null;
		}

		public GameObject GetTileGameObject(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex)) return null;
			var dataIndex = tiles[tileIndex];
			return dataIndex >= 0 && dataIndex < tileArray.Length ? tileArray[dataIndex].GameObject : null;
		}

		public GridCoord GetTileDelta(int nSrc, int nDst) => GetTileCoordinates(nDst) - GetTileCoordinates(nSrc);

		public GridCoord GetTileCoordinates(int tileIndex) => new(tileIndex % Width, tileIndex / Width);

		public Vector3 GetTilePosition(int tileIndex) => GetTileCoordinates(tileIndex).ToPosition();

		public float GetTileDistance(int nSrc, int nDst) => GetTileDelta(nSrc, nDst).magnitude;

		public int ToIndex(GridCoord coord) => coord.Z * Width + coord.X;

		public int[] GetTiles() => tiles;

		public Vector3 ScreenToWorld(Vector3 screenPos)
		{
			var ray = Camera.main.ScreenPointToRay(screenPos);
			var mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return Vector3.zero;
			return ray.GetPoint(distance);
		}

		public int ScreenToMapIndex(Vector3 screenPos)
		{
			var worldPos = ScreenToWorld(screenPos);
			if (worldPos.x < 0 || worldPos.x >= Width || worldPos.z < 0 || worldPos.z >= Height) return -1;
			return ToIndex(new GridCoord(worldPos));
		}

		public int WorldToMapIndex(Vector3 worldPos)
		{
			if (worldPos.x < 0 || worldPos.x >= Width || worldPos.z < 0 || worldPos.z >= Height) return -1;
			return ToIndex(new GridCoord(worldPos));
		}

		private void Load(string mapName)
		{
			if (null == mapName) return;

			currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);

			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			name = $"Map_{currentMap.name}";
			LoadTileData(currentMap.tiles);

			waypoints = currentMap.waypoints?.Where(w => w != null).ToArray();
			if (null == waypoints || 0 == waypoints.Length)
				waypoints = Navigation.SetupWaypoints(this);

			if (PreviewSettings.Scrambled)
			{
				Scramble();//scramble calls UpdateTileObjectNamesAndPositions
				return;
			}

			UpdateTileObjectNamesAndPositions();
		}

		private void LoadTileData(DatabaseLoader.Tiles tiles)
		{
			var tileMap = tiles.TileData.bytes;
			if (tileMap == null || tileMap.Length != tiles.nWidth * tiles.nHeight)
			{
				Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={tiles.nWidth * tiles.nHeight}");
				return;
			}

			this.tiles = new int[tiles.nWidth * tiles.nHeight];
			tileArray = new Tile[tiles.nWidth * tiles.nHeight];

			for (var index = 0; index < tileMap.Length; ++index)
			{
				this.tiles[index] = index;

				var tileDefIndex = tileMap[index];
				if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length)
				{
					Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex}");
					continue;
				}

				var szType = currentMap.defs[tileDefIndex].szType;
				if (szType == "tile_empty") continue;

				var szTheme = currentMap.defs[tileDefIndex].szTheme;
				if (string.IsNullOrEmpty(szTheme)) Debug.LogWarning($"Null szTheme at tileDefIndex {tileDefIndex}");

				var properties = TilePropertiesManager.GetOrCreateTileProperties(szType, szTheme);
				if (null == properties) continue;
				tileArray[index].Properties = properties;

				var coord = GetTileCoordinates(index);
				if (szType == "tile_invisible")
				{
					if (PreviewSettings.ShowHiddenTiles) tileArray[index].GameObject = GeometryManager.CreateDebugTile(transform, coord.ToPosition());
					continue;
				}

				var tileDef = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				tileArray[index].GameObject = GeometryManager.InstantiateTile(tileDef, transform, coord.ToPosition(), properties.Interactive);
			}
		}

		public void Scramble()
		{
			if (null  == currentMap?.mixed?.TileData?.bytes || null == tiles)
			{
				Debug.LogWarning("Cannot scramble: invalid map or tiles data");
				return;
			}

			var offsets = currentMap.mixed.TileData.bytes;
			for (var n = 0; n < tiles.Length; ++n) tiles[n] = offsets[n] + n;
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			for (var n = 0; n < tiles.Length; ++n) tiles[n] = n; 
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			for (var n = 0; n < tiles.Length; ++n)
			{
				var gameObject = GetTileGameObject(n);
				if (gameObject == null) continue;
				var coord = GetTileCoordinates(n);
				gameObject.transform.position = coord.ToPosition();
#if DEBUG
				gameObject.name = $"{GetTileProperties(n)?.Type ?? "Empty"}_{coord.X}_{coord.Z}";
#endif
			}
		}

		public static MapManager Instantiate(Transform parent = null, string mapName = "Industrial 01")
		{
			var container = new GameObject("MapManager");
			if (null != parent) container.transform.SetParent(parent, false);

			var mapManager = container.AddComponent<MapManager>();
			mapManager.Load(mapName);
			return mapManager;
		}
	}
}