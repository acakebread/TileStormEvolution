using UnityEngine;
using GameDatabase;
using System.Linq;

namespace GamePreviewNamespace
{
	public class GamePreview : MonoBehaviour
	{
		[Header("Workaround for inverted .obj meshes")]
		public bool flip = true;
		[SerializeField] private DatabaseLoader databaseLoader;
		[SerializeField] private string mapName = "Industrial 01";
		[SerializeField] private string geometryPath = "Geometry/fbx/";
		[SerializeField] private string texturePath = "Textures/";
		[SerializeField] private float tileMoveSpeed = 2f;
		[SerializeField] private float pauseDuration = 1f;
		[SerializeField] private float dragThreshold = 0.5f;

		private MapManager mapManager;
		private EggbotController eggbotController;
		private TileInteractionController tileInteractionController;
		private bool isInitialized;

		void Awake()
		{
			mapManager = GetComponent<MapManager>();
			eggbotController = GetComponent<EggbotController>();
			tileInteractionController = GetComponent<TileInteractionController>();

			if (mapManager == null) mapManager = gameObject.AddComponent<MapManager>();
			if (eggbotController == null) eggbotController = gameObject.AddComponent<EggbotController>();
			if (tileInteractionController == null) tileInteractionController = gameObject.AddComponent<TileInteractionController>();
		}

		void Start()
		{
			if (databaseLoader == null)
			{
				databaseLoader = FindObjectOfType<DatabaseLoader>(true);
				if (databaseLoader == null)
				{
					Debug.LogError("GamePreview requires a DatabaseLoader!");
					return;
				}
			}

			Debug.Log($"GamePreview Start: databaseLoader found, Maps.Count={databaseLoader.Maps.Count}");
			databaseLoader.OnDatabaseLoaded += Initialize;

			if (databaseLoader.Maps.Count > 0)
			{
				Initialize();
			}
		}

		void OnDestroy()
		{
			if (databaseLoader != null)
			{
				databaseLoader.OnDatabaseLoaded -= Initialize;
			}
		}

		void Initialize()
		{
			if (isInitialized)
				return;

			isInitialized = true;
			Debug.Log($"GamePreview Initialize: Maps.Count={databaseLoader.Maps.Count}, mapName={mapName}");

			// Reset all components
			mapManager.Reset();
			eggbotController.Reset();
			tileInteractionController.Initialize(mapManager, tileMoveSpeed, dragThreshold);

			// Initialize in order
			mapManager.Initialize(databaseLoader, mapName, geometryPath, texturePath, flip);
			eggbotController.Initialize(mapManager, pauseDuration);
		}

		void Update()
		{
			eggbotController.UpdateEggbot();
			tileInteractionController.UpdateInteractions();
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload"))
			{
				isInitialized = false;
				Initialize();
			}

			if (eggbotController.IsLevelComplete && GUI.Button(new Rect(120, 10, 150, 30), "Next Level"))
			{
				int currentIndex = databaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % databaseLoader.Maps.Count;
				mapName = databaseLoader.Maps[currentIndex].name;
				isInitialized = false;
				Initialize();
			}
		}
	}
}


//using UnityEngine;
//using GameDatabase;
//using System.Linq;
//using System.Collections.Generic;
//using static GameDatabase.DatabaseLoader;

//namespace GamePreviewNamespace
//{
//	public class GamePreview : MonoBehaviour
//	{
//		[Header("Workaround for inverted .obj meshes")]
//		public bool flip = true;
//		[SerializeField] private DatabaseLoader databaseLoader;
//		[SerializeField] private string mapName = "Industrial 01";
//		[SerializeField] private string geometryPath = "Geometry/fbx/";
//		[SerializeField] private string texturePath = "Textures/";
//		[SerializeField] private float tileMoveSpeed = 2f;
//		[SerializeField] private float pauseDuration = 1f;
//		[SerializeField] private float dragThreshold = 0.5f;

//		private Map currentMap;
//		private int width, height;
//		private GameObject mapRoot;
//		private GameObject eggbot;
//		private int[] tileMap;
//		private List<int> waypoints;
//		private int currentWaypointIndex;
//		private bool isMoving;
//		private float moveTimer;
//		private float pauseTimer;
//		private bool isPuzzleBlocked;
//		private bool isLevelComplete;
//		private List<int> currentPath;
//		private int pathStepIndex;
//		private Vector3 dragStartPos;
//		private bool isDragging;
//		private int dragTileIndex;
//		private int dragStride;
//		private bool isInitialized;
//		private GameObject draggedTileObj;
//		private Vector3 draggedTileOriginalPos;
//		private List<(int tileIndex, Vector3 newPos, Vector3 startPos, float timer, GameObject tileObj)> pendingMoves;

//		void Start()
//		{
//			if (databaseLoader == null)
//			{
//				databaseLoader = FindObjectOfType<DatabaseLoader>(true);
//				if (databaseLoader == null)
//				{
//					Debug.LogError("GamePreview requires a DatabaseLoader!");
//					return;
//				}
//			}

//			Debug.Log($"GamePreview Start: databaseLoader found, Maps.Count={databaseLoader.Maps.Count}");
//			databaseLoader.OnDatabaseLoaded += Initialize;

//			if (databaseLoader.Maps.Count > 0)
//			{
//				Initialize();
//			}
//		}

