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

		private float stateTimer; // Combined timer for Pausing, Spinning, Turning, CheckingConsole, Moving
		private float stateDuration; // Duration for Pausing, Spinning, Turning, CheckingConsole, Moving

		private bool isLevelComplete;//ToDo replace with event / action
		public bool IsLevelComplete => isLevelComplete;//ToDo replace with event / action

		private float walkSpeed = 6f;
		private Vector3 startPosition;
		private Vector3 targetPosition;

		private float startYaw;
		private float targetYaw;

		private float spinAngle = 1260f;

		private float wobble = 0.1f;
		private static float mod1 = 0.0f;
		private static float mod2 = 0.0f;

		private enum State { IDLE, MOVE, TURN, SPIN }
		private State currentState = State.IDLE;

		public void Initialize()
		{
			Reset();
			InitializeEggbot();
		}

		public void Reset()
		{
			currentTile = -1;
			currentWaypointIndex = 0;

			stateTimer = 0f;
			stateDuration = 1f; // Initial pause duration
			wobble = 0.1f;
			mod1 = mod2 = 0.0f;

			isLevelComplete = false;//ToDo replace with event / action
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
			SetState(State.IDLE);
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
				case State.SPIN:
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
						SetState(State.IDLE, 0.2f);
					}
					break;

				case State.TURN:
					var tTurn = Mathf.Clamp01(stateTimer / stateDuration);
					var cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
					var currentYaw = Mathf.LerpAngle(startYaw, targetYaw, cosTTurn);
					eggbotRoot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

					if (tTurn >= 1f)
					{
						eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw, 0f);
						SetState(State.IDLE);// After turning (for console or navigation), transition to Pausing to re-evaluate
					}
					break;

				case State.MOVE:
					var tMove = stateDuration > 0 ? Mathf.Clamp01(stateTimer / stateDuration) : 1f;
					var cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
					eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosTMove);

					if (tMove >= 1f)
					{
						eggbotRoot.position = targetPosition;

						var dst_index = (currentWaypointIndex + 1) % mapManager.Waypoints.Count;
						int destinationTile = mapManager.Waypoints[dst_index].nTile;
						if (currentTile == destinationTile)// Check if we've reached the destination waypoint's tile
						{
							currentWaypointIndex = dst_index;
							cameraController?.OnWaypointReached(currentWaypointIndex);

							if (currentTile == Navigation.GetStartTile(mapManager) || currentTile == Navigation.GetEndTile(mapManager))
							{
								if (currentTile == Navigation.GetEndTile(mapManager)) isLevelComplete = true;//ToDo invoke event
								StartSpinning();
							}
							else
								CheckAndFaceAdjacentConsole(currentTile);
						}
						else
							PrepareNextSegment();
					}
					break;

				case State.IDLE:
					if (stateTimer >= stateDuration)
						MoveToNextWaypoint();
					break;
			}

			mod1 += 7.8f * Time.deltaTime;
			mod2 += 1.8f * Time.deltaTime;

			var targetWobble = currentState == State.IDLE ? 0.02f : 0.1f;
			wobble = (wobble * 99.0f + targetWobble) / 100.0f;

			var pitch = wobble * Mathf.Sin(mod1) * Mathf.Sin(mod2);
			var pitchRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
			var localOffset = new Vector3(0f, 0f, -pitch);
			var wobblePos = pitchRotation * localOffset;
			eggbotMesh.localPosition = wobblePos;
			eggbotMesh.localRotation = pitchRotation;
		}

		private void SetState(State state, float duration = 1f)
		{
			currentState = state;

			switch (state)
			{
				case State.IDLE:
					stateTimer = 0f;
					stateDuration = duration;
					break;
				case State.MOVE:
					stateTimer = 0f;
					// Compute movement parameters
					var dst_index = (currentWaypointIndex + 1) % mapManager.Waypoints.Count;
					int destinationTile = mapManager.Waypoints[dst_index].nTile;
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
				case State.TURN:
					stateTimer = 0f;
					stateDuration = 1f / 6f;
					break;
				case State.SPIN:
					stateTimer = 0f;
					stateDuration = 1f;
					break;
			}
		}

		private void MoveToNextWaypoint()
		{
			var currentWaypointTile = mapManager.Waypoints[currentWaypointIndex].nTile;

			var dst_index = (currentWaypointIndex + 1) % mapManager.Waypoints.Count;
			var nextWaypointTile = mapManager.Waypoints[dst_index].nTile;

			var dir = Navigation.NavToDest(mapManager, currentWaypointTile, nextWaypointTile);
			if (dir != 0)
			{
				cameraController?.OnPuzzleSolved(currentWaypointIndex);
				currentTile = currentWaypointTile;
				PrepareNextSegment();
			}
			else
				SetState(State.IDLE);//Puzzle is blocked
		}

		private bool CheckAndFaceAdjacentConsole(int tile)
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
						StartTurning(consoleYaw);
						return true;
					}
				}
			}
			SetState(State.IDLE); // Idle after facing console, re-evaluate in MoveToNextWaypoint
			return false;
		}

		private void PrepareNextSegment()
		{
			var dst_index = (currentWaypointIndex + 1) % mapManager.Waypoints.Count;
			int destinationTile = mapManager.Waypoints[dst_index].nTile;

			var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
			var length = Navigation.LengthDir(mapManager, currentTile, destinationTile, direction);
			targetYaw = DirToAngle(direction);
			if (eggbotRoot.eulerAngles.y == targetYaw && length != 0f)
			{
				SetState(State.MOVE);
				return;
			}
			StartTurning(targetYaw);
		}

		private void StartTurning(float newTargetYaw)
		{
			startYaw = eggbotRoot.eulerAngles.y;
			targetYaw = newTargetYaw;
			SetState(State.TURN);
		}

		private void StartSpinning()
		{
			startYaw = eggbotRoot.eulerAngles.y;
			SetState(State.SPIN);
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
