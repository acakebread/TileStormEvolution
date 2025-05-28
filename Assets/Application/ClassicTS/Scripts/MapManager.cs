using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class MapManager : MonoBehaviour, IMap
	{
		private int[] indices;
		private int[] offsets;
		private Tile[] tiles;

		public int[] Indices { get => indices; private set => indices = value; }

		public int Width { get; private set; }
		public int Height { get; private set; }

		private void Awake()
		{
			indices = null;
			offsets = null;
			tiles = null;
		}

		public Tile GetTile(int index)
		{
			if (index < 0 || index >= indices?.Length || Width <= 0) return default;
			var dataIndex = indices[index];
			return dataIndex >= 0 && dataIndex < tiles.Length ? tiles[dataIndex] : default;
		}

		public Vector3 GetTilePosition(int index) => new(index % Width, 0f, index / Width);

		public int WorldToMapIndex(Vector3 worldPos)
		{
			var x = Mathf.RoundToInt(worldPos.x);//this rounds -0.5 to +0.5 to zero - an error in conversion forces this
			var z = Mathf.RoundToInt(worldPos.z);//this rounds -0.5 to +0.5 to zero - an error in conversion forces this
			return (x >= 0 && x < Width && z >= 0 && z < Height) ? z * Width + x : -1;
		}

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

			Debug.AssertFormat(null != indices && null != offsets, "invalid map tile indices or offsets data");

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
					if (string.IsNullOrEmpty(szType)) Debug.LogWarning($"Null szType at tileDefIndex {tileDefIndex}");
					var szTheme = map.defs[tileDefIndex].szTheme;
					if (string.IsNullOrEmpty(szTheme)) Debug.LogWarning($"Null szTheme at tileDefIndex {tileDefIndex}");
					tiles[n] = new(szType, szTheme);
					if (szType == "tile_empty") continue;

					var tileDef = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					tiles[n].GameObject = GeometryManager.InstantiateTile(tileDef, transform, GetTilePosition(n), tiles[n].Interactive);
				}
			}
		}

		public void Scramble()
		{
			indices = new int[Width * Height];
			for (var n = 0; n < indices.Length; ++n) indices[n] = offsets[n] + n;
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			indices = new int[Width * Height];
			for (var n = 0; n < indices.Length; ++n) indices[n] = n;
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			Debug.AssertFormat(indices.Length > 0 && indices.Length == tiles.Length, "mismatched tiles and indices");
			for (var n = 0; n < indices.Length; ++n)
			{
				var gameObject = tiles[indices[n]].GameObject;
				if (null == gameObject) continue;
				gameObject.transform.position = GetTilePosition(n);
#if DEBUG
				gameObject.name = $"{gameObject.GetComponent<RTTI>().tileDef.szType ?? "Empty"} ({gameObject.transform.position.x},{gameObject.transform.position.z})";//gameObject.name = $"{tiles[indices[n]].Properties.Type ?? "Empty"}_{gameObject.transform.position.x}_{gameObject.transform.position.z}";
#endif
			}
		}

		public static MapManager Instantiate(DatabaseLoader.Map map, Transform parent = null)
		{
			var container = new GameObject($"Map: {map.name}");
			if (null != parent) container.transform.SetParent(parent, false);

			var mapManager = container.AddComponent<MapManager>();
			mapManager.Initialise(map);
			return mapManager;
		}
	}
}