//		void OnDestroy()
//		{
//			if (databaseLoader != null)
//			{
//				databaseLoader.OnDatabaseLoaded -= Initialize;
//			}
//			if (mapRoot != null)
//			{
//				Destroy(mapRoot);
//			}
//		}

//		void Initialize()
//		{
//			if (isInitialized)
//				return;

//			isInitialized = true;
//			Debug.Log($"GamePreview Initialize: Maps.Count={databaseLoader.Maps.Count}, mapName={mapName}");
//			InitializeMap();
//		}

//		void InitializeMap()
//		{
//			currentMap = string.IsNullOrEmpty(mapName)
//				? databaseLoader.Maps.FirstOrDefault()
//				: databaseLoader.Maps.FirstOrDefault(m => m.name == mapName);

//			if (currentMap == null)
//			{
//				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", databaseLoader.Maps.Select(m => m.name))}");
//				return;
//			}

//			width = currentMap.tiles.nWidth;
//			height = currentMap.tiles.nHeight;
//			Debug.Log($"Map {currentMap.name}: width={width}, height={height}, defs.Length={currentMap.defs?.Length}");

//			tileMap = currentMap.tiles?.nTileIndex?.unpacked_bytes;
//			if (tileMap == null || tileMap.Length != width * height)
//			{
//				Debug.LogError($"Invalid tiles data! tiles={currentMap.tiles != null}, nTileIndex={currentMap.tiles?.nTileIndex != null}, length={(tileMap != null ? tileMap.Length : -1)}, expected={width * height}");
//				return;
//			}

//			Debug.Log($"tileMap: [{string.Join(", ", tileMap.Take(10))}...] (first 10)");

//			if (mapRoot != null)
//			{
//				Destroy(mapRoot);
//			}
//			mapRoot = new GameObject($"Map_{currentMap.name}");
//			mapRoot.transform.SetParent(transform, false);

//			Vector3 mapCentre = Vector3.zero;
//			int activeTileCount = 0;

//			for (int z = 0; z < height; z++)
//			{
//				for (int x = 0; x < width; x++)
//				{
//					int index = z * width + x;
//					int defIndex = tileMap[index];
//					if (defIndex < 0 || defIndex >= currentMap.defs.Length)
//					{
//						Debug.LogWarning($"Invalid defIndex={defIndex} at ({x},{z}), defs.Length={currentMap.defs.Length}");
//						continue;
//					}

//					string szType = currentMap.defs[defIndex].szType;
//					string szTheme = currentMap.defs[defIndex].szTheme;
//					if (string.IsNullOrEmpty(szType))
//					{
//						Debug.LogWarning($"Null or empty szType at defIndex {defIndex} in map {currentMap.name}");
//						continue;
//					}

//					if (szType == "tile_empty")
//						continue;

//					TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
//					if (tileDef == null)
//					{
//						Debug.LogWarning($"TileDef not found for szType={szType}, szTheme={szTheme} at ({x}, {z}) in map {currentMap.name}");
//						continue;
//					}

//					Debug.Log($"Tile at ({x},{z}): szType={szType}, bSlide={tileDef.bSlide}, bRoll={tileDef.bRoll}, bDock={tileDef.bDock}, bStart={tileDef.bStart}, bConsole={tileDef.bConsole}, bEnd={tileDef.bEnd}, bEast={tileDef.bEast}, bWest={tileDef.bWest}, bNorth={tileDef.bNorth}, bSouth={tileDef.bSouth}");

//					GameObject tileObj = new GameObject($"{tileDef.name}_{x}_{z}");
//					tileObj.transform.SetParent(mapRoot.transform, false);
//					tileObj.transform.position = new Vector3(x, 0f, z);
//					if (flip)
//					{
//						tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);
//					}

//					if (szType != "tile_invisible")
//					{
//						BoxCollider collider = tileObj.AddComponent<BoxCollider>();
//						collider.size = new Vector3(1f, 0.5f, 1f);
//						collider.center = new Vector3(0f, 0.25f, 0f);
//					}

//					string geomPath = $"{geometryPath}{tileDef.szGeom}".Replace(".x", "");
//					Debug.Log($"Loading geometry: {geomPath}");
//					GameObject geomAsset = Resources.Load<GameObject>(geomPath);
//					if (geomAsset != null)
//					{
//						GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
//						geomInstance.transform.localPosition = Vector3.zero;
//						geomInstance.name = tileDef.szGeom;
//					}
//					else
//					{
//						if (szType != "tile_invisible")
//						{
//							Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.name}");
//							GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//							cube.transform.SetParent(tileObj.transform, false);
//							cube.transform.localPosition = Vector3.zero;
//							cube.transform.localScale = Vector3.one * 0.1f;
//							cube.name = "Fallback_Cube";
//						}
//					}

//					TextureSet textureSet = GetTextureForTileDef(tileDef);
//					if (textureSet != null && textureSet.frames != null && textureSet.frames.Length > 0)
//					{
//						TileAnimator animator = tileObj.AddComponent<TileAnimator>();
//						animator.Initialize(textureSet, texturePath);
//					}
//					else
//					{
//						Debug.LogWarning($"No valid texture set for TileDef {tileDef.name}, szTheme={tileDef.szTheme}");
//					}

//					mapCentre += new Vector3(x, 0f, z);
//					activeTileCount++;
//				}
//			}

