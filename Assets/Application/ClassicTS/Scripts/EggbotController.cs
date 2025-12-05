using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;

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

		public event System.Action<int> OnWaypointReached;
		public event System.Action<int> OnPuzzleSolved;
		public event System.Action OnLevelCompleted;

		public int NavDirection(IMapManager map) => Navigation.NavToDest(map, currentTile, map.Waypoints[dstWaypoint].tile);

		private void Awake()
		{
			dstWaypoint = 1;
			mod1 = mod2 = 0f;
			sway = 0.1f;
			actionQueue.Clear();
			SetState(State.IDLE, 1f);
		}

		public void Initialise(IMapManager map)
		{
			currentTile = map.GetStartTile();
			if (null == map || -1 == currentTile) { Debug.LogError("Initialize: Invalid setup"); return; }

			transform.position = targetPosition = map.TileWorldPosition(currentTile);
			var yaw = map.Waypoints?.Length > 1 ? Navigation.DirToAngle(Navigation.NavToDest(map, map.Waypoints[0].tile, map.Waypoints[1].tile)) : 0f;
			transform.rotation = Quaternion.Euler(0f, yaw, 0f);
		}

		private void SetState(State state, float duration = 0f)
		{
			currentState = state;
			stateTimer = 0f;
			stateDuration = duration;
		}

		public void UpdateEggbot(IMapManager map)
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

				var destinationTile = map.Waypoints[dstWaypoint].tile;
				if (TestSpin(destinationTile)) return;
				if (TestMove(destinationTile)) return;
				if (TestTurn(destinationTile)) return;
				SetState(State.IDLE, 1f);

				bool TestSpin(int destinationTile)
				{
					if (null == map) return false;
					if (currentTile != destinationTile || (destinationTile != map.GetEndTile() && destinationTile != map.GetStartTile())) return false;
					if (destinationTile == map.GetEndTile()) { OnLevelCompleted?.Invoke(); }
					dstWaypoint = (dstWaypoint + 1) % map.Waypoints.Length;
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
						OnWaypointReached?.Invoke(dstWaypoint);
						dstWaypoint = (dstWaypoint + 1) % map.Waypoints.Length;
						return false;
					}

					var direction = Navigation.NavToDest(map, currentTile, destinationTile);
					if (0 == direction || 0 != (int)Mathf.DeltaAngle(transform.eulerAngles.y, Navigation.DirToAngle(direction))) return false;
					isBlocked = false;

					startPosition = map.TileWorldPosition(currentTile);
					var prevTargetPosition = targetPosition;
					var prevCurrentTile = currentTile;
					currentTile = Navigation.LineOfSight(map, currentTile, destinationTile, direction);
					targetPosition = map.TileWorldPosition(currentTile);
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
		}

		/// <summary>
		/// Called when the map origin shifts due to expand/crop.
		/// Updates currentTile and snaps position to new grid.
		/// </summary>
		/// <param name="originDelta">World-space shift of the map origin (in tile units)</param>
		public void OnMapOriginShift(IMapManager map, Vector3 originDelta)
		{
			if (originDelta == Vector3.zero) return;

			int deltaX = Mathf.RoundToInt(originDelta.x);
			int deltaZ = Mathf.RoundToInt(originDelta.z);

			if (deltaX == 0 && deltaZ == 0) return;

			// Get current world position before we lose context
			Vector3 oldWorldPos = transform.position;

			// Calculate old grid position (reverse-engineer from world pos)
			// This works because TileWorldPosition(index) = (x, 0, z) + tile_origin
			Vector3 localPos = oldWorldPos - MapManager.tile_origin; // remove origin offset
			int oldX = Mathf.RoundToInt(localPos.x);
			int oldZ = Mathf.RoundToInt(localPos.z);

			// Apply delta
			int newX = oldX + deltaX;
			int newZ = oldZ + deltaZ;

			var mapManager = map;

			if (newX < 0 || newX >= mapManager.Width || newZ < 0 || newZ >= mapManager.Height)
			{
				// Eggbot was cropped out — snap to nearest valid tile or start?
				currentTile = mapManager.GetStartTile();
			}
			else
			{
				currentTile = newZ * mapManager.Width + newX;
			}

			// Snap position to new grid
			transform.position = mapManager.TileWorldPosition(currentTile);

			// Optional: preserve sub-tile offset (e.g. during movement)
			// But for editor, we want clean snap
		}

		public static EggbotController Instantiate(string costume = "Eggbot Default", Transform parent = null)
		{
			costume = string.IsNullOrEmpty(costume) ? "Eggbot Default" : costume;
			var eggbotController = new GameObject($"Eggbot: {costume}");

			if (null != parent) eggbotController.transform.SetParent(parent, false);

			var def = ResourceManager.Definitions.FirstOrDefault(td => td.id == "Eggbot" && td.texture == costume);
			if (null == def?.model) { Debug.LogError("Initialize: Invalid Eggbot geometry"); return null; }

			var mesh = GeometryManager.InstantiatePrefab(def, eggbotController.transform, Vector3.zero);
			mesh.name = "Mesh";

			return eggbotController.AddComponent<EggbotController>();
		}
	}
}