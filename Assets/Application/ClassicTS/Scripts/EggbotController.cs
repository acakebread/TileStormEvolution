using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public class EggbotController : MonoBehaviour
	{
		private int currentTile;
		private int dstWaypoint;
		private float stateTimer;
		private float stateDuration;

		private readonly float walkSpeed = 6f;
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

		public event System.Action<int, bool> OnWaypointReached;
		public event System.Action<int> OnPuzzleSolved;
		public event System.Action OnLevelCompleted;

		public int NavDirection(IMapPlay map) => Navigation.NavToDest(map, currentTile, map.GetWaypoint(dstWaypoint).tile);

		private void Awake()
		{
			dstWaypoint = -1;
			mod1 = mod2 = 0f;
			sway = 0.1f;
			actionQueue.Clear();
			SetState(State.IDLE, 1f);
		}

		private System.Action _unsubscribeAction;

		public void Initialise(IMapEdit map)
		{
			if (null == map) { Debug.LogError("Initialize: null map!"); return; }

			currentTile = map.GetStartTile();
			if (-1 == currentTile)
			{
				Debug.LogWarning("Initialize: Invalid setup or empty map: No start tile");
			}

			transform.position = targetPosition = map.TileRenderPosition(currentTile >= 0 ? currentTile : map.Count >> 1);

			if (currentTile >= 0)
			{
				var waypoints = map.GetWaypoints();
				var yaw = waypoints?.Length > 1 ? Navigation.DirToAngle(Navigation.NavToDest(map, waypoints[0].tile, waypoints[1].tile)) : 0f;
				transform.rotation = Quaternion.Euler(0f, yaw, 0f);
				startYaw = targetYaw = yaw;
				dstWaypoint = 1;
			}
			else
			{
				transform.rotation = Quaternion.Euler(0f, 0f, 0f);
				startYaw = targetYaw = 0f;
			}

			map.OnMapEdited += HandleMapEdited;// Subscribe to map changes
			_unsubscribeAction = () => map.OnMapEdited -= HandleMapEdited;// Capture the map instance in a closure
		}

		private void HandleMapEdited(IMapPlay map, bool resized, Vector3 originDelta)
		{
			if (resized) OnMapOriginShift(map, originDelta);
		}

		private void SetState(State state, float duration = 0f)
		{
			currentState = state;
			stateTimer = 0f;
			stateDuration = duration;
		}

		public void UpdateEggbot(IMapEdit map)
		{
			if (!isActiveAndEnabled) return;
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

				if (dstWaypoint < 0) return;
				var wp = map.GetWaypoint(dstWaypoint);
				if (null == wp || -1 == wp.tile) return;
				if (TestSpin(wp.tile)) return;
				if (TestMove(wp.tile)) return;
				if (TestTurn(wp.tile)) return;
				SetState(State.IDLE, 1f);

				bool TestSpin(int destinationTile)
				{
					if (null == map) return false;
					if (currentTile != destinationTile || (destinationTile != map.GetEndTile() && destinationTile != map.GetStartTile())) return false;
					if (destinationTile == map.GetEndTile()) { OnLevelCompleted?.Invoke(); }

					var waypoints = map.GetWaypoints();
					if (null != waypoints) dstWaypoint = (dstWaypoint + 1) % waypoints.Length;
					startYaw = transform.eulerAngles.y;
					targetYaw = transform.eulerAngles.y + SpinAngle;
					actionQueue.Enqueue(() => SetState(State.TURN, 1.5f));
					actionQueue.Enqueue(() => SetState(State.IDLE, 0.5f));
					return true;
				}

				bool TestMove(int destinationTile)
				{
					if (null == map) return false;
					if (currentTile == destinationTile)
					{
						var waypoints = map.GetWaypoints();
						var next = (null != waypoints) ? waypoints[(dstWaypoint + 1) % waypoints.Length].tile : -1;
						var enable_gestures = 0 == Navigation.NavToDest(map, destinationTile, next);//path blocked

						OnWaypointReached?.Invoke(dstWaypoint, enable_gestures);
						if (null != waypoints) dstWaypoint = (dstWaypoint + 1) % waypoints.Length;
						return false;
					}

					var direction = Navigation.NavToDest(map, currentTile, destinationTile);
					if (0 == direction || 0 != (int)Mathf.DeltaAngle(transform.eulerAngles.y, Navigation.DirToAngle(direction))) return false;
					isBlocked = false;

					startPosition = map.TileRenderPosition(currentTile);
					var prevTargetPosition = targetPosition;
					var prevCurrentTile = currentTile;
					currentTile = Navigation.LineOfSight(map, currentTile, destinationTile, direction);
					targetPosition = map.TileRenderPosition(currentTile);
					actionQueue.Enqueue(() => SetState(State.MOVE, ((targetPosition - prevTargetPosition).magnitude + 1.0f) / walkSpeed));
					return true;
				}

				bool TestTurn(int destinationTile)
				{
					if (null == map) return false;
					var direction = Navigation.NavToDest(map, currentTile, destinationTile);
					var consoleTile = map.FindAdjacentConsole(currentTile);
					if (0 != direction && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, Navigation.DirToAngle(direction))) > 0.01f)
					{
						startYaw = transform.eulerAngles.y;
						targetYaw = (int)transform.eulerAngles.y + Mathf.DeltaAngle(transform.eulerAngles.y, Navigation.DirToAngle(direction));
						actionQueue.Enqueue(() => SetState(State.TURN, 1f / 4f));
						if (-1 != consoleTile) OnPuzzleSolved?.Invoke(dstWaypoint - 1);
						return true;
					}

					if (-1 != consoleTile && 0 != map.GetTile(consoleTile).Nav)
					{
						isBlocked = direction == 0;
						var consoleYaw = Navigation.DirToAngle(Navigation.GetOppositeDirection(map.GetTile(consoleTile).Nav));
						if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, consoleYaw)) > 0.01f)
						{
							startYaw = transform.eulerAngles.y;
							targetYaw = transform.eulerAngles.y + Mathf.DeltaAngle(transform.eulerAngles.y, consoleYaw);
							actionQueue.Enqueue(() => SetState(State.TURN, 1f / 4f));
							actionQueue.Enqueue(() => SetState(State.IDLE, 0.5f));
							return true;
						}

						// Queue four BUSY states for jiggling to simulate looking at console
						var currentYaw = transform.eulerAngles.y;
						var offset = (Random.Range(0f, 8f) - 4f) * 4.3f; // [-17.2, 17.2] degrees
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
				transform.rotation = Quaternion.Euler(0f, Mathf.Lerp(startYaw, targetYaw, SmoothingUtils.Ease(t)) % 360f, 0f);
				if (t >= 1f) { transform.rotation = Quaternion.Euler(0f, (int)(targetYaw % 360f), 0f); SetState(State.TEST); }
			}

			void UpdateMove()
			{
				var t = stateDuration > 0 ? Mathf.Clamp01(stateTimer / stateDuration) : 1f;
				transform.position = Vector3.Lerp(startPosition, targetPosition, SmoothingUtils.Ease(t));
				if (t >= 1f) SetState(State.TEST);
			}

			void UpdateSway()
			{
				mod1 += 7.8f * Time.deltaTime;
				mod2 += 1.8f * Time.deltaTime;
				sway = SmoothingUtils.Smooth(sway, isBlocked ? 0.02f : 0.1f, 99f, Time.deltaTime);
				var pitch = sway * Mathf.Sin(mod1) * Mathf.Sin(mod2);
				var rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, 0f, 0f);
				var eggbot = transform.GetChild(0);
				eggbot.localPosition = rotation * new Vector3(0f, 0f, -pitch);
				eggbot.localRotation = rotation;
			}
		}

		private void OnDestroy()
		{
			OnWaypointReached = null;
			OnPuzzleSolved = null;
			OnLevelCompleted = null;
			_unsubscribeAction?.Invoke();
			_unsubscribeAction = null; // Optional: prevent reuse
		}

		/// <summary>
		/// Called when the map origin shifts due to expand/crop.
		/// Updates currentTile and snaps position to new grid.
		/// </summary>
		/// <param name="originDelta">World-space shift of the map origin (in tile units)</param>
		private void OnMapOriginShift(IMapPlay map, Vector3 originDelta)
		{
			if (originDelta == Vector3.zero) return;

			int deltaX = Mathf.RoundToInt(originDelta.x);
			int deltaZ = Mathf.RoundToInt(originDelta.z);

			if (deltaX == 0 && deltaZ == 0) return;

			// Get current world position before we lose context
			Vector3 oldWorldPos = transform.position;

			// Calculate old grid position (reverse-engineer from world pos)
			Vector3 localPos = Map.FullFloorVec(oldWorldPos);

			int oldX = Mathf.RoundToInt(localPos.x);
			int oldZ = Mathf.RoundToInt(localPos.z);

			// Apply delta
			int newX = oldX + deltaX;
			int newZ = oldZ + deltaZ;

			if (newX < 0 || newX >= map.Width || newZ < 0 || newZ >= map.Height)
			{
				// Eggbot was cropped out — snap to nearest valid tile or start?
				currentTile = map.GetStartTile();
			}
			else
			{
				currentTile = newZ * map.Width + newX;
			}

			// Snap position to new grid
			transform.position = map.TileRenderPosition(currentTile);

			// Optional: preserve sub-tile offset (e.g. during movement)
			// But for editor, we want clean snap
		}

		public static EggbotController Instantiate(string costume = "Eggbot Default", Transform parent = null)
		{
			costume = string.IsNullOrEmpty(costume) ? "Eggbot Default" : costume;
			var resolvedCostume = CharacterResourceTable.GetDisplayName(costume) ?? costume;
			var eggbotController = new GameObject($"Eggbot: {resolvedCostume}");

			if (null != parent) eggbotController.transform.SetParent(parent, false);

			//var def = ResourceManager.Definitions.FirstOrDefault(td => td.id == "Eggbot" && td.texture == costume);
			var def = ResourceManager.Definitions.FirstOrDefault(td => td.name == resolvedCostume);
			if (null == def?.model) { Debug.LogError("Initialize: Invalid Eggbot geometry"); return null; }

			var mesh = DefinitionFactory.Instantiate(def, Vector3.zero, null, eggbotController.transform);
			return eggbotController.AddComponent<EggbotController>();
		}
	}
}
