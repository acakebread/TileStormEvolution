using UnityEngine;
using GameDatabase;
using System.Linq;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class MapManager : MonoBehaviour
	{
		private DatabaseLoader.Map currentMap;
		private GameObject mapRoot;
		private GameObject[] tiles;
		private List<DatabaseLoader.Waypoint> waypoints;

		public DatabaseLoader.Map CurrentMap => currentMap;
		public string CurrentMapName => currentMap?.name;
		public int Width => currentMap?.tiles.nWidth ?? 0;
		public int Height => currentMap?.tiles.nHeight ?? 0;
		public GameObject MapRoot => mapRoot;
		public GameObject[] Tiles => tiles;
		public IReadOnlyList<DatabaseLoader.Waypoint> Waypoints => waypoints?.AsReadOnly();

		private bool IsValidTileIndex(int tileIndex)
		{
			bool isValid = tileIndex >= 0 && tileIndex < tiles?.Length && Width > 0;
			if (!isValid)
				Debug.LogWarning($"Invalid tile index: {tileIndex}");
			return isValid;
		}

		public TileProperties GetTileDefAt(int tileIndex) => IsValidTileIndex(tileIndex) ? tiles[tileIndex]?.GetComponent<TileProperties>() : null;

		public Vector3 GetTilePosition(int tileIndex) => IsValidTileIndex(tileIndex) ? new Vector3(tileIndex % Width, 1f, tileIndex / Width) : Vector3.zero;

		public void Reset()
		{
			if (mapRoot != null) Destroy(mapRoot);
			mapRoot = null;
			currentMap = null;
			waypoints = null;
			tiles = null;
		}

		public void Initialize(string mapName)
		{
			Reset();
			currentMap = string.IsNullOrEmpty(mapName)
				? DatabaseLoader.instance.Maps.FirstOrDefault()
				: DatabaseLoader.instance.Maps.FirstOrDefault(m => m.name == mapName);

			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.instance.Maps.Select(m => m.name))}");
				return;
			}

			var tileMap = currentMap.tiles?.TileData?.unpacked_bytes;
			if (tileMap == null || tileMap.Length != Width * Height)
			{
				Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={Width * Height}");
				return;
			}

			waypoints = currentMap.waypoints?.Where(w => w != null).ToList() ?? new List<DatabaseLoader.Waypoint>();
			tiles = new GameObject[Width * Height];
			mapRoot = new GameObject($"Map_{currentMap.name}");
			mapRoot.transform.SetParent(transform, false);

			Vector3 mapCentre = Vector3.zero;
			int activeTileCount = 0;

			for (int index = 0; index < tileMap.Length; index++)
			{
				int x = index % Width;
				int z = index / Width;
				int scrambledIndex = PreviewSettings.Scramble ? index + currentMap.mixed.TileData.unpacked_bytes[index] : index;
				int defIndex = tileMap[scrambledIndex];

				if (defIndex < 0 || defIndex >= currentMap.defs.Length)
				{
					Debug.LogWarning($"Invalid defIndex={defIndex} at ({x},{z})");
					continue;
				}

				string szType = currentMap.defs[defIndex].szType;
				string szTheme = currentMap.defs[defIndex].szTheme;
				if (string.IsNullOrEmpty(szType))
				{
					Debug.LogWarning($"Null szType at defIndex {defIndex}");
					continue;
				}

				if (szType == "tile_empty")
					continue;

				DatabaseLoader.TileDef tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				if (tileDef == null)
					continue;

				tiles[index] = null; // Set after validation
				GameObject tileObj = new GameObject($"{tileDef.szType}_{x}_{z}", typeof(TileProperties));
				var properties = tileObj.GetComponent<TileProperties>();
				properties.TileDef = tileDef;
				tiles[index] = tileObj;
				tileObj.transform.SetParent(mapRoot.transform, false);
				tileObj.transform.position = new Vector3(x, 0f, z);

				if (PreviewSettings.FlipGeometry)
					tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

				if (properties.Movable)
				{
					var collider = tileObj.AddComponent<BoxCollider>();
					collider.size = new Vector3(1f, 0.5f, 1f);
					collider.center = new Vector3(0f, 0.25f, 0f);
				}

				if (string.IsNullOrEmpty(tileDef.szGeom))
				{
					if (szType != "tile_invisible")
						Debug.LogWarning($"Empty szGeom for {szType} at ({x},{z})");
					continue;
				}

				string geomPath = $"{PreviewSettings.GeometryPath}{tileDef.szGeom}".Replace(".x", "");
				GameObject geomAsset = Resources.Load<GameObject>(geomPath);
				if (geomAsset != null)
				{
					GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
					geomInstance.transform.localPosition = Vector3.zero;
					geomInstance.name = tileDef.szGeom;
				}
				else if (szType != "tile_invisible")
				{
					Debug.LogWarning($"Geometry not found: {geomPath}");
					GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
					cube.transform.SetParent(tileObj.transform, false);
					cube.transform.localPosition = Vector3.zero;
					cube.transform.localScale = Vector3.one * 0.1f;
					cube.name = "Fallback_Cube";
				}

				DatabaseLoader.TextureSet textureSet = TileAnimator.GetTextureSetForTileDef(tileDef);
				if (textureSet?.frames?.Length > 0)
				{
					var animator = tileObj.AddComponent<TileAnimator>();
					animator.Initialize(textureSet);
				}
				else
				{
					Debug.LogWarning($"No texture set for {tileDef.szType}, theme={tileDef.szTheme}");
				}

				mapCentre += new Vector3(x, 0f, z);
				activeTileCount++;
			}

			SetCameraPosition(mapCentre, activeTileCount);
		}

		private void SetCameraPosition(Vector3 mapCentre, int activeTileCount)
		{
			Camera.main.transform.position = activeTileCount > 0 ? (mapCentre / activeTileCount) + Vector3.up * 10f : Vector3.up * 10f;
			Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			Debug.Log($"Map {currentMap.name}: width={Width}, height={Height}, defs={currentMap.defs?.Length ?? 0}, waypoints={waypoints.Count}, camera at {Camera.main.transform.position}, tiles={activeTileCount}");
		}

		public int GetStartTile()
		{
			for (int i = 0; i < Width * Height; i++)
			{
				if (GetTileDefAt(i)?.IsStart == true)
					return i;
			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public bool CheckPathBetweenWaypoints(int currentWaypointIndex, out List<int> path)
		{
			path = new List<int>();
			if (Waypoints == null || currentWaypointIndex < 0 || currentWaypointIndex + 1 >= Waypoints.Count)
			{
				Debug.Log($"No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={Waypoints?.Count ?? 0})");
				return false;
			}

			int startTile = Waypoints[currentWaypointIndex].nTile;
			int targetTile = Waypoints[currentWaypointIndex + 1].nTile;
			var startDef = GetTileDefAt(startTile);
			if (startDef == null)
				return false;

			foreach (int dirBit in TileProperties.Directions)
			{
				if ((startDef.Nav & dirBit) == 0)
					continue;
				if (FindPathRecursive(startTile, targetTile, dirBit, new List<int>(), out path))
				{
					Debug.Log($"Found path to waypoint {targetTile}: [{FormatPath(path)}]");
					return true;
				}
			}
			return false;
		}

		private bool FindPathRecursive(int currentTile, int targetTile, int currentDirBit, List<int> currentPath, out List<int> resultPath)
		{
			resultPath = null;
			currentPath.Add(currentTile);

			if (currentTile == targetTile)
			{
				resultPath = new List<int>(currentPath);
				return true;
			}
			var currentDef = GetTileDefAt(currentTile);
			if (currentDef == null)
				return false;

			foreach (int dirBit in GetTryDirections(currentDef.Nav, currentDirBit))
			{
				if ((currentDef.Nav & dirBit) == 0)
					continue;

				int nextTile = GetAdjacentTile(currentTile, dirBit);
				if (nextTile == -1)
					continue;

				var nextDef = GetTileDefAt(nextTile);
				if (!TileProperties.CanMoveBetweenTiles(currentDef, nextDef, dirBit) || nextDef?.IsConsole == true)
					continue;

				if (FindPathRecursive(nextTile, targetTile, dirBit, new List<int>(currentPath), out resultPath))
					return true;
			}
			return false;
		}

		private int[] GetTryDirections(int nav, int currentDirBit)
		{
			if ((TileProperties.GetOppositeDirection(nav) & nav) == nav)
				return new[] { currentDirBit };
			if (currentDirBit != 0)
				return new[] { nav & ~TileProperties.GetOppositeDirection(currentDirBit) };
			return TileProperties.Directions;
		}

		private int GetAdjacentTile(int nTile, int dirBit)
		{
			var x = nTile % Width;
			var z = nTile / Width;
			z += (dirBit & 1) - ((dirBit & 2) >> 1); // North (+1), South (-1)
			x += ((dirBit & 4) >> 2) - ((dirBit & 8) >> 3); // East (+1), West (-1)
			return x >= 0 && x < Width && z >= 0 && z < Height ? z * Width + x : -1;
		}

		public int FindAdjacentConsole(int nTile)
		{
			if (!IsValidTileIndex(nTile))
				return -1;

			foreach (int dirBit in TileProperties.Directions)
			{
				int adjacentTile = GetAdjacentTile(nTile, dirBit);
				if (adjacentTile != -1 && GetTileDefAt(adjacentTile)?.IsConsole == true)
					return adjacentTile;
			}
			return -1;
		}

		private string FormatPath(List<int> path) => string.Join(" -> ", path.Select(t => $"({t % Width},{t / Width})"));
	}
}
