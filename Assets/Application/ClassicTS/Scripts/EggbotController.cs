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
		private Transform eggbotMesh;

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

		private float consoleCheckTimer;
		private float consoleCheckDuration = 0.5f;

		private float moveTimer;
		private float moveDuration;
		private float walkSpeed = 6f;
		private Vector3 startPosition;
		private Vector3 targetPosition;
		private int segmentStartIndex;
		private int segmentEndIndex;

		private float turnTimer;
		private float startYaw;
		private float targetYaw;
		private float turnDuration = 1f / 6f;

		private float spinTimer;
		private float spinDuration = 1f;
		private float spinAngle = 1260f;

		private float wobble = 0.1f;
		private static float mod1 = 0.0f;
		private static float mod2 = 0.0f;

		private enum State
		{
			Pausing,
			Moving,
			Turning,
			CheckingConsole,
			Spinning
		}
		private State currentState = State.Pausing;

		public void Initialize()
		{
			Reset();
			InitializeEggbot();
		}

		public void Reset()
		{
			currentPath = null;
			pathStepIndex = currentWaypointIndex = 0;
			segmentStartIndex = segmentEndIndex = 0;

			isReturningToStart = hasReachedEnd = false;
			isLevelComplete = isPuzzleBlocked = false;

			moveTimer = turnTimer = consoleCheckTimer = spinTimer = 0f;
			pauseTimer = pauseDuration;
			wobble = 0.1f;
			mod1 = mod2 = 0.0f;

			//InitializeEggbot();

			//try
			//{
			//	MoveToNextWaypoint();
			//	Debug.Log("Reset: Eggbot reloaded, repositioned, reoriented, and movement to waypoint 1 initiated");
			//}
			//catch (System.Exception e)
			//{
			//	Debug.LogError($"Reset: Failed to complete - {e.Message}");
			//}
		}

		private void InitializeEggbot()
		{
			if (mapManager == null)
			{
				Debug.LogError("InitializeEggbot: mapManager is null");
				return;
			}

			int startTile = Navigation.GetStartTile(mapManager);
			if (startTile == -1)
			{
				Debug.LogError("InitializeEggbot: No start tile found");
				return;
			}

			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

			eggbotRoot = new GameObject("Eggbot").transform;
			eggbotRoot.SetParent(mapManager.transform, false);

			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
			if (def == null || def.szGeom == null)
			{
				Debug.LogError($"InitializeEggbot: No Eggbot definition found for costume: {eggbotCostume}");
				return;
			}

			var prefab = GeometryManager.Get(def.szGeom);
			if (prefab == null)
			{
				Debug.LogError($"InitializeEggbot: Failed to load prefab for geometry: {def.szGeom}");
				return;
			}
			var mesh = Instantiate(prefab, eggbotRoot);
			mesh.name = "Mesh";
			var transform = mesh.transform;
			transform.SetParent(eggbotRoot, false);
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			eggbotMesh = transform;

			DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
			{
				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
				if (textureFrames?.Length > 0)
				{
					var animator = mesh.AddComponent<TextureSetAnimator>();
					animator.Initialize(textureFrames);
				}
				else
				{
					Debug.LogWarning($"InitializeEggbot: No texture set for {eggbotRoot}");
				}
			}

			eggbotRoot.position = mapManager.GetTilePosition(startTile);
			Debug.Log($"InitializeEggbot: Eggbot placed at start tile: {startTile}, position: {eggbotRoot.position}");

			OrientEggbot(startTile);
			SetState(State.Pausing);
		}

		private void OrientEggbot(int startTile)
		{
			if (mapManager.Waypoints == null || mapManager.Waypoints.Count < 2)
			{
				Debug.LogWarning("OrientEggbot: Not enough waypoints to orient Eggbot");
				eggbotRoot.rotation = Quaternion.identity;
				return;
			}

			int fromWaypointIndex = 0;
			int toWaypointIndex = 1;
			int destinationTile = mapManager.Waypoints[toWaypointIndex].nTile;

			List<int> path;
			if (!Navigation.CheckPathBetweenWaypoints(mapManager, fromWaypointIndex, toWaypointIndex, out path) || path == null || path.Count < 2)
			{
				Debug.LogWarning($"OrientEggbot: No valid path from waypoint {fromWaypointIndex} (tile {startTile}) to waypoint {toWaypointIndex} (tile {destinationTile})");
				eggbotRoot.rotation = Quaternion.identity;
				return;
			}

			int nextTile = path[1];
			Debug.Log($"OrientEggbot: Path to waypoint {toWaypointIndex}: {string.Join(", ", path)}, nextTile: {nextTile}");

			int tileOffset = nextTile - startTile;
			int direction = Navigation.GetTileOffsetToDirection(mapManager, tileOffset);
			float yaw = DirToAngle(direction);

			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
			Debug.Log($"OrientEggbot: Eggbot oriented to face direction {direction} (yaw={yaw}) toward tile {nextTile}");
		}

		public void UpdateEggbot()
		{
			if (mapManager.Waypoints?.Count == 0)
				return;

			mod1 += 7.8f * Time.deltaTime;
			mod2 += 1.8f * Time.deltaTime;

			var targetWobble = currentState == State.Pausing ? 0.02f : 0.1f;
			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

			float pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);

			switch (currentState)
			{
				case State.Spinning:
					spinTimer += Time.deltaTime;
					var tSpin = Mathf.Clamp01(spinTimer / spinDuration);
					var cosTSpin = (1f - Mathf.Cos(tSpin * Mathf.PI)) / 2f;
					var angle = cosTSpin * spinAngle;
					var displayAngle = angle % 360f;
					eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

					eggbotMesh.localPosition = Vector3.zero;
					eggbotMesh.localRotation = Quaternion.identity;

					if (tSpin >= 1f)
					{
						eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
						if (hasReachedEnd && isReturningToStart && currentWaypointIndex == 0)
						{
							isReturningToStart = false;
							hasReachedEnd = false;
							isLevelComplete = false;
							SetState(State.Pausing);
						}
						else if (hasReachedEnd && !isReturningToStart)
						{
							isReturningToStart = true;
							SetState(State.Pausing);
						}
						else
						{
							SetState(State.Pausing);
						}
					}
					break;

				case State.Turning:
					turnTimer += Time.deltaTime;
					var tTurn = Mathf.Clamp01(turnTimer / turnDuration);
					var cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
					var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosTTurn);
					eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

					if (tTurn >= 1f)
					{
						eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
						if (moveDuration > 0)
						{
							StartSegmentMovement();
						}
						else if (currentState == State.CheckingConsole)
						{
							consoleCheckTimer = consoleCheckDuration;
							SetState(State.CheckingConsole);
						}
						else
						{
							SetState(State.Pausing);
						}
					}
					break;

				case State.CheckingConsole:
					consoleCheckTimer -= Time.deltaTime;
					if (consoleCheckTimer <= 0)
					{
						var waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
						var pathClear = Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath);

						if (pathClear && currentPath != null && currentPath.Count > 1)
						{
							var nextPos = mapManager.GetTilePosition(currentPath[1]);
							var currentPos = mapManager.GetTilePosition(currentPath[0]);
							var direction = (nextPos - currentPos).normalized;
							var pathYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
							StartTurning(pathYaw, false);
						}
						else
						{
							isPuzzleBlocked = !pathClear;
							SetState(State.Pausing);
						}
					}
					break;

				case State.Moving:
					moveTimer += Time.deltaTime;
					var tMove = moveDuration > 0 ? Mathf.Clamp01(moveTimer / moveDuration) : 1f;
					var cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
					eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosTMove);

					if (tMove >= 1f)
					{
						eggbotRoot.position = targetPosition;
						pathStepIndex = segmentEndIndex;

						if (pathStepIndex >= currentPath.Count - 1)
						{
							currentWaypointIndex = isReturningToStart ? 0 : currentWaypointIndex + 1;
							currentPath = null;
							segmentStartIndex = segmentEndIndex = 0;

							if (currentWaypointIndex < mapManager.Waypoints.Count)
							{
								int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
								eggbotRoot.position = mapManager.GetTilePosition(waypointTile);
								var tileProps = mapManager.GetTileProperties(waypointTile);
								cameraController?.OnWaypointReached(currentWaypointIndex);

								if (isReturningToStart && currentWaypointIndex == 0 && hasReachedEnd)
								{
									StartSpinning();
								}
								else if (currentWaypointIndex >= mapManager.Waypoints.Count - 1 && tileProps?.IsEnd == true && !isReturningToStart)
								{
									isLevelComplete = true;
									hasReachedEnd = true;
									StartSpinning();
								}
								else
								{
									CheckAndFaceAdjacentConsole(waypointTile);
								}
							}
							else
							{
								SetState(State.Pausing);
							}
						}
						else
						{
							PrepareNextSegment();
						}
					}
					break;

				case State.Pausing:
					pauseTimer -= Time.deltaTime;
					if (pauseTimer <= 0)
					{
						MoveToNextWaypoint();
					}
					break;
			}

			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
			var localOffset = new Vector3(0f, 0f, -pitch);
			var wobblePos = pitchRotation * localOffset;
			eggbotMesh.localPosition = wobblePos;
			eggbotMesh.localRotation = pitchRotation;

			//Debug.Log($"UpdateEggbot: State={currentState}, Position={eggbotRoot.position}, Yaw={eggbotRoot.eulerAngles.y}, Waypoint={currentWaypointIndex}, PathStep={pathStepIndex}");
		}

		private void SetState(State state)
		{
			//Debug.Log($"SetState: {state}");
			currentState = state;

			switch (state)
			{
				case State.Pausing:
					pauseTimer = pauseDuration;
					moveTimer = turnTimer = consoleCheckTimer = spinTimer = 0f;
					moveDuration = 0f;
					break;
				case State.Moving:
					moveTimer = 0f;
					break;
				case State.Turning:
					turnTimer = 0f;
					break;
				case State.CheckingConsole:
					consoleCheckTimer = consoleCheckDuration;
					break;
				case State.Spinning:
					spinTimer = 0f;
					break;
			}
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count && !isReturningToStart)
			{
				SetState(State.Pausing);
				return;
			}

			if (isReturningToStart)
			{
				if (currentWaypointIndex == 0)
				{
					if (hasReachedEnd)
					{
						StartSpinning();
					}
					else
					{
						isReturningToStart = false;
						hasReachedEnd = false;
						isLevelComplete = false;
						SetState(State.Pausing);
					}
					return;
				}
				var targetWaypoint = 0;
				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, targetWaypoint, out currentPath))
				{
					isPuzzleBlocked = false;
					pathStepIndex = 0;
					segmentStartIndex = segmentEndIndex = 0;
					PrepareNextSegment();
				}
				else
				{
					SetState(State.Pausing);
				}
			}
			else
			{
				var currentTile = mapManager.Waypoints[currentWaypointIndex].nTile;
				if (Navigation.FindAdjacentConsole(mapManager, currentTile) != -1 && !Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out _))
				{
					CheckAndFaceAdjacentConsole(currentTile);
					return;
				}

				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath))
				{
					isPuzzleBlocked = false;
					cameraController?.OnPuzzleSolved(currentWaypointIndex);
					pathStepIndex = 0;
					segmentStartIndex = segmentEndIndex = 0;
					PrepareNextSegment();
				}
				else
				{
					SetState(State.Pausing);
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
						return;
					}
				}
			}
			SetState(State.CheckingConsole);
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
			moveDuration = (distance + 1.0f) / walkSpeed;

			if (moveDuration <= 0f && segmentEndIndex > segmentStartIndex)
			{
				moveDuration = ((segmentEndIndex - segmentStartIndex) + 1.0f) / walkSpeed;
			}

			Debug.Log($"PrepareNextSegment: Segment from tile {currentPath[segmentStartIndex]} to {currentPath[segmentEndIndex]}, distance={distance}, moveDuration={moveDuration}");

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
			SetState(State.Moving);
		}

		private void StartTurning(float newTargetYaw, bool continueMoving)
		{
			startYaw = eggbotRoot.eulerAngles.y;
			targetYaw = newTargetYaw;
			moveDuration = continueMoving ? moveDuration : 0f;
			SetState(State.Turning);
		}

		private void StartSpinning()
		{
			startYaw = eggbotRoot.eulerAngles.y;
			SetState(State.Spinning);
		}

		private static float DirToAngle(int dir)
		{
			if ((dir & 1) != 0) return 0f;   // North
			if ((dir & 2) != 0) return 180f; // South
			if ((dir & 4) != 0) return 90f;  // East
			if ((dir & 8) != 0) return -90f; // West
			return 0f;
		}
	}
}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//namespace ClassicTilestorm
//{
//	public class EggbotController : MonoBehaviour
//	{
//		private MapManager mapManager => GamePreview.mapManager;
//		private CameraController cameraController => GamePreview.cameraController;