//			if (activeTileCount > 0)
//			{
//				mapCentre /= activeTileCount;
//				Camera.main.transform.position = mapCentre + Vector3.up * 10f;
//				Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
//				Debug.Log($"Camera set to {Camera.main.transform.position}, mapCentre={mapCentre}, activeTileCount={activeTileCount}");
//			}
//			else
//			{
//				Debug.LogWarning("No active tiles found, camera at origin");
//				Camera.main.transform.position = Vector3.up * 10f;
//				Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
//			}

//			InitializeEggbot();
//			InitializeWaypoints();
//			currentWaypointIndex = 0;
//			isMoving = false;
//			pauseTimer = pauseDuration;
//			isLevelComplete = false;
//			currentPath = null;
//			pathStepIndex = 0;
//		}

//		private void InitializeWaypoints()
//		{
//			waypoints = new List<int>();
//			if (currentMap.Waypoints != null && currentMap.Waypoints.nWaypointCount > 0)
//			{
//				Waypoint[] wpArray = new Waypoint[] { currentMap.Waypoints.WP0, currentMap.Waypoints.WP1, currentMap.Waypoints.WP2, currentMap.Waypoints.WP3 };
//				for (int i = 0; i < currentMap.Waypoints.nWaypointCount && i < wpArray.Length; i++)
//				{
//					if (wpArray[i] != null)
//					{
//						waypoints.Add(wpArray[i].nTile);
//					}
//				}
//			}
//			Debug.Log($"Found {waypoints.Count} waypoints: [{string.Join(", ", waypoints)}]");
//		}

//		private TextureSet GetTextureForTileDef(TileDef tileDef)
//		{
//			Theme theme = databaseLoader.Themes.FirstOrDefault(t => t.name == tileDef.szTheme || t.szTileTextureSet == tileDef.szTheme);
//			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
//			{
//				TextureSet texSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
//				if (texSet != null && texSet.frames != null && texSet.frames.Length > 0)
//				{
//					Debug.Log($"TextureSet found: {texSet.name}, frames={texSet.frames.Length}");
//					return texSet;
//				}
//			}
//			Debug.LogWarning($"No TextureSet for theme={tileDef.szTheme}");
//			return null;
//		}

//		private void InitializeEggbot()
//		{
//			int startTile = -1;
//			for (int i = 0; i < tileMap.Length; i++)
//			{
//				int defIndex = tileMap[i];
//				if (defIndex >= 0 && defIndex < currentMap.defs.Length)
//				{
//					string szType = currentMap.defs[defIndex].szType;
//					string szTheme = currentMap.defs[defIndex].szTheme;
//					if (string.IsNullOrEmpty(szType))
//						continue;

//					TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
//					if (tileDef != null && tileDef.bStart)
//					{
//						startTile = i;
//						break;
//					}
//				}
//			}

//			if (startTile == -1)
//			{
//				Debug.LogError("No start tile found!");
//				return;
//			}

//			if (eggbot != null)
//			{
//				Destroy(eggbot);
//			}

//			eggbot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
//			eggbot.name = "Eggbot";
//			eggbot.transform.SetParent(mapRoot.transform, false);
//			eggbot.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
//			eggbot.transform.position = new Vector3(startTile % width, 1f, startTile / width);
//			Debug.Log($"Eggbot placed at tile {startTile} ({startTile % width}, {startTile / width})");
//		}

//		void Update()
//		{
//			if (currentMap == null || waypoints.Count == 0)
//				return;

//			if (!isMoving)
//			{
//				pauseTimer -= Time.deltaTime;
//				if (pauseTimer <= 0)
//				{
//					MoveToNextWaypoint();
//				}
//			}
//			else
//			{
//				moveTimer += Time.deltaTime * tileMoveSpeed;
//				float t = Mathf.Clamp01(moveTimer);
//				int currentTile = currentPath[pathStepIndex];
//				int nextTile = pathStepIndex + 1 < currentPath.Count ? currentPath[pathStepIndex + 1] : currentTile;
//				Vector3 startPos = new Vector3(currentTile % width, 1f, currentTile / width);
//				Vector3 endPos = new Vector3(nextTile % width, 1f, nextTile / width);
//				eggbot.transform.position = Vector3.Lerp(startPos, endPos, t);

//				if (t >= 1f)
//				{
//					pathStepIndex++;
//					if (pathStepIndex >= currentPath.Count - 1)
//					{
//						isMoving = false;
//						pauseTimer = pauseDuration;
//						currentWaypointIndex++;
//						currentPath = null;
//						if (currentWaypointIndex >= waypoints.Count - 1)
//						{
//							TileDef currentTileDef = GetTileDefAt(waypoints[currentWaypointIndex]);
//							if (currentTileDef != null && currentTileDef.bEnd)
//							{
//								Debug.Log("Level complete!");
//							}
//						}
//						else
//						{
//							TileDef currentTileDef = GetTileDefAt(waypoints[currentWaypointIndex]);
//							if (currentTileDef != null && currentTileDef.bConsole)
//							{
//								isPuzzleBlocked = !CheckPathToNextWaypoint(out _);
//								if (isPuzzleBlocked)
//								{
//									Debug.Log("Waiting at console for puzzle...");
//								}
//							}
//						}
//					}
//					else
//					{
//						moveTimer = 0f;
//					}
//				}
//			}

//			HandleInput();
//			UpdatePendingMoves();
//		}

