using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager;
		private float pauseDuration;
		private GameObject eggbot;
		private List<int> waypoints;
		private int currentWaypointIndex;
		private bool isMoving;
		private float moveTimer;
		private float pauseTimer;
		private bool isPuzzleBlocked;
		private bool isLevelComplete;
		private List<int> currentPath;
		private int pathStepIndex;
		private float moveSpeed = 8f;

		public bool IsLevelComplete => isLevelComplete;

		public void Initialize(MapManager manager, float pause)
		{
			mapManager = manager;
			pauseDuration = pause;
			InitializeEggbot();
			InitializeWaypoints();
			Reset();
		}

		public void Reset()
		{
			currentWaypointIndex = 0;
			isMoving = false;
			moveTimer = 0f;
			pauseTimer = pauseDuration;
			isLevelComplete = false;
			isPuzzleBlocked = false;
			currentPath = null;
			pathStepIndex = 0;
		}

		public void UpdateEggbot()
		{
			if (mapManager.CurrentMap == null || waypoints.Count == 0)
				return;

			if (!isMoving)
			{
				pauseTimer -= Time.deltaTime;
				if (pauseTimer <= 0)
				{
					MoveToNextWaypoint();
				}
			}
			else
			{
				moveTimer += Time.deltaTime * moveSpeed;
				float t = Mathf.Clamp01(moveTimer);
				int currentTile = currentPath[pathStepIndex];
				int nextTile = pathStepIndex + 1 < currentPath.Count ? currentPath[pathStepIndex + 1] : currentTile;
				Vector3 startPos = new Vector3(currentTile % mapManager.Width, 1f, currentTile / mapManager.Width);
				Vector3 endPos = new Vector3(nextTile % mapManager.Width, 1f, nextTile / mapManager.Width);
				eggbot.transform.position = Vector3.Lerp(startPos, endPos, t);

				if (t >= 1f)
				{
					pathStepIndex++;
					if (pathStepIndex >= currentPath.Count - 1)
					{
						isMoving = false;
						pauseTimer = pauseDuration;
						currentWaypointIndex++;
						currentPath = null;
						if (currentWaypointIndex >= waypoints.Count - 1)
						{
							var currentTileDef = mapManager.GetTileDefAt(waypoints[currentWaypointIndex]);
							if (currentTileDef != null && currentTileDef.tileDef.bEnd)
							{
								Debug.Log("Level complete!");
								isLevelComplete = true;
							}
						}
						else
						{
							var currentTileDef = mapManager.GetTileDefAt(waypoints[currentWaypointIndex]);
							if (currentTileDef != null && currentTileDef.tileDef.bConsole)
							{
								isPuzzleBlocked = !CheckPathToNextWaypoint(out _);
								if (isPuzzleBlocked)
								{
									Debug.Log("Waiting at console for puzzle...");
								}
							}
						}
					}
					else
					{
						moveTimer = 0f;
					}
				}
			}
		}

		private void InitializeEggbot()
		{
			int startTile = -1;
			for (int i = 0; i < mapManager.Width * mapManager.Height; i++)
			{
				var tileDef = mapManager.GetTileDefAt(i);
				if (tileDef != null && tileDef.tileDef.bStart)
				{
					startTile = i;
					break;
				}
			}

			if (startTile == -1)
			{
				Debug.LogError("No start tile found!");
				return;
			}

			if (eggbot != null)
			{
				Destroy(eggbot);
			}

			eggbot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			eggbot.name = "Eggbot";
			eggbot.transform.SetParent(mapManager.MapRoot.transform, false);
			eggbot.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
			eggbot.transform.position = new Vector3(startTile % mapManager.Width, 1f, startTile / mapManager.Width);
			Debug.Log($"Eggbot placed at tile {startTile} ({startTile % mapManager.Width}, {startTile / mapManager.Width})");
		}

		private void InitializeWaypoints()
		{
			waypoints = new List<int>();
			if (mapManager.CurrentMap.waypoints != null && mapManager.CurrentMap.waypoints.Length > 0)
			{
				foreach (var waypoint in mapManager.CurrentMap.waypoints)
				{
					if (waypoint != null)
					{
						waypoints.Add(waypoint.nTile);
					}
				}
			}
			Debug.Log($"Found {waypoints.Count} waypoints: [{string.Join(", ", waypoints)}]");
		}

		private bool CheckPathToNextWaypoint(out List<int> path)
		{
			path = new List<int>();
			if (currentWaypointIndex + 1 >= waypoints.Count)
			{
				Debug.Log($"CheckPathToNextWaypoint: No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={waypoints.Count})");
				return false;
			}

			int currentTile = waypoints[currentWaypointIndex];
			int targetWaypointTile = waypoints[currentWaypointIndex + 1];
			HashSet<int> visited = new HashSet<int>();
			List<string> traversalLog = new List<string>();
			Dictionary<int, int> parent = new Dictionary<int, int>();

			Queue<int> queue = new Queue<int>();
			queue.Enqueue(currentTile);
			visited.Add(currentTile);
			parent[currentTile] = -1;

			while (queue.Count > 0)
			{
				int tile = queue.Dequeue();
				if (tile == targetWaypointTile)
				{
					int current = tile;
					while (current != -1)
					{
						path.Add(current);
						current = parent.ContainsKey(current) ? parent[current] : -1;
					}
					path.Reverse();
					Debug.Log($"CheckPathToNextWaypoint: Found waypoint {targetWaypointTile}. Path: [{string.Join(" -> ", path.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
					return true;
				}

				var tileDef = mapManager.GetTileDefAt(tile);
				if (tileDef == null)
				{
					Debug.LogWarning($"CheckPathToNextWaypoint: No TileDef at tile {tile}");
					continue;
				}

				int x = tile % mapManager.Width;
				int z = tile / mapManager.Width;
				List<(int nextTile, string direction)> validNeighbors = new List<(int, string)>();

				if (tileDef.tileDef.bEast)
				{
					int eastTile = tile + 1;
					if (x < mapManager.Width - 1 && eastTile < mapManager.Width * mapManager.Height)
					{
						var eastDef = mapManager.GetTileDefAt(eastTile);
						if (eastDef != null && eastDef.tileDef.bWest)
						{
							validNeighbors.Add((eastTile, "East"));
						}
					}
				}
				if (tileDef.tileDef.bWest)
				{
					int westTile = tile - 1;
					if (x > 0)
					{
						var westDef = mapManager.GetTileDefAt(westTile);
						if (westDef != null && westDef.tileDef.bEast)
						{
							validNeighbors.Add((westTile, "West"));
						}
					}
				}
				if (tileDef.tileDef.bNorth)
				{
					int northTile = tile + mapManager.Width;
					if (z < mapManager.Height - 1 && northTile < mapManager.Width * mapManager.Height)
					{
						var northDef = mapManager.GetTileDefAt(northTile);
						if (northDef != null && northDef.tileDef.bSouth)
						{
							validNeighbors.Add((northTile, "North"));
						}
					}
				}
				if (tileDef.tileDef.bSouth)
				{
					int southTile = tile - mapManager.Width;
					if (z > 0)
					{
						var southDef = mapManager.GetTileDefAt(southTile);
						if (southDef != null && southDef.tileDef.bNorth)
						{
							validNeighbors.Add((southTile, "South"));
						}
					}
				}

				foreach (var (nextTile, direction) in validNeighbors)
				{
					if (!visited.Contains(nextTile))
					{
						visited.Add(nextTile);
						queue.Enqueue(nextTile);
						parent[nextTile] = tile;
						traversalLog.Add($"{tile} (x={x}, z={z}, {tileDef.tileDef.szType}) -> {direction}");
					}
				}
			}

			//Debug.Log($"CheckPathToNextWaypoint: No path to waypoint {targetWaypointTile}. Visited: [{string.Join(", ", traversalLog)}]");
			return false;
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= waypoints.Count)
			{
				// Finished level
				// Debug.Log($"MoveToNextWaypoint: No next waypoint (currentIndex={currentWaypointIndex})");
				return;
			}

			var currentDef = mapManager.GetTileDefAt(waypoints[currentWaypointIndex]);
			if (currentDef != null && currentDef.tileDef.bConsole && isPuzzleBlocked)
			{
				if (CheckPathToNextWaypoint(out _))
				{
					isPuzzleBlocked = false;
					Debug.Log("Puzzle solved, proceeding...");
				}
				else
				{
					Debug.Log("Puzzle still blocked at console");
					return;
				}
			}

			if (CheckPathToNextWaypoint(out currentPath))
			{
				isMoving = true;
				moveTimer = 0f;
				pathStepIndex = 0;
				Debug.Log($"Moving to waypoint {currentWaypointIndex + 1}: tile={waypoints[currentWaypointIndex + 1]}, path=[{string.Join(" -> ", currentPath.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
			}
			else
			{
				//Debug.LogWarning($"Failed to move to waypoint {currentWaypointIndex + 1}: tile={waypoints[currentWaypointIndex + 1]}");
			}
		}
	}
}