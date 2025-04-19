using UnityEngine;
using GameDatabase;
using System.Linq;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class MapManager : MonoBehaviour
	{
		public struct TileData
		{
			public TileProperties Properties;
			public GameObject GameObject;
		}

		private DatabaseLoader.Map currentMap;
		private GameObject mapRoot;
		private TileData[] tiles;
		private List<DatabaseLoader.Waypoint> waypoints;

		public DatabaseLoader.Map CurrentMap => currentMap;
		public string CurrentMapName => currentMap?.name;
		public int Width => currentMap?.tiles.nWidth ?? 0;
		public int Height => currentMap?.tiles.nHeight ?? 0;
		public GameObject MapRoot => mapRoot;
		public TileData[] Tiles => tiles;
		public IReadOnlyList<DatabaseLoader.Waypoint> Waypoints => waypoints?.AsReadOnly();

		private bool IsValidTileIndex(int tileIndex)
		{
			bool isValid = tileIndex >= 0 && tileIndex < tiles?.Length && Width > 0;
			if (!isValid)
				Debug.LogWarning($"Invalid tile index: {tileIndex}");
			return isValid;
		}

		public TileProperties GetTilePropertiesAt(int tileIndex, TileData[] tileData = null)
		{
			tileData ??= tiles;
			bool isValid = tileIndex >= 0 && tileIndex < tileData?.Length && Width > 0;
			if (!isValid)
			{
				Debug.LogWarning($"Invalid tile index: {tileIndex}");
				return null;
			}
			return tileData[tileIndex].Properties;
		}

		public Vector3 GetTilePosition(int tileIndex) =>
			IsValidTileIndex(tileIndex) ? new Vector3(GetTileCoordinates(tileIndex).x, 1f, GetTileCoordinates(tileIndex).z) : Vector3.zero;

		public (int x, int z) GetTileCoordinates(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex))
				return (0, 0);
			return (tileIndex % Width, tileIndex / Width);
		}

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

			tiles = new TileData[Width * Height];
			mapRoot = new GameObject($"Map_{currentMap.name}");
			mapRoot.transform.SetParent(transform, false);

			Vector3 mapCentre = Vector3.zero;
			int activeTileCount = 0;

			for (int index = 0; index < tileMap.Length; index++)
			{
				var (x, z) = GetTileCoordinates(index);
				int scrambledIndex = PreviewSettings.Scramble ? index + currentMap.mixed.TileData.unpacked_bytes[index] : index;
				int tileDefIndex = tileMap[scrambledIndex];

				if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length ||
					string.IsNullOrEmpty(currentMap.defs[tileDefIndex].szType) ||
					currentMap.defs[tileDefIndex].szType == "tile_empty")
				{
					if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length)
						Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex} at ({x},{z})");
					else if (string.IsNullOrEmpty(currentMap.defs[tileDefIndex].szType))
						Debug.LogWarning($"Null szType at tileDefIndex {tileDefIndex}");
					continue;
				}

				string szType = currentMap.defs[tileDefIndex].szType;
				string szTheme = currentMap.defs[tileDefIndex].szTheme;
				DatabaseLoader.TileDef tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				if (tileDef == null)
					continue;

				tiles[index].Properties = new TileProperties(tileDef);

				if (szType == "tile_invisible")
					continue;

				GameObject tileObj = new GameObject($"{tileDef.szType}_{x}_{z}");
				tiles[index].GameObject = tileObj;
				tileObj.transform.SetParent(mapRoot.transform, false);
				tileObj.transform.position = new Vector3(x, 0f, z);
				if (PreviewSettings.FlipGeometry)
					tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

				if (tiles[index].Properties.Movable)
				{
					var collider = tileObj.AddComponent<BoxCollider>();
					collider.size = new Vector3(1f, 0.5f, 1f);
					collider.center = new Vector3(0f, 0.25f, 0f);
				}

				if (string.IsNullOrEmpty(tileDef.szGeom))
				{
					Debug.LogWarning($"Empty szGeom for {szType} at ({x},{z})");
					continue;
				}

				string geomPath = $"{PreviewSettings.GeometryPath}{tileDef.szGeom}".Replace(".x", "");
				GameObject geomAsset = Resources.Load<GameObject>(geomPath);
				if (geomAsset != null)
				{
					GameObject geomInstance = Object.Instantiate(geomAsset, tileObj.transform);
					geomInstance.transform.localPosition = Vector3.zero;
					geomInstance.name = tileDef.szGeom;
				}
				else
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

			waypoints = currentMap.waypoints?.Where(w => w != null).ToList();
			if (waypoints == null || waypoints.Count == 0)
			{
				waypoints = SetupWaypoints();
			}

			SetCameraPosition(mapCentre, activeTileCount);
		}

		private void SetCameraPosition(Vector3 mapCentre, int activeTileCount)
		{
			Camera.main.transform.position = activeTileCount > 0 ? (mapCentre / activeTileCount) + Vector3.up * 10f : Vector3.up * 10f;
			Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			Debug.Log($"Map {currentMap.name}: width={Width}, height={Height}, defs={currentMap.defs?.Length ?? 0}, waypoints={waypoints.Count}, camera at {Camera.main.transform.position}, tiles={activeTileCount}");
		}

		public int GetStartTile(TileData[] tileData = null)
		{
			tileData ??= tiles;
			for (int i = 0; i < Width * Height; i++)
			{
				var props = GetTilePropertiesAt(i, tileData);
				if (props != null && props.IsStart)
					return i;
			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile(TileData[] tileData = null)
		{
			tileData ??= tiles;
			for (int i = 0; i < Width * Height; i++)
			{
				var props = GetTilePropertiesAt(i, tileData);
				if (props != null && props.IsEnd)
					return i;
			}
			Debug.LogError("No end tile found!");
			return -1;
		}

		private List<DatabaseLoader.Waypoint> SetupWaypoints()
		{
			var generatedWaypoints = new List<DatabaseLoader.Waypoint>();
			var tileMap = currentMap.tiles?.TileData?.unpacked_bytes;
			if (tileMap == null || tileMap.Length != Width * Height)
			{
				Debug.LogWarning("Cannot setup waypoints: invalid tile data");
				return generatedWaypoints;
			}

			// Create unscrambled tile data
			TileData[] unscrambledTiles = new TileData[Width * Height];
			for (int index = 0; index < tileMap.Length; index++)
			{
				int tileDefIndex = tileMap[index]; // Use raw (unscrambled) index
				if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length ||
					string.IsNullOrEmpty(currentMap.defs[tileDefIndex].szType) ||
					currentMap.defs[tileDefIndex].szType == "tile_empty")
				{
					continue;
				}

				string szType = currentMap.defs[tileDefIndex].szType;
				string szTheme = currentMap.defs[tileDefIndex].szTheme;
				DatabaseLoader.TileDef tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				if (tileDef == null)
					continue;

				unscrambledTiles[index].Properties = new TileProperties(tileDef);
			}

			int startTile = GetStartTile(unscrambledTiles);
			int endTile = GetEndTile(unscrambledTiles);

			if (startTile == -1 || endTile == -1)
			{
				Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
				return generatedWaypoints;
			}

			// Add start waypoint
			generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = startTile });

			// Find path from start to end using unscrambled tiles
			List<int> path = null;
			var startProps = GetTilePropertiesAt(startTile, unscrambledTiles);
			if (startProps != null)
			{
				foreach (int dirBit in TileProperties.Directions)
				{
					if ((startProps.Nav & dirBit) == 0)
						continue;
					path = FindPathRecursive(startTile, endTile, dirBit, null, unscrambledTiles);
					if (path != null)
						break;
				}
			}

			if (path != null)
			{
				// Add waypoints for console-adjacent tiles and end tile
				foreach (int tile in path)
				{
					if (tile == startTile)
						continue; // Already added
					if (FindAdjacentConsole(tile, unscrambledTiles) != -1 || tile == endTile)
					{
						generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = tile });
					}
				}
			}
			else
			{
				Debug.LogWarning("Failed to find path from start to end for waypoint setup");
			}

			Debug.Log($"Generated {generatedWaypoints.Count} waypoints: [{string.Join(", ", generatedWaypoints.Select(w => w.nTile))}]");
			return generatedWaypoints;
		}

		public bool CheckPathBetweenWaypoints(int currentWaypointIndex, out List<int> path)
		{
			path = null;
			if (Waypoints == null || currentWaypointIndex < 0 || currentWaypointIndex + 1 >= Waypoints.Count)
			{
				Debug.Log($"No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={Waypoints?.Count ?? 0})");
				return false;
			}

			int startTile = Waypoints[currentWaypointIndex].nTile;
			int targetTile = Waypoints[currentWaypointIndex + 1].nTile;
			var startProps = GetTilePropertiesAt(startTile);
			if (startProps == null)
				return false;

			foreach (int dirBit in TileProperties.Directions)
			{
				if ((startProps.Nav & dirBit) == 0)
					continue;
				path = FindPathRecursive(startTile, targetTile, dirBit);
				if (path != null)
				{
					Debug.Log($"Found path to waypoint {targetTile}: [{FormatPath(path)}]");
					return true;
				}
			}
			return false;
		}

		private List<int> FindPathRecursive(int currentTile, int targetTile, int currentDirBit, List<int> path = null, TileData[] tileData = null)
		{
			path ??= new List<int>();
			path.Add(currentTile);

			if (currentTile == targetTile)
				return path;

			var currentProps = GetTilePropertiesAt(currentTile, tileData);
			if (currentProps == null)
			{
				path.RemoveAt(path.Count - 1);
				return null;
			}

			foreach (int dirBit in GetTryDirections(currentProps.Nav, currentDirBit))
			{
				int nextTile = GetAdjacentTile(currentTile, dirBit);
				if (nextTile == -1)
					continue;

				var nextProps = GetTilePropertiesAt(nextTile, tileData);
				if (!TileProperties.CanMoveBetweenTiles(currentProps, nextProps, dirBit))
					continue;

				var result = FindPathRecursive(nextTile, targetTile, dirBit, path, tileData);
				if (result != null)
					return result;
			}

			path.RemoveAt(path.Count - 1);
			return null;
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
			var (x, z) = GetTileCoordinates(nTile);
			z += (dirBit & 1) - ((dirBit & 2) >> 1); // North (+1), South (-1)
			x += ((dirBit & 4) >> 2) - ((dirBit & 8) >> 3); // East (+1), West (-1)
			return x >= 0 && x < Width && z >= 0 && z < Height ? z * Width + x : -1;
		}

		public int FindAdjacentConsole(int nTile, TileData[] tileData = null)
		{
			if (!IsValidTileIndex(nTile))
				return -1;

			foreach (int dirBit in TileProperties.Directions)
			{
				int consoleTile = GetAdjacentTile(nTile, dirBit);
				if (consoleTile == -1)
					continue;

				var consoleProps = GetTilePropertiesAt(consoleTile, tileData);
				if (consoleProps?.IsConsole != true)
					continue;

				// Check if console's Nav direction points to nTile
				int consoleNav = consoleProps.Nav;
				if (consoleNav == 0)
					continue; // Invalid console orientation

				int navTile = GetAdjacentTile(consoleTile, consoleNav);
				if (navTile == nTile)
					return consoleTile;
			}

			return -1;
		}


		public string FormatPath(List<int> path) =>
			string.Join(" -> ", path.Select(t => {
				var (x, z) = GetTileCoordinates(t);
				return $"({x},{z})";
			}));
	}
}