//		private TileDef GetTileDefAt(int tileIndex)
//		{
//			if (tileIndex >= 0 && tileIndex < tileMap.Length)
//			{
//				int defIndex = tileMap[tileIndex];
//				if (defIndex >= 0 && defIndex < currentMap.defs.Length)
//				{
//					string szType = currentMap.defs[defIndex].szType;
//					string szTheme = currentMap.defs[defIndex].szTheme;
//					if (!string.IsNullOrEmpty(szType))
//					{
//						TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
//						if (tileDef != null)
//							return tileDef;
//						Debug.LogWarning($"No TileDef found for szType={szType}, szTheme={szTheme} at tileIndex={tileIndex}");
//					}
//					else
//					{
//						Debug.LogWarning($"Empty szType at defIndex={defIndex}, tileIndex={tileIndex}");
//					}
//				}
//				else
//				{
//					Debug.LogWarning($"Invalid defIndex={defIndex} at tileIndex={tileIndex}, defs.Length={currentMap.defs.Length}");
//				}
//			}
//			else
//			{
//				Debug.LogWarning($"Invalid tileIndex={tileIndex}, tileMap.Length={tileMap.Length}");
//			}
//			return null;
//		}

//		private bool CheckPathToNextWaypoint(out List<int> path)
//		{
//			path = new List<int>();
//			if (currentWaypointIndex + 1 >= waypoints.Count)
//			{
//				Debug.Log($"CheckPathToNextWaypoint: No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={waypoints.Count})");
//				return false;
//			}

//			int currentTile = waypoints[currentWaypointIndex];
//			int targetWaypointTile = waypoints[currentWaypointIndex + 1];
//			HashSet<int> visited = new HashSet<int>();
//			List<string> traversalLog = new List<string>();
//			Dictionary<int, int> parent = new Dictionary<int, int>();

//			Queue<int> queue = new Queue<int>();
//			queue.Enqueue(currentTile);
//			visited.Add(currentTile);
//			parent[currentTile] = -1;

//			while (queue.Count > 0)
//			{
//				int tile = queue.Dequeue();
//				if (tile == targetWaypointTile)
//				{
//					int current = tile;
//					while (current != -1)
//					{
//						path.Add(current);
//						current = parent.ContainsKey(current) ? parent[current] : -1;
//					}
//					path.Reverse();
//					Debug.Log($"CheckPathToNextWaypoint: Found waypoint {targetWaypointTile}. Path: [{string.Join(" -> ", path.Select(t => $"({t % width},{t / width})"))}]");
//					return true;
//				}

//				TileDef tileDef = GetTileDefAt(tile);
//				if (tileDef == null)
//				{
//					Debug.LogWarning($"CheckPathToNextWaypoint: No TileDef at tile {tile}");
//					continue;
//				}

//				int x = tile % width;
//				int z = tile / width;
//				bool isInvisible = tileDef.szType == "tile_invisible" || tileDef.szType == "tile_empty";
//				List<(int nextTile, string direction)> validNeighbors = new List<(int, string)>();

//				if (isInvisible || tileDef.bEast)
//				{
//					int eastTile = tile + 1;
//					if (x < width - 1 && eastTile < tileMap.Length)
//					{
//						TileDef eastDef = GetTileDefAt(eastTile);
//						if (eastDef != null && (eastDef.szType == "tile_invisible" || eastDef.szType == "tile_empty" || eastDef.bWest))
//						{
//							validNeighbors.Add((eastTile, "East"));
//						}
//					}
//				}
//				if (isInvisible || tileDef.bWest)
//				{
//					int westTile = tile - 1;
//					if (x > 0)
//					{
//						TileDef westDef = GetTileDefAt(westTile);
//						if (westDef != null && (westDef.szType == "tile_invisible" || westDef.szType == "tile_empty" || westDef.bEast))
//						{
//							validNeighbors.Add((westTile, "West"));
//						}
//					}
//				}
//				if (isInvisible || tileDef.bNorth)
//				{
//					int northTile = tile + width;
//					if (z < height - 1 && northTile < tileMap.Length)
//					{
//						TileDef northDef = GetTileDefAt(northTile);
//						if (northDef != null && (northDef.szType == "tile_invisible" || northDef.szType == "tile_empty" || northDef.bSouth))
//						{
//							validNeighbors.Add((northTile, "North"));
//						}
//					}
//				}
//				if (isInvisible || tileDef.bSouth)
//				{
//					int southTile = tile - width;
//					if (z > 0)
//					{
//						TileDef southDef = GetTileDefAt(southTile);
//						if (southDef != null && (southDef.szType == "tile_invisible" || southDef.szType == "tile_empty" || southDef.bNorth))
//						{
//							validNeighbors.Add((southTile, "South"));
//						}
//					}
//				}

//				foreach (var (nextTile, direction) in validNeighbors)
//				{
//					if (!visited.Contains(nextTile))
//					{
//						visited.Add(nextTile);
//						queue.Enqueue(nextTile);
//						parent[nextTile] = tile;
//						traversalLog.Add($"{tile} (x={x}, z={z}, {tileDef.szType}) -> {direction}");
//					}
//				}
//			}

//			Debug.Log($"CheckPathToNextWaypoint: No path to waypoint {targetWaypointTile}. Visited: [{string.Join(", ", traversalLog)}]");
//			return false;
//		}

//		private void MoveToNextWaypoint()
//		{
//			if (currentWaypointIndex + 1 >= waypoints.Count)
//			{
//				Debug.Log($"MoveToNextWaypoint: No next waypoint (currentIndex={currentWaypointIndex})");
//				return;
//			}

