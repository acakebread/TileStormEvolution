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

		public Vector3 GetTilePosition(int tileIndex)
		{
			if (tileIndex < 0 || tileIndex >= Width * Height || Width == 0)
			{
				Debug.LogWarning($"Invalid tile index: {tileIndex}. Returning Vector3.zero.");
				return Vector3.zero;
			}
			return new Vector3(tileIndex % Width, 1f, tileIndex / Width);
		}

		public void Initialize(string name)
		{
			Reset();
			InitializeMap(name);
		}

		public void Reset()
		{
			if (mapRoot != null)
			{
				Destroy(mapRoot);
				mapRoot = null;
			}
			ResetTileMap();
		}

		public void ResetTileMap()
		{
			tiles = null;
			currentMap = null;
			waypoints = null;
		}

		public TileProperties GetTileDefAt(int tileIndex)
		{
			return tileIndex >= 0 && tileIndex < tiles?.Length ? tiles[tileIndex]?.GetComponent<TileProperties>() : null;
		}

		public int GetStartTile()
		{
			for (int i = 0; i < Width * Height; i++)
			{
				var tileDef = GetTileDefAt(i);
				if (tileDef != null && tileDef.IsStart)
					return i;
			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public List<int> GetWaypointTiles()
		{
			List<int> waypointTiles = new List<int>();
			if (CurrentMap?.waypoints != null)
			{
				foreach (var waypoint in CurrentMap.waypoints)
				{
					if (waypoint != null)
						waypointTiles.Add(waypoint.nTile);
				}
			}
			return waypointTiles;
		}

		public bool CheckPathBetweenWaypoints(int currentWaypointIndex, out List<int> path)
		{
			path = new List<int>();
			if (Waypoints == null || currentWaypointIndex + 1 >= Waypoints.Count)
			{
				Debug.Log($"No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={Waypoints?.Count ?? 0})");
				return false;
			}

			int startTile = Waypoints[currentWaypointIndex].nTile;
			int targetTile = Waypoints[currentWaypointIndex + 1].nTile;
			var startDef = GetTileDefAt(startTile);
			if (startDef == null)
				return false;

			int startNav = startDef.Nav;
			foreach (int dirBit in new[] { TileProperties.North, TileProperties.South, TileProperties.East, TileProperties.West })
			{
				if ((startNav & dirBit) == 0)
					continue;
				if (FindPath(startTile, targetTile, dirBit, out path))
				{
					Debug.Log($"Found path to waypoint {targetTile}: [{string.Join(" -> ", path.Select(t => $"({t % Width},{t / Width})"))}]");
					return true;
				}
			}

			return false;
		}

		public bool FindPath(int startTile, int targetTile, int startDirBit, out List<int> path)
		{
			path = new List<int>();
			var visited = new List<int>();
			return FindPathRecursive(startTile, targetTile, startDirBit, visited, out path);
		}

		private bool FindPathRecursive(int currentTile, int targetTile, int currentDirBit, List<int> currentPath, out List<int> resultPath)
		{
			resultPath = null;
			currentPath.Add(currentTile);

			// If we have reached the target, return the path
			if (currentTile == targetTile)
			{
				resultPath = new List<int>(currentPath);
				return true;
			}
			var currentDef = GetTileDefAt(currentTile);
			if (currentDef == null)
				return false;

			int nav = currentDef.Nav;
			int[] tryDirs = GetTryDirections(nav, currentDirBit);

			foreach (int dirBit in tryDirs)
			{
				if (dirBit == 0 || (nav & dirBit) == 0)
					continue;

				int nextTile = GetAdjacentTile(currentTile, dirBit);
				if (-1 == nextTile)
					continue;

				var nextDef = GetTileDefAt(nextTile);
				if (!TileProperties.CanMoveBetweenTiles(currentDef, nextDef, dirBit))
					continue;

				if (nextDef?.IsConsole == true)
					continue;

				if (FindPathRecursive(nextTile, targetTile, dirBit, new List<int>(currentPath), out resultPath))
					return true;
			}
			return false;
		}

		private int[] GetTryDirections(int nav, int currentDirBit)
		{
			if ((TileProperties.GetOppositeDirection(nav) & nav) == nav) // Straight corridor or crossroads
			{
				return new[] { currentDirBit }; // Only continue in current direction
			}
			if (currentDirBit != 0)
			{
				return new[] { nav & ~TileProperties.GetOppositeDirection(currentDirBit) }; // All directions except opposite
			}
			return new[] { TileProperties.North, TileProperties.South, TileProperties.East, TileProperties.West }; // Try all directions
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
			if (nTile < 0 || nTile >= Tiles?.Length)
				return 0;

			foreach (int dirBit in new[] { TileProperties.North, TileProperties.South, TileProperties.East, TileProperties.West }) // North, South, East, West
			{
				int adjacentTile = GetAdjacentTile(nTile, dirBit);
				if (-1 != adjacentTile)
				{
					var adjacentDef = GetTileDefAt(adjacentTile);
					if (adjacentDef?.IsConsole == true)
						return dirBit; // Return first console's direction
				}
			}

			return 0; // No console found
		}

		private void InitializeMap(string mapName)
		{
			currentMap = string.IsNullOrEmpty(mapName)
				? DatabaseLoader.instance.Maps.FirstOrDefault()
				: DatabaseLoader.instance.Maps.FirstOrDefault(m => m.name == mapName);

			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.instance.Maps.Select(m => m.name))}");
				return;
			}

			Debug.Log($"Map {currentMap.name}: width={Width}, height={Height}, defs.Length={currentMap.defs?.Length}");

			// Initialize waypoints
			waypoints = new List<DatabaseLoader.Waypoint>();
			if (currentMap.waypoints != null)
			{
				foreach (var waypoint in CurrentMap.waypoints)
				{
					if (waypoint != null)
						waypoints.Add(waypoint);
				}
			}
			Debug.Log($"Found {waypoints.Count} waypoints: [{string.Join(", ", waypoints.Select(w => $"Tile={w.nTile}, Name={w.name}"))}]");

			tiles = new GameObject[Width * Height];
			var tileMap = currentMap.tiles?.TileData?.unpacked_bytes;
			if (tileMap == null || tileMap.Length != Width * Height)
			{
				Debug.LogError($"Invalid tiles data! tiles={currentMap.tiles != null}, nTileIndex={currentMap.tiles?.TileData != null}, length={(tileMap != null ? tileMap.Length : -1)}, expected={Width * Height}");
				return;
			}

			if (mapRoot != null)
			{
				Destroy(mapRoot);
			}
			mapRoot = new GameObject($"Map_{currentMap.name}");
			mapRoot.transform.SetParent(transform, false);

			Vector3 mapCentre = Vector3.zero;
			int activeTileCount = 0;

			for (int index = 0; index < tileMap.Length; index++)
			{
				tiles[index] = null;
				int x = index % Width;
				int z = index / Width;
				int scrambledIndex = index;
				if (PreviewSettings.Scramble)
					scrambledIndex += currentMap.mixed.TileData.unpacked_bytes[index];

				int defIndex = tileMap[scrambledIndex];
				if (defIndex < 0 || defIndex >= currentMap.defs.Length)
				{
					Debug.LogWarning($"Invalid defIndex={defIndex} at index={index} ({x},{z}), defs.Length={currentMap.defs.Length}");
					continue;
				}

				string szType = currentMap.defs[defIndex].szType;
				string szTheme = currentMap.defs[defIndex].szTheme;
				if (string.IsNullOrEmpty(szType))
				{
					Debug.LogWarning($"Null or empty szType at defIndex {defIndex} in map {currentMap.name}");
					continue;
				}

				if (szType == "tile_empty")
					continue;

				DatabaseLoader.TileDef tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				if (tileDef == null)
					continue;

				GameObject tileObj = new GameObject($"{tileDef.szType}_{x}_{z}", typeof(TileProperties));
				var properties = tileObj.GetComponent<TileProperties>();
				properties.TileDef = tileDef; // Sets navBits
				tiles[index] = tileObj;
				tileObj.transform.SetParent(mapRoot.transform, false);
				tileObj.transform.position = new Vector3(x, 0f, z);

				if (PreviewSettings.FlipGeometry)
				{
					tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);
				}

				if (properties.Movable)
				{
					BoxCollider collider = tileObj.AddComponent<BoxCollider>();
					collider.size = new Vector3(1f, 0.5f, 1f);
					collider.center = new Vector3(0f, 0.25f, 0f);
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
					Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.szType}");
					GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
					cube.transform.SetParent(tileObj.transform, false);
					cube.transform.localPosition = Vector3.zero;
					cube.transform.localScale = Vector3.one * 0.1f;
					cube.name = "Fallback_Cube";
				}

				DatabaseLoader.TextureSet textureSet = TileAnimator.GetTextureSetForTileDef(tileDef);
				if (textureSet != null && textureSet.frames != null && textureSet.frames.Length > 0)
				{
					TileAnimator animator = tileObj.AddComponent<TileAnimator>();
					animator.Initialize(textureSet);
				}
				else
				{
					Debug.LogWarning($"No valid texture set for TileDef {tileDef.szType}, szTheme={tileDef.szTheme}");
				}

				mapCentre += new Vector3(x, 0f, z);
				activeTileCount++;
			}

			if (activeTileCount > 0)
			{
				mapCentre /= activeTileCount;
				Camera.main.transform.position = mapCentre + Vector3.up * 10f;
				Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				Debug.Log($"Camera set to {Camera.main.transform.position}, mapCentre={mapCentre}, activeTileCount={activeTileCount}");
			}
			else
			{
				Debug.LogWarning("No active tiles found, camera at origin");
				Camera.main.transform.position = Vector3.up * 10f;
				Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			}
		}
	}
}