//using UnityEngine;
//using GameDatabase;
//using System.Linq;
//using System.Collections.Generic;

//namespace GamePreviewNamespace
//{
//	public class MapManager : MonoBehaviour
//	{
//		public struct TileData
//		{
//			public TileProperties Properties;
//			public GameObject GameObject;
//		}

//		private DatabaseLoader.Map currentMap;
//		private GameObject mapRoot;
//		private TileData[] tiles;
//		private List<DatabaseLoader.Waypoint> waypoints;

//		public DatabaseLoader.Map CurrentMap => currentMap;
//		public string CurrentMapName => currentMap?.name;
//		public int Width => currentMap?.tiles.nWidth ?? 0;
//		public int Height => currentMap?.tiles.nHeight ?? 0;
//		public GameObject MapRoot => mapRoot;
//		public TileData[] Tiles => tiles;
//		public IReadOnlyList<DatabaseLoader.Waypoint> Waypoints => waypoints?.AsReadOnly();

//		private bool IsValidTileIndex(int tileIndex)
//		{
//			bool isValid = tileIndex >= 0 && tileIndex < tiles?.Length && Width > 0;
//			if (!isValid)
//				Debug.LogWarning($"Invalid tile index: {tileIndex}");
//			return isValid;
//		}

//		public TileProperties GetTilePropertiesAt(int tileIndex) =>
//			IsValidTileIndex(tileIndex) ? tiles[tileIndex].Properties : null;