//			TileDef currentDef = GetTileDefAt(waypoints[currentWaypointIndex]);
//			if (currentDef != null && currentDef.bConsole && isPuzzleBlocked)
//			{
//				if (CheckPathToNextWaypoint(out _))
//				{
//					isPuzzleBlocked = false;
//					Debug.Log("Puzzle solved, proceeding...");
//				}
//				else
//				{
//					Debug.Log("Puzzle still blocked at console");
//					return;
//				}
//			}

//			if (CheckPathToNextWaypoint(out currentPath))
//			{
//				isMoving = true;
//				moveTimer = 0f;
//				pathStepIndex = 0;
//				Debug.Log($"Moving to waypoint {currentWaypointIndex + 1}: tile={waypoints[currentWaypointIndex + 1]}, path=[{string.Join(" -> ", currentPath.Select(t => $"({t % width},{t / width})"))}]");
//			}
//			else
//			{
//				Debug.LogWarning($"Failed to move to waypoint {currentWaypointIndex + 1}: tile={waypoints[currentWaypointIndex + 1]}");
//			}
//		}

//		private void UpdateTilePositions()
//		{
//			foreach (Transform tile in mapRoot.transform)
//			{
//				if (tile.name.Contains("Eggbot"))
//					continue;

//				string[] parts = tile.name.Split('_');
//				if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 2], out int x) && int.TryParse(parts[parts.Length - 1], out int z))
//				{
//					int index = z * width + x;
//					if (index >= 0 && index < tileMap.Length)
//					{
//						int tileIndex = tileMap[index];
//						if (tileIndex >= 0 && tileIndex < currentMap.defs.Length)
//						{
//							if (tile.gameObject != draggedTileObj)
//							{
//								tile.position = new Vector3(x, 0f, z);
//							}
//						}
//					}
//				}
//			}
//			Debug.Log("Tile positions updated");
//		}

//		void OnGUI()
//		{
//			GUI.skin.label.fontSize = 24;
//			GUI.color = Color.green;

//			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload"))
//			{
//				isInitialized = false;
//				if (mapRoot != null)
//				{
//					Destroy(mapRoot);
//				}
//				Initialize();
//			}

//			if (isLevelComplete && GUI.Button(new Rect(120, 10, 150, 30), "Next Level"))
//			{
//				int currentIndex = databaseLoader.Maps.ToList().FindIndex(m => m.name == currentMap.name);
//				currentIndex = (currentIndex + 1) % databaseLoader.Maps.Count;
//				mapName = databaseLoader.Maps[currentIndex].name;
//				isInitialized = false;
//				if (mapRoot != null)
//				{
//					Destroy(mapRoot);
//				}
//				Initialize();
//			}
//		}

//		private void HandleInput()
//		{
//			if (Input.GetMouseButtonDown(0))
//			{
//				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//				RaycastHit hit;
//				if (Physics.Raycast(ray, out hit))
//				{
//					Vector3 hitPos = hit.point;
//					int x = Mathf.FloorToInt(hitPos.x + 0.5f);
//					int z = Mathf.FloorToInt(hitPos.z + 0.5f);
//					int tileIndex = z * width + x;
//					Debug.Log($"Raycast hit: hitPos=({hitPos.x},{hitPos.z}), calculated=({x},{z}), tileIndex={tileIndex}");

//					if (tileIndex >= 0 && tileIndex < tileMap.Length)
//					{
//						TileDef tileDef = GetTileDefAt(tileIndex);
//						if (tileDef != null && tileDef.szType != "tile_invisible")
//						{
//							Debug.Log($"Mouse down at ({x},{z}), tileIndex={tileIndex}, szType={tileDef.szType}, bSlide={tileDef.bSlide}, bRoll={tileDef.bRoll}, bDock={tileDef.bDock}");
//							if (tileDef.bSlide || tileDef.bRoll)
//							{
//								isDragging = true;
//								dragStartPos = hitPos;
//								dragTileIndex = tileIndex;
//								dragStride = 0;
//								draggedTileObj = FindTileObject(tileIndex);
//								if (draggedTileObj != null)
//								{
//									draggedTileOriginalPos = draggedTileObj.transform.position;
//								}
//								pendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
//								Debug.Log($"Start drag: tile={tileIndex} ({x},{z})");
//							}
//							else
//							{
//								Debug.Log("Tile not movable (bSlide=false, bRoll=false)");
//							}
//						}
//						else
//						{
//							Debug.Log($"No TileDef or invisible tile at tileIndex={tileIndex}");
//						}
//					}
//					else
//					{
//						Debug.Log($"Invalid tileIndex={tileIndex}, tileMap.Length={tileMap.Length}");
//					}
//				}
//				else
//				{
//					Debug.Log("Raycast missed");
//				}
//			}

//			if (isDragging && Input.GetMouseButton(0))
//			{
//				Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//				RaycastHit hit;
//				if (Physics.Raycast(ray, out hit))
//				{
//					Vector3 currentPos = hit.point;
//					Vector3 delta = currentPos - dragStartPos;
//					Debug.Log($"Dragging: delta=({delta.x},{delta.z}), dragTileIndex={dragTileIndex}");

//					int newStride = 0;
//					float absX = Mathf.Abs(delta.x);
//					float absZ = Mathf.Abs(delta.z);
//					if (absX > absZ && absX > 0.1f)
//					{
//						newStride = delta.x > 0 ? 1 : -1;
//					}
//					else if (absZ > 0.1f)
//					{
//						newStride = delta.z > 0 ? width : -width;
//					}

