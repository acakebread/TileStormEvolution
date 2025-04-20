using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager;
		private GameObject eggbot;

		private List<int> currentPath;
		private int pathStepIndex;
		private int currentWaypointIndex;

		private bool isMoving;
		private float moveTimer;
		private float moveSpeed = 8f;

		private float pauseTimer;
		private float pauseDuration = 1f;

		private bool isPuzzleBlocked;
		private bool isLevelComplete;
		public bool IsLevelComplete => isLevelComplete;

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			InitializeEggbot();
			Reset();
		}

		public void Reset()
		{
			currentPath = null;
			pathStepIndex = currentWaypointIndex = 0;
			isMoving = isLevelComplete = isPuzzleBlocked = false;
			moveTimer = 0f;
			pauseTimer = pauseDuration;
		}

		private void InitializeEggbot()
		{
			int startTile = mapManager.GetStartTile();
			if (startTile == -1)
				return;

			if (eggbot != null) Destroy(eggbot);
			eggbot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			eggbot.name = "Eggbot";
			var transform = eggbot.transform;
			transform.SetParent(mapManager.MapRoot.transform, false);
			transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
			transform.position = mapManager.GetTilePosition(startTile);
			Debug.Log($"Eggbot at tile {startTile} ({transform.position.x}, {transform.position.z})");
		}

		public void UpdateEggbot()
		{
			if (mapManager.Waypoints?.Count == 0)
				return;

			if (!isMoving)
			{
				pauseTimer -= Time.deltaTime;
				if (pauseTimer <= 0)
					MoveToNextWaypoint();
			}
			else
			{
				moveTimer += Time.deltaTime * moveSpeed;
				float t = Mathf.Clamp01(moveTimer);
				int currentTile = currentPath[pathStepIndex];
				int nextTile = currentPath[Mathf.Min(pathStepIndex + 1, currentPath.Count - 1)];
				eggbot.transform.position = Vector3.Lerp(
					mapManager.GetTilePosition(currentTile),
					mapManager.GetTilePosition(nextTile),
					t
				);

				if (t >= 1f)
				{
					pathStepIndex++;
					if (pathStepIndex >= currentPath.Count - 1)
					{
						isMoving = false;
						pauseTimer = pauseDuration;
						currentWaypointIndex++;
						currentPath = null;

						int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
						var tileProps = mapManager.GetTilePropertiesAt(waypointTile);
						if (currentWaypointIndex >= mapManager.Waypoints.Count - 1 && tileProps?.IsEnd == true)
						{
							Debug.Log($"Level complete at tile ({waypointTile % mapManager.Width},{waypointTile / mapManager.Width})!");
							isLevelComplete = true;
						}
						else if (tileProps?.IsConsole == true)
						{
							isPuzzleBlocked = !mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out _);
							if (isPuzzleBlocked)
								Debug.Log($"Waiting at console at tile ({waypointTile % mapManager.Width},{waypointTile / mapManager.Width})...");
						}
					}
					else
					{
						moveTimer = 0f;
					}
				}
			}
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count)
				return;

			int currentTile = mapManager.Waypoints[currentWaypointIndex].nTile;
			if (mapManager.FindAdjacentConsole(currentTile) != -1 && !mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out _))
			{
				//Debug.Log($"Puzzle blocked at console at tile ({currentTile % mapManager.Width},{currentTile / mapManager.Width})");
				return;
			}

			if (mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out currentPath))
			{
				isPuzzleBlocked = false;
				isMoving = true;
				moveTimer = 0f;
				pathStepIndex = 0;
				Debug.Log($"Moving to waypoint {currentWaypointIndex + 1}: tile={mapManager.Waypoints[currentWaypointIndex + 1].nTile}, path=[{mapManager.FormatPath(currentPath)}]");
			}
		}
	}
}