//		public Vector3 GetTilePosition(int tileIndex) =>
//			IsValidTileIndex(tileIndex) ? new Vector3(GetTileCoordinates(tileIndex).x, 1f, GetTileCoordinates(tileIndex).z) : Vector3.zero;

//		public (int x, int z) GetTileCoordinates(int tileIndex)
//		{
//			if (!IsValidTileIndex(tileIndex))
//				return (0, 0);
//			return (tileIndex % Width, tileIndex / Width);
//		}

//		public void Reset()
//		{
//			if (mapRoot != null) Destroy(mapRoot);
//			mapRoot = null;
//			currentMap = null;
//			waypoints = null;
//			tiles = null;
//		}

//		public void Initialize(string mapName)
//		{
//			Reset();
//			currentMap = string.IsNullOrEmpty(mapName)
//				? DatabaseLoader.instance.Maps.FirstOrDefault()
//				: DatabaseLoader.instance.Maps.FirstOrDefault(m => m.name == mapName);

//			if (currentMap == null)
//			{
//				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.instance.Maps.Select(m => m.name))}");
//				return;
//			}

//			var tileMap = currentMap.tiles?.TileData?.unpacked_bytes;
//			if (tileMap == null || tileMap.Length != Width * Height)
//			{
//				Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={Width * Height}");
//				return;
//			}