//					if (newStride != 0 && CanSlideTiles(dragTileIndex, newStride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap))
//					{
//						if (newStride != dragStride)
//						{
//							dragStride = newStride;
//							pendingMoves.Clear();
//							Debug.Log($"Direction set: stride={dragStride} ({(dragStride == 1 ? "East" : dragStride == -1 ? "West" : dragStride == width ? "North" : "South")})");
//						}

//						Debug.Log($"Chain formed: tiles=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}]");
//						pendingMoves.Clear();
//						float distance = dragStride == 1 || dragStride == -1 ? delta.x : delta.z;
//						Vector3 direction = dragStride == 1 ? Vector3.right :
//										   dragStride == -1 ? Vector3.left :
//										   dragStride == width ? Vector3.forward :
//										   Vector3.back;

//						for (int i = 0; i < tilesToMove.Count; i++)
//						{
//							int tile = tilesToMove[i];
//							int nextTile = isRollGroup && willWrap ? GetWrappedTile(tilesToMove, tile, dragStride) : tile + dragStride;
//							Vector3 newPos = new Vector3(nextTile % width, 0f, nextTile / width);
//							Vector3 startPos = new Vector3(tile % width, 0f, tile / width);
//							GameObject tileObj = tile == dragTileIndex ? draggedTileObj : FindTileObject(tile);
//							if (tileObj != null)
//							{
//								pendingMoves.Add((tile, newPos, startPos, 0f, tileObj));
//							}
//						}

//						foreach (var move in pendingMoves)
//						{
//							if (move.tileObj != null)
//							{
//								float visualDistance = Mathf.Clamp(distance * (tilesToMove.IndexOf(move.tileIndex) + 1), -1f, 1f);
//								move.tileObj.transform.position = move.startPos + direction * visualDistance;
//							}
//						}
//					}
//					else
//					{
//						Debug.Log($"Direction rejected: stride={newStride}, reason={(newStride == 0 ? "No direction" : "Invalid move")}");
//						if (draggedTileObj != null)
//							draggedTileObj.transform.position = draggedTileOriginalPos;
//						pendingMoves.Clear();
//					}
//				}
//				else
//				{
//					Debug.Log("Dragging raycast missed");
//				}
//			}

//			if (isDragging && Input.GetMouseButtonUp(0))
//			{
//				bool commitMove = false;
//				float distance = 0f;
//				if (draggedTileObj != null && dragStride != 0)
//				{
//					Vector3 delta = draggedTileObj.transform.position - draggedTileOriginalPos;
//					distance = dragStride == 1 || dragStride == -1 ? Mathf.Abs(delta.x) : Mathf.Abs(delta.z);
//					commitMove = distance >= dragThreshold;
//					Debug.Log($"Mouse up: distance={distance}, commitMove={commitMove}, threshold={dragThreshold}");
//				}

//				if (commitMove && dragStride != 0 && CanSlideTiles(dragTileIndex, dragStride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap))
//				{
//					Debug.Log($"Committing move: tiles=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}]");
//					SlideTiles(dragTileIndex, dragStride, tilesToMove, isRollGroup, willWrap);
//				}
//				else
//				{
//					Debug.Log($"Drag cancelled: commitMove={commitMove}, stride={dragStride}");
//					foreach (var move in pendingMoves)
//					{
//						if (move.tileObj != null && move.tileIndex != dragTileIndex)
//						{
//							move.tileObj.transform.position = move.newPos;
//							int nextTile = (int)(move.newPos.x + move.newPos.z * width);
//							int currentTile = (int)(move.startPos.x + move.startPos.z * width);
//							if (nextTile >= 0 && nextTile < tileMap.Length && currentTile >= 0 && currentTile < tileMap.Length)
//							{
//								tileMap[nextTile] = tileMap[currentTile];
//							}
//						}
//					}
//					if (pendingMoves.Any(m => m.tileIndex != dragTileIndex))
//					{
//						int firstTile = (int)(pendingMoves.First(m => m.tileIndex != dragTileIndex).startPos.x +
//											 pendingMoves.First(m => m.tileIndex != dragTileIndex).startPos.z * width);
//						if (firstTile >= 0 && firstTile < tileMap.Length && dragTileIndex + dragStride >= 0 && dragTileIndex + dragStride < tileMap.Length)
//						{
//							tileMap[firstTile] = tileMap[dragTileIndex + dragStride];
//						}
//					}
//				}

//				if (draggedTileObj != null)
//				{
//					draggedTileObj.transform.position = draggedTileOriginalPos;
//				}

//				isDragging = false;
//				dragStride = 0;
//				draggedTileObj = null;
//				pendingMoves.Clear();
//				UpdateTilePositions();
//				Debug.Log($"TileMap after: [{string.Join(", ", tileMap.Take(10))}...] (first 10)");
//				if (isPuzzleBlocked)
//				{
//					MoveToNextWaypoint();
//				}
//				Debug.Log("End drag");
//			}
//		}

//		private GameObject FindTileObject(int tileIndex)
//		{
//			int x = tileIndex % width;
//			int z = tileIndex / width;
//			foreach (Transform tile in mapRoot.transform)
//			{
//				if (tile.name.Contains("Eggbot"))
//					continue;
//				string[] parts = tile.name.Split('_');
//				if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 2], out int tx) && int.TryParse(parts[parts.Length - 1], out int tz))
//				{
//					if (tx == x && tz == z)
//						return tile.gameObject;
//				}
//			}
//			Debug.LogWarning($"FindTileObject: No object for tileIndex={tileIndex} ({x},{z})");
//			return null;
//		}

