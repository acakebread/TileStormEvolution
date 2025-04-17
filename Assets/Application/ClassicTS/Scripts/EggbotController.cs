using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GamePreviewNamespace
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager;
		private GameObject eggbot;
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
			if (mapManager.CurrentMap == null || mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
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
				Vector3 startPos = mapManager.GetTilePosition(currentTile);
				Vector3 endPos = mapManager.GetTilePosition(nextTile);
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
						if (currentWaypointIndex >= mapManager.Waypoints.Count - 1)
						{
							var currentTileDef = mapManager.GetTileDefAt(mapManager.Waypoints[currentWaypointIndex].nTile);
							if (currentTileDef?.tileDef.bEnd == true)
							{
								Debug.Log("Level complete!");
								isLevelComplete = true;
							}
						}
						else
						{
							var currentTileDef = mapManager.GetTileDefAt(mapManager.Waypoints[currentWaypointIndex].nTile);
							if (currentTileDef?.tileDef.bConsole == true)
							{
								isPuzzleBlocked = !mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out _);
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
			eggbot.transform.position = mapManager.GetTilePosition(startTile);
			Debug.Log($"Eggbot placed at tile {startTile} ({mapManager.GetTilePosition(startTile).x}, {mapManager.GetTilePosition(startTile).z})");
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count)
				return;

			var currentDef = mapManager.GetTileDefAt(mapManager.Waypoints[currentWaypointIndex].nTile);
			if (currentDef?.tileDef.bConsole == true && isPuzzleBlocked)
			{
				if (mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out _))
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

			if (mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out currentPath))
			{
				isMoving = true;
				moveTimer = 0f;
				pathStepIndex = 0;
				Debug.Log($"Moving to waypoint {currentWaypointIndex + 1}: tile={mapManager.Waypoints[currentWaypointIndex + 1].nTile}, path=[{string.Join(" -> ", currentPath.Select(t => $"({t % mapManager.Width},{t / mapManager.Width})"))}]");
			}
		}
	}
}