//			waypoints = currentMap.waypoints?.Where(w => w != null).ToList() ?? new List<DatabaseLoader.Waypoint>();
//			tiles = new TileData[Width * Height];
//			mapRoot = new GameObject($"Map_{currentMap.name}");
//			mapRoot.transform.SetParent(transform, false);

//			Vector3 mapCentre = Vector3.zero;
//			int activeTileCount = 0;

//			for (int index = 0; index < tileMap.Length; index++)
//			{
//				var (x, z) = GetTileCoordinates(index);
//				int scrambledIndex = PreviewSettings.Scramble ? index + currentMap.mixed.TileData.unpacked_bytes[index] : index;
//				int tileDefIndex = tileMap[scrambledIndex];

//				if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length ||
//					string.IsNullOrEmpty(currentMap.defs[tileDefIndex].szType) ||
//					currentMap.defs[tileDefIndex].szType == "tile_empty")
//				{
//					if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length)
//						Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex} at ({x},{z})");
//					else if (string.IsNullOrEmpty(currentMap.defs[tileDefIndex].szType))
//						Debug.LogWarning($"Null szType at tileDefIndex {tileDefIndex}");
//					continue;
//				}

//				string szType = currentMap.defs[tileDefIndex].szType;
//				string szTheme = currentMap.defs[tileDefIndex].szTheme;
//				DatabaseLoader.TileDef tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
//				if (tileDef == null)
//					continue;

//				tiles[index].Properties = new TileProperties(tileDef);

//				if (szType == "tile_invisible")
//					continue;

//				GameObject tileObj = new GameObject($"{tileDef.szType}_{x}_{z}");
//				tiles[index].GameObject = tileObj;
//				tileObj.transform.SetParent(mapRoot.transform, false);
//				tileObj.transform.position = new Vector3(x, 0f, z);
//				if (PreviewSettings.FlipGeometry)
//					tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

//				if (tiles[index].Properties.Movable)
//				{
//					var collider = tileObj.AddComponent<BoxCollider>();
//					collider.size = new Vector3(1f, 0.5f, 1f);
//					collider.center = new Vector3(0f, 0.25f, 0f);
//				}

//				if (string.IsNullOrEmpty(tileDef.szGeom))
//				{
//					Debug.LogWarning($"Empty szGeom for {szType} at ({x},{z})");
//					continue;
//				}

//				string geomPath = $"{PreviewSettings.GeometryPath}{tileDef.szGeom}".Replace(".x", "");
//				GameObject geomAsset = Resources.Load<GameObject>(geomPath);
//				if (geomAsset != null)
//				{
//					GameObject geomInstance = Object.Instantiate(geomAsset, tileObj.transform);
//					geomInstance.transform.localPosition = Vector3.zero;
//					geomInstance.name = tileDef.szGeom;
//				}
//				else
//				{
//					Debug.LogWarning($"Geometry not found: {geomPath}");
//					GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//					cube.transform.SetParent(tileObj.transform, false);
//					cube.transform.localPosition = Vector3.zero;
//					cube.transform.localScale = Vector3.one * 0.1f;
//					cube.name = "Fallback_Cube";
//				}

//				DatabaseLoader.TextureSet textureSet = TileAnimator.GetTextureSetForTileDef(tileDef);
//				if (textureSet?.frames?.Length > 0)
//				{
//					var animator = tileObj.AddComponent<TileAnimator>();
//					animator.Initialize(textureSet);
//				}
//				else
//				{
//					Debug.LogWarning($"No texture set for {tileDef.szType}, theme={tileDef.szTheme}");
//				}

//				mapCentre += new Vector3(x, 0f, z);
//				activeTileCount++;
//			}

//			SetCameraPosition(mapCentre, activeTileCount);
//		}