//		private void UpdatePendingMoves()
//		{
//			if (pendingMoves == null)
//				return;

//			List<(int tileIndex, Vector3 newPos, Vector3 startPos, float timer, GameObject tileObj)> newPendingMoves = new List<(int, Vector3, Vector3, float, GameObject)>();
//			foreach (var move in pendingMoves)
//			{
//				if (move.tileObj != null)
//				{
//					float newTimer = move.timer + Time.deltaTime * tileMoveSpeed;
//					float t = Mathf.Clamp01(newTimer);
//					move.tileObj.transform.position = Vector3.Lerp(move.startPos, move.newPos, t);
//					if (t < 1f)
//					{
//						newPendingMoves.Add((move.tileIndex, move.newPos, move.startPos, newTimer, move.tileObj));
//					}
//				}
//			}
//			pendingMoves = newPendingMoves;
//		}

//		private bool CanSlideTiles(int startTile, int stride, out List<int> tilesToMove, out bool isRollGroup, out bool willWrap)
//		{
//			tilesToMove = new List<int>();
//			isRollGroup = false;
//			willWrap = false;

//			if (startTile < 0 || startTile >= tileMap.Length)
//			{
//				Debug.LogError($"CanSlideTiles: Invalid startTile={startTile}");
//				return false;
//			}

//			TileDef startDef = GetTileDefAt(startTile);
//			if (startDef == null || (!startDef.bSlide && !startDef.bRoll))
//			{
//				Debug.Log($"CanSlideTiles: Invalid start tile {startTile}, bSlide={startDef?.bSlide}, bRoll={startDef?.bRoll}");
//				return false;
//			}

//			int currentTile = startTile;
//			tilesToMove.Add(currentTile);
//			bool isStartRoll = startDef.bRoll;
//			isRollGroup = isStartRoll;
//			bool hasDock = startDef.bDock;

//			int currentX = currentTile % width;
//			int currentZ = currentTile / width;
//			bool withinBounds = stride == 1 ? (currentX < width - 1) :
//							   stride == -1 ? (currentX > 0) :
//							   stride == width ? (currentZ < height - 1) :
//							   stride == -width ? (currentZ > 0) : false;

//			if (!withinBounds)
//			{
//				if (isStartRoll && !hasDock)
//				{
//					willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
//					Debug.Log($"CanSlideTiles: Out of bounds, willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}]");
//					return willWrap;
//				}
//				Debug.Log($"CanSlideTiles: Start tile out of bounds: ({currentX},{currentZ}), stride={stride}");
//				return false;
//			}

//			int nextTile = currentTile + stride;
//			while (withinBounds && nextTile >= 0 && nextTile < tileMap.Length)
//			{
//				TileDef nextDef = GetTileDefAt(nextTile);
//				if (nextDef == null)
//				{
//					Debug.Log($"CanSlideTiles: No TileDef at nextTile={nextTile}");
//					return false;
//				}

//				if (nextDef.szType == "tile_empty" || nextDef.szType == "tile_invisible")
//				{
//					Debug.Log($"CanSlideTiles: Found gap at {nextTile} ({nextTile % width},{nextTile / width}), tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}]");
//					return true;
//				}

//				if (!nextDef.bSlide && !nextDef.bRoll)
//				{
//					if (isRollGroup && !hasDock)
//					{
//						willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
//						Debug.Log($"CanSlideTiles: Hit fixed tile at {nextTile} ({nextTile % width},{nextTile / width}), willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}]");
//						return willWrap;
//					}
//					Debug.Log($"CanSlideTiles: Blocked by fixed tile {nextTile} ({nextTile % width},{nextTile / width}), szType={nextDef.szType}");
//					return false;
//				}

//				if (nextDef.bDock)
//				{
//					hasDock = true;
//					Debug.Log($"CanSlideTiles: Blocked by docked tile {nextTile} ({nextTile % width},{nextTile / width})");
//					return false;
//				}

//				tilesToMove.Add(nextTile);
//				isRollGroup = isRollGroup || nextDef.bRoll;
//				currentTile = nextTile;
//				currentX = currentTile % width;
//				currentZ = currentTile / width;
//				nextTile = currentTile + stride;
//				withinBounds = stride == 1 ? (currentX < width - 1) :
//							  stride == -1 ? (currentX > 0) :
//							  stride == width ? (currentZ < height - 1) :
//							  stride == -width ? (currentZ > 0) : false;
//			}

//			if (isRollGroup && !hasDock)
//			{
//				willWrap = CheckRollWrap(startTile, stride, out tilesToMove);
//				Debug.Log($"CanSlideTiles: End of chain, willWrap={willWrap}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}]");
//				return willWrap;
//			}

//			Debug.Log($"CanSlideTiles: No gap or valid wrap found, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}]");
//			return false;
//		}

//		private bool CheckRollWrap(int startTile, int stride, out List<int> tilesToMove)
//		{
//			tilesToMove = new List<int>();
//			int currentTile = startTile;
//			TileDef startDef = GetTileDefAt(startTile);
//			if (startDef == null || !startDef.bRoll)
//				return false;

//			tilesToMove.Add(currentTile);
//			bool withinBounds = stride == 1 ? (currentTile % width < width - 1) :
//							   stride == -1 ? (currentTile % width > 0) :
//							   stride == width ? (currentTile / width < height - 1) :
//							   stride == -width ? (currentTile / width > 0) : false;

