using UnityEngine;
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

		private int currentTile; // Tracks current tile index
		private int currentWaypointIndex;
		private bool isReturningToStart;
		private bool hasReachedEnd;
		private bool continueToMove; // Flag to indicate Turning should transition to Moving

		private float stateTimer; // Combined timer for Pausing, Spinning, Turning, CheckingConsole, Moving
		private float stateDuration; // Duration for Pausing, Spinning, Turning, CheckingConsole, Moving

		public static bool isPuzzleBlocked;
		private bool isLevelComplete;
		public bool IsLevelComplete => isLevelComplete;

		private float walkSpeed = 6f;
		private Vector3 startPosition;
		private Vector3 targetPosition;

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
			currentTile = -1;
			currentWaypointIndex = 0;
			continueToMove = false;

			isReturningToStart = hasReachedEnd = false;
			isLevelComplete = isPuzzleBlocked = false;

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

			currentTile = startTile;

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
			}

			eggbotRoot.position = mapManager.GetTilePosition(startTile);
			OrientEggbot(startTile);
			SetState(State.Pausing);
		}

		private void OrientEggbot(int startTile)
		{
			var yaw = 0f;
			if (mapManager.Waypoints != null && mapManager.Waypoints.Count > 1)
			{
				var dir = Navigation.NavToDest(mapManager, mapManager.Waypoints[0].nTile, mapManager.Waypoints[1].nTile);
				if (0 != dir) yaw = DirToAngle(dir);
			}
			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
		}

		public void UpdateEggbot()
		{
			if (mapManager.Waypoints?.Count == 0)
				return;

			stateTimer += Time.deltaTime;

			switch (currentState)
			{
				case State.Spinning:
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
					var tTurn = Mathf.Clamp01(stateTimer / stateDuration);
					var cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
					var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosTTurn);
					eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

					if (tTurn >= 1f)
					{
						eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
						if (continueToMove)
						{
							continueToMove = false;
							SetState(State.Moving);
						}
						else
						{
							SetState(State.CheckingConsole);
						}
					}
					break;

				case State.CheckingConsole:
					if (stateTimer >= stateDuration)
					{
						var waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
						var nextWaypointTile = mapManager.Waypoints[currentWaypointIndex + 1].nTile;
						var direction = Navigation.NavToDest(mapManager, waypointTile, nextWaypointTile);
						var pathClear = 0 != direction;
						if (pathClear)
						{
							cameraController?.OnPuzzleSolved(currentWaypointIndex);
							var pathYaw = DirToAngle(direction);
							StartTurning(pathYaw, true);
						}
						else
						{
							isPuzzleBlocked = !pathClear;
							SetState(State.Pausing);
						}
					}
					break;

				case State.Moving:
					var tMove = stateDuration > 0 ? Mathf.Clamp01(stateTimer / stateDuration) : 1f;
					var cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
					eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosTMove);

					if (tMove >= 1f)
					{
						eggbotRoot.position = targetPosition;

						// Check if we've reached the destination waypoint's tile
						int destinationTile = isReturningToStart ? mapManager.Waypoints[0].nTile : mapManager.Waypoints[currentWaypointIndex + 1].nTile;
						if (currentTile == destinationTile)
						{
							currentWaypointIndex = isReturningToStart ? 0 : currentWaypointIndex + 1;

							if (currentWaypointIndex < mapManager.Waypoints?.Count)
							{
								int waypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
								eggbotRoot.position = mapManager.GetTilePosition(waypointTile);
								currentTile = waypointTile;
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
		}

		private void SetState(State state)
		{
			currentState = state;

			switch (state)
			{
				case State.Pausing:
					stateTimer = 0f;
					stateDuration = 1f;
					continueToMove = false;
					break;
				case State.Moving:
					stateTimer = 0f;
					// Compute movement parameters
					int destinationTile = isReturningToStart ? mapManager.Waypoints[0].nTile : mapManager.Waypoints[currentWaypointIndex + 1].nTile;
					var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
					if (direction == 0)
					{
						startPosition = mapManager.GetTilePosition(currentTile);
						targetPosition = startPosition;
						stateDuration = 0f;
					}
					else
					{
						var length = Navigation.LengthDir(mapManager, currentTile, destinationTile, direction);
						startPosition = mapManager.GetTilePosition(currentTile);
						var (dx, dz) = TileProperties.GetDirectionOffset(direction);
						var gridCoord = mapManager.GetTileCoordinates(currentTile).Add(dx * (int)length, dz * (int)length);
						targetPosition = gridCoord.ToPosition();
						stateDuration = (length + 1.0f) / walkSpeed;
						currentTile = mapManager.ToIndex(gridCoord);
					}
					break;
				case State.Turning:
					stateTimer = 0f;
					stateDuration = 1f / 6f;
					break;
				case State.CheckingConsole:
					stateTimer = 0f;
					stateDuration = 0.5f;
					break;
				case State.Spinning:
					stateTimer = 0f;
					stateDuration = 1f;
					break;
			}
		}

		private void MoveToNextWaypoint()
		{
			if (currentWaypointIndex + 1 >= mapManager.Waypoints?.Count && !isReturningToStart)
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
				var targetWaypoint = mapManager.Waypoints[0].nTile;
				var direction = Navigation.NavToDest(mapManager, currentTile, targetWaypoint);
				if (direction != 0)
				{
					isPuzzleBlocked = false;
					PrepareNextSegment();
				}
				else
				{
					isPuzzleBlocked = true;
					SetState(State.Pausing);
				}
			}
			else
			{
				var currentWaypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;
				var nextWaypointTile = mapManager.Waypoints[currentWaypointIndex + 1].nTile;
				var dir = Navigation.NavToDest(mapManager, currentWaypointTile, nextWaypointTile);
				if (Navigation.FindAdjacentConsole(mapManager, currentWaypointTile) != -1 && 0 == dir)
				{
					CheckAndFaceAdjacentConsole(currentWaypointTile);
					return;
				}

				if (dir != 0)
				{
					isPuzzleBlocked = false;
					cameraController?.OnPuzzleSolved(currentWaypointIndex);
					currentTile = currentWaypointTile;
					PrepareNextSegment();
				}
				else
				{
					isPuzzleBlocked = true;
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
					if (yawDelta > 0.01f)
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
			int destinationTile = isReturningToStart ? mapManager.Waypoints[0].nTile : mapManager.Waypoints[currentWaypointIndex + 1].nTile;
			var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
			if (direction == 0)
			{
				SetState(State.Moving);
				return;
			}

			var length = Navigation.LengthDir(mapManager, currentTile, destinationTile, direction);
			if (length > 0)
			{
				targetYaw = DirToAngle(direction);
				StartTurning(targetYaw, true);
			}
			else
			{
				SetState(State.Moving);
			}
		}

		private void StartTurning(float newTargetYaw, bool continueMoving)
		{
			startYaw = eggbotRoot.eulerAngles.y;
			targetYaw = newTargetYaw;
			continueToMove = continueMoving;
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