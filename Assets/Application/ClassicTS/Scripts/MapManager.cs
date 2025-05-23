using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class MapManager : MonoBehaviour, IMap
	{
		private struct TileData
		{
			public TileProperties Properties;
			public GameObject GameObject;

			public Vector3 position
			{
				get => null != GameObject ? GameObject.transform.position : Vector3.zero;
				set { if (null != GameObject) GameObject.transform.position = value; }
			}
		}

		private DatabaseLoader.Map currentMap;
		//private GameObject mapRoot;
		private int[] tiles;
		private TileData[] tileDataArray;
		private List<DatabaseLoader.Waypoint> waypoints;

		public string CurrentMapName => currentMap?.name;
		public int Width => currentMap?.tiles.nWidth ?? 0;
		public int Height => currentMap?.tiles.nHeight ?? 0;
		public GameObject GetMapRoot() => gameObject;
		public IReadOnlyList<DatabaseLoader.Waypoint> Waypoints => waypoints?.AsReadOnly();
		public string EggbotCostume => currentMap?.szEggbotCostume;

		public static MapManager Instantiate(string mapName, Transform parent = null)
		{
			var container = new GameObject("MapManager");
			if (null != parent) container.transform.SetParent(parent, false);

			var mapManager = container.AddComponent<MapManager>();
			mapManager.Load(mapName);
			return mapManager;
		}

		private void Awake()
		{
			//if (null != mapRoot) Destroy(mapRoot);
			//mapRoot = null;
			currentMap = null;
			waypoints = null;
			tiles = null;
			tileDataArray = null;

			if (TileStripHelper.SpareTile != null) // Clear static SpareTile
			{
				Destroy(TileStripHelper.SpareTile);
				TileStripHelper.SpareTile = null;
			}
		}

		public bool IsValidTileIndex(int tileIndex) => tileIndex >= 0 && tileIndex < tiles?.Length && Width > 0;

		public TileProperties GetTileProperties(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex)) return null;
			int dataIndex = tiles[tileIndex];
			return dataIndex >= 0 && dataIndex < tileDataArray.Length ? tileDataArray[dataIndex].Properties : null;
		}

		public GameObject GetTileGameObject(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex)) return null;
			int dataIndex = tiles[tileIndex];
			return dataIndex >= 0 && dataIndex < tileDataArray.Length ? tileDataArray[dataIndex].GameObject : null;
		}

		public GridCoord GetTileCoordinates(int tileIndex) => new(tileIndex % Width, tileIndex / Width);

		public Vector3 GetTilePosition(int tileIndex) => GetTileCoordinates(tileIndex).ToPosition();

		public float GetTileDistance(int nSrc, int nDst) => (GetTilePosition(nDst) - GetTilePosition(nSrc)).magnitude;

		public int ToIndex(GridCoord coord) => coord.Z * Width + coord.X;

		public int[] GetTiles() => tiles;

		public Vector3 ScreenToWorld(Vector3 screenPos)
		{
			Ray ray = Camera.main.ScreenPointToRay(screenPos);
			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
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

			//Reset();
			currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);

			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			//mapRoot = new GameObject($"Map_{currentMap.name}");
			//mapRoot.transform.SetParent(transform, false);

			name = $"Map_{currentMap.name}";
			LoadTileData(currentMap.tiles);

			waypoints = currentMap.waypoints?.Where(w => w != null).ToList();
			if (waypoints == null || waypoints.Count == 0)
				waypoints = Navigation.SetupWaypoints(this);

			if (PreviewSettings.Scrambled)
				Scramble();

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
			var tileDataList = new List<TileData>();

			for (var index = 0; index < tileMap.Length; index++)
			{
				GameObject gameObject = null;
				TileProperties properties = null;

				var tileDefIndex = tileMap[index];
				if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length)
				{
					Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex}");
					this.tiles[index] = -1;
					continue;
				}

				var szTheme = currentMap.defs[tileDefIndex].szTheme;
				if (string.IsNullOrEmpty(szTheme)) Debug.LogWarning($"Null szTheme at tileDefIndex {tileDefIndex}");

				var szType = currentMap.defs[tileDefIndex].szType;
				if (szType == "tile_empty")
				{
					this.tiles[index] = -1;
					continue;
				}

				properties = TilePropertiesManager.GetOrCreateTileProperties(szType, szTheme);
				if (properties == null)
				{
					this.tiles[index] = -1;
					continue;
				}

				var coord = GetTileCoordinates(index);
				if (szType == "tile_invisible")
				{
					if (PreviewSettings.ShowHiddenTiles) gameObject = GeometryManager.CreateDebugTile(transform, coord.ToPosition());

					this.tiles[index] = tileDataList.Count;
					tileDataList.Add(new TileData { Properties = properties, GameObject = gameObject });
					continue;
				}

				var tileDef = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				gameObject = GeometryManager.InstantiateTile(tileDef, transform, coord.ToPosition(), properties.Interactive);
				this.tiles[index] = tileDataList.Count;
				tileDataList.Add(new TileData { Properties = properties, GameObject = gameObject });
			}

			tileDataArray = tileDataList.ToArray();
		}

		public void Scramble()
		{
			if (null  == currentMap?.mixed?.TileData?.bytes|| null == tiles)
			{
				Debug.LogWarning("Cannot scramble: invalid map or tiles data");
				return;
			}

			var scrambledTiles = new int[tiles.Length];
			var offsets = currentMap.mixed.TileData.bytes;
			for (var index = 0; index < tiles.Length; index++)
			{
				var scrambledIndex = index + offsets[index];
				if (scrambledIndex >= 0 && scrambledIndex < tiles.Length)
					scrambledTiles[index] = tiles[scrambledIndex];
				else
					scrambledTiles[index] = -1;
			}

			tiles = scrambledTiles;
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			if (null == currentMap?.tiles?.TileData?.bytes || null == tiles)
			{
				Debug.LogWarning("Cannot solve: invalid map or tiles data");
				return;
			}

			var def = 0;
			var solvedTiles = new int[tiles.Length];
			for (var index = 0; index < tiles.Length; index++)
			{
				solvedTiles[index] = -1;
				if (0 != currentMap.tiles.TileData.bytes[index])
				{
					solvedTiles[index] = def;
					def++;
				}
			}
			tiles = solvedTiles;
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			for (var index = 0; index < tiles.Length; index++)
			{
				var gameObject = GetTileGameObject(index);
				if (gameObject == null) continue;
				var coord = GetTileCoordinates(index);
#if DEBUG
				gameObject.name = $"{GetTileProperties(index)?.Type ?? "Empty"}_{coord.X}_{coord.Z}";
#endif
				gameObject.transform.position = coord.ToPosition();
			}
		}
	}
}