//		private void SetCameraPosition(Vector3 mapCentre, int activeTileCount)
//		{
//			Camera.main.transform.position = activeTileCount > 0 ? (mapCentre / activeTileCount) + Vector3.up * 10f : Vector3.up * 10f;
//			Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
//			Debug.Log($"Map {currentMap.name}: width={Width}, height={Height}, defs={currentMap.defs?.Length ?? 0}, waypoints={waypoints.Count}, camera at {Camera.main.transform.position}, tiles={activeTileCount}");
//		}

//		public int GetStartTile()
//		{
//			for (int i = 0; i < Width * Height; i++)
//			{
//				var props = GetTilePropertiesAt(i);
//				if (props != null && props.IsStart)
//					return i;
//			}
//			Debug.LogError("No start tile found!");
//			return -1;
//		}

//		public bool CheckPathBetweenWaypoints(int currentWaypointIndex, out List<int> path)
//		{
//			path = null;
//			if (Waypoints == null || currentWaypointIndex < 0 || currentWaypointIndex + 1 >= Waypoints.Count)
//			{
//				Debug.Log($"No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={Waypoints?.Count ?? 0})");
//				return false;
//			}

//			int startTile = Waypoints[currentWaypointIndex].nTile;
//			int targetTile = Waypoints[currentWaypointIndex + 1].nTile;
//			var startProps = GetTilePropertiesAt(startTile);
//			if (startProps == null)
//				return false;

//			foreach (int dirBit in TileProperties.Directions)
//			{
//				if ((startProps.Nav & dirBit) == 0)
//					continue;
//				path = FindPathRecursive(startTile, targetTile, dirBit);
//				if (path != null)
//				{
//					Debug.Log($"Found path to waypoint {targetTile}: [{FormatPath(path)}]");
//					return true;
//				}
//			}
//			return false;
//		}

//		private List<int> FindPathRecursive(int currentTile, int targetTile, int currentDirBit, List<int> path = null)
//		{
//			path ??= new List<int>();
//			path.Add(currentTile);

//			if (currentTile == targetTile)
//				return path;

//			var currentProps = GetTilePropertiesAt(currentTile);
//			if (currentProps == null)
//			{
//				path.RemoveAt(path.Count - 1);
//				return null;
//			}

//			foreach (int dirBit in GetTryDirections(currentProps.Nav, currentDirBit))
//			{
//				int nextTile = GetAdjacentTile(currentTile, dirBit);
//				if (nextTile == -1)
//					continue;

//				var nextProps = GetTilePropertiesAt(nextTile);
//				//if (!TileProperties.CanMoveBetweenTiles(currentProps, nextProps, dirBit) || nextProps?.IsConsole == true)
//				if (!TileProperties.CanMoveBetweenTiles(currentProps, nextProps, dirBit))
//					continue;

//				var result = FindPathRecursive(nextTile, targetTile, dirBit, path);
//				if (result != null)
//					return result;
//			}

//			path.RemoveAt(path.Count - 1);
//			return null;
//		}

//		private int[] GetTryDirections(int nav, int currentDirBit)
//		{
//			if ((TileProperties.GetOppositeDirection(nav) & nav) == nav)
//				return new[] { currentDirBit };
//			if (currentDirBit != 0)
//				return new[] { nav & ~TileProperties.GetOppositeDirection(currentDirBit) };
//			return TileProperties.Directions;
//		}

//		private int GetAdjacentTile(int nTile, int dirBit)
//		{
//			var (x, z) = GetTileCoordinates(nTile);
//			z += (dirBit & 1) - ((dirBit & 2) >> 1); // North (+1), South (-1)
//			x += ((dirBit & 4) >> 2) - ((dirBit & 8) >> 3); // East (+1), West (-1)
//			return x >= 0 && x < Width && z >= 0 && z < Height ? z * Width + x : -1;
//		}

//		public int FindAdjacentConsole(int nTile)
//		{
//			if (!IsValidTileIndex(nTile))
//				return -1;

//			foreach (int dirBit in TileProperties.Directions)
//			{
//				int adjacentTile = GetAdjacentTile(nTile, dirBit);
//				if (adjacentTile != -1 && GetTilePropertiesAt(adjacentTile)?.IsConsole == true)
//					return adjacentTile;
//			}
//			return -1;
//		}

//		public string FormatPath(List<int> path) =>
//			string.Join(" -> ", path.Select(t => {
//				var (x, z) = GetTileCoordinates(t);
//				return $"({x},{z})";
//			}));
//	}
//}