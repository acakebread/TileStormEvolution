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

		private int currentTile;
		private int dstWaypoint;

		private float stateTimer;
		private float stateDuration;

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

		private enum State { IDLE, TEST, TURN, MOVE }
		private State currentState = State.IDLE;

		public void Initialize()
		{
			Reset();
			if (mapManager == null) { Debug.LogError("Initialize: mapManager is null"); return; }

			var startTile = Navigation.GetStartTile(mapManager);
			if (startTile == -1) { Debug.LogError("Initialize: No start tile found"); return; }

			currentTile = startTile;

			if (eggbotRoot != null) Destroy(eggbotRoot.gameObject);

			eggbotRoot = new GameObject("Eggbot").transform;
			eggbotRoot.SetParent(mapManager.transform, false);

			var eggbotCostume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == eggbotCostume);
			if (def == null || def.szGeom == null) { Debug.LogError($"Initialize: No Eggbot definition found for costume: {eggbotCostume}"); return; }

			var prefab = GeometryManager.Get(def.szGeom);
			if (prefab == null) { Debug.LogError($"Initialize: Failed to load prefab for geometry: {def.szGeom}"); return; }
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
			eggbotRoot.rotation = Quaternion.Euler(0f, mapManager.Waypoints != null && mapManager.Waypoints.Count > 1 ? DirToAngle(Navigation.NavToDest(mapManager, mapManager.Waypoints[0].nTile, mapManager.Waypoints[1].nTile)) : 0f, 0f);

			SetState(State.IDLE);
		}

		public void Reset()
		{
			currentTile = -1;
			dstWaypoint = 1;
			stateTimer = 0f;
			stateDuration = 1f;
			wobble = 0.1f;
			mod1 = mod2 = 0.0f;
			isLevelComplete = false;
		}

		private void SetState(State state, float duration = 1f)
		{
			currentState = state;
			stateTimer = 0f;
			stateDuration = duration;
		}

		public void UpdateEggbot()
		{
			if (mapManager.Waypoints?.Count == 0)
				return;

			stateTimer += Time.deltaTime;

			switch (currentState)
			{
				case State.IDLE:
					UpdateIdle();
					break;
				case State.TEST:
					UpdateTest();
					break;
				case State.TURN:
					UpdateTurn();
					break;
				case State.MOVE:
					UpdateMove();
					break;
			}

			UpdateWobble();
		}

		private void UpdateIdle()
		{
			if (stateTimer >= stateDuration)
				SetState(State.TEST, 0f);
		}

		private void UpdateTest()
		{
			if (stateTimer >= stateDuration)
			{
				var destinationTile = mapManager.Waypoints[dstWaypoint].nTile;

				if (currentTile == destinationTile)
				{
					if (TestDestination())
						return;
				}
				else
				{
					if (MoveToDestination())
						return;
				}
				if (FaceConsole())
					return;

				if (currentTile == destinationTile)
					SetNextDestination();

				SetState(State.IDLE);
			}

			bool FaceConsole()
			{
				var consoleTile = Navigation.FindAdjacentConsole(mapManager, currentTile);
				if (consoleTile == -1)
					return false;

				var consoleProps = mapManager.GetTileProperties(consoleTile);
				if (consoleProps?.Nav == 0)
					return false;

				var oppositeDir = TileProperties.GetOppositeDirection(consoleProps.Nav);
				var consoleYaw = DirToAngle(oppositeDir);
				var currentYaw = Mathf.DeltaAngle(0, eggbotRoot.eulerAngles.y);
				var yawDelta = Mathf.Abs(Mathf.DeltaAngle(currentYaw, consoleYaw));
				if (yawDelta > 0.01f)
				{
					StartTurn(eggbotRoot.eulerAngles.y + Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, consoleYaw), 1f / 6f);
					return true;
				}

				return false;
			}

			bool TestDestination()
			{
				var destinationTile = mapManager.Waypoints[dstWaypoint].nTile;
				if (destinationTile == Navigation.GetEndTile(mapManager))
				{
					isLevelComplete = true;
					SetNextDestination(); // Advance waypoint before spinning
					StartTurn(eggbotRoot.eulerAngles.y + spinAngle, 1f);
					return true;
				}
				if (destinationTile == Navigation.GetStartTile(mapManager) && dstWaypoint == 0)
				{
					SetNextDestination(); // Advance waypoint before spinning
					StartTurn(eggbotRoot.eulerAngles.y + spinAngle, 1f);
					return true;
				}

				cameraController?.OnWaypointReached(dstWaypoint);
				return false;
			}

			bool MoveToDestination()
			{
				var destinationTile = mapManager.Waypoints[dstWaypoint].nTile;
				var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
				if (direction == 0)
					return false;

				var length = Navigation.LengthDir(mapManager, currentTile, destinationTile, direction);
				targetYaw = DirToAngle(direction);

				if (Mathf.DeltaAngle(0, eggbotRoot.eulerAngles.y) != targetYaw)
				{
					StartTurn(eggbotRoot.eulerAngles.y + Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, targetYaw), 1f / 6f);
					return true;
				}

				startPosition = mapManager.GetTilePosition(currentTile);
				var (dx, dz) = TileProperties.GetDirectionOffset(direction);
				var gridCoord = mapManager.GetTileCoordinates(currentTile).Add(dx * (int)length, dz * (int)length);
				currentTile = mapManager.ToIndex(gridCoord);
				targetPosition = gridCoord.ToPosition();
				SetState(State.MOVE, (length + 1.0f) / walkSpeed);
				cameraController?.OnPuzzleSolved(dstWaypoint - 1);
				return true;
			}
		}

		private void UpdateTurn()
		{
			var tTurn = Mathf.Clamp01(stateTimer / stateDuration);
			var cosTTurn = (1f - Mathf.Cos(tTurn * Mathf.PI)) / 2f;
			var angle = Mathf.Lerp(startYaw, targetYaw, cosTTurn);
			eggbotRoot.rotation = Quaternion.Euler(0f, angle % 360f, 0f);

			if (tTurn >= 1f)
			{
				eggbotRoot.rotation = Quaternion.Euler(0f, targetYaw % 360f, 0f);
				SetState(State.TEST, 0.2f);
			}
		}

		private void UpdateMove()
		{
			var tMove = stateDuration > 0 ? Mathf.Clamp01(stateTimer / stateDuration) : 1f;
			var cosTMove = (1f - Mathf.Cos(tMove * Mathf.PI)) / 2f;
			eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, cosTMove);

			if (tMove >= 1f)
			{
				eggbotRoot.position = targetPosition;
				SetState(State.TEST, 0.2f);
			}
		}

		private void UpdateWobble()
		{
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

		private void SetNextDestination() => dstWaypoint = (dstWaypoint + 1) % mapManager.Waypoints.Count;

		private void StartTurn(float dstYaw, float duration)
		{
			startYaw = eggbotRoot.eulerAngles.y;
			targetYaw = dstYaw;
			SetState(State.TURN, duration);
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