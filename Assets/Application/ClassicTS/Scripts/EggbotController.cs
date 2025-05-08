using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GamePreviewNamespace
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager;
		private CameraController cameraController;
		public GameObject eggbot;
		private Transform meshTransform; // Reference to the Mesh child transform

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

		public static bool isPuzzleBlocked;
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
		private float spinAngle = 1260f; // 3.5 rotations

		// Wobble variables
		private float wobble = 0.1f; // Initial wobble amplitude
		private static float mod1 = 0.0f; // Persistent accumulator for wobble
		private static float mod2 = 0.0f; // Persistent accumulator for wobble

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			cameraController = GetComponent<CameraController>();
			InitializeEggbot(manager.EggbotCostume);
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
			wobble = 0.1f; // Reset wobble to initial value
			mod1 = 0.0f;
			mod2 = 0.0f;
		}

		public void UpdateEggbot()
		{
			if (mapManager.Waypoints?.Count == 0)
				return;

			// Update wobble accumulators
			mod1 += 7.8f * Time.deltaTime;
			mod2 += 1.8f * Time.deltaTime;

			// Update wobble factor based on state
			bool isIdle = !isMoving && !isTurning && !isSpinning && !isCheckingConsole;
			float targetWobble = isIdle ? 0.02f : 0.1f;
			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

			// Calculate pitch for wobble
			float pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);

			if (isSpinning)
			{
				spinTimer += Time.deltaTime;
				float t = Mathf.Clamp01(spinTimer / spinDuration);
				float cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f; // Sigmoid interpolation
				float angle = cosT * spinAngle;
				float displayAngle = angle % 360f; // Ensure visual rotation aligns
				eggbot.transform.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

				// Reset mesh transform during spin to avoid wobble
				meshTransform.localPosition = Vector3.zero;
				meshTransform.localRotation = Quaternion.identity;

				if (t >= 1f)
				{
					isSpinning = false;
					spinTimer = 0f;
					eggbot.transform.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
					if (hasReachedEnd && isReturningToStart && currentWaypointIndex == 0)
					{
						// After celebratory spin at start, reset state
						isReturningToStart = false;
						hasReachedEnd = false;
						isLevelComplete = false;
						pauseTimer = pauseDuration;
					}
					else if (hasReachedEnd && !isReturningToStart)
					{
						// After spin at end, begin returning to start
						isReturningToStart = true;
						pauseTimer = pauseDuration;
					}
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
					bool pathClear = Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, out currentPath);

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
							var tileProps = mapManager.GetTileProperties(waypointTile);
							cameraController?.OnWaypointReached(currentWaypointIndex);

							if (isReturningToStart && currentWaypointIndex == 0 && hasReachedEnd)
							{
								StartSpinning(); // Celebratory spin at start after returning
							}
							else if (currentWaypointIndex >= mapManager.Waypoints.Count - 1 && tileProps?.IsEnd == true && !isReturningToStart)
							{
								isLevelComplete = true;
								hasReachedEnd = true;
								StartSpinning(); // Spin at end
							}
							else
							{
								CheckAndFaceAdjacentConsole(waypointTile);
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

			// Apply wobble to the Mesh child transform (offset * pitch)
			Quaternion pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f); // X-axis rotation
			Vector3 localOffset = new Vector3(0f, 0f, -pitch); // Local Z-offset (forward/backward)
			Vector3 wobblePos = pitchRotation * localOffset; // offset * pitch in local space

			// Set the Mesh's local transform
			meshTransform.localPosition = wobblePos;
			meshTransform.localRotation = pitchRotation;
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count && !isReturningToStart)
				return;

			if (isReturningToStart)
			{
				if (currentWaypointIndex == 0)
				{
					if (hasReachedEnd)
					{
						StartSpinning(); // Trigger celebratory spin at start
					}
					else
					{
						isReturningToStart = false;
						hasReachedEnd = false;
						isLevelComplete = false;
						pauseTimer = pauseDuration;
					}
					return;
				}
				// Navigate back to waypoint 0
				int targetWaypoint = 0;
				if (Navigation.CheckPathToWaypoint(mapManager, currentWaypointIndex, targetWaypoint, out currentPath))
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
				if (Navigation.FindAdjacentConsole(mapManager, currentTile) != -1 && !Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, out _))
				{
					if (!isTurning)
					{
						CheckAndFaceAdjacentConsole(currentTile);
					}
					return;
				}

				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, out currentPath))
				{
					isPuzzleBlocked = false;
					cameraController?.OnPuzzleSolved(currentWaypointIndex);
					pathStepIndex = 0;
					segmentStartIndex = segmentEndIndex = 0;
					PrepareNextSegment();
				}
			}
		}

		private void CheckAndFaceAdjacentConsole(int tile)
		{
			int consoleTile = Navigation.FindAdjacentConsole(mapManager, tile);
			if (consoleTile != -1)
			{
				TileProperties consoleProps = mapManager.GetTileProperties(consoleTile);
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
			// Add 1.0f to distance to match original game's movement duration
			moveDuration = (distance + 1.0f) / walkSpeed;

			if (moveDuration <= 0f && segmentEndIndex > segmentStartIndex)
			{
				// Use tile count with offset for fallback duration
				moveDuration = ((segmentEndIndex - segmentStartIndex) + 1.0f) / walkSpeed;
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

		private void InitializeEggbot(string eggbotCostume = "Eggbot Default")
		{
			int startTile = Navigation.GetStartTile(mapManager);
			if (startTile == -1)
				return;

			if (eggbot != null) Destroy(eggbot);

			eggbot = new GameObject("Eggbot");
			eggbot.transform.position = mapManager.GetTilePosition(startTile);
			eggbot.transform.rotation = Quaternion.identity;
			eggbot.transform.SetParent(mapManager.transform, false);

			if (null == eggbotCostume) eggbotCostume = "Eggbot Default";
			var def = GameDatabase.DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
			if (def == null || def.szGeom == null) return;

			var prefab = GeometryManager.Get(def.szGeom);
			var mesh = Instantiate(prefab, eggbot.transform);
			mesh.name = "Mesh";

			var transform = mesh.transform;
			transform.SetParent(eggbot.transform, false);
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;

			//if (PreviewSettings.FlipGeometry)
			//	transform.GetChild(0).transform.localRotation = Quaternion.AngleAxis(180, Vector3.up);

			// Store reference to the Mesh transform
			meshTransform = transform;

			GameDatabase.DatabaseLoader.Theme theme = GameDatabase.DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
			if (theme == null || string.IsNullOrEmpty(theme.szTileTextureSet)) return;

			var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
			if (textureFrames?.Length > 0)
			{
				var animator = mesh.AddComponent<TextureSetAnimator>();
				animator.Initialize(textureFrames);
			}
			else
			{
				Debug.LogWarning($"No texture set for {eggbot}");
			}
		}
	}
}
