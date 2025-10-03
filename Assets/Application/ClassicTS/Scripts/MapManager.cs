using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public interface IMapManager
	{
		int Width { get; }
		int Height { get; }
		int Count { get; }
		int[] Indices { get; }
	}

	public class MapManager : MonoBehaviour, IMapManager
	{
		private int[] indices;
		private int[] offsets;
		private Tile[] tiles;

		public int[] Indices { get => indices; private set => indices = value; }
		public Tile[] Tiles { get => tiles; private set => tiles = value; }

		public int Width { get; private set; }
		public int Height { get; private set; }
		public int Count { get => Width * Height; }

		private WindController windController; // Reference to WindController

		private void Awake()
		{
			indices = null;
			offsets = null;
			tiles = null;
		}

		private void Initialise(DatabaseLoader.Map map)
		{
			offsets = map?.mixed?.TileData?.bytes;
			Width = map?.tiles.nWidth ?? 0;
			Height = map?.tiles.nHeight ?? 0;

			LoadTileData(map.tiles);

			if (PreviewSettings.Scrambled) Scramble(); else Solve();

			Debug.AssertFormat(null != indices && null != offsets, "invalid map tile indices or offsets data");

			InitializeWindController(); // Initialize WindController after tiles are loaded

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
					tiles[n] = new Tile(szType, szTheme);
					if (szType == "tile_empty") continue;

					var tileDef = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					tiles[n].GameObject = GeometryManager.InstantiateTile(tileDef, transform, TileWorldPosition(this, n), tiles[n].Interactive);

					//if (tiles[n].GameObject != null)
					//{
					//	var meshRenderer = tiles[n].GameObject.GetComponentInChildren<MeshRenderer>(true);
					//	if (meshRenderer != null)
					//	{
					//		var filter = meshRenderer.GetComponent<MeshFilter>();
					//		if (filter != null && filter.IsRuntimeWritable())// || tiles[n].GameObject.name.Contains("door")
					//		{
					//			var morphGeomSway = tiles[n].GameObject.AddComponent<MorphGeomSway>();
					//			morphGeomSway.SetCustomInfluenceVolume(Vector3.up, 0.2f);
					//			morphGeomSway.swayInfluencePower = 0.5f; // More top sway
					//			morphGeomSway.ConfigureSubdivision(true, 0.3f); // Enable stratification with maxSegmentLength for influence volume
					//		}
					//	}
					//}
				}
			}
		}

		// Initialize WindController and collect MorphGeomSway components
		private void InitializeWindController()
		{
			windController = gameObject.AddComponent<WindController>();
			var swayComponents = new List<(MorphGeomSway sway, Vector3 position)>();

			for (int n = 0; n < tiles.Length; ++n)
			{
				if (tiles[n].GameObject == null) continue;
				var sway = tiles[n].GameObject.GetComponent<MorphGeomSway>();
				if (sway != null)
				{
					Vector3 position = TileWorldPosition(this, n);
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
				var position = TileWorldPosition(this, n);
				gameObject.transform.position = position;
				position -= tile_origin;
#if DEBUG
				gameObject.name = $"{gameObject.GetComponent<RTTI>()?.tileDef.szType ?? "Empty"} ({position.x},{position.z})";
#endif
			}
		}

		public static Vector3 ScreenToWorld(Vector3 screenPos)
		{
			var ray = Camera.main.ScreenPointToRay(screenPos);
			var mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return Vector3.zero;
			return ray.GetPoint(distance);
		}

		private static Vector3 tile_origin = new Vector3(0.5f, 0f, 0.5f);
		public static Vector3 TileWorldPosition(IMapManager map, int index) => new Vector3(index % map.Width, 0f, index / map.Width) + tile_origin;

		public static int WorldToMapIndex(IMapManager map, Vector3 vec) => vec.x >= 0 && vec.x < map.Width && vec.z >= 0 && vec.z < map.Height ? (int)vec.z * map.Width + (int)vec.x : -1;

		public static Tile GetTile(IMapManager map, int index)
		{
			if (map is not MapManager concrete || index < 0 || index >= map.Indices?.Length || map.Width <= 0) return default;
			var dataIndex = map.Indices[index];
			return dataIndex >= 0 && dataIndex < concrete.tiles.Length ? concrete.tiles[dataIndex] : default;
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