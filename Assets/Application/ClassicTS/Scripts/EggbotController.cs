using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager => GamePreview.mapManager;
		private CameraController cameraController => GamePreview.cameraController;

		[HideInInspector] public Transform eggbotRoot;
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
		private const float SpinAngle = 1260f;

		private float sway = 0.1f;
		private static float mod1;
		private static float mod2;

		private enum State { IDLE, TURN, MOVE }
		private State currentState = State.IDLE;
		private System.Action<State> onActionComplete;

		public void Initialize()
		{
			Reset();
			currentTile = Navigation.GetStartTile(mapManager);
			if (null == mapManager || -1 == currentTile) { Debug.LogError("Initialize: Invalid setup"); return; }

			if (null != eggbotRoot) Destroy(eggbotRoot.gameObject);
			eggbotRoot = new GameObject("Eggbot").transform;
			eggbotRoot.SetParent(mapManager.transform, false);

			var costume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == costume);
			if (null == def?.szGeom) { Debug.LogError("Initialize: Invalid Eggbot geometry"); return; }
			var mesh = Instantiate(GeometryManager.Get(def.szGeom), eggbotRoot);
			mesh.name = "Mesh";
			eggbotMesh = mesh.transform;
			eggbotMesh.localPosition = Vector3.zero;
			eggbotMesh.localRotation = Quaternion.identity;
			Debug.Log($"Eggbot mesh localPosition: {eggbotMesh.localPosition}");

			if (null != DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme)?.szTileTextureSet)
				mesh.AddComponent<TextureSetAnimator>().Initialize(TextureSetManager.GetTextureFrames(DatabaseLoader.Themes.FirstOrDefault(t => t.name == def.szTheme).szTileTextureSet));

			eggbotRoot.position = mapManager.GetTilePosition(currentTile);
			var yaw = mapManager.Waypoints?.Count > 1 ? DirToAngle(Navigation.NavToDest(mapManager, mapManager.Waypoints[0].nTile, mapManager.Waypoints[1].nTile)) : 0f;
			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
			SetState(State.IDLE);
		}

		public void Reset()
		{
			currentTile = -1;
			dstWaypoint = 1;
			stateTimer = stateDuration = 0f;
			sway = 0.1f;
			mod1 = mod2 = 0f;
			isLevelComplete = false;
			onActionComplete = null;
		}

		private void SetState(State state, float duration = 1f, System.Action<State> onComplete = null)
		{
			currentState = state;
			stateTimer = 0f;
			stateDuration = duration;
			onActionComplete = onComplete ?? onActionComplete;
		}

		public void UpdateEggbot()
		{
			stateTimer += Time.deltaTime;
			switch (currentState)
			{
				case State.IDLE: UpdateIdle(); break;
				case State.TURN: UpdateTurn(); break;
				case State.MOVE: UpdateMove(); break;
			}
			UpdateSway();
		}

		private void UpdateIdle()
		{
			if (stateTimer < stateDuration) return;
			var destinationTile = mapManager.Waypoints[dstWaypoint].nTile;

			if (TestSpin(destinationTile)) return;
			if (TestMove(destinationTile)) return;
			if (TestTurn(destinationTile)) return;

			bool TestSpin(int destinationTile)
			{
				if (currentTile != destinationTile || (destinationTile != Navigation.GetEndTile(mapManager) && destinationTile != Navigation.GetStartTile(mapManager))) return false;
				if (destinationTile == Navigation.GetEndTile(mapManager)) isLevelComplete = true;
				dstWaypoint = (dstWaypoint + 1) % mapManager.Waypoints.Count;
				startYaw = eggbotRoot.eulerAngles.y;
				targetYaw = eggbotRoot.eulerAngles.y + SpinAngle;
				SetState(State.TURN, 1f, state => SetState(State.IDLE, 0.5f));
				return true;
			}

			bool TestMove(int destinationTile)
			{
				if (currentTile == destinationTile)
				{
					cameraController?.OnWaypointReached(dstWaypoint);
					dstWaypoint = (dstWaypoint + 1) % mapManager.Waypoints.Count;
					return false;
				}

				var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
				if (0 == direction || Mathf.DeltaAngle(0, eggbotRoot.eulerAngles.y) != DirToAngle(direction)) return false;

				startPosition = mapManager.GetTilePosition(currentTile);
				var prevCurrentTile = currentTile;
				currentTile = Navigation.LineOfSight(mapManager, currentTile, destinationTile, direction);
				targetPosition = mapManager.GetTilePosition(currentTile);
				SetState(State.MOVE, (mapManager.GetTileDistance(prevCurrentTile, currentTile) + 1.0f) / walkSpeed, state => SetState(State.IDLE, 0f));
				cameraController?.OnPuzzleSolved(dstWaypoint - 1);
				return true;
			}

			bool TestTurn(int destinationTile)
			{
				var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
				if (0 != direction && Mathf.Abs(Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, DirToAngle(direction))) > 0.01f)
				{
					startYaw = eggbotRoot.eulerAngles.y;
					targetYaw = eggbotRoot.eulerAngles.y + Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, DirToAngle(direction));
					SetState(State.TURN, 1f / 4f, state => SetState(State.IDLE, 0f));
					return true;
				}

				var consoleTile = Navigation.FindAdjacentConsole(mapManager, currentTile);
				if (-1 != consoleTile && null != mapManager.GetTileProperties(consoleTile)?.Nav)
				{
					var consoleYaw = DirToAngle(TileProperties.GetOppositeDirection(mapManager.GetTileProperties(consoleTile).Nav));
					if (Mathf.Abs(Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, consoleYaw)) > 0.01f)
					{
						startYaw = eggbotRoot.eulerAngles.y;
						targetYaw = eggbotRoot.eulerAngles.y + Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, consoleYaw);
						SetState(State.TURN, 1f / 4f, state => SetState(State.IDLE, 0.5f));
						return true;
					}
				}
				return false;
			}
		}

		private void UpdateTurn()
		{
			var t = Mathf.Clamp01(stateTimer / stateDuration);
			eggbotRoot.rotation = Quaternion.Euler(0f, Mathf.Lerp(startYaw, targetYaw, (1f - Mathf.Cos(t * Mathf.PI)) / 2f) % 360f, 0f);
			if (t >= 1f) CompleteAction();
		}

		private void UpdateMove()
		{
			var t = stateDuration > 0 ? Mathf.Clamp01(stateTimer / stateDuration) : 1f;
			eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, (1f - Mathf.Cos(t * Mathf.PI)) / 2f);
			if (t >= 1f) CompleteAction();
		}

		private void CompleteAction()
		{
			onActionComplete?.Invoke(currentState);
			if (null == onActionComplete) SetState(State.IDLE, 0f);
			onActionComplete = null;
		}

		private void UpdateSway()
		{
			mod1 += 7.8f * Time.deltaTime;
			mod2 += 1.8f * Time.deltaTime;
			sway = (sway * 99f + (State.IDLE == currentState ? 0.02f : 0.1f)) / 100f;
			var pitch = sway * Mathf.Sin(mod1) * Mathf.Sin(mod2);
			var rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
			eggbotMesh.localPosition = rotation * new Vector3(0f, 0f, -pitch);
			eggbotMesh.localRotation = rotation;
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