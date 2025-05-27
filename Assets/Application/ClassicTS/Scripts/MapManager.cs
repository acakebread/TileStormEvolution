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
		public int Width { get; private set; }
		public int Height { get; private set; }

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

		public Vector3 GetTilePosition(int tileIndex) => new(tileIndex % Width, 0f, tileIndex / Width);

		public float GetTileDistance(int nSrc, int nDst) => (GetTilePosition(nDst) - GetTilePosition(nSrc)).magnitude;

		private int ToIndex(int X, int Z) => (X >= 0 && X < Width && Z >= 0 || Z < Height) ? Z * Width + X : -1;

		private int WorldToMapIndex(Vector3 worldPos) => (worldPos.x >= 0 && worldPos.x < Width && worldPos.z >= 0 && worldPos.z < Height) ? ToIndex(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.z)) : -1;

		public int ScreenToMapIndex(Vector3 screenPos) => WorldToMapIndex(ScreenToWorld(screenPos));

		public int[] GetTileIndexes() => indexes;

		public Vector3 ScreenToWorld(Vector3 screenPos)
		{
			var ray = Camera.main.ScreenPointToRay(screenPos);
			var mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return Vector3.zero;
			return ray.GetPoint(distance);
		}

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
				for (var n = 0; n < tileMap.Length; ++n)
				{
					var tileDefIndex = tileMap[n];
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
					tiles[n].Properties = properties;

					var position = GetTilePosition(n);
					if (szType == "tile_invisible")
					{
						if (PreviewSettings.ShowHiddenTiles) tiles[n].GameObject = GeometryManager.CreateDebugTile(transform, position);
						continue;
					}

					var tileDef = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					tiles[n].GameObject = GeometryManager.InstantiateTile(tileDef, transform, position, properties.Interactive);
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
			Debug.AssertFormat(indexes.Length > 0 && indexes.Length == tiles.Length, "mismatched tiles and indexes");
			for (var n = 0; n < indexes.Length; ++n)
			{
				var gameObject = tiles[indexes[n]].GameObject;
				if (null == gameObject) continue;
				gameObject.transform.position = GetTilePosition(n);
#if DEBUG
				gameObject.name = $"{tiles[indexes[n]].Properties.Type ?? "Empty"}_{gameObject.transform.position.x}_{gameObject.transform.position.z}";
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