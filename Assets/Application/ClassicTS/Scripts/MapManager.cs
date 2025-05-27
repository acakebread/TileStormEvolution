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

		private int[] indexes;
		private int[] offsets;
		private Tile[] tiles;
		public int Width { get; set; }
		public int Height { get; set; }

		private void Awake()
		{
			indexes = null;
			offsets = null;
			tiles = null;
		}

		private bool IsValidTileIndex(int tileIndex) => tileIndex >= 0 && tileIndex < indexes?.Length && Width > 0;

		public TileProperties GetTileProperties(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex)) return null;
			var dataIndex = indexes[tileIndex];
			return dataIndex >= 0 && dataIndex < tiles.Length ? tiles[dataIndex].Properties : null;
		}

		public GameObject GetTileGameObject(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex)) return null;
			var dataIndex = indexes[tileIndex];
			return dataIndex >= 0 && dataIndex < tiles.Length ? tiles[dataIndex].GameObject : null;
		}

		public GridCoord GetTileDelta(int nSrc, int nDst) => GetTileCoordinates(nDst) - GetTileCoordinates(nSrc);

		private GridCoord GetTileCoordinates(int tileIndex) => new(tileIndex % Width, tileIndex / Width);

		public Vector3 GetTilePosition(int tileIndex) => GetTileCoordinates(tileIndex).ToPosition();

		public float GetTileDistance(int nSrc, int nDst) => GetTileDelta(nSrc, nDst).magnitude;

		private int ToIndex(int X, int Z) => (X >= 0 && X < Width && Z >= 0 || Z < Height) ? Z * Width + X : -1;

		//public int ToIndex(GridCoord coord) => ToIndex(coord.X, coord.Z);

		public int[] GetTiles() => indexes;

		public Vector3 ScreenToWorld(Vector3 screenPos)
		{
			var ray = Camera.main.ScreenPointToRay(screenPos);
			var mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return Vector3.zero;
			return ray.GetPoint(distance);
		}

		public int WorldToMapIndex(Vector3 worldPos) => (worldPos.x >= 0 && worldPos.x < Width && worldPos.z >= 0 && worldPos.z < Height) ? ToIndex(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z)) : -1;

		public int ScreenToMapIndex(Vector3 screenPos) => WorldToMapIndex(ScreenToWorld(screenPos));

		private void Initialise(DatabaseLoader.Map map)
		{
			offsets = map?.mixed?.TileData?.bytes;
			Width = map?.tiles.nWidth ?? 0;
			Height = map?.tiles.nHeight ?? 0;

			LoadTileData(map.tiles);

			if (PreviewSettings.Scrambled)
				Scramble();
			else
				Solve();

			Debug.AssertFormat(null != indexes && null != offsets, "invalid map tile indexes or offsets data");

			void LoadTileData(DatabaseLoader.Tiles dbTiles)
			{
				var tileMap = dbTiles.TileData.bytes;
				if (tileMap == null || tileMap.Length != dbTiles.nWidth * dbTiles.nHeight)
				{
					Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={dbTiles.nWidth * dbTiles.nHeight}");
					return;
				}

				tiles = new Tile[dbTiles.nWidth * dbTiles.nHeight];
				for (var index = 0; index < tileMap.Length; ++index)
				{
					var tileDefIndex = tileMap[index];
					if (tileDefIndex < 0 || tileDefIndex >= map.defs.Length)
					{
						Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex}");
						continue;
					}

					var szType = map.defs[tileDefIndex].szType;
					if (szType == "tile_empty") continue;

					var szTheme = map.defs[tileDefIndex].szTheme;
					if (string.IsNullOrEmpty(szTheme)) Debug.LogWarning($"Null szTheme at tileDefIndex {tileDefIndex}");

					var properties = TilePropertiesManager.GetOrCreateTileProperties(szType, szTheme);
					if (null == properties) continue;
					tiles[index].Properties = properties;

					var coord = GetTileCoordinates(index);
					if (szType == "tile_invisible")
					{
						if (PreviewSettings.ShowHiddenTiles) this.tiles[index].GameObject = GeometryManager.CreateDebugTile(transform, coord.ToPosition());
						continue;
					}

					var tileDef = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					tiles[index].GameObject = GeometryManager.InstantiateTile(tileDef, transform, coord.ToPosition(), properties.Interactive);
				}
			}
		}

		public void Scramble()
		{
			indexes = new int[Width * Height];
			for (var n = 0; n < indexes.Length; ++n) indexes[n] = offsets[n] + n;
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			indexes = new int[Width * Height];
			for (var n = 0; n < indexes.Length; ++n) indexes[n] = n; 
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			for (var n = 0; n < indexes.Length; ++n)
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

		public static MapManager Instantiate(Transform parent, DatabaseLoader.Map map)
		{
			var container = new GameObject($"Map: {map.name}");
			if (null != parent) container.transform.SetParent(parent, false);

			var mapManager = container.AddComponent<MapManager>();
			mapManager.Initialise(map);
			return mapManager;
		}
	}
}