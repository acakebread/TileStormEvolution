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

		private float stateTimer; // Combined timer for Pausing, Spinning, Turning, and CheckingConsole
		private float stateDuration; // Duration for Pausing, Spinning, Turning, and CheckingConsole

		public static bool isPuzzleBlocked;
		private bool isLevelComplete;
		public bool IsLevelComplete => isLevelComplete;

		private float moveTimer;
		private float moveDuration;
		private float walkSpeed = 6f;
		private Vector3 startPosition;
		private Vector3 targetPosition;
		private int segmentStartIndex;
		private int segmentEndIndex;

		private float startYaw;
		private float targetYaw;

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

			moveTimer = 0f;
			stateTimer = 0f;
			stateDuration = 1f; // Initial pause duration
			wobble = 0.1f;
			mod1 = mod2 = 0.0f;
		}

		private void InitializeEggbot()
		{
			if (mapManager == null) { Debug.LogError("InitializeEggbot: mapManager is null"); return; }

			int startTile = Navigation.GetStartTile(mapManager);
			if (startTile == -1) { Debug.LogError("InitializeEggbot: No start tile found"); return; }

			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

			eggbotRoot = new GameObject("Eggbot").transform;
			eggbotRoot.SetParent(mapManager.transform, false);

			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
			if (def == null || def.szGeom == null) { Debug.LogError($"InitializeEggbot: No Eggbot definition found for costume: {eggbotCostume}"); return; }

			var prefab = GeometryManager.Get(def.szGeom);
			if (prefab == null) { Debug.LogError($"InitializeEggbot: Failed to load prefab for geometry: {def.szGeom}"); return; }
			var mesh = Instantiate(prefab, eggbotRoot);
			mesh.name = "Mesh";
			eggbotMesh = mesh.transform;
			eggbotMesh.SetParent(eggbotRoot, false);
			eggbotMesh.localPosition = Vector3.zero;
			eggbotMesh.localRotation = Quaternion.identity;

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

			var fromWaypointIndex = 0;
			var toWaypointIndex = 1;
			var destinationTile = mapManager.Waypoints[toWaypointIndex].nTile;

			List<int> path;
			if (!Navigation.CheckPathBetweenWaypoints(mapManager, fromWaypointIndex, toWaypointIndex, out path) || path == null || path.Count < 2)
			{
				Debug.LogWarning($"OrientEggbot: No valid path from waypoint {fromWaypointIndex} (tile {startTile}) to waypoint {toWaypointIndex} (tile {destinationTile})");
				eggbotRoot.rotation = Quaternion.identity;
				return;
			}

			var nextTile = path[1];
			Debug.Log($"OrientEggbot: Path to waypoint {toWaypointIndex}: {string.Join(", ", path)}, nextTile: {nextTile}");

			var direction = Navigation.GetTileOffsetToDirection(mapManager, nextTile - startTile);
			var yaw = DirToAngle(direction);

			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
			Debug.Log($"OrientEggbot: Eggbot oriented to face direction {direction} (yaw={yaw}) toward tile {nextTile}");
		}

		public void UpdateEggbot()
		{
			if (mapManager.Waypoints?.Count == 0)
				return;

			switch (currentState)
			{
				case State.Spinning:
					stateTimer += Time.deltaTime;
					var tSpin = Mathf.Clamp01(stateTimer / stateDuration);
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
						}
						else if (hasReachedEnd && !isReturningToStart)
						{
							isReturningToStart = true;
						}
						SetState(State.Pausing);
					}
					break;

				case State.Turning:
					stateTimer += Time.deltaTime;
					var tTurn = Mathf.Clamp01(stateTimer / stateDuration);
					var cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
					var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosTTurn);
					eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

					if (tTurn >= 1f)
					{
						eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
						Debug.Log($"Turning complete: moveDuration={moveDuration}, currentState={currentState}");
						if (moveDuration > 0)
						{
							StartSegmentMovement();
						}
						else if (currentState == State.CheckingConsole)
						{
							SetState(State.CheckingConsole);
						}
						else
						{
							SetState(State.Pausing);
						}
					}
					break;

				case State.CheckingConsole:
					stateTimer += Time.deltaTime;
					if (stateTimer >= stateDuration)
					{
						var waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
						var pathClear = Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath);

						Debug.Log($"CheckingConsole: pathClear={pathClear}, currentPath={(currentPath != null ? currentPath.Count : 0)}");
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
					stateTimer += Time.deltaTime;
					if (stateTimer >= stateDuration)
					{
						MoveToNextWaypoint();
					}
					break;
			}

			mod1 += 7.8f * Time.deltaTime;
			mod2 += 1.8f * Time.deltaTime;

			var targetWobble = currentState == State.Pausing ? 0.02f : 0.1f;
			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

			var pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);
			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
			var localOffset = new Vector3(0f, 0f, -pitch);
			var wobblePos = pitchRotation * localOffset;
			eggbotMesh.localPosition = wobblePos;
			eggbotMesh.localRotation = pitchRotation;
			//Debug.Log($"UpdateEggbot: State={currentState}, Position={eggbotRoot.position}, Yaw={eggbotRoot.eulerAngles.y}, Waypoint={currentWaypointIndex}, PathStep={pathStepIndex}");
		}

		private void SetState(State state)
		{
			Debug.Log($"SetState: {state}, stateTimer={stateTimer}, stateDuration={stateDuration}, moveDuration={moveDuration}");
			currentState = state;

			switch (state)
			{
				case State.Pausing:
					stateTimer = 0f;
					stateDuration = 1f; // Equivalent to pauseDuration
					moveTimer = 0f;
					moveDuration = 0f;
					break;
				case State.Moving:
					moveTimer = 0f;
					stateTimer = 0f; // Reset to avoid carryover
					break;
				case State.Turning:
					stateTimer = 0f;
					stateDuration = 1f / 6f; // Equivalent to turnDuration
					break;
				case State.CheckingConsole:
					stateTimer = 0f;
					stateDuration = 0.5f; // Equivalent to consoleCheckDuration
					break;
				case State.Spinning:
					stateTimer = 0f;
					stateDuration = 1f; // Equivalent to spinDuration
					break;
			}
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= mapManager.Waypoints.Count && !isReturningToStart)
			{
				Debug.Log("MoveToNextWaypoint: No more waypoints, pausing");
				SetState(State.Pausing);
				return;
			}

			if (isReturningToStart)
			{
				if (currentWaypointIndex == 0)
				{
					if (hasReachedEnd)
					{
						Debug.Log("MoveToNextWaypoint: Reached start, spinning");
						StartSpinning();
					}
					else
					{
						Debug.Log("MoveToNextWaypoint: Resetting return state");
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
					Debug.Log($"MoveToNextWaypoint: Path found to start, length={currentPath?.Count}");
					isPuzzleBlocked = false;
					pathStepIndex = 0;
					segmentStartIndex = segmentEndIndex = 0;
					PrepareNextSegment();
				}
				else
				{
					Debug.Log("MoveToNextWaypoint: No path to start, pausing");
					SetState(State.Pausing);
				}
			}
			else
			{
				var currentTile = mapManager.Waypoints[currentWaypointIndex].nTile;
				if (Navigation.FindAdjacentConsole(mapManager, currentTile) != -1 && !Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out _))
				{
					Debug.Log("MoveToNextWaypoint: Adjacent console found, checking");
					CheckAndFaceAdjacentConsole(currentTile);
					return;
				}

				if (Navigation.CheckPathBetweenWaypoints(mapManager, currentWaypointIndex, currentWaypointIndex + 1, out currentPath))
				{
					Debug.Log($"MoveToNextWaypoint: Path found to next waypoint, length={currentPath?.Count}");
					isPuzzleBlocked = false;
					cameraController?.OnPuzzleSolved(currentWaypointIndex);
					pathStepIndex = 0;
					segmentStartIndex = segmentEndIndex = 0;
					PrepareNextSegment();
				}
				else
				{
					Debug.Log("MoveToNextWaypoint: No path to next waypoint, pausing");
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
					var yawDelta = Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw));
					Debug.Log($"CheckAndFaceAdjacentConsole: currentYaw={currentYaw}, consoleYaw={consoleYaw}, yawDelta={yawDelta}");
					if (yawDelta > 0.01f) // Lowered threshold for consistent turning
					{
						StartTurning(consoleYaw, false);
						return;
					}
				}
			}
			Debug.Log("CheckAndFaceAdjacentConsole: No turn needed, checking console");
			SetState(State.CheckingConsole);
		}

		private void PrepareNextSegment()
		{
			if (currentPath == null || pathStepIndex >= currentPath.Count - 1)
			{
				Debug.Log($"PrepareNextSegment: Path ended or null, setting to current position, pathStepIndex={pathStepIndex}");
				segmentStartIndex = segmentEndIndex = pathStepIndex;
				startPosition = mapManager.GetTilePosition(currentPath != null && pathStepIndex < currentPath.Count ? currentPath[pathStepIndex] : mapManager.Waypoints[currentWaypointIndex].nTile);
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
			Debug.Log($"StartSegmentMovement: Moving from {startPosition} to {targetPosition}, moveDuration={moveDuration}");
			moveTimer = 0f;
			SetState(State.Moving);
		}

		private void StartTurning(float newTargetYaw, bool continueMoving)
		{
			startYaw = eggbotRoot.eulerAngles.y;
			targetYaw = newTargetYaw;
			moveDuration = continueMoving ? moveDuration : 0f;
			Debug.Log($"StartTurning: From yaw={startYaw} to {targetYaw}, continueMoving={continueMoving}, moveDuration={moveDuration}");
			SetState(State.Turning);
		}

		private void StartSpinning()
		{
			startYaw = eggbotRoot.eulerAngles.y;
			Debug.Log($"StartSpinning: Starting at yaw={startYaw}");
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

//		private float stateTimer; // Combined timer for Pausing and Spinning
//		private float stateDuration; // Duration for Pausing and Spinning

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

//			moveTimer = turnTimer = consoleCheckTimer = 0f;
//			stateTimer = 0f;
//			stateDuration = 1f; // Initial pause duration
//			wobble = 0.1f;
//			mod1 = mod2 = 0.0f;
//		}

//		private void InitializeEggbot()
//		{
//			if (mapManager == null) { Debug.LogError("InitializeEggbot: mapManager is null"); return; }

//			int startTile = Navigation.GetStartTile(mapManager);
//			if (startTile == -1) { Debug.LogError("InitializeEggbot: No start tile found"); return; }

//			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

//			eggbotRoot = new GameObject("Eggbot").transform;
//			eggbotRoot.SetParent(mapManager.transform, false);

//			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
//			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
//			if (def == null || def.szGeom == null) { Debug.LogError($"InitializeEggbot: No Eggbot definition found for costume: {eggbotCostume}"); return; }

//			var prefab = GeometryManager.Get(def.szGeom);
//			if (prefab == null) { Debug.LogError($"InitializeEggbot: Failed to load prefab for geometry: {def.szGeom}"); return; }
//			var mesh = Instantiate(prefab, eggbotRoot);
//			mesh.name = "Mesh";
//			eggbotMesh = mesh.transform;
//			eggbotMesh.SetParent(eggbotRoot, false);
//			eggbotMesh.localPosition = Vector3.zero;
//			eggbotMesh.localRotation = Quaternion.identity;

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
//			Debug.Log($"InitializeEggbot: Eggbot placed at start tile: {startTile}, position: {eggbotRoot.position}");

//			OrientEggbot(startTile);
//			SetState(State.Pausing);
//		}

//		private void OrientEggbot(int startTile)
//		{
//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count < 2)
//			{
//				Debug.LogWarning("OrientEggbot: Not enough waypoints to orient Eggbot");
//				eggbotRoot.rotation = Quaternion.identity;
//				return;
//			}

//			var fromWaypointIndex = 0;
//			var toWaypointIndex = 1;
//			var destinationTile = mapManager.Waypoints[toWaypointIndex].nTile;

//			List<int> path;
//			if (!Navigation.CheckPathBetweenWaypoints(mapManager, fromWaypointIndex, toWaypointIndex, out path) || path == null || path.Count < 2)
//			{
//				Debug.LogWarning($"OrientEggbot: No valid path from waypoint {fromWaypointIndex} (tile {startTile}) to waypoint {toWaypointIndex} (tile {destinationTile})");
//				eggbotRoot.rotation = Quaternion.identity;
//				return;
//			}

//			var nextTile = path[1];
//			Debug.Log($"OrientEggbot: Path to waypoint {toWaypointIndex}: {string.Join(", ", path)}, nextTile: {nextTile}");

//			var direction = Navigation.GetTileOffsetToDirection(mapManager, nextTile - startTile);
//			var yaw = DirToAngle(direction);

//			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
//			Debug.Log($"OrientEggbot: Eggbot oriented to face direction {direction} (yaw={yaw}) toward tile {nextTile}");
//		}

//		public void UpdateEggbot()
//		{
//			if (mapManager.Waypoints?.Count == 0)
//				return;

//			switch (currentState)
//			{
//				case State.Spinning:
//					stateTimer += Time.deltaTime;
//					var tSpin = Mathf.Clamp01(stateTimer / stateDuration);
//					var cosTSpin = (1f - Mathf.Cos(tSpin * Mathf.PI)) / 2f;
//					var angle = cosTSpin * spinAngle;
//					var displayAngle = angle % 360f;
//					eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + displayAngle, 0f);

//					eggbotMesh.localPosition = Vector3.zero;
//					eggbotMesh.localRotation = Quaternion.identity;

//					if (tSpin >= 1f)
//					{
//						eggbotRoot.rotation = Quaternion.Euler(0f, startYaw + 180f, 0f);
//						if (hasReachedEnd && isReturningToStart && currentWaypointIndex == 0)
//						{
//							isReturningToStart = false;
//							hasReachedEnd = false;
//							isLevelComplete = false;
//						}
//						else if (hasReachedEnd && !isReturningToStart)
//						{
//							isReturningToStart = true;
//						}
//						SetState(State.Pausing);
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
//					stateTimer += Time.deltaTime;
//					if (stateTimer >= stateDuration)
//					{
//						MoveToNextWaypoint();
//					}
//					break;
//			}

//			mod1 += 7.8f * Time.deltaTime;
//			mod2 += 1.8f * Time.deltaTime;

//			var targetWobble = currentState == State.Pausing ? 0.02f : 0.1f;
//			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

//			var pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);
//			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
//			var localOffset = new Vector3(0f, 0f, -pitch);
//			var wobblePos = pitchRotation * localOffset;
//			eggbotMesh.localPosition = wobblePos;
//			eggbotMesh.localRotation = pitchRotation;
//			//Debug.Log($"UpdateEggbot: State={currentState}, Position={eggbotRoot.position}, Yaw={eggbotRoot.eulerAngles.y}, Waypoint={currentWaypointIndex}, PathStep={pathStepIndex}");
//		}

//		private void SetState(State state)
//		{
//			Debug.Log($"SetState: {state}, stateTimer={stateTimer}, stateDuration={stateDuration}");
//			currentState = state;

//			switch (state)
//			{
//				case State.Pausing:
//					stateTimer = 0f;
//					stateDuration = 1f; // Equivalent to pauseDuration
//					moveTimer = turnTimer = consoleCheckTimer = 0f;
//					moveDuration = 0f;
//					break;
//				case State.Moving:
//					moveTimer = 0f;
//					stateTimer = 0f; // Reset to avoid carryover
//					break;
//				case State.Turning:
//					turnTimer = 0f;
//					stateTimer = 0f; // Reset to avoid carryover
//					break;
//				case State.CheckingConsole:
//					consoleCheckTimer = consoleCheckDuration;
//					stateTimer = 0f; // Reset to avoid carryover
//					break;
//				case State.Spinning:
//					stateTimer = 0f;
//					stateDuration = 1f; // Equivalent to spinDuration
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