//			int nextTile = currentTile + stride;
//			while (withinBounds && nextTile >= 0 && nextTile < tileMap.Length)
//			{
//				TileDef nextDef = GetTileDefAt(nextTile);
//				if (nextDef == null || !nextDef.bRoll || nextDef.bDock)
//					break;
//				tilesToMove.Add(nextTile);
//				currentTile = nextTile;
//				nextTile = currentTile + stride;
//				withinBounds = stride == 1 ? (currentTile % width < width - 1) :
//							  stride == -1 ? (currentTile % width > 0) :
//							  stride == width ? (currentTile / width < height - 1) :
//							  stride == -width ? (currentTile / width > 0) : false;
//			}

//			currentTile = startTile;
//			int prevTile = currentTile - stride;
//			withinBounds = stride == 1 ? (currentTile % width > 0) :
//						  stride == -1 ? (currentTile % width < width - 1) :
//						  stride == width ? (currentTile / width > 0) :
//						  stride == -width ? (currentTile / width < height - 1) : false;

//			while (withinBounds && prevTile >= 0 && prevTile < tileMap.Length)
//			{
//				TileDef prevDef = GetTileDefAt(prevTile);
//				if (prevDef == null || !prevDef.bRoll || prevDef.bDock)
//					break;
//				tilesToMove.Insert(0, prevTile);
//				currentTile = prevTile;
//				prevTile = currentTile - stride;
//				withinBounds = stride == 1 ? (currentTile % width > 0) :
//							  stride == -1 ? (currentTile % width < width - 1) :
//							  stride == width ? (currentTile / width > 0) :
//							  stride == -width ? (currentTile / width < height - 1) : false;
//			}

//			int firstTile = tilesToMove[0];
//			int lastTile = tilesToMove[tilesToMove.Count - 1];
//			int beforeFirst = stride > 0 ? firstTile - stride : lastTile - stride;
//			int afterLast = stride > 0 ? lastTile + stride : firstTile + stride;

//			bool beforeValid = beforeFirst >= 0 && beforeFirst < tileMap.Length;
//			bool afterValid = afterLast >= 0 && afterLast < tileMap.Length;

//			TileDef beforeDef = beforeValid ? GetTileDefAt(beforeFirst) : null;
//			TileDef afterDef = afterValid ? GetTileDefAt(afterLast) : null;

//			bool isBounded = (beforeValid && beforeDef != null && !beforeDef.bSlide && !beforeDef.bRoll) ||
//							 (afterValid && afterDef != null && !afterDef.bSlide && !afterDef.bRoll) ||
//							 (!beforeValid || !afterValid);

//			Debug.Log($"CheckRollWrap: tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}], isBounded={isBounded}, beforeValid={beforeValid}, afterValid={afterValid}");
//			return isBounded && tilesToMove.Count > 1;
//		}

//		private int GetWrappedTile(List<int> tilesToMove, int tile, int stride)
//		{
//			int index = tilesToMove.IndexOf(tile);
//			int nextIndex = index + (stride > 0 ? 1 : -1);
//			if (nextIndex < 0)
//				nextIndex = tilesToMove.Count - 1;
//			else if (nextIndex >= tilesToMove.Count)
//				nextIndex = 0;
//			return tilesToMove[nextIndex];
//		}

//		private void SlideTiles(int startTile, int stride, List<int> tilesToMove, bool isRollGroup, bool willWrap)
//		{
//			int[] newTileMap = (int[])tileMap.Clone();

//			if (isRollGroup && willWrap)
//			{
//				int[] tempMap = (int[])tileMap.Clone();
//				foreach (int tile in tilesToMove)
//				{
//					int newTile = GetWrappedTile(tilesToMove, tile, stride);
//					newTileMap[tile] = tempMap[newTile];
//				}
//			}
//			else
//			{
//				for (int i = tilesToMove.Count - 1; i >= 0; i--)
//				{
//					int tile = tilesToMove[i];
//					int nextTile = i < tilesToMove.Count - 1 ? tilesToMove[i + 1] : tile + stride;
//					if (nextTile >= 0 && nextTile < tileMap.Length)
//					{
//						newTileMap[nextTile] = tileMap[tile];
//					}
//				}
//				int firstTile = tilesToMove[0];
//				int gapTile = firstTile + stride;
//				if (gapTile >= 0 && gapTile < tileMap.Length)
//				{
//					newTileMap[firstTile] = tileMap[gapTile];
//				}
//			}

//			tileMap = newTileMap;
//			foreach (int tile in tilesToMove)
//			{
//				int nextTile = isRollGroup && willWrap ? GetWrappedTile(tilesToMove, tile, stride) : tile + stride;
//				GameObject tileObj = FindTileObject(tile);
//				if (tileObj != null && nextTile >= 0 && nextTile < tileMap.Length)
//				{
//					tileObj.transform.position = new Vector3(nextTile % width, 0f, nextTile / width);
//				}
//			}
//			UpdateTilePositions();
//			Debug.Log($"SlideTiles: startTile={startTile} ({startTile % width},{startTile / width}), stride={stride}, tilesToMove=[{string.Join(", ", tilesToMove.Select(t => $"({t % width},{t / width})"))}], isRollGroup={isRollGroup}, willWrap={willWrap}");
//		}
//	}
//}
