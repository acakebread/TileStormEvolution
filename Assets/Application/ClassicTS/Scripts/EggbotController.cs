using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager => GamePreview.mapManager;
		private CameraController cameraController => GamePreview.cameraController;

		[HideInInspector]
		public Transform eggbotRoot;
		private Transform eggbotMesh; // Reference to the Mesh child transform

		private List<int> currentPath;
		private int pathStepIndex;
		private int currentWaypointIndex;
		private bool isReturningToStart;
		private bool hasReachedEnd;

		private float pauseTimer;
		private float pauseDuration = 1f;

		public static bool isPuzzleBlocked;
		private bool isLevelComplete;
		public bool IsLevelComplete => isLevelComplete;

		private bool isCheckingConsole;
		private float consoleCheckTimer;
		private float consoleCheckDuration = 0.5f; // Time to face console

		private bool isMoving;
		private float moveTimer;
		private float moveDuration;
		private float walkSpeed = 6f; // Based on gfWalkRate=6.0f (tiles per second)
		private Vector3 startPosition;
		private Vector3 targetPosition;
		private int segmentStartIndex;
		private int segmentEndIndex;

		private bool isTurning;
		private float turnTimer;
		private float startYaw;
		private float targetYaw;
		private float turnDuration = 1f / 6f; // Based on gfTurnRate=6.0f

		private bool isSpinning;
		private float spinTimer;
		private float spinDuration = 1f; // Time for spin
		private float spinAngle = 1260f; // 3.5 rotations

		// Wobble variables
		private float wobble = 0.1f; // Initial wobble amplitude
		private static float mod1 = 0.0f; // Persistent accumulator for wobble
		private static float mod2 = 0.0f; // Persistent accumulator for wobble

		public void Initialize()
		{
			Reset();
			InitializeEggbot();

			//local function
			void InitializeEggbot()
			{
				var eggbotCostume = string.IsNullOrEmpty(GamePreview.mapManager.EggbotCostume) ? "Eggbot Default" : GamePreview.mapManager.EggbotCostume;
				int startTile = Navigation.GetStartTile(mapManager);
				if (startTile == -1)
					return;

				if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

				eggbotRoot = new GameObject("Eggbot").transform;
				eggbotRoot.position = mapManager.GetTilePosition(startTile);
				eggbotRoot.rotation = Quaternion.identity;
				eggbotRoot.SetParent(mapManager.transform, false);

				if (null == eggbotCostume) eggbotCostume = "Eggbot Default";
				var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
				if (def == null || def.szGeom == null) return;

				var prefab = GeometryManager.Get(def.szGeom);
				var mesh = Instantiate(prefab, eggbotRoot);
				mesh.name = "Mesh";

				var transform = mesh.transform;
				transform.SetParent(eggbotRoot, false);
				transform.localPosition = Vector3.zero;
				transform.localRotation = Quaternion.identity;

				// Store reference to the Mesh transform
				eggbotMesh = transform;

				DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
				if (theme == null || string.IsNullOrEmpty(theme.szTileTextureSet)) return;

				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
				if (textureFrames?.Length > 0)
				{
					var animator = mesh.AddComponent<TextureSetAnimator>();
					animator.Initialize(textureFrames);
				}
				else
				{
					Debug.LogWarning($"No texture set for {eggbotRoot}");
				}
			}
		}

		public void Reset()
		{
			currentPath = null;
			pathStepIndex = currentWaypointIndex = 0;
			segmentStartIndex = segmentEndIndex = 0;

			isReturningToStart = hasReachedEnd = false;
			isMoving = isLevelComplete = isPuzzleBlocked = false;
			isTurning = isCheckingConsole = isSpinning = false;
			
			moveTimer = turnTimer = consoleCheckTimer = spinTimer = 0f;
			pauseTimer = pauseDuration;
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
			var isIdle = !isMoving && !isTurning && !isSpinning && !isCheckingConsole;
			var targetWobble = isIdle ? 0.02f : 0.1f;
			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

			// Calculate pitch for wobble
			float pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);

			if (isSpinning)
			{
				spinTimer += Time.deltaTime;
				var t = Mathf.Clamp01(spinTimer / spinDuration);
				var cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f; // Sigmoid interpolation
				var angle = cosT * spinAngle;
				var displayAngle = angle % 360f; // Ensure visual rotation aligns
				eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

				// Reset mesh transform during spin to avoid wobble
				eggbotMesh.localPosition = Vector3.zero;
				eggbotMesh.localRotation = Quaternion.identity;

				if (t >= 1f)
				{
					isSpinning = false;
					spinTimer = 0f;
					eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
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
				var t = Mathf.Clamp01(turnTimer / turnDuration);
				var cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
				var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosT);
				eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

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
					var waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
					var pathClear = Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, out currentPath);

					if (pathClear)
					{
						if (currentPath != null && currentPath.Count > 1)
						{
							var nextPos = mapManager.GetTilePosition(currentPath[1]);
							var currentPos = mapManager.GetTilePosition(currentPath[0]);
							var direction = (nextPos - currentPos).normalized;
							var pathYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
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
				var t = moveDuration > 0 ? Mathf.Clamp01(moveTimer / moveDuration) : 1f;
				var cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
				eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosT);

				if (t >= 1f)
				{
					eggbotRoot.position = targetPosition;
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
							eggbotRoot.position = mapManager.GetTilePosition(waypointTile);
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
			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f); // X-axis rotation
			var localOffset = new Vector3(0f, 0f, -pitch); // Local Z-offset (forward/backward)
			var wobblePos = pitchRotation * localOffset; // offset * pitch in local space

			// Set the Mesh's local transform
			eggbotMesh.localPosition = wobblePos;
			eggbotMesh.localRotation = pitchRotation;
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
				var targetWaypoint = 0;
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
				var currentTile = mapManager.Waypoints[currentWaypointIndex].nTile;
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
			var consoleTile = Navigation.FindAdjacentConsole(mapManager, tile);
			if (consoleTile != -1)
			{
				var consoleProps = mapManager.GetTileProperties(consoleTile);
				if (consoleProps?.Nav != 0)
				{
					var oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
					var consoleYaw = DirToAngle(oppositeDir);
					var currentYaw = eggbotRoot.eulerAngles.y;
					if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw)) > 0.1f)
					{
						StartTurning(consoleYaw, false);
						isCheckingConsole = true;
					}
				}
			}

			static float DirToAngle(int dir)
			{
				if ((dir & 1) != 0) return 0f;   // North (positive Z)
				if ((dir & 2) != 0) return 180f; // South (negative Z)
				if ((dir & 4) != 0) return 90f;  // East (positive X)
				if ((dir & 8) != 0) return -90f; // West (negative X)
				return 0f;
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

			var currentDir = 0;
			segmentEndIndex = pathStepIndex;
			while (segmentEndIndex < currentPath.Count - 1)
			{
				var direction = Navigation.GetTileOffsetToDirection(mapManager, currentPath[segmentEndIndex + 1] - currentPath[segmentEndIndex]);

				if (currentDir == 0)
					currentDir = direction;
				else if (direction != currentDir)
					break;
				segmentEndIndex++;
			}

			segmentStartIndex = pathStepIndex;
			startPosition = mapManager.GetTilePosition(currentPath[segmentStartIndex]);
			targetPosition = mapManager.GetTilePosition(currentPath[segmentEndIndex]);
			var distance = Vector3.Distance(startPosition, targetPosition);
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
			startYaw = eggbotRoot.eulerAngles.y;
			targetYaw = newTargetYaw;
			turnTimer = 0f;
			isTurning = true;
			isMoving = continueMoving;
		}

		private void StartSpinning()
		{
			startYaw = eggbotRoot.eulerAngles.y;
			spinTimer = 0f;
			isSpinning = true;
		}
	}
}