//		[HideInInspector]
//		public Transform eggbotRoot;
//		private Transform eggbotMesh;

//		private List<int> currentPath;
//		private int pathStepIndex;
//		private int currentWaypointIndex;
//		private bool isReturningToStart;
//		private bool hasReachedEnd;

//		private float pauseTimer;
//		private float pauseDuration = 1f;

//		public static bool isPuzzleBlocked;
//		private bool isLevelComplete;
//		public bool IsLevelComplete => isLevelComplete;

//		private float consoleCheckTimer;
//		private float consoleCheckDuration = 0.5f;

//		private float moveTimer;
//		private float moveDuration;
//		private float walkSpeed = 6f;
//		private Vector3 startPosition;
//		private Vector3 targetPosition;
//		private int segmentStartIndex;
//		private int segmentEndIndex;

//		private float turnTimer;
//		private float startYaw;
//		private float targetYaw;
//		private float turnDuration = 1f / 6f;

//		private float spinTimer;
//		private float spinDuration = 1f;
//		private float spinAngle = 1260f;

//		private float wobble = 0.1f;
//		private static float mod1 = 0.0f;
//		private static float mod2 = 0.0f;

//		private enum State
//		{
//			Pausing,
//			Moving,
//			Turning,
//			CheckingConsole,
//			Spinning
//		}
//		private State currentState = State.Pausing;

//		public void Initialize()
//		{
//			Reset();
//			InitializeEggbot();
//		}

//		public void Reset()
//		{
//			currentPath = null;
//			pathStepIndex = currentWaypointIndex = 0;
//			segmentStartIndex = segmentEndIndex = 0;

//			isReturningToStart = hasReachedEnd = false;
//			isLevelComplete = isPuzzleBlocked = false;

//			moveTimer = turnTimer = consoleCheckTimer = spinTimer = 0f;
//			pauseTimer = pauseDuration;
//			wobble = 0.1f;
//			mod1 = mod2 = 0.0f;

//			SetState(State.Pausing);
//		}

//		private void InitializeEggbot()
//		{
//			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
//			int startTile = Navigation.GetStartTile(mapManager);
//			if (startTile == -1)
//			{
//				Debug.LogError("InitializeEggbot: No start tile found");
//				return;
//			}

//			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

//			eggbotRoot = new GameObject("Eggbot").transform;
//			eggbotRoot.position = mapManager.GetTilePosition(startTile);
//			eggbotRoot.rotation = Quaternion.identity;
//			eggbotRoot.SetParent(mapManager.transform, false);

//			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
//			if (def == null || def.szGeom == null)
//			{
//				Debug.LogError($"InitializeEggbot: No Eggbot definition found for costume: {eggbotCostume}");
//				return;
//			}

//			var prefab = GeometryManager.Get(def.szGeom);
//			var mesh = Instantiate(prefab, eggbotRoot);
//			mesh.name = "Mesh";
//			var transform = mesh.transform;
//			transform.SetParent(eggbotRoot, false);
//			transform.localPosition = Vector3.zero;
//			transform.localRotation = Quaternion.identity;
//			eggbotMesh = transform;

//			DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
//			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
//			{
//				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
//				if (textureFrames?.Length > 0)
//				{
//					var animator = mesh.AddComponent<TextureSetAnimator>();
//					animator.Initialize(textureFrames);
//				}
//				else
//				{
//					Debug.LogWarning($"InitializeEggbot: No texture set for {eggbotRoot}");
//				}
//			}

//			Debug.Log($"InitializeEggbot: Eggbot placed at tile {startTile}, position: {eggbotRoot.position}");
//		}

//		public void UpdateEggbot()
//		{
//			if (mapManager.Waypoints?.Count == 0)
//				return;

//			// Update wobble accumulators
//			mod1 += 7.8f * Time.deltaTime;
//			mod2 += 1.8f * Time.deltaTime;

//			// Update wobble factor
//			var targetWobble = currentState == State.Pausing ? 0.02f : 0.1f;
//			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

//			// Calculate pitch for wobble
//			float pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);

//			switch (currentState)
//			{
//				case State.Spinning:
//					spinTimer += Time.deltaTime;
//					var tSpin = Mathf.Clamp01(spinTimer / spinDuration);
//					var cosTSpin = (1f - Mathf.Cos(tSpin * Mathf.PI)) / 2f;
//					var angle = cosTSpin * spinAngle;
//					var displayAngle = angle % 360f;
//					eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

//					// Reset mesh transform during spin
//					eggbotMesh.localPosition = Vector3.zero;
//					eggbotMesh.localRotation = Quaternion.identity;

//					if (tSpin >= 1f)
//					{
//						eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
//						if (hasReachedEnd && isReturningToStart && currentWaypointIndex == 0)
//						{
//							// Reset after celebratory spin at start
//							isReturningToStart = false;
//							hasReachedEnd = false;
//							isLevelComplete = false;
//							SetState(State.Pausing);
//						}
//						else if (hasReachedEnd && !isReturningToStart)
//						{
//							// Begin returning to start after spin at end
//							isReturningToStart = true;
//							SetState(State.Pausing);
//						}
//						else
//						{
//							SetState(State.Pausing);
//						}
//					}
//					break;

//				case State.Turning:
//					turnTimer += Time.deltaTime;
//					var tTurn = Mathf.Clamp01(turnTimer / turnDuration);
//					var cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
//					var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosTTurn);
//					eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

//					if (tTurn >= 1f)
//					{
//						eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
//						if (moveDuration > 0)
//						{
//							StartSegmentMovement();
//						}
//						else if (currentState == State.CheckingConsole)
//						{
//							consoleCheckTimer = consoleCheckDuration;
//							SetState(State.CheckingConsole);
//						}
//						else
//						{
//							SetState(State.Pausing);
//						}
//					}
//					break;

//				case State.CheckingConsole:
//					consoleCheckTimer -= Time.deltaTime;
//					if (consoleCheckTimer <= 0)
//					{
//						var waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
//						var pathClear = Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath);

//						if (pathClear && currentPath != null && currentPath.Count > 1)
//						{
//							var nextPos = mapManager.GetTilePosition(currentPath[1]);
//							var currentPos = mapManager.GetTilePosition(currentPath[0]);
//							var direction = (nextPos - currentPos).normalized;
//							var pathYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
//							StartTurning(pathYaw, false);
//						}
//						else
//						{
//							isPuzzleBlocked = !pathClear;
//							SetState(State.Pausing);
//						}
//					}
//					break;

//				case State.Moving:
//					moveTimer += Time.deltaTime;
//					var tMove = moveDuration > 0 ? Mathf.Clamp01(moveTimer / moveDuration) : 1f;
//					var cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
//					eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosTMove);

//					if (tMove >= 1f)
//					{
//						eggbotRoot.position = targetPosition;
//						pathStepIndex = segmentEndIndex;

//						if (pathStepIndex >= currentPath.Count - 1)
//						{
//							currentWaypointIndex = isReturningToStart ? 0 : currentWaypointIndex + 1;
//							currentPath = null;
//							segmentStartIndex = segmentEndIndex = 0;

//							if (currentWaypointIndex < mapManager.Waypoints.Count)
//							{
//								int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
//								eggbotRoot.position = mapManager.GetTilePosition(waypointTile);
//								var tileProps = mapManager.GetTileProperties(waypointTile);
//								cameraController?.OnWaypointReached(currentWaypointIndex);

//								if (isReturningToStart && currentWaypointIndex == 0 && hasReachedEnd)
//								{
//									StartSpinning();
//								}
//								else if (currentWaypointIndex >= mapManager.Waypoints.Count - 1 && tileProps?.IsEnd == true && !isReturningToStart)
//								{
//									isLevelComplete = true;
//									hasReachedEnd = true;
//									StartSpinning();
//								}
//								else
//								{
//									CheckAndFaceAdjacentConsole(waypointTile);
//								}
//							}
//							else
//							{
//								SetState(State.Pausing);
//							}
//						}
//						else
//						{
//							PrepareNextSegment();
//						}
//					}
//					break;

//				case State.Pausing:
//					pauseTimer -= Time.deltaTime;
//					if (pauseTimer <= 0)
//					{
//						MoveToNextWaypoint();
//					}
//					break;
//			}

//			// Apply wobble to mesh
//			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
//			var localOffset = new Vector3(0f, 0f, -pitch);
//			var wobblePos = pitchRotation * localOffset;
//			eggbotMesh.localPosition = wobblePos;
//			eggbotMesh.localRotation = pitchRotation;

//			//Debug.Log($"UpdateEggbot: State={currentState}, Position={eggbotRoot.position}, Yaw={eggbotRoot.eulerAngles.y}, Waypoint={currentWaypointIndex}, PathStep={pathStepIndex}");
//		}

//		private void SetState(State state)
//		{
//			//Debug.Log($"SetState: {state}");
//			currentState = state;

//			switch (state)
//			{
//				case State.Pausing:
//					pauseTimer = pauseDuration;
//					moveTimer = turnTimer = consoleCheckTimer = spinTimer = 0f;
//					moveDuration = 0f;
//					break;
//				case State.Moving:
//					moveTimer = 0f;
//					break;
//				case State.Turning:
//					turnTimer = 0f;
//					break;
//				case State.CheckingConsole:
//					consoleCheckTimer = consoleCheckDuration;
//					break;
//				case State.Spinning:
//					spinTimer = 0f;
//					break;
//			}
//		}

//		private void MoveToNextWaypoint()
//		{
//			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count && !isReturningToStart)
//			{
//				SetState(State.Pausing);
//				return;
//			}

//			if (isReturningToStart)
//			{
//				if (currentWaypointIndex == 0)
//				{
//					if (hasReachedEnd)
//					{
//						StartSpinning();
//					}
//					else
//					{
//						isReturningToStart = false;
//						hasReachedEnd = false;
//						isLevelComplete = false;
//						SetState(State.Pausing);
//					}
//					return;
//				}
//				var targetWaypoint = 0;
//				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, targetWaypoint, out currentPath))
//				{
//					isPuzzleBlocked = false;
//					pathStepIndex = 0;
//					segmentStartIndex = segmentEndIndex = 0;
//					PrepareNextSegment();
//				}
//				else
//				{
//					SetState(State.Pausing);
//				}
//			}
//			else
//			{
//				var currentTile = mapManager.Waypoints[currentWaypointIndex].nTile;
//				if (Navigation.FindAdjacentConsole(mapManager, currentTile) != -1 && !Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out _))
//				{
//					CheckAndFaceAdjacentConsole(currentTile);
//					return;
//				}

//				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath))
//				{
//					isPuzzleBlocked = false;
//					cameraController?.OnPuzzleSolved(currentWaypointIndex);
//					pathStepIndex = 0;
//					segmentStartIndex = segmentEndIndex = 0;
//					PrepareNextSegment();
//				}
//				else
//				{
//					SetState(State.Pausing);
//				}
//			}
//		}

//		private void CheckAndFaceAdjacentConsole(int tile)
//		{
//			var consoleTile = Navigation.FindAdjacentConsole(mapManager, tile);
//			if (consoleTile != -1)
//			{
//				var consoleProps = mapManager.GetTileProperties(consoleTile);
//				if (consoleProps?.Nav != 0)
//				{
//					var oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
//					var consoleYaw = DirToAngle(oppositeDir);
//					var currentYaw = eggbotRoot.eulerAngles.y;
//					if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw)) > 0.1f)
//					{
//						StartTurning(consoleYaw, false);
//						return;
//					}
//				}
//			}
//			SetState(State.CheckingConsole);
//		}

