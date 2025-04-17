using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GamePreviewNamespace
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager;
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
		private float pauseDuration = 1f;

		public bool IsLevelComplete => isLevelComplete;

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
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
							if (currentTileDef?.tileDef.bEnd == true)
							{
								Debug.Log("Level complete!");
								isLevelComplete = true;
							}
						}
						else
						{
							var currentTileDef = mapManager.GetTileDefAt(waypoints[currentWaypointIndex]);
							if (currentTileDef?.tileDef.bConsole == true)
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
			int startTile = mapManager.GetStartTile();
			if (startTile == -1)
				return;

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
			if (mapManager.CurrentMap?.waypoints != null)
			{
				foreach (var waypoint in mapManager.CurrentMap.waypoints)
				{
					if (waypoint != null)
						waypoints.Add(waypoint.nTile);
				}
			}
			Debug.Log($"Found {waypoints.Count} waypoints: [{string.Join(", ", waypoints)}]");
		}

		private bool CheckPathToNextWaypoint(out List<int> path)
		{
			path = new List<int>();
			if (currentWaypointIndex + 1 >= waypoints.Count)
			{
				Debug.Log($"No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={waypoints.Count})");
				return false;
			}

			int startTile = waypoints[currentWaypointIndex];
			int targetTile = waypoints[currentWaypointIndex + 1];
			var startDef = mapManager.GetTileDefAt(startTile);
			if (startDef == null)
				return false;

			int startNav = startDef.GetNav(false);
			foreach (var dir in mapManager.Directions)
			{
				if ((startNav & dir.bit) == 0)
					continue;
				if (mapManager.FindPath(startTile, targetTile, dir.bit, out path))
				{
					Debug.Log($"Found path to waypoint {targetTile}: [{string.Join(" -> ", path.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
					return true;
				}
			}

			return false;
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= waypoints.Count)
				return;

			var currentDef = mapManager.GetTileDefAt(waypoints[currentWaypointIndex]);
			if (currentDef?.tileDef.bConsole == true && isPuzzleBlocked)
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
		}
	}
}

