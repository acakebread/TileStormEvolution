using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager;
		private CameraController cameraController;
		public GameObject eggbot;

		private List<int> currentPath;
		private int pathStepIndex;
		private int currentWaypointIndex;
		private bool isReturningToStart;
		private bool hasReachedEnd;

		private bool isMoving;
		private float moveTimer;
		private float moveDuration;

		private float pauseTimer;
		private float pauseDuration = 1f;

		private bool isPuzzleBlocked;
		private bool isLevelComplete;
		public bool IsLevelComplete => isLevelComplete;

		private Vector3 startPosition;
		private Vector3 targetPosition;
		private float startYaw;
		private float targetYaw;
		private float turnTimer;
		private float turnDuration = 1f / 6f; // Based on gfTurnRate=6.0f
		private bool isTurning;

		private int segmentStartIndex;
		private int segmentEndIndex;
		private float walkSpeed = 6f; // Based on gfWalkRate=6.0f (tiles per second)

		private bool isCheckingConsole;
		private float consoleCheckTimer;
		private float consoleCheckDuration = 0.5f; // Time to face console

		private bool isSpinning;
		private float spinTimer;
		private float spinDuration = 1f; // Time for spin
		private float spinAngle = 1260f; // 3.5 rotations (from second script)

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			cameraController = GetComponent<CameraController>();
			InitializeEggbot();
			Reset();
		}

		public void Reset()
		{
			currentPath = null;
			pathStepIndex = currentWaypointIndex = 0;
			isReturningToStart = hasReachedEnd = false;
			isMoving = isLevelComplete = isPuzzleBlocked = false;
			moveTimer = turnTimer = consoleCheckTimer = spinTimer = 0f;
			pauseTimer = pauseDuration;
			isTurning = isCheckingConsole = isSpinning = false;
			segmentStartIndex = segmentEndIndex = 0;
		}

		public void UpdateEggbot()
		{
			if (mapManager.Waypoints?.Count == 0)
				return;

			if (isSpinning)
			{
				spinTimer += Time.deltaTime;
				float t = Mathf.Clamp01(spinTimer / spinDuration);
				float cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f; // Sigmoid interpolation
				float angle = cosT * spinAngle;
				float displayAngle = angle % 360f; // Ensure visual rotation aligns
				eggbot.transform.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

				if (t >= 1f)
				{
					isSpinning = false;
					spinTimer = 0f;
					eggbot.transform.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
					isReturningToStart = true; // Begin return to start
					hasReachedEnd = true;
					pauseTimer = pauseDuration;
				}
				return;
			}

			if (isTurning)
			{
				turnTimer += Time.deltaTime;
				float t = Mathf.Clamp01(turnTimer / turnDuration);
				float cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
				float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosT);
				eggbot.transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

				if (t >= 1f)
				{
					isTurning = false;
					turnTimer = 0f;
					if (isMoving)
					{
						StartSegmentMovement();
					}
					else if (isCheckingConsole)
					{
						consoleCheckTimer = consoleCheckDuration;
					}
				}
			}
			else if (isCheckingConsole)
			{
				consoleCheckTimer -= Time.deltaTime;
				if (consoleCheckTimer <= 0)
				{
					isCheckingConsole = false;
					int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
					bool pathClear = mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out currentPath);

					if (pathClear)
					{
						if (currentPath != null && currentPath.Count > 1)
						{
							Vector3 nextPos = mapManager.GetTilePosition(currentPath[1]);
							Vector3 currentPos = mapManager.GetTilePosition(currentPath[0]);
							Vector3 direction = (nextPos - currentPos).normalized;
							float pathYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
							StartTurning(pathYaw, false);
						}
						else
						{
							pauseTimer = pauseDuration;
						}
					}
					else
					{
						isPuzzleBlocked = true;
					}
				}
			}
			else if (isMoving)
			{
				moveTimer += Time.deltaTime;
				float t = moveDuration > 0 ? Mathf.Clamp01(moveTimer / moveDuration) : 1f;
				float cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
				eggbot.transform.position = Vector3.Lerp(startPosition, targetPosition, cosT);

				if (t >= 1f)
				{
					eggbot.transform.position = targetPosition;
					isMoving = false;
					pathStepIndex = segmentEndIndex;

					if (pathStepIndex >= currentPath.Count - 1)
					{
						currentWaypointIndex = isReturningToStart ? 0 : currentWaypointIndex + 1;
						currentPath = null;
						segmentStartIndex = segmentEndIndex = 0;
						pauseTimer = pauseDuration;

						if (currentWaypointIndex < mapManager.Waypoints.Count)
						{
							int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
							eggbot.transform.position = mapManager.GetTilePosition(waypointTile);
							var tileProps = mapManager.GetTilePropertiesAt(waypointTile);
							cameraController?.OnWaypointReached(currentWaypointIndex);

							if (isReturningToStart && currentWaypointIndex == 0)
							{
								isReturningToStart = false;
								hasReachedEnd = false;
								isLevelComplete = false;
								pauseTimer = pauseDuration;
							}
							else if (currentWaypointIndex >= mapManager.Waypoints.Count - 1 && tileProps?.IsEnd == true && !isReturningToStart)
							{
								isLevelComplete = true;
								StartSpinning();
							}
							else
							{
								int consoleTile = mapManager.FindAdjacentConsole(waypointTile);
								if (consoleTile != -1)
								{
									TileProperties consoleProps = mapManager.GetTilePropertiesAt(consoleTile);
									if (consoleProps?.Nav != 0)
									{
										int oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
										float consoleYaw = DirToAngle(oppositeDir);
										float currentYaw = eggbot.transform.eulerAngles.y;
										if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw)) > 0.1f)
										{
											StartTurning(consoleYaw, false);
											isCheckingConsole = true;
										}
									}
								}
							}
						}
					}
					else
					{
						PrepareNextSegment();
					}
				}
			}
			else
			{
				pauseTimer -= Time.deltaTime;
				if (pauseTimer <= 0)
					MoveToNextWaypoint();
			}
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count && !isReturningToStart)
				return;

			if (isReturningToStart)
			{
				if (currentWaypointIndex == 0)
				{
					isReturningToStart = false;
					hasReachedEnd = false;
					isLevelComplete = false;
					pauseTimer = pauseDuration;
					return;
				}
				// Navigate back to waypoint 0
				int targetWaypoint = 0;
				if (mapManager.CheckPathToWaypoint(currentWaypointIndex, targetWaypoint, out currentPath))
				{
					isPuzzleBlocked = false;
					pathStepIndex = 0;
					segmentStartIndex = segmentEndIndex = 0;
					PrepareNextSegment();
				}
				else
				{
					pauseTimer = pauseDuration; // Fallback if no path
				}
			}
			else
			{
				int currentTile = mapManager.Waypoints[currentWaypointIndex].nTile;
				if (mapManager.FindAdjacentConsole(currentTile) != -1 && !mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out _))
				{
					if (!isTurning)
					{
						int consoleTile = mapManager.FindAdjacentConsole(currentTile);
						if (consoleTile != -1)
						{
							TileProperties consoleProps = mapManager.GetTilePropertiesAt(consoleTile);
							if (consoleProps?.Nav != 0)
							{
								int oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
								float consoleYaw = DirToAngle(oppositeDir);
								float currentYaw = eggbot.transform.eulerAngles.y;
								if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw)) > 0.1f)
								{
									StartTurning(consoleYaw, false);
									isCheckingConsole = true;
								}
							}
						}
					}
					return;
				}

				if (mapManager.CheckPathBetweenWaypoints(currentWaypointIndex, out currentPath))
				{
					isPuzzleBlocked = false;
					cameraController?.OnPuzzleSolved(currentWaypointIndex);
					pathStepIndex = 0;
					segmentStartIndex = segmentEndIndex = 0;
					PrepareNextSegment();
				}
			}
		}

		private void PrepareNextSegment()
		{
			if (pathStepIndex >= currentPath.Count - 1)
			{
				segmentStartIndex = segmentEndIndex = pathStepIndex;
				startPosition = mapManager.GetTilePosition(currentPath[pathStepIndex]);
				targetPosition = startPosition;
				moveDuration = 0f;
				StartSegmentMovement();
				return;
			}

			segmentStartIndex = pathStepIndex;
			segmentEndIndex = segmentStartIndex;
			int prevTile = currentPath[segmentStartIndex];
			Vector3 prevPos = mapManager.GetTilePosition(prevTile);
			int currentDir = 0;

			for (int i = segmentStartIndex + 1; i < currentPath.Count; i++)
			{
				int nextTile = currentPath[i];
				Vector3 nextPos = mapManager.GetTilePosition(nextTile);
				Vector3 direction = (nextPos - prevPos).normalized;
				int newDir = GetDirectionFlag(direction);

				if (currentDir == 0)
					currentDir = newDir;
				else if (newDir != currentDir)
				{
					segmentEndIndex = i - 1;
					break;
				}

				segmentEndIndex = i;
				prevTile = nextTile;
				prevPos = nextPos;
			}

			startPosition = mapManager.GetTilePosition(currentPath[segmentStartIndex]);
			targetPosition = mapManager.GetTilePosition(currentPath[segmentEndIndex]);
			float distance = Vector3.Distance(startPosition, targetPosition);
			moveDuration = distance / walkSpeed;

			if (moveDuration <= 0f && segmentEndIndex > segmentStartIndex)
			{
				moveDuration = (segmentEndIndex - segmentStartIndex) / walkSpeed;
			}

			if (segmentEndIndex > segmentStartIndex)
			{
				Vector3 direction = (targetPosition - startPosition).normalized;
				targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
				StartTurning(targetYaw, true);
			}
			else
			{
				StartSegmentMovement();
			}
		}

		private void StartSegmentMovement()
		{
			moveTimer = 0f;
			isMoving = true;
		}

		private void StartTurning(float newTargetYaw, bool continueMoving)
		{
			startYaw = eggbot.transform.eulerAngles.y;
			targetYaw = newTargetYaw;
			turnTimer = 0f;
			isTurning = true;
			isMoving = continueMoving;
		}

		private void StartSpinning()
		{
			startYaw = eggbot.transform.eulerAngles.y;
			spinTimer = 0f;
			isSpinning = true;
		}

		private int GetDirectionFlag(Vector3 direction)
		{
			if (direction.sqrMagnitude < 0.01f)
				return 0;
			float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
			if (Mathf.Abs(angle) <= 45f) return 1; // North (positive Z)
			if (Mathf.Abs(angle - 180) <= 45f || Mathf.Abs(angle + 180) <= 45f) return 2; // South (negative Z)
			if (Mathf.Abs(angle - 90) <= 45f) return 4; // East (positive X)
			if (Mathf.Abs(angle + 90) <= 45f) return 8; // West (negative X)
			return 0;
		}

		private float DirToAngle(int dir)
		{
			if ((dir & 1) != 0) return 0f;   // North (positive Z)
			if ((dir & 2) != 0) return 180f; // South (negative Z)
			if ((dir & 4) != 0) return 90f;  // East (positive X)
			if ((dir & 8) != 0) return -90f; // West (negative X)
			return 0f;
		}

		private void InitializeEggbot()
		{
			int startTile = mapManager.GetStartTile();
			if (startTile == -1)
				return;

			if (eggbot != null) Destroy(eggbot);

			var prefabPath = $"{PreviewSettings.PrefabPath}eggbot_default";
			eggbot = Instantiate(Resources.Load(prefabPath, typeof(GameObject))) as GameObject;
			eggbot.name = "Eggbot";
			var transform = eggbot.transform;
			transform.SetParent(mapManager.MapRoot.transform, false);
			transform.position = mapManager.GetTilePosition(startTile);
			transform.rotation = Quaternion.identity;
		}
	}
}