//		private void PrepareNextSegment()
//		{
//			if (pathStepIndex >= currentPath.Count - 1)
//			{
//				segmentStartIndex = segmentEndIndex = pathStepIndex;
//				startPosition = mapManager.GetTilePosition(currentPath[pathStepIndex]);
//				targetPosition = startPosition;
//				moveDuration = 0f;
//				StartSegmentMovement();
//				return;
//			}

//			var currentDir = 0;
//			segmentEndIndex = pathStepIndex;
//			while (segmentEndIndex < currentPath.Count - 1)
//			{
//				var direction = Navigation.GetTileOffsetToDirection(mapManager, currentPath[segmentEndIndex + 1] - currentPath[segmentEndIndex]);
//				if (currentDir == 0)
//					currentDir = direction;
//				else if (direction != currentDir)
//					break;
//				segmentEndIndex++;
//			}

//			segmentStartIndex = pathStepIndex;
//			startPosition = mapManager.GetTilePosition(currentPath[segmentStartIndex]);
//			targetPosition = mapManager.GetTilePosition(currentPath[segmentEndIndex]);
//			var distance = Vector3.Distance(startPosition, targetPosition);
//			moveDuration = (distance + 1.0f) / walkSpeed;

//			if (moveDuration <= 0f && segmentEndIndex > segmentStartIndex)
//			{
//				moveDuration = ((segmentEndIndex - segmentStartIndex) + 1.0f) / walkSpeed;
//			}

//			Debug.Log($"PrepareNextSegment: Segment from tile {currentPath[segmentStartIndex]} to {currentPath[segmentEndIndex]}, distance={distance}, moveDuration={moveDuration}");

//			if (segmentEndIndex > segmentStartIndex)
//			{
//				Vector3 direction = (targetPosition - startPosition).normalized;
//				targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
//				StartTurning(targetYaw, true);
//			}
//			else
//			{
//				StartSegmentMovement();
//			}
//		}

//		private void StartSegmentMovement()
//		{
//			moveTimer = 0f;
//			SetState(State.Moving);
//		}

//		private void StartTurning(float newTargetYaw, bool continueMoving)
//		{
//			startYaw = eggbotRoot.eulerAngles.y;
//			targetYaw = newTargetYaw;
//			moveDuration = continueMoving ? moveDuration : 0f;
//			SetState(State.Turning);
//		}

//		private void StartSpinning()
//		{
//			startYaw = eggbotRoot.eulerAngles.y;
//			SetState(State.Spinning);
//		}

//		private static float DirToAngle(int dir)
//		{
//			if ((dir & 1) != 0) return 0f;   // North
//			if ((dir & 2) != 0) return 180f; // South
//			if ((dir & 4) != 0) return 90f;  // East
//			if ((dir & 8) != 0) return -90f; // West
//			return 0f;
//		}
//	}
//}



//using UnityEngine;
//using System.Linq;
//using System.Collections.Generic;

//namespace ClassicTilestorm
//{
//	public class EggbotController : MonoBehaviour
//	{
//		private MapManager mapManager => GamePreview.mapManager;

//		[HideInInspector]
//		public Transform eggbotRoot;
//		private Transform eggbotMesh;

//		private List<int> currentPath; // Path of tile indices
//		private int pathStepIndex; // Current index in path
//		private int segmentStartIndex; // Start of current segment
//		private int segmentEndIndex; // End of current segment
//		private int currentWaypointIndex = 0; // Current waypoint index
//		private int destinationWaypointIndex = -1; // Destination waypoint
//		private Vector3 startPosition; // Start position for movement
//		private Vector3 destinationPosition; // Current segment destination
//		private int currentTile; // Current tile Eggbot is on
//		private float stateTimer; // Timer for current state
//		private float moveTimer; // Tracks movement progress
//		private float moveDuration; // Total time for movement
//		private float walkSpeed = 6f; // gfWalkRate=6.0f
//		private float turnTimer; // Tracks turning progress
//		private float turnDuration; // Total time for turning
//		private float turnSpeed = 2160f; // gfTurnRate=6 rotations/s = 2160 deg/s
//		private float startYaw; // Start yaw for turning
//		private float targetYaw; // Target yaw for turning

//		private enum State
//		{
//			Idle,
//			Busy
//		}
//		private State currentState = State.Idle;

//		public void Initialize()
//		{
//			InitializeEggbot();
//		}

//		public void Reset()
//		{
//			// Clear state variables
//			currentPath = null;
//			pathStepIndex = segmentStartIndex = segmentEndIndex = 0;
//			currentWaypointIndex = 0;
//			destinationWaypointIndex = -1;
//			destinationPosition = startPosition = Vector3.zero;
//			stateTimer = moveTimer = moveDuration = 0f;
//			turnTimer = turnDuration = startYaw = targetYaw = 0f;
//			currentState = State.Idle;

//			// Reload Eggbot
//			InitializeEggbot();

//			// Set destination to waypoint 1 and start movement
//			try
//			{
//				SetDestination(1);
//				Evaluate();
//				Debug.Log("Reset: Eggbot reloaded, repositioned, reoriented, and movement to waypoint 1 initiated");
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"Reset: Failed to complete - {e.Message}");
//			}
//		}

//		private void InitializeEggbot()
//		{
//			if (mapManager == null)
//			{
//				Debug.LogError("InitializeEggbot: mapManager is null");
//				return;
//			}

//			int startTile = Navigation.GetStartTile(mapManager);
//			if (startTile == -1)
//			{
//				Debug.LogError("InitializeEggbot: No start tile found");
//				return;
//			}

//			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

//			eggbotRoot = new GameObject("Eggbot").transform;
//			eggbotRoot.SetParent(mapManager.transform, false);

//			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
//			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
//			if (def == null || def.szGeom == null)
//			{
//				Debug.LogError($"InitializeEggbot: No Eggbot definition found for costume: {eggbotCostume}");
//				return;
//			}

//			var prefab = GeometryManager.Get(def.szGeom);
//			if (prefab == null)
//			{
//				Debug.LogError($"InitializeEggbot: Failed to load prefab for geometry: {def.szGeom}");
//				return;
//			}
//			var mesh = Instantiate(prefab, eggbotRoot);
//			mesh.name = "Mesh";
//			var transform = mesh.transform;
//			transform.SetParent(eggbotRoot, false);
//			transform.localPosition = Vector3.zero;
//			transform.localRotation = Quaternion.identity;
//			eggbotMesh = transform;

//			DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
//			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
//			{
//				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
//				if (textureFrames?.Length > 0)
//				{
//					var animator = mesh.AddComponent<TextureSetAnimator>();
//					animator.Initialize(textureFrames);
//				}
//				else
//				{
//					Debug.LogWarning($"InitializeEggbot: No texture set for {eggbotRoot}");
//				}
//			}

//			eggbotRoot.position = mapManager.GetTilePosition(startTile);
//			currentTile = startTile;
//			Debug.Log($"InitializeEggbot: Eggbot placed at start tile: {startTile}, position: {eggbotRoot.position}, mapIndex: {mapManager.WorldToMapIndex(eggbotRoot.position)}");

//			OrientEggbot(startTile);

//			try
//			{
//				SetDestination(1);
//				Evaluate();
//				Debug.Log("InitializeEggbot: Initialization completed, movement to waypoint 1 initiated");
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"InitializeEggbot: Failed to set destination or evaluate - {e.Message}");
//			}
//		}

//		private void OrientEggbot(int startTile)
//		{
//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count < 2)
//			{
//				Debug.LogWarning("OrientEggbot: Not enough waypoints to orient Eggbot");
//				eggbotRoot.rotation = Quaternion.identity;
//				return;
//			}

//			int fromWaypointIndex = 0;
//			int toWaypointIndex = 1;
//			int destinationTile = mapManager.Waypoints[toWaypointIndex].nTile;

//			List<int> path;
//			if (!Navigation.CheckPathBetweenWaypoints(mapManager, fromWaypointIndex, toWaypointIndex, out path) || path == null || path.Count < 2)
//			{
//				Debug.LogWarning($"OrientEggbot: No valid path from waypoint {fromWaypointIndex} (tile {startTile}) to waypoint {toWaypointIndex} (tile {destinationTile})");
//				eggbotRoot.rotation = Quaternion.identity;
//				return;
//			}

//			int nextTile = path[1];
//			Debug.Log($"OrientEggbot: Path to waypoint {toWaypointIndex}: {string.Join(", ", path)}, nextTile: {nextTile}");

//			int tileOffset = nextTile - startTile;
//			int direction = Navigation.GetTileOffsetToDirection(mapManager, tileOffset);
//			float yaw = DirToAngle(direction);

//			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
//			Debug.Log($"OrientEggbot: Eggbot oriented to face direction {direction} (yaw={yaw}) toward tile {nextTile}");

//			SetState(State.Idle, 0f);
//		}

//		private void SetDestination(int waypointIndex)
//		{
//			if (mapManager.Waypoints == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
//			{
//				Debug.LogWarning($"SetDestination: Invalid waypoint index: {waypointIndex}");
//				return;
//			}

//			destinationWaypointIndex = waypointIndex;
//			int destinationTile = mapManager.Waypoints[waypointIndex].nTile;

//			if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, waypointIndex, out currentPath))
//			{
//				pathStepIndex = 0;
//				segmentStartIndex = segmentEndIndex = 0;
//				Debug.Log($"SetDestination: Path to waypoint {waypointIndex}, tile {destinationTile}: {string.Join(", ", currentPath)}");
//				PrepareNextSegment();
//				Evaluate(); // Ensure movement starts
//			}
//			else
//			{
//				Debug.LogWarning($"SetDestination: No path from waypoint {currentWaypointIndex} (tile {mapManager.Waypoints[currentWaypointIndex].nTile}) to waypoint {waypointIndex} (tile {destinationTile})");
//				SetState(State.Idle, 0f);
//			}
//		}

//		private void PrepareNextSegment()
//		{
//			if (currentPath == null || pathStepIndex >= currentPath.Count - 1)
//			{
//				// Reached waypoint
//				segmentStartIndex = segmentEndIndex = pathStepIndex;
//				if (currentPath != null)
//				{
//					startPosition = mapManager.GetTilePosition(currentPath[pathStepIndex]);
//					destinationPosition = startPosition;
//					currentTile = currentPath[pathStepIndex];
//					currentWaypointIndex = destinationWaypointIndex;
//				}
//				moveDuration = 0f;
//				destinationWaypointIndex = -1;
//				currentPath = null;
//				SetState(State.Idle, 0f);
//				Debug.Log($"PrepareNextSegment: Reached waypoint {currentWaypointIndex}, tile {currentTile}, clearing path");
//				return;
//			}

//			int currentDir = 0;
//			segmentEndIndex = pathStepIndex;
//			while (segmentEndIndex < currentPath.Count - 1)
//			{
//				int direction = Navigation.GetTileOffsetToDirection(mapManager, currentPath[segmentEndIndex + 1] - currentPath[segmentEndIndex]);
//				if (currentDir == 0)
//					currentDir = direction;
//				else if (direction != currentDir)
//					break;
//				segmentEndIndex++;
//			}

//			segmentStartIndex = pathStepIndex;
//			startPosition = mapManager.GetTilePosition(currentPath[segmentStartIndex]);
//			destinationPosition = mapManager.GetTilePosition(currentPath[segmentEndIndex]);
//			float distance = Vector3.Distance(startPosition, destinationPosition);
//			moveDuration = (distance + 1.0f) / walkSpeed;

//			if (moveDuration <= 0f && segmentEndIndex > segmentStartIndex)
//			{
//				moveDuration = ((segmentEndIndex - segmentStartIndex) + 1.0f) / walkSpeed;
//			}

//			Debug.Log($"PrepareNextSegment: Segment from tile {currentPath[segmentStartIndex]} to {currentPath[segmentEndIndex]}, distance={distance}, moveDuration={moveDuration}");

