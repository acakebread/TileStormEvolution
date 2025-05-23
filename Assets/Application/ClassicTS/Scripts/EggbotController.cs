using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager => GameController.mapManager;

		[HideInInspector] public Transform eggbotRoot;
		private Transform eggbotMesh;

		private int currentTile;
		private int dstWaypoint;
		private float stateTimer;
		private float stateDuration;

		private float walkSpeed = 6f;
		private Vector3 startPosition;
		private Vector3 targetPosition;
		private float startYaw;
		private float targetYaw;
		private const float SpinAngle = 1260f;

		private float sway = 0.1f;
		private float mod1, mod2;

		private Queue<System.Action> actionQueue = new();
		private enum State { IDLE, TEST, TURN, MOVE }
		private State currentState = State.IDLE;
		private bool isBlocked = false;

		public event System.Action<int> OnWaypointReached;
		public event System.Action<int> OnPuzzleSolved;
		public event System.Action OnLevelCompleted;

		public static EggbotController instance;
		private void Awake() { instance = this; Reset(); }

		public void Reset()
		{
			dstWaypoint = 1;
			mod1 = mod2 = 0f;
			sway = 0.1f;
			actionQueue.Clear();

			currentTile = Navigation.GetStartTile(mapManager);
			if (null == mapManager || -1 == currentTile) { Debug.LogError("Initialize: Invalid setup"); return; }

			if (null != eggbotRoot) Destroy(eggbotRoot.gameObject);
			eggbotRoot = new GameObject("Eggbot").transform;
			eggbotRoot.SetParent(mapManager.transform, false);

			var costume = string.IsNullOrEmpty(mapManager.EggbotCostume) ? "Eggbot Default" : mapManager.EggbotCostume;
			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == "Eggbot" && td.szTheme == costume);
			if (null == def?.szGeom) { Debug.LogError("Initialize: Invalid Eggbot geometry"); return; }

			var mesh = GeometryManager.InstantiatePrefab(def, eggbotRoot, Vector3.zero);
			mesh.name = "Mesh";
			eggbotMesh = mesh.transform;

			eggbotRoot.position = mapManager.GetTilePosition(currentTile);
			var yaw = mapManager.Waypoints?.Count > 1 ? Navigation.DirToAngle(Navigation.NavToDest(mapManager, mapManager.Waypoints[0].nTile, mapManager.Waypoints[1].nTile)) : 0f;
			eggbotRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
			SetState(State.IDLE, 1f);
		}

		private void SetState(State state, float duration = 0f)
		{
			currentState = state;
			stateTimer = 0f;
			stateDuration = duration;
		}

		public void UpdateEggbot()
		{
			stateTimer += Time.deltaTime;
			switch (currentState)
			{
				case State.IDLE: UpdateIdle(); break;
				case State.TEST: UpdateTest(); break;
				case State.TURN: UpdateTurn(); break;
				case State.MOVE: UpdateMove(); break;
			}
			UpdateSway();

			void UpdateIdle()
			{
				if (stateTimer < stateDuration) return;
				SetState(State.TEST);
			}

			void UpdateTest()
			{
				if (actionQueue.Count > 0) { actionQueue.Dequeue()?.Invoke(); return; }

				var destinationTile = mapManager.Waypoints[dstWaypoint].nTile;
				if (TestSpin(destinationTile)) return;
				if (TestMove(destinationTile)) return;
				if (TestTurn(destinationTile)) return;
				SetState(State.IDLE, 1f);

				bool TestSpin(int destinationTile)
				{
					if (currentTile != destinationTile || (destinationTile != Navigation.GetEndTile(mapManager) && destinationTile != Navigation.GetStartTile(mapManager))) return false;
					if (destinationTile == Navigation.GetEndTile(mapManager)) { OnLevelCompleted?.Invoke(); }
					dstWaypoint = (dstWaypoint + 1) % mapManager.Waypoints.Count;
					startYaw = eggbotRoot.eulerAngles.y;
					targetYaw = eggbotRoot.eulerAngles.y + SpinAngle;
					actionQueue.Enqueue(() => SetState(State.TURN, 1.5f));
					actionQueue.Enqueue(() => SetState(State.IDLE, 0.5f));
					return true;
				}

				bool TestMove(int destinationTile)
				{
					if (currentTile == destinationTile)
					{
						OnWaypointReached?.Invoke(dstWaypoint);
						dstWaypoint = (dstWaypoint + 1) % mapManager.Waypoints.Count;
						return false;
					}

					var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
					if (0 == direction || 0 != (int)Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, Navigation.DirToAngle(direction))) return false;

					if (true == isBlocked) OnPuzzleSolved?.Invoke(dstWaypoint - 1);
					isBlocked = false;
					startPosition = mapManager.GetTilePosition(currentTile);
					var prevCurrentTile = currentTile;
					currentTile = Navigation.LineOfSight(mapManager, currentTile, destinationTile, direction);
					targetPosition = mapManager.GetTilePosition(currentTile);
					actionQueue.Enqueue(() => SetState(State.MOVE, (mapManager.GetTileDistance(prevCurrentTile, currentTile) + 1.0f) / walkSpeed));
					return true;
				}

				bool TestTurn(int destinationTile)
				{
					var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
					if (0 != direction && Mathf.Abs(Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, Navigation.DirToAngle(direction))) > 0.01f)
					{
						startYaw = eggbotRoot.eulerAngles.y;
						targetYaw = (int)eggbotRoot.eulerAngles.y + Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, Navigation.DirToAngle(direction));
						actionQueue.Enqueue(() => SetState(State.TURN, 1f / 4f));
						return true;
					}

					var consoleTile = Navigation.FindAdjacentConsole(mapManager, currentTile);
					if (-1 != consoleTile && null != mapManager.GetTileProperties(consoleTile)?.Nav)
					{
						isBlocked = direction == 0;
						var consoleYaw = Navigation.DirToAngle(TileProperties.GetOppositeDirection(mapManager.GetTileProperties(consoleTile).Nav));
						if (Mathf.Abs(Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, consoleYaw)) > 0.01f)
						{
							startYaw = eggbotRoot.eulerAngles.y;
							targetYaw = eggbotRoot.eulerAngles.y + Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, consoleYaw);
							actionQueue.Enqueue(() => SetState(State.TURN, 1f / 4f));
							actionQueue.Enqueue(() => SetState(State.IDLE, 0.5f));
							return true;
						}

						// Queue four BUSY states for jiggling to simulate looking at console
						float currentYaw = eggbotRoot.eulerAngles.y;
						float offset = (Random.Range(0f, 8f) - 4f) * 4.3f; // [-17.2, 17.2] degrees
						actionQueue.Enqueue(() => { startYaw = Mathf.DeltaAngle(0f, currentYaw); targetYaw = startYaw + offset; SetState(State.TURN, Random.Range(0.25f, 0.75f)); });
						actionQueue.Enqueue(() => { startYaw = Mathf.DeltaAngle(0f, currentYaw + offset); targetYaw = startYaw + (Random.Range(0f, 8f) - 4f) * 4.3f; SetState(State.TURN, Random.Range(0.25f, 0.75f)); });
						return true;
					}
					return false;
				}
			}

			void UpdateTurn()
			{
				var t = stateDuration > 0 ? Mathf.Clamp01(stateTimer / stateDuration) : 1f;
				eggbotRoot.rotation = Quaternion.Euler(0f, Mathf.Lerp(startYaw, targetYaw, SmoothingUtils.Ease(t)) % 360f, 0f);
				if (t >= 1f) { eggbotRoot.rotation = Quaternion.Euler(0f, (int)(targetYaw % 360f), 0f); SetState(State.TEST); }
			}

			void UpdateMove()
			{
				var t = stateDuration > 0 ? Mathf.Clamp01(stateTimer / stateDuration) : 1f;
				eggbotRoot.position = Vector3.Lerp(startPosition, targetPosition, SmoothingUtils.Ease(t));
				if (t >= 1f) SetState(State.TEST);
			}

			void UpdateSway()
			{
				mod1 += 7.8f * Time.deltaTime;
				mod2 += 1.8f * Time.deltaTime;
				sway = SmoothingUtils.Smooth(sway, isBlocked ? 0.02f : 0.1f, 99f, Time.deltaTime);
				var pitch = sway * Mathf.Sin(mod1) * Mathf.Sin(mod2);
				var rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
				eggbotMesh.localPosition = rotation * new Vector3(0f, 0f, -pitch);
				eggbotMesh.localRotation = rotation;
			}
		}

		private void OnDestroy()
		{
			instance = null;
			OnWaypointReached = null;
			OnPuzzleSolved = null;
			OnLevelCompleted = null;
		}
	}
}