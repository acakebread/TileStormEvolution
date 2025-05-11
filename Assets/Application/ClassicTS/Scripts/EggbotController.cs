using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class EggbotController : MonoBehaviour
	{
		private MapManager mapManager => GamePreview.mapManager;

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
		private float mod1, mod2;

		private Queue<System.Action<State>> actionQueue = new();
		private enum State { IDLE, TURN, MOVE }
		private State currentState = State.IDLE;
		private System.Action<State> onActionComplete;

		public event System.Action<int> OnWaypointReached;
		public event System.Action<int> OnPuzzleSolved;
		public event System.Action OnLevelCompleted;

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
			actionQueue.Clear();
		}

		private void SetState(State state, float duration = 1f, System.Action<State> onComplete = null)
		{
			currentState = state;
			stateTimer = 0f;
			stateDuration = duration;
			onActionComplete = onComplete ?? onActionComplete;
		}

		private void QueueAction(System.Action<State> action) => actionQueue.Enqueue(action);

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
			if (actionQueue.Count > 0) { actionQueue.Dequeue()?.Invoke(currentState); return; }
			var destinationTile = mapManager.Waypoints[dstWaypoint].nTile;

			if (TestSpin(destinationTile)) return;
			if (TestMove(destinationTile)) return;
			if (TestTurn(destinationTile)) return;

			bool TestSpin(int destinationTile)
			{
				if (currentTile != destinationTile || (destinationTile != Navigation.GetEndTile(mapManager) && destinationTile != Navigation.GetStartTile(mapManager))) return false;
				if (destinationTile == Navigation.GetEndTile(mapManager)) { isLevelComplete = true; OnLevelCompleted?.Invoke(); }
				dstWaypoint = (dstWaypoint + 1) % mapManager.Waypoints.Count;
				startYaw = eggbotRoot.eulerAngles.y;
				targetYaw = eggbotRoot.eulerAngles.y + SpinAngle;
				QueueAction(state => SetState(State.TURN, 1f, s => SetState(State.IDLE, 0.5f)));
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
				if (0 == direction || Mathf.DeltaAngle(0, eggbotRoot.eulerAngles.y) != DirToAngle(direction)) return false;

				startPosition = mapManager.GetTilePosition(currentTile);
				var prevCurrentTile = currentTile;
				currentTile = Navigation.LineOfSight(mapManager, currentTile, destinationTile, direction);
				targetPosition = mapManager.GetTilePosition(currentTile);
				QueueAction(state => SetState(State.MOVE, (mapManager.GetTileDistance(prevCurrentTile, currentTile) + 1.0f) / walkSpeed, s => SetState(State.IDLE, 0f)));
				OnPuzzleSolved?.Invoke(dstWaypoint - 1);
				return true;
			}

			bool TestTurn(int destinationTile)
			{
				var direction = Navigation.NavToDest(mapManager, currentTile, destinationTile);
				if (0 != direction && Mathf.Abs(Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, DirToAngle(direction))) > 0.01f)
				{
					startYaw = eggbotRoot.eulerAngles.y;
					targetYaw = eggbotRoot.eulerAngles.y + Mathf.DeltaAngle(eggbotRoot.eulerAngles.y, DirToAngle(direction));
					QueueAction(state => SetState(State.TURN, 1f / 4f, s => SetState(State.IDLE, 0f)));
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
						QueueAction(state => SetState(State.TURN, 1f / 4f, s => SetState(State.IDLE, 0.5f)));
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

		private void OnDestroy()
		{
			OnWaypointReached = null;
			OnPuzzleSolved = null;
			OnLevelCompleted = null;
		}

		private static readonly float[] DirAngles = { 0f, 0f, 180f, 0f, 90f, 45f, 135f, 0f, -90f, -45f, -135f };
		private static float DirToAngle(int dir) => dir >= 0 && dir < DirAngles.Length ? DirAngles[dir] : 0f;
	}
}