//			if (segmentEndIndex > segmentStartIndex)
//			{
//				Vector3 direction = (destinationPosition - startPosition).normalized;
//				targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
//				if (!Turn(targetYaw, turnSpeed))
//				{
//					StartSegmentMovement(); // Start moving if no turn needed
//				}
//			}
//			else
//			{
//				StartSegmentMovement();
//			}
//		}

//		private void StartSegmentMovement()
//		{
//			moveTimer = 0f;
//			SetState(State.Busy, moveDuration);
//			Debug.Log($"StartSegmentMovement: Moving from tile {currentPath[segmentStartIndex]} to {currentPath[segmentEndIndex]}");
//		}

//		private bool Move()
//		{
//			if (destinationWaypointIndex == -1 && currentPath == null)
//			{
//				Debug.Log("Move: No destination or path set");
//				return false;
//			}

//			if (currentPath != null && pathStepIndex < currentPath.Count)
//			{
//				Debug.Log($"Move: Continuing path, pathStepIndex={pathStepIndex}, currentTile={currentTile}");
//				return true;
//			}

//			Debug.Log("Move: Path completed or no path");
//			return false;
//		}

//		private bool Face()
//		{
//			if (destinationWaypointIndex != -1 || currentState != State.Idle)
//			{
//				Debug.Log("Face: Not at destination or not idle");
//				return false;
//			}

//			int consoleTile = Navigation.FindAdjacentConsole(mapManager, currentTile);
//			if (consoleTile == -1)
//			{
//				Debug.Log("Face: No adjacent console found");
//				return false;
//			}

//			var consoleProps = mapManager.GetTileProperties(consoleTile);
//			if (consoleProps == null || consoleProps.Nav == 0)
//			{
//				Debug.Log($"Face: Console at tile {consoleTile} has invalid properties or Nav=0");
//				return false;
//			}

//			int oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
//			float consoleYaw = DirToAngle(oppositeDir);
//			Debug.Log($"Face: Console at tile {consoleTile}, Nav={consoleProps.Nav}, oppositeDir={oppositeDir}, consoleYaw={consoleYaw}");

//			float currentYaw = eggbotRoot.eulerAngles.y;
//			float angleDiff = DiffAngle(consoleYaw, currentYaw);
//			if (Mathf.Abs(angleDiff) > 0.1f)
//			{
//				if (Turn(consoleYaw, turnSpeed))
//				{
//					Debug.Log("Face: Initiating turn to face console");
//					return true;
//				}
//			}

//			Debug.Log("Face: Already facing console or turn completed, staying in Idle");
//			SetState(State.Idle, 0f);
//			return true;
//		}

//		private bool Turn(float fDstY, float fSpeed)
//		{
//			float currentYaw = eggbotRoot.eulerAngles.y;
//			float angleDiff = DiffAngle(fDstY, currentYaw);

//			if (Mathf.Abs(angleDiff) > 0.1f)
//			{
//				turnDuration = Mathf.Abs(angleDiff) / fSpeed;
//				startYaw = currentYaw;
//				targetYaw = currentYaw + angleDiff;
//				turnTimer = 0f;

//				SetState(State.Busy, turnDuration);
//				Debug.Log($"Turn: Initiating turn from yaw={currentYaw} to yaw={targetYaw}, angleDiff={angleDiff}, turnDuration={turnDuration}, turnSpeed={fSpeed}");
//				return true;
//			}

//			eggbotRoot.rotation = Quaternion.Euler(0f, fDstY, 0f);
//			Debug.Log($"Turn: No turn needed, set yaw={fDstY}");
//			return false;
//		}

//		private float DiffAngle(float fDstY, float fSrcY)
//		{
//			fDstY = ((fDstY % 360f) + 360f) % 360f;
//			fSrcY = ((fSrcY % 360f) + 360f) % 360f;
//			float diff = fDstY - fSrcY;
//			if (diff > 180f) diff -= 360f;
//			if (diff < -180f) diff += 360f;
//			return diff;
//		}

//		private void Evaluate()
//		{
//			Debug.Log($"Evaluate: Current state={currentState}, destinationWaypointIndex={destinationWaypointIndex}, pathStepIndex={pathStepIndex}");
//			if (Move()) return;
//			if (Face()) return;
//			SetState(State.Idle, 0f);
//			Debug.Log("Evaluate: No actions taken, set to Idle");
//		}

//		public void UpdateEggbot()
//		{
//			currentTile = mapManager.WorldToMapIndex(eggbotRoot.position);

//			if (currentState == State.Busy)
//			{
//				if (moveDuration > 0)
//				{
//					// Handle segment movement
//					moveTimer += Time.deltaTime;
//					float tMove = Mathf.Clamp01(moveTimer / moveDuration);
//					float cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
//					eggbotRoot.position = Vector3.Lerp(startPosition, destinationPosition, cosTMove);

//					Debug.Log($"UpdateEggbot: Busy (Move), tMove={tMove}, position={eggbotRoot.position}, moveTimer={moveTimer}, moveDuration={moveDuration}, tile={currentTile}");

//					if (tMove >= 1f)
//					{
//						eggbotRoot.position = destinationPosition;
//						moveDuration = 0f;
//						pathStepIndex = segmentEndIndex;
//						currentTile = currentPath[segmentEndIndex];
//						Debug.Log($"UpdateEggbot: Reached segment end, tile {currentTile}, pathStepIndex={pathStepIndex}");
//						PrepareNextSegment();
//					}
//				}
//				else if (turnDuration > 0)
//				{
//					// Handle turning
//					turnTimer += Time.deltaTime;
//					float tTurn = Mathf.Clamp01(turnTimer / turnDuration);
//					float cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
//					float currentYaw = Mathf.Lerp(startYaw, targetYaw, cosTTurn);
//					eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

//					Debug.Log($"UpdateEggbot: Busy (Turn), tTurn={tTurn}, yaw={currentYaw}, turnTimer={turnTimer}, turnDuration={turnDuration}, startYaw={startYaw}, targetYaw={targetYaw}");

//					if (tTurn >= 1f)
//					{
//						eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
//						turnDuration = 0f;
//						stateTimer = 0f;
//						if (moveDuration > 0 || (currentPath != null && pathStepIndex < currentPath.Count - 1))
//						{
//							StartSegmentMovement();
//						}
//						else
//						{
//							Evaluate();
//						}
//					}
//				}
//			}

//			Debug.Log($"UpdateEggbot: State={currentState}, Position={eggbotRoot.position}, Tile={currentTile}, CurrentWaypoint={currentWaypointIndex}, DestinationWaypoint={destinationWaypointIndex}, PathStep={pathStepIndex}, Yaw={eggbotRoot.eulerAngles.y}");
//		}

//		private void SetState(State state, float time)
//		{
//			Debug.Log($"SetState: {state}, time={time}");
//			currentState = state;
//			stateTimer = time;

//			if (state != State.Busy)
//			{
//				moveTimer = moveDuration = 0f;
//				turnTimer = turnDuration = startYaw = targetYaw = 0f;
//			}
//		}

//		private float DirToAngle(int dir)
//		{
//			if ((dir & 1) != 0) return 0f;   // North
//			if ((dir & 2) != 0) return 180f; // South
//			if ((dir & 4) != 0) return 90f;  // East
//			if ((dir & 8) != 0) return -90f; // West
//			Debug.LogWarning($"DirToAngle: Invalid direction: {dir}, defaulting to North");
//			return 0f;
//		}
//	}
//}

//using UnityEngine;
//using System.Linq;
//using System.Collections.Generic;

//namespace ClassicTilestorm
//{
//	public class EggbotController : MonoBehaviour
//	{
//		private MapManager mapManager => GamePreview.mapManager;

//		[HideInInspector]
//		public Transform eggbotRoot;
//		private Transform eggbotMesh;

//		private int destinationWaypointIndex = -1; // Destination waypoint (e.g., 1)
//		private Vector3 destinationPosition; // Destination position
//		private int currentTile; // Current tile Eggbot is on
//		private float stateTimer; // Timer for current state
//		private float moveTimer; // Tracks movement progress
//		private float moveDuration; // Total time for movement
//		private float walkSpeed = 6f; // gfWalkRate=6.0f
//		private Vector3 startPosition; // Start position for movement
//		private float turnTimer; // Tracks turning progress
//		private float turnDuration; // Total time for turning
//		private float turnSpeed = 180f; // Degrees per second (gfTurnRate)
//		private float startYaw; // Start yaw for turning
//		private float targetYaw; // Target yaw for turning

//		private enum State
//		{
//			Idle,
//			Busy
//		}
//		private State currentState = State.Idle;

//		public void Initialize()
//		{
//			InitializeEggbot();
//		}

//		public void Reset()
//		{
//			// Clear state variables
//			destinationWaypointIndex = -1;
//			destinationPosition = Vector3.zero;
//			stateTimer = moveTimer = moveDuration = 0f;
//			turnTimer = turnDuration = startYaw = targetYaw = 0f;
//			startPosition = Vector3.zero;
//			currentState = State.Idle;

//			// Reload Eggbot (includes position and orientation)
//			InitializeEggbot();

//			// Set destination to waypoint 1 and start movement
//			try
//			{
//				SetDestination(1);
//				Evaluate();
//				Debug.Log("Reset: Eggbot reloaded, repositioned, reoriented, and movement to waypoint 1 initiated");
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"Reset: Failed to complete - {e.Message}");
//			}
//		}

//		private void InitializeEggbot()
//		{
//			// Validate mapManager
//			if (mapManager == null)
//			{
//				Debug.LogError("InitializeEggbot: mapManager is null");
//				return;
//			}

//			// Get the starting tile (first waypoint)
//			int startTile = Navigation.GetStartTile(mapManager);
//			if (startTile == -1)
//			{
//				Debug.LogError("InitializeEggbot: No start tile found");
//				return;
//			}

//			// Destroy existing Eggbot if any
//			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

//			// Create Eggbot transform
//			eggbotRoot = new GameObject("Eggbot").transform;
//			eggbotRoot.SetParent(mapManager.transform, false);

//			// Load Eggbot costume
//			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
//			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
//			if (def == null || def.szGeom == null)
//			{
//				Debug.LogError($"InitializeEggbot: No Eggbot definition found for costume: {eggbotCostume}");
//				return;
//			}

//			// Instantiate Eggbot mesh
//			var prefab = GeometryManager.Get(def.szGeom);
//			if (prefab == null)
//			{
//				Debug.LogError($"InitializeEggbot: Failed to load prefab for geometry: {def.szGeom}");
//				return;
//			}
//			var mesh = Instantiate(prefab, eggbotRoot);
//			mesh.name = "Mesh";
//			var transform = mesh.transform;
//			transform.SetParent(eggbotRoot, false);
//			transform.localPosition = Vector3.zero;
//			transform.localRotation = Quaternion.identity;
//			eggbotMesh = transform;

//			// Apply texture set
//			DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
//			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
//			{
//				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
//				if (textureFrames?.Length > 0)
//				{
//					var animator = mesh.AddComponent<TextureSetAnimator>();
//					animator.Initialize(textureFrames);
//				}
//				else
//				{
//					Debug.LogWarning($"InitializeEggbot: No texture set for {eggbotRoot}");
//				}
//			}

//			// Place Eggbot at the start tile
//			eggbotRoot.position = mapManager.GetTilePosition(startTile);
//			currentTile = startTile;
//			Debug.Log($"InitializeEggbot: Eggbot placed at start tile: {startTile}, position: {eggbotRoot.position}");

//			// Orient Eggbot toward the first tile in the path to the second waypoint
//			OrientEggbot(startTile);

//			// Set destination to waypoint 1 and start movement
//			try
//			{
//				SetDestination(1);
//				Evaluate();
//				Debug.Log("InitializeEggbot: Initialization completed, movement to waypoint 1 initiated");
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"InitializeEggbot: Failed to set destination or evaluate - {e.Message}");
//			}
//		}

//		private void OrientEggbot(int startTile)
//		{
//			// Ensure waypoints exist
//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count < 2)
//			{
//				Debug.LogWarning("OrientEggbot: Not enough waypoints to orient Eggbot");
//				eggbotRoot.rotation = Quaternion.identity;
//				return;
//			}

//			// Get the first and second waypoints
//			int fromWaypointIndex = 0;
//			int toWaypointIndex = 1;
//			int destinationTile = mapManager.Waypoints[toWaypointIndex].nTile;

//			// Find the navigable path
//			List<int> path;
//			if (!Navigation.CheckPathBetweenWaypoints(mapManager, fromWaypointIndex, toWaypointIndex, out path) || path == null || path.Count < 2)
//			{
//				Debug.LogWarning($"OrientEggbot: No valid path from waypoint {fromWaypointIndex} (tile {startTile}) to waypoint {toWaypointIndex} (tile {destinationTile})");
//				eggbotRoot.rotation = Quaternion.identity;
//				return;
//			}

//			// Get the first tile in the path (after start tile)
//			int nextTile = path[1];
//			Debug.Log($"OrientEggbot: Path to waypoint {toWaypointIndex}: {string.Join(", ", path)}, nextTile: {nextTile}");

//			// Calculate direction
//			int tileOffset = nextTile - startTile;
//			int direction = Navigation.GetTileOffsetToDirection(mapManager, tileOffset);
//			float yaw = DirToAngle(direction);

//			// Set Eggbot's rotation
//			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
//			Debug.Log($"OrientEggbot: Eggbot oriented to face direction {direction} (yaw={yaw}) toward tile {nextTile}");

//			// Set initial state to Idle
//			SetState(State.Idle, 0f);
//		}

//		private void SetDestination(int waypointIndex)
//		{
//			if (mapManager.Waypoints == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
//			{
//				Debug.LogWarning($"SetDestination: Invalid waypoint index: {waypointIndex}");
//				return;
//			}

//			destinationWaypointIndex = waypointIndex;
//			destinationPosition = mapManager.GetTilePosition(mapManager.Waypoints[waypointIndex].nTile);
//			Debug.Log($"SetDestination: Destination set to waypoint {waypointIndex}, tile {mapManager.Waypoints[waypointIndex].nTile}, position {destinationPosition}");
//		}

//		private bool Walk(Vector3 vDst, float fSpeed)
//		{
//			Vector3 currentPos = eggbotRoot.position;
//			float distance = Vector3.Distance(currentPos, vDst);

//			// Check if already at destination
//			if (distance < 0.01f)
//			{
//				eggbotRoot.position = vDst;
//				currentTile = mapManager.WorldToMapIndex(vDst);
//				Debug.Log($"Walk: Reached destination, tile {currentTile}, position {vDst}");
//				return false;
//			}

//			// Calculate travel time
//			moveDuration = (distance + 1.0f) / fSpeed;
//			startPosition = currentPos;
//			moveTimer = 0f;

//			// Update Busy state timer
//			SetState(State.Busy, moveDuration);
//			Debug.Log($"Walk: Moving to {vDst}, distance={distance}, moveDuration={moveDuration}");

//			return true;
//		}

//		private bool Move()
//		{
//			if (destinationWaypointIndex == -1)
//			{
//				Debug.Log("Move: No destination set");
//				return false;
//			}

//			int destinationTile = mapManager.Waypoints[destinationWaypointIndex].nTile;
//			if (currentTile == destinationTile)
//			{
//				Debug.Log($"Move: Reached destination waypoint {destinationWaypointIndex}, tile {destinationTile}");
//				destinationWaypointIndex = -1; // Clear destination
//				SetState(State.Idle, 0f);
//				return false;
//			}

//			Debug.Log($"Move: Initiating walk to waypoint {destinationWaypointIndex}, tile {destinationTile}");
//			return Walk(destinationPosition, walkSpeed);
//		}

//		private bool Face()
//		{
//			if (destinationWaypointIndex != -1 || currentState != State.Idle)
//			{
//				Debug.Log("Face: Not at destination or not idle");
//				return false;
//			}

//			int consoleTile = Navigation.FindAdjacentConsole(mapManager, currentTile);
//			if (consoleTile == -1)
//			{
//				Debug.Log("Face: No adjacent console found");
//				return false;
//			}

//			var consoleProps = mapManager.GetTileProperties(consoleTile);
//			if (consoleProps == null || consoleProps.Nav == 0)
//			{
//				Debug.Log($"Face: Console at tile {consoleTile} has invalid properties or Nav=0");
//				return false;
//			}

//			int oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
//			float consoleYaw = DirToAngle(oppositeDir);
//			Debug.Log($"Face: Console at tile {consoleTile}, Nav={consoleProps.Nav}, oppositeDir={oppositeDir}, consoleYaw={consoleYaw}");

//			float currentYaw = eggbotRoot.eulerAngles.y;
//			float angleDiff = DiffAngle(consoleYaw, currentYaw);
//			if (Mathf.Abs(angleDiff) > 0.1f)
//			{
//				if (Turn(consoleYaw, turnSpeed))
//				{
//					Debug.Log("Face: Initiating turn to face console");
//					return true;
//				}
//			}

//			Debug.Log("Face: Already facing console or turn completed, staying in Idle");
//			SetState(State.Idle, 0f);
//			return true; // Stay in Idle for potential console interaction
//		}

//		private bool Turn(float fDstY, float fSpeed)
//		{
//			float currentYaw = eggbotRoot.eulerAngles.y;
//			float angleDiff = DiffAngle(fDstY, currentYaw);

//			if (Mathf.Abs(angleDiff) > 0.1f)
//			{
//				turnDuration = Mathf.Abs(angleDiff) / fSpeed;
//				startYaw = currentYaw;
//				targetYaw = currentYaw + angleDiff; // Ensure correct final yaw
//				turnTimer = 0f;

//				SetState(State.Busy, turnDuration);
//				Debug.Log($"Turn: Initiating turn from yaw={currentYaw} to yaw={targetYaw}, angleDiff={angleDiff}, turnDuration={turnDuration}, turnSpeed={fSpeed}");
//				return true;
//			}

//			eggbotRoot.rotation = Quaternion.Euler(0f, fDstY, 0f);
//			Debug.Log($"Turn: No turn needed, set yaw={fDstY}");
//			return false;
//		}

//		private float DiffAngle(float fDstY, float fSrcY)
//		{
//			// Normalize angles to [0, 360)
//			fDstY = ((fDstY % 360f) + 360f) % 360f;
//			fSrcY = ((fSrcY % 360f) + 360f) % 360f;

//			// Calculate shortest angle difference
//			float diff = fDstY - fSrcY;
//			if (diff > 180f) diff -= 360f;
//			if (diff < -180f) diff += 360f;

//			return diff;
//		}

//		private void Evaluate()
//		{
//			if (Move()) return;
//			if (Face()) return;
//			SetState(State.Idle, 0f);
//			Debug.Log("Evaluate: No actions taken, set to Idle");
//		}

//		public void UpdateEggbot()
//		{
//			// Update current tile
//			currentTile = mapManager.WorldToMapIndex(eggbotRoot.position);

//			// Process Busy state
//			if (currentState == State.Busy)
//			{
//				if (moveDuration > 0)
//				{
//					// Handle movement
//					moveTimer += Time.deltaTime;
//					float tMove = Mathf.Clamp01(moveTimer / moveDuration);
//					float cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
//					eggbotRoot.position = Vector3.Lerp(startPosition, destinationPosition, cosTMove);

//					Debug.Log($"UpdateEggbot: Busy (Move), tMove={tMove}, position={eggbotRoot.position}, moveTimer={moveTimer}, moveDuration={moveDuration}");

//					if (tMove >= 1f)
//					{
//						eggbotRoot.position = destinationPosition;
//						moveDuration = 0f;
//						stateTimer = 0f;
//						Evaluate();
//					}
//				}
//				else if (turnDuration > 0)
//				{
//					// Handle turning
//					turnTimer += Time.deltaTime;
//					float tTurn = Mathf.Clamp01(turnTimer / turnDuration);
//					float currentYaw = Mathf.Lerp(startYaw, targetYaw, tTurn); // Linear interpolation
//					eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

//					Debug.Log($"UpdateEggbot: Busy (Turn), tTurn={tTurn}, yaw={currentYaw}, turnTimer={turnTimer}, turnDuration={turnDuration}, startYaw={startYaw}, targetYaw={targetYaw}");

//					if (tTurn >= 1f)
//					{
//						eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
//						turnDuration = 0f;
//						stateTimer = 0f;
//						Evaluate();
//					}
//				}
//			}

//			// Log state for debugging
//			Debug.Log($"UpdateEggbot: State={currentState}, Position={eggbotRoot.position}, Tile={currentTile}, DestinationWaypoint={destinationWaypointIndex}, Yaw={eggbotRoot.eulerAngles.y}");
//		}

//		private void SetState(State state, float time)
//		{
//			Debug.Log($"SetState: {state}, time={time}");
//			currentState = state;
//			stateTimer = time;

//			// Reset movement and turn timers if not Busy
//			if (state != State.Busy)
//			{
//				moveTimer = moveDuration = 0f;
//				turnTimer = turnDuration = startYaw = targetYaw = 0f;
//			}
//		}

//		private float DirToAngle(int dir)
//		{
//			if ((dir & 1) != 0) return 0f;   // North
//			if ((dir & 2) != 0) return 180f; // South
//			if ((dir & 4) != 0) return 90f;  // East
//			if ((dir & 8) != 0) return -90f; // West
//			Debug.LogWarning($"DirToAngle: Invalid direction: {dir}, defaulting to North");
//			return 0f;
//		}
//	}
//}


//using UnityEngine;
//using System.Linq;
//using System.Collections.Generic;

//namespace ClassicTilestorm
//{
//	public class EggbotController : MonoBehaviour
//	{
//		private MapManager mapManager => GamePreview.mapManager;

//		[HideInInspector]
//		public Transform eggbotRoot;
//		private Transform eggbotMesh;

//		private int destinationWaypointIndex = -1; // Destination waypoint (e.g., 1)
//		private Vector3 destinationPosition; // Destination position
//		private int currentTile; // Current tile Eggbot is on
//		private float stateTimer; // Timer for current state
//		private float moveTimer; // Tracks movement progress
//		private float moveDuration; // Total time for movement
//		private float walkSpeed = 6f; // gfWalkRate=6.0f
//		private Vector3 startPosition; // Start position for movement

//		private enum State
//		{
//			Idle,
//			Busy
//		}
//		private State currentState = State.Idle;

//		public void Initialize()
//		{
//			InitializeEggbot();
//		}

//		public void Reset()
//		{
//			// Clear state variables
//			destinationWaypointIndex = -1;
//			destinationPosition = Vector3.zero;
//			stateTimer = moveTimer = moveDuration = 0f;
//			startPosition = Vector3.zero;
//			currentState = State.Idle;

//			// Reload Eggbot (includes position and orientation)
//			InitializeEggbot();

//			// Set destination to waypoint 1 and start movement
//			SetDestination(1);
//			Evaluate();

//			Debug.Log("Reset: Eggbot reloaded, repositioned, reoriented, and movement to waypoint 1 initiated");
//		}

//		private void InitializeEggbot()
//		{
//			// Get the starting tile (first waypoint)
//			int startTile = Navigation.GetStartTile(mapManager);
//			if (startTile == -1)
//			{
//				Debug.LogError("No start tile found");
//				return;
//			}

//			// Destroy existing Eggbot if any
//			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

//			// Create Eggbot transform
//			eggbotRoot = new GameObject("Eggbot").transform;
//			eggbotRoot.SetParent(mapManager.transform, false);

//			// Load Eggbot costume
//			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
//			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
//			if (def == null || def.szGeom == null)
//			{
//				Debug.LogError($"No Eggbot definition found for costume: {eggbotCostume}");
//				return;
//			}

//			// Instantiate Eggbot mesh
//			var prefab = GeometryManager.Get(def.szGeom);
//			var mesh = Instantiate(prefab, eggbotRoot);
//			mesh.name = "Mesh";
//			var transform = mesh.transform;
//			transform.SetParent(eggbotRoot, false);
//			transform.localPosition = Vector3.zero;
//			transform.localRotation = Quaternion.identity;
//			eggbotMesh = transform;

//			// Apply texture set
//			DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
//			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
//			{
//				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
//				if (textureFrames?.Length > 0)
//				{
//					var animator = mesh.AddComponent<TextureSetAnimator>();
//					animator.Initialize(textureFrames);
//				}
//				else
//				{
//					Debug.LogWarning($"No texture set for {eggbotRoot}");
//				}
//			}

//			// Place Eggbot at the start tile
//			eggbotRoot.position = mapManager.GetTilePosition(startTile);
//			currentTile = startTile;
//			Debug.Log($"Eggbot placed at start tile: {startTile}, position: {eggbotRoot.position}");

//			// Orient Eggbot toward the first tile in the path to the second waypoint
//			OrientEggbot(startTile);
//		}

//		private void OrientEggbot(int startTile)
//		{
//			// Ensure waypoints exist
//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count < 2)
//			{
//				Debug.LogWarning("Not enough waypoints to orient Eggbot");
//				eggbotRoot.rotation = Quaternion.identity; // Default orientation
//				return;
//			}

//			// Get the first and second waypoints
//			int fromWaypointIndex = 0; // First waypoint
//			int toWaypointIndex = 1; // Second waypoint
//			int destinationTile = mapManager.Waypoints[toWaypointIndex].nTile;

//			// Find the navigable path from start to second waypoint
//			List<int> path;
//			if (!Navigation.CheckPathBetweenWaypoints(mapManager, fromWaypointIndex, toWaypointIndex, out path) || path == null || path.Count < 2)
//			{
//				Debug.LogWarning($"No valid path from waypoint {fromWaypointIndex} (tile {startTile}) to waypoint {toWaypointIndex} (tile {destinationTile})");
//				eggbotRoot.rotation = Quaternion.identity;
//				return;
//			}

//			// Get the first tile in the path (after start tile)
//			int nextTile = path[1]; // path[0] should be startTile, path[1] is the next tile
//			Debug.Log($"Path to waypoint {toWaypointIndex}: {string.Join(", ", path)}, nextTile: {nextTile}");

//			// Calculate direction from startTile to nextTile
//			int tileOffset = nextTile - startTile;
//			int direction = Navigation.GetTileOffsetToDirection(mapManager, tileOffset);
//			float yaw = DirToAngle(direction);

//			// Set Eggbot's rotation
//			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
//			Debug.Log($"Eggbot oriented to face direction {direction} (yaw={yaw}) toward tile {nextTile}");

//			// Set initial state to Idle
//			SetState(State.Idle, 0f);
//		}

//		private void SetDestination(int waypointIndex)
//		{
//			if (mapManager.Waypoints == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
//			{
//				Debug.LogWarning($"Invalid waypoint index: {waypointIndex}");
//				return;
//			}

//			destinationWaypointIndex = waypointIndex;
//			destinationPosition = mapManager.GetTilePosition(mapManager.Waypoints[waypointIndex].nTile);
//			Debug.Log($"Destination set to waypoint {waypointIndex}, tile {mapManager.Waypoints[waypointIndex].nTile}, position {destinationPosition}");
//		}

//		private bool Walk(Vector3 vDst, float fSpeed)
//		{
//			Vector3 currentPos = eggbotRoot.position;
//			float distance = Vector3.Distance(currentPos, vDst);

//			// Check if already at destination
//			if (distance < 0.01f)
//			{
//				eggbotRoot.position = vDst;
//				currentTile = mapManager.WorldToMapIndex(vDst);
//				Debug.Log($"Walk: Reached destination, tile {currentTile}, position {vDst}");
//				return false;
//			}

//			// Calculate travel time
//			moveDuration = (distance + 1.0f) / fSpeed;
//			startPosition = currentPos;
//			moveTimer = 0f;

//			// Update Busy state timer
//			SetState(State.Busy, moveDuration);
//			Debug.Log($"Walk: Moving to {vDst}, distance={distance}, moveDuration={moveDuration}");

//			return true;
//		}

//		private bool Move()
//		{
//			if (destinationWaypointIndex == -1)
//			{
//				Debug.Log("Move: No destination set");
//				return false;
//			}

//			int destinationTile = mapManager.Waypoints[destinationWaypointIndex].nTile;
//			if (currentTile == destinationTile)
//			{
//				Debug.Log($"Move: Reached destination waypoint {destinationWaypointIndex}, tile {destinationTile}");
//				destinationWaypointIndex = -1; // Clear destination
//				SetState(State.Idle, 0f);
//				return false;
//			}

//			Debug.Log($"Move: Initiating walk to waypoint {destinationWaypointIndex}, tile {destinationTile}");
//			return Walk(destinationPosition, walkSpeed);
//		}

//		private void Evaluate()
//		{
//			if (Move()) return;
//			SetState(State.Idle, 0f);
//		}

//		public void UpdateEggbot()
//		{
//			// Update current tile
//			currentTile = mapManager.WorldToMapIndex(eggbotRoot.position);

//			// Process Busy state
//			if (currentState == State.Busy && moveDuration > 0)
//			{
//				moveTimer += Time.deltaTime;
//				float tMove = Mathf.Clamp01(moveTimer / moveDuration);
//				float cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f; // Smooth interpolation
//				eggbotRoot.position = Vector3.Lerp(startPosition, destinationPosition, cosTMove);

//				Debug.Log($"UpdateEggbot: Busy, tMove={tMove}, position={eggbotRoot.position}, moveTimer={moveTimer}, moveDuration={moveDuration}");

//				if (tMove >= 1f)
//				{
//					eggbotRoot.position = destinationPosition;
//					stateTimer = 0f;
//					Evaluate();
//				}
//			}

//			// Log state for debugging
//			Debug.Log($"UpdateEggbot: State={currentState}, Position={eggbotRoot.position}, Tile={currentTile}, DestinationWaypoint={destinationWaypointIndex}");
//		}

//		private void SetState(State state, float time)
//		{
//			Debug.Log($"SetState: {state}, time={time}");
//			currentState = state;
//			stateTimer = time;

//			// Reset movement timers if not Busy
//			if (state != State.Busy)
//			{
//				moveTimer = 0f;
//				moveDuration = 0f;
//			}
//		}

//		private float DirToAngle(int dir)
//		{
//			// Map navigation direction to yaw (degrees)
//			if ((dir & 1) != 0) return 0f;   // North
//			if ((dir & 2) != 0) return 180f; // South
//			if ((dir & 4) != 0) return 90f;  // East
//			if ((dir & 8) != 0) return -90f; // West
//			Debug.LogWarning($"Invalid direction: {dir}, defaulting to North");
//			return 0f;
//		}
//	}
//}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//namespace ClassicTilestorm
//{
//	public class EggbotController : MonoBehaviour
//	{
//		private MapManager mapManager => GamePreview.mapManager;
//		private CameraController cameraController => GamePreview.cameraController;

//		[HideInInspector]
//		public Transform eggbotRoot;
//		private Transform eggbotMesh;

//		private List<int> currentPath;
//		private int pathStepIndex;
//		private int currentWaypointIndex;
//		private bool isReturningToStart;
//		private bool hasReachedEnd;

//		private float stateTimer;
//		private float pauseDuration = 1f;

//		public static bool isPuzzleBlocked;
//		private bool isLevelComplete;
//		public bool IsLevelComplete => isLevelComplete;

//		private float moveTimer;
//		private float moveDuration;
//		private float walkSpeed = 6f; // gfWalkRate=6.0f
//		private Vector3 startPosition;
//		private Vector3 targetPosition;
//		private int segmentStartIndex;
//		private int segmentEndIndex;

//		private float turnTimer;
//		private float startYaw;
//		private float targetYaw;
//		private float turnDuration = 1f / 6f; // gfTurnRate=6.0f

//		private float spinTimer;
//		private float spinDuration = 1f; // Time for spin
//		private float spinAngle = 1260f; // 3.5 rotations

//		private float wobble = 0.1f;
//		private static float mod1 = 0.0f;
//		private static float mod2 = 0.0f;

//		private enum State
//		{
//			Idle,
//			Busy,
//			Face,
//			Spin
//		}
//		private State currentState = State.Idle;

//		public void Initialize()
//		{
//			Reset();
//			InitializeEggbot();

//			void InitializeEggbot()
//			{
//				var eggbotCostume = string.IsNullOrEmpty(GamePreview.mapManager.EggbotCostume) ? "Eggbot Default" : GamePreview.mapManager.EggbotCostume;
//				int startTile = Navigation.GetStartTile(mapManager);
//				if (startTile == -1)
//					return;

//				if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

//				eggbotRoot = new GameObject("Eggbot").transform;
//				eggbotRoot.position = mapManager.GetTilePosition(startTile);
//				eggbotRoot.rotation = Quaternion.identity;
//				eggbotRoot.SetParent(mapManager.transform, false);

//				if (null == eggbotCostume) eggbotCostume = "Eggbot Default";
//				var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
//				if (def == null || def.szGeom == null) return;

//				var prefab = GeometryManager.Get(def.szGeom);
//				var mesh = Instantiate(prefab, eggbotRoot);
//				mesh.name = "Mesh";

//				var transform = mesh.transform;
//				transform.SetParent(eggbotRoot, false);
//				transform.localPosition = Vector3.zero;
//				transform.localRotation = Quaternion.identity;

//				eggbotMesh = transform;

//				DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
//				if (theme == null || string.IsNullOrEmpty(theme.szTileTextureSet)) return;

//				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
//				if (textureFrames?.Length > 0)
//				{
//					var animator = mesh.AddComponent<TextureSetAnimator>();
//					animator.Initialize(textureFrames);
//				}
//				else
//				{
//					Debug.LogWarning($"No texture set for {eggbotRoot}");
//				}
//			}
//		}

//		public void Reset()
//		{
//			currentPath = null;
//			pathStepIndex = currentWaypointIndex = 0;
//			segmentStartIndex = segmentEndIndex = 0;

//			isReturningToStart = hasReachedEnd = false;
//			isLevelComplete = isPuzzleBlocked = false;

//			moveTimer = turnTimer = spinTimer = stateTimer = 0f;
//			wobble = 0.1f;
//			mod1 = mod2 = 0.0f;

//			currentState = State.Idle;
//			stateTimer = pauseDuration;
//		}

//		public void UpdateEggbot()
//		{
//			if (mapManager.Waypoints?.Count == 0)
//				return;

//			// Update wobble accumulators
//			mod1 += 7.8f * Time.deltaTime;
//			mod2 += 1.8f * Time.deltaTime;

//			// Update wobble factor
//			var isIdle = currentState == State.Idle;
//			var targetWobble = isIdle ? 0.02f : 0.1f;
//			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

//			// Calculate pitch for wobble
//			float pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);

//			// Simulate state machine with cascading evaluation
//			float deltaTime = Time.deltaTime;
//			int hangPrevention = 8;
//			while (deltaTime > 0 && hangPrevention-- > 0)
//			{
//				float timeStep = Mathf.Min(stateTimer, deltaTime);
//				deltaTime -= timeStep;
//				stateTimer -= timeStep;

//				if (stateTimer > 0)
//				{
//					UpdateCurrentState(timeStep, pitch);
//					break; // Continue current state
//				}

//				// State completed, evaluate next state
//				Evaluate();
//			}

//			// Apply wobble to mesh
//			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
//			var localOffset = new Vector3(0f, 0f, -pitch);
//			var wobblePos = pitchRotation * localOffset;

//			eggbotMesh.localPosition = wobblePos;
//			eggbotMesh.localRotation = pitchRotation;
//		}

//		private void UpdateCurrentState(float deltaTime, float pitch)
//		{
//			switch (currentState)
//			{
//				case State.Spin:
//					spinTimer += deltaTime;
//					var tSpin = Mathf.Clamp01(spinTimer / spinDuration);
//					var cosTSpin = (1f - Mathf.Cos(tSpin * Mathf.PI)) / 2f;
//					var angle = cosTSpin * spinAngle;
//					var displayAngle = angle % 360f;
//					eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

//					eggbotMesh.localPosition = Vector3.zero;
//					eggbotMesh.localRotation = Quaternion.identity;

//					if (tSpin >= 1f)
//					{
//						eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
//						stateTimer = 0f; // Force immediate re-evaluation
//					}
//					break;

//				case State.Busy:
//					if (moveDuration > 0) // Moving
//					{
//						moveTimer += deltaTime;
//						var tMove = Mathf.Clamp01(moveTimer / moveDuration);
//						var cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
//						eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosTMove);

//						if (tMove >= 1f)
//						{
//							eggbotRoot.position = targetPosition;
//							stateTimer = 0f;
//						}
//					}
//					else if (turnDuration > 0) // Turning
//					{
//						turnTimer += deltaTime;
//						var tTurn = Mathf.Clamp01(turnTimer / turnDuration);
//						var cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
//						var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosTTurn);
//						eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

//						if (tTurn >= 1f)
//						{
//							stateTimer = 0f;
//						}
//					}
//					break;

//				case State.Face:
//					// Facing console, wait for timer
//					break;

//				case State.Idle:
//					// No action needed, just wait
//					break;
//			}
//		}

//		private void Evaluate()
//		{
//			if (Next()) return;
//			if (Move()) return;
//			if (Face()) return;
//			if (Test()) return;
//			if (Idle()) return;

//			SetState(State.Idle, pauseDuration);
//		}

//		private bool Next()
//		{
//			switch (currentState)
//			{
//				case State.Spin:
//					if (hasReachedEnd && isReturningToStart && currentWaypointIndex == 0)
//					{
//						// After celebratory spin at start, reset state
//						isReturningToStart = false;
//						hasReachedEnd = false;
//						isLevelComplete = false;
//						SetState(State.Idle, pauseDuration);
//						return true;
//					}
//					else if (hasReachedEnd && !isReturningToStart)
//					{
//						// After spin at end, begin returning to start
//						isReturningToStart = true;
//						SetState(State.Busy, pauseDuration);
//						return true;
//					}
//					SetState(State.Busy, pauseDuration);
//					return true;

//				case State.Face:
//					SetState(State.Busy, pauseDuration);
//					return true;
//			}
//			return false;
//		}

//		private bool Move()
//		{
//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
//				return false;

//			// Use Eggbot's actual position to determine current tile
//			int currentTile = mapManager.WorldToMapIndex(eggbotRoot.position);
//			if (currentTile == -1)
//				return false;

//			// Find the waypoint closest to the Eggbot's current position
//			int fromWaypointIndex = FindClosestWaypoint(currentTile);
//			if (fromWaypointIndex == -1)
//				return false;

//			// Determine target waypoint
//			int toWaypointIndex = isReturningToStart ? 0 : fromWaypointIndex + 1;
//			if (!isReturningToStart && toWaypointIndex >= mapManager.Waypoints.Count)
//				return false;

//			// Check if already at the target waypoint
//			int destinationTile = mapManager.Waypoints[toWaypointIndex].nTile;
//			if (currentTile == destinationTile && currentPath == null)
//			{
//				// Update currentWaypointIndex to reflect the current position
//				currentWaypointIndex = fromWaypointIndex;
//				return false; // No movement needed, let Test handle advancement
//			}

//			if (currentPath == null)
//			{
//				if (Navigation.CheckPathBetweenWaypoints(mapManager, fromWaypointIndex, toWaypointIndex, out currentPath))
//				{
//					isPuzzleBlocked = false;
//					cameraController?.OnPuzzleSolved(fromWaypointIndex);
//					pathStepIndex = segmentStartIndex = segmentEndIndex = 0;
//				}
//				else
//				{
//					isPuzzleBlocked = true;
//					SetState(State.Idle, pauseDuration);
//					return false;
//				}
//			}

//			PrepareNextSegment();
//			return false;
//		}

//		private bool Face()
//		{
//			int currentTile = mapManager.WorldToMapIndex(eggbotRoot.position);
//			if (currentTile == -1)
//				return false;

//			var consoleTile = Navigation.FindAdjacentConsole(mapManager, currentTile);
//			if (consoleTile != -1)
//			{
//				var consoleProps = mapManager.GetTileProperties(consoleTile);
//				if (consoleProps?.Nav != 0)
//				{
//					var oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
//					var consoleYaw = DirToAngle(oppositeDir);
//					var currentYaw = eggbotRoot.eulerAngles.y;
//					if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw)) > 0.1f)
//					{
//						StartTurning(consoleYaw);
//						SetState(State.Face, 0.5f); // Console check duration
//						return true;
//					}
//					// Already facing console, stay in Idle to allow console interaction
//					SetState(State.Idle, pauseDuration);
//					return true;
//				}
//			}
//			return false;

//			static float DirToAngle(int dir)
//			{
//				if ((dir & 1) != 0) return 0f;
//				if ((dir & 2) != 0) return 180f;
//				if ((dir & 4) != 0) return 90f;
//				if ((dir & 8) != 0) return -90f;
//				return 0f;
//			}
//		}

//		private bool Test()
//		{
//			int currentTile = mapManager.WorldToMapIndex(eggbotRoot.position);
//			if (currentTile == -1)
//				return false;

//			int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;

//			if (currentTile == waypointTile)
//			{
//				cameraController?.OnWaypointReached(currentWaypointIndex);

//				if (isReturningToStart && currentWaypointIndex == 0 && hasReachedEnd)
//				{
//					StartSpinning();
//					SetState(State.Spin, spinDuration);
//					return true;
//				}
//				else if (!isReturningToStart && currentWaypointIndex >= mapManager.Waypoints.Count - 1)
//				{
//					var tileProps = mapManager.GetTileProperties(currentTile);
//					if (tileProps?.IsEnd == true)
//					{
//						isLevelComplete = true;
//						hasReachedEnd = true;
//						StartSpinning();
//						SetState(State.Spin, spinDuration);
//						return true;
//					}
//				}

//				// Don't advance waypoint here; let Move determine the next waypoint
//				return true;
//			}
//			return false;
//		}

//		private bool Idle()
//		{
//			int currentTile = mapManager.WorldToMapIndex(eggbotRoot.position);
//			if (currentTile == -1)
//				return false;

//			var consoleTile = Navigation.FindAdjacentConsole(mapManager, currentTile);
//			if (consoleTile != -1)
//			{
//				SetState(State.Idle, pauseDuration);
//				return true;
//			}
//			return false;
//		}

//		private int FindClosestWaypoint(int currentTile)
//		{
//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
//				return -1;

//			int closestWaypointIndex = -1;
//			float minDistance = float.MaxValue;

//			for (int i = 0; i < mapManager.Waypoints.Count; i++)
//			{
//				int waypointTile = mapManager.Waypoints[i].nTile;
//				Vector3 waypointPos = mapManager.GetTilePosition(waypointTile);
//				Vector3 currentPos = mapManager.GetTilePosition(currentTile);
//				float distance = Vector3.Distance(waypointPos, currentPos);
//				if (distance < minDistance)
//				{
//					minDistance = distance;
//					closestWaypointIndex = i;
//				}
//			}

//			return closestWaypointIndex;
//		}

//		private void SetState(State state, float time)
//		{
//			currentState = state;
//			stateTimer = time;

//			// Reset timers based on state
//			if (state != State.Busy)
//			{
//				moveTimer = turnTimer = 0f;
//				moveDuration = turnDuration = 0f;
//			}
//			if (state != State.Spin)
//			{
//				spinTimer = 0f;
//			}
//		}

//		private void PrepareNextSegment()
//		{
//			if (currentPath == null || pathStepIndex >= currentPath.Count - 1)
//			{
//				currentPath = null;
//				pathStepIndex = segmentStartIndex = segmentEndIndex = 0;
//				startPosition = eggbotRoot.position;
//				targetPosition = startPosition;
//				moveDuration = 0f;
//				SetState(State.Idle, pauseDuration); // Allow re-evaluation
//				return;
//			}

//			var currentDir = 0;
//			segmentEndIndex = pathStepIndex;
//			while (segmentEndIndex < currentPath.Count - 1)
//			{
//				var direction = Navigation.GetTileOffsetToDirection(mapManager, currentPath[segmentEndIndex + 1] - currentPath[segmentEndIndex]);
//				if (currentDir == 0)
//					currentDir = direction;
//				else if (direction != currentDir)
//					break;
//				segmentEndIndex++;
//			}

//			segmentStartIndex = pathStepIndex;
//			startPosition = mapManager.GetTilePosition(currentPath[segmentStartIndex]);
//			targetPosition = mapManager.GetTilePosition(currentPath[segmentEndIndex]);
//			var distance = Vector3.Distance(startPosition, targetPosition);
//			moveDuration = (distance + 1.0f) / walkSpeed;

//			if (moveDuration <= 0f && segmentEndIndex > segmentStartIndex)
//			{
//				moveDuration = ((segmentEndIndex - segmentStartIndex) + 1.0f) / walkSpeed;
//			}

//			if (segmentEndIndex > segmentStartIndex)
//			{
//				Vector3 direction = (targetPosition - startPosition).normalized;
//				targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
//				StartTurning(targetYaw);
//				SetState(State.Busy, turnDuration);
//			}
//			else
//			{
//				StartSegmentMovement();
//				SetState(State.Busy, moveDuration);
//			}
//		}

//		private void StartSegmentMovement()
//		{
//			moveTimer = 0f;
//			SetState(State.Busy, moveDuration);
//		}

//		private void StartTurning(float newTargetYaw)
//		{
//			startYaw = eggbotRoot.eulerAngles.y;
//			targetYaw = newTargetYaw;
//			turnTimer = 0f;
//			turnDuration = 1f / 6f; // gfTurnRate=6.0f
//			SetState(State.Busy, turnDuration);
//		}

//		private void StartSpinning()
//		{
//			startYaw = eggbotRoot.eulerAngles.y;
//			spinTimer = 0f;
//			SetState(State.Spin, spinDuration);
//		}
//	}
//}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//namespace ClassicTilestorm
//{
//	public class EggbotController : MonoBehaviour
//	{
//		private MapManager mapManager => GamePreview.mapManager;
//		private CameraController cameraController => GamePreview.cameraController;

//		[HideInInspector]
//		public Transform eggbotRoot;
//		private Transform eggbotMesh; // Reference to the Mesh child transform

//		private List<int> currentPath;
//		private int pathStepIndex;
//		private int currentWaypointIndex;
//		private bool isReturningToStart;
//		private bool hasReachedEnd;

//		private float pauseTimer;
//		private float pauseDuration = 1f;

//		public static bool isPuzzleBlocked;
//		private bool isLevelComplete;
//		public bool IsLevelComplete => isLevelComplete;

//		private bool isCheckingConsole;
//		private float consoleCheckTimer;
//		private float consoleCheckDuration = 0.5f; // Time to face console

//		private bool isMoving;
//		private float moveTimer;
//		private float moveDuration;
//		private float walkSpeed = 6f; // Based on gfWalkRate=6.0f (tiles per second)
//		private Vector3 startPosition;
//		private Vector3 targetPosition;
//		private int segmentStartIndex;
//		private int segmentEndIndex;

//		private bool isTurning;
//		private float turnTimer;
//		private float startYaw;
//		private float targetYaw;
//		private float turnDuration = 1f / 6f; // Based on gfTurnRate=6.0f

//		private bool isSpinning;
//		private float spinTimer;
//		private float spinDuration = 1f; // Time for spin
//		private float spinAngle = 1260f; // 3.5 rotations

//		// Wobble variables
//		private float wobble = 0.1f; // Initial wobble amplitude
//		private static float mod1 = 0.0f; // Persistent accumulator for wobble
//		private static float mod2 = 0.0f; // Persistent accumulator for wobble

//		public void Initialize()
//		{
//			Reset();
//			InitializeEggbot();

//			//local function
//			void InitializeEggbot()
//			{
//				var eggbotCostume = string.IsNullOrEmpty(GamePreview.mapManager.EggbotCostume) ? "Eggbot Default" : GamePreview.mapManager.EggbotCostume;
//				int startTile = Navigation.GetStartTile(mapManager);
//				if (startTile == -1)
//					return;

//				if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

//				eggbotRoot = new GameObject("Eggbot").transform;
//				eggbotRoot.position = mapManager.GetTilePosition(startTile);
//				eggbotRoot.rotation = Quaternion.identity;
//				eggbotRoot.SetParent(mapManager.transform, false);

//				if (null == eggbotCostume) eggbotCostume = "Eggbot Default";
//				var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
//				if (def == null || def.szGeom == null) return;

//				var prefab = GeometryManager.Get(def.szGeom);
//				var mesh = Instantiate(prefab, eggbotRoot);
//				mesh.name = "Mesh";

//				var transform = mesh.transform;
//				transform.SetParent(eggbotRoot, false);
//				transform.localPosition = Vector3.zero;
//				transform.localRotation = Quaternion.identity;

//				// Store reference to the Mesh transform
//				eggbotMesh = transform;

//				DatabaseLoader.Theme theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme);
//				if (theme == null || string.IsNullOrEmpty(theme.szTileTextureSet)) return;

//				var textureFrames = TextureSetManager.GetTextureFrames(theme.szTileTextureSet);
//				if (textureFrames?.Length > 0)
//				{
//					var animator = mesh.AddComponent<TextureSetAnimator>();
//					animator.Initialize(textureFrames);
//				}
//				else
//				{
//					Debug.LogWarning($"No texture set for {eggbotRoot}");
//				}
//			}
//		}

//		public void Reset()
//		{
//			currentPath = null;
//			pathStepIndex = currentWaypointIndex = 0;
//			segmentStartIndex = segmentEndIndex = 0;

//			isReturningToStart = hasReachedEnd = false;
//			isMoving = isLevelComplete = isPuzzleBlocked = false;
//			isTurning = isCheckingConsole = isSpinning = false;

//			moveTimer = turnTimer = consoleCheckTimer = spinTimer = 0f;
//			pauseTimer = pauseDuration;
//			wobble = 0.1f; // Reset wobble to initial value
//			mod1 = 0.0f;
//			mod2 = 0.0f;
//		}

//		public void UpdateEggbot()
//		{
//			if (mapManager.Waypoints?.Count == 0)
//				return;

//			// Update wobble accumulators
//			mod1 += 7.8f * Time.deltaTime;
//			mod2 += 1.8f * Time.deltaTime;

//			// Update wobble factor based on state
//			var isIdle = !isMoving && !isTurning && !isSpinning && !isCheckingConsole;
//			var targetWobble = isIdle ? 0.02f : 0.1f;
//			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

//			// Calculate pitch for wobble
//			float pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);

//			if (isSpinning)
//			{
//				spinTimer += Time.deltaTime;
//				var t = Mathf.Clamp01(spinTimer / spinDuration);
//				var cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f; // Sigmoid interpolation
//				var angle = cosT * spinAngle;
//				var displayAngle = angle % 360f; // Ensure visual rotation aligns
//				eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

//				// Reset mesh transform during spin to avoid wobble
//				eggbotMesh.localPosition = Vector3.zero;
//				eggbotMesh.localRotation = Quaternion.identity;

//				if (t >= 1f)
//				{
//					isSpinning = false;
//					spinTimer = 0f;
//					eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
//					if (hasReachedEnd && isReturningToStart && currentWaypointIndex == 0)
//					{
//						// After celebratory spin at start, reset state
//						isReturningToStart = false;
//						hasReachedEnd = false;
//						isLevelComplete = false;
//						pauseTimer = pauseDuration;
//					}
//					else if (hasReachedEnd && !isReturningToStart)
//					{
//						// After spin at end, begin returning to start
//						isReturningToStart = true;
//						pauseTimer = pauseDuration;
//					}
//				}
//				return;
//			}

//			if (isTurning)
//			{
//				turnTimer += Time.deltaTime;
//				var t = Mathf.Clamp01(turnTimer / turnDuration);
//				var cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
//				var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosT);
//				eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

//				if (t >= 1f)
//				{
//					isTurning = false;
//					turnTimer = 0f;
//					if (isMoving)
//					{
//						StartSegmentMovement();
//					}
//					else if (isCheckingConsole)
//					{
//						consoleCheckTimer = consoleCheckDuration;
//					}
//				}
//			}
//			else if (isCheckingConsole)
//			{
//				consoleCheckTimer -= Time.deltaTime;
//				if (consoleCheckTimer <= 0)
//				{
//					isCheckingConsole = false;
//					var waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
//					var pathClear = Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath);

//					if (pathClear)
//					{
//						if (currentPath != null && currentPath.Count > 1)
//						{
//							var nextPos = mapManager.GetTilePosition(currentPath[1]);
//							var currentPos = mapManager.GetTilePosition(currentPath[0]);
//							var direction = (nextPos - currentPos).normalized;
//							var pathYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
//							StartTurning(pathYaw, false);
//						}
//						else
//						{
//							pauseTimer = pauseDuration;
//						}
//					}
//					else
//					{
//						isPuzzleBlocked = true;
//					}
//				}
//			}
//			else if (isMoving)
//			{
//				moveTimer += Time.deltaTime;
//				var t = moveDuration > 0 ? Mathf.Clamp01(moveTimer / moveDuration) : 1f;
//				var cosT = (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
//				eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosT);

//				if (t >= 1f)
//				{
//					eggbotRoot.position = targetPosition;
//					isMoving = false;
//					pathStepIndex = segmentEndIndex;

//					if (pathStepIndex >= currentPath.Count - 1)
//					{
//						currentWaypointIndex = isReturningToStart ? 0 : currentWaypointIndex + 1;
//						currentPath = null;
//						segmentStartIndex = segmentEndIndex = 0;
//						pauseTimer = pauseDuration;

//						if (currentWaypointIndex < mapManager.Waypoints.Count)
//						{
//							int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
//							eggbotRoot.position = mapManager.GetTilePosition(waypointTile);
//							var tileProps = mapManager.GetTileProperties(waypointTile);
//							cameraController?.OnWaypointReached(currentWaypointIndex);

//							if (isReturningToStart && currentWaypointIndex == 0 && hasReachedEnd)
//							{
//								StartSpinning(); // Celebratory spin at start after returning
//							}
//							else if (currentWaypointIndex >= mapManager.Waypoints.Count - 1 && tileProps?.IsEnd == true && !isReturningToStart)
//							{
//								isLevelComplete = true;
//								hasReachedEnd = true;
//								StartSpinning(); // Spin at end
//							}
//							else
//							{
//								CheckAndFaceAdjacentConsole(waypointTile);
//							}
//						}
//					}
//					else
//					{
//						PrepareNextSegment();
//					}
//				}
//			}
//			else
//			{
//				pauseTimer -= Time.deltaTime;
//				if (pauseTimer <= 0)
//					MoveToNextWaypoint();
//			}

//			// Apply wobble to the Mesh child transform (offset * pitch)
//			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f); // X-axis rotation
//			var localOffset = new Vector3(0f, 0f, -pitch); // Local Z-offset (forward/backward)
//			var wobblePos = pitchRotation * localOffset; // offset * pitch in local space

//			// Set the Mesh's local transform
//			eggbotMesh.localPosition = wobblePos;
//			eggbotMesh.localRotation = pitchRotation;
//		}

//		private void MoveToNextWaypoint()
//		{
//			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count && !isReturningToStart)
//				return;

//			if (isReturningToStart)
//			{
//				if (currentWaypointIndex == 0)
//				{
//					if (hasReachedEnd)
//					{
//						StartSpinning(); // Trigger celebratory spin at start
//					}
//					else
//					{
//						isReturningToStart = false;
//						hasReachedEnd = false;
//						isLevelComplete = false;
//						pauseTimer = pauseDuration;
//					}
//					return;
//				}
//				// Navigate back to waypoint 0
//				var targetWaypoint = 0;
//				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, targetWaypoint, out currentPath))
//				{
//					isPuzzleBlocked = false;
//					pathStepIndex = 0;
//					segmentStartIndex = segmentEndIndex = 0;
//					PrepareNextSegment();
//				}
//				else
//				{
//					pauseTimer = pauseDuration; // Fallback if no path
//				}
//			}
//			else
//			{
//				var currentTile = mapManager.Waypoints[currentWaypointIndex].nTile;
//				if (Navigation.FindAdjacentConsole(mapManager, currentTile) != -1 && !Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out _))
//				{
//					if (!isTurning)
//					{
//						CheckAndFaceAdjacentConsole(currentTile);
//					}
//					return;
//				}

//				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath))
//				{
//					isPuzzleBlocked = false;
//					cameraController?.OnPuzzleSolved(currentWaypointIndex);
//					pathStepIndex = 0;
//					segmentStartIndex = segmentEndIndex = 0;
//					PrepareNextSegment();
//				}
//			}
//		}

//		private void CheckAndFaceAdjacentConsole(int tile)
//		{
//			var consoleTile = Navigation.FindAdjacentConsole(mapManager, tile);
//			if (consoleTile != -1)
//			{
//				var consoleProps = mapManager.GetTileProperties(consoleTile);
//				if (consoleProps?.Nav != 0)
//				{
//					var oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
//					var consoleYaw = DirToAngle(oppositeDir);
//					var currentYaw = eggbotRoot.eulerAngles.y;
//					if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw)) > 0.1f)
//					{
//						StartTurning(consoleYaw, false);
//						isCheckingConsole = true;
//					}
//				}
//			}

//			static float DirToAngle(int dir)
//			{
//				if ((dir & 1) != 0) return 0f;   // North (positive Z)
//				if ((dir & 2) != 0) return 180f; // South (negative Z)
//				if ((dir & 4) != 0) return 90f;  // East (positive X)
//				if ((dir & 8) != 0) return -90f; // West (negative X)
//				return 0f;
//			}
//		}

//		private void PrepareNextSegment()
//		{
//			if (pathStepIndex >= currentPath.Count - 1)
//			{
//				segmentStartIndex = segmentEndIndex = pathStepIndex;
//				startPosition = mapManager.GetTilePosition(currentPath[pathStepIndex]);
//				targetPosition = startPosition;
//				moveDuration = 0f;
//				StartSegmentMovement();
//				return;
//			}

//			var currentDir = 0;
//			segmentEndIndex = pathStepIndex;
//			while (segmentEndIndex < currentPath.Count - 1)
//			{
//				var direction = Navigation.GetTileOffsetToDirection(mapManager, currentPath[segmentEndIndex + 1] - currentPath[segmentEndIndex]);

//				if (currentDir == 0)
//					currentDir = direction;
//				else if (direction != currentDir)
//					break;
//				segmentEndIndex++;
//			}

//			segmentStartIndex = pathStepIndex;
//			startPosition = mapManager.GetTilePosition(currentPath[segmentStartIndex]);
//			targetPosition = mapManager.GetTilePosition(currentPath[segmentEndIndex]);
//			var distance = Vector3.Distance(startPosition, targetPosition);
//			// Add 1.0f to distance to match original game's movement duration
//			moveDuration = (distance + 1.0f) / walkSpeed;

//			if (moveDuration <= 0f && segmentEndIndex > segmentStartIndex)
//			{
//				// Use tile count with offset for fallback duration
//				moveDuration = ((segmentEndIndex - segmentStartIndex) + 1.0f) / walkSpeed;
//			}

//			if (segmentEndIndex > segmentStartIndex)
//			{
//				Vector3 direction = (targetPosition - startPosition).normalized;
//				targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
//				StartTurning(targetYaw, true);
//			}
//			else
//			{
//				StartSegmentMovement();
//			}
//		}

//		private void StartSegmentMovement()
//		{
//			moveTimer = 0f;
//			isMoving = true;
//		}

//		private void StartTurning(float newTargetYaw, bool continueMoving)
//		{
//			startYaw = eggbotRoot.eulerAngles.y;
//			targetYaw = newTargetYaw;
//			turnTimer = 0f;
//			isTurning = true;
//			isMoving = continueMoving;
//		}

//		private void StartSpinning()
//		{
//			startYaw = eggbotRoot.eulerAngles.y;
//			spinTimer = 0f;
//			isSpinning = true;
//		}
//	}
//}
