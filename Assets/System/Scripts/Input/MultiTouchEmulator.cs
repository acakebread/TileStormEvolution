// Copyright 2019 massivehadron.com ltd. created 03/11/2019 by Andrew Cakebread
// Pinch-zoom emulation added 2025/2026
// Refactored 2026: removed frame cache, prevent mid-frame overwrites, shared computation, commit in LateUpdate

using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class MultiTouchEmulator
	{
		// Authoritative map — holds the PREVIOUS frame's simulated touches
		private static Dictionary<int, Touch> map = new Dictionary<int, Touch>();

		private const float EPSILON = 1e-6f;

		public static Touch[] touches
		{
			get
			{
				var old = map;
				var current = ComputeCurrentSimulatedTouches(old);

				List<Touch> result = new List<Touch>();

				bool wasSimulatedPinch = old != null && IsExactlySimulatedPinch(old);

				// Pinch end cleanup (modifies old if needed)
				if (wasSimulatedPinch && !current.ContainsKey(0) && !current.ContainsKey(1))
				{
					Vector2 finalPos = Input.mousePosition;
					const float arbitraryDelta = 0.5f;
					Vector2 arbitraryLeft = Vector2.left * arbitraryDelta;
					Vector2 arbitraryRight = Vector2.right * arbitraryDelta;

					var t0 = old[0];
					t0.position = finalPos;
					t0.deltaPosition = arbitraryLeft;
					old[0] = t0;

					var t1 = old[1];
					t1.position = finalPos;
					t1.deltaPosition = arbitraryRight;
					old[1] = t1;
				}

				// Ended touches from previous frame
				if (old != null)
				{
					foreach (var kvp in old)
					{
						if (!current.ContainsKey(kvp.Key))
						{
							var ended = kvp.Value;
							ended.phase = TouchPhase.Ended;
							result.Add(ended);
						}
					}
				}

				// Current frame touches with corrected phase
				foreach (var kvp in current)
				{
					var touch = kvp.Value;
					touch.phase = old.ContainsKey(kvp.Key)
						? (touch.deltaPosition.sqrMagnitude > 0.0001f ? TouchPhase.Moved : TouchPhase.Stationary)
						: TouchPhase.Began;

					result.Add(touch);
				}

				return result.ToArray();
			}
		}

		// ────────────────────────────────────────────────
		// Core shared logic: compute what the simulated touches would be this frame
		// Returns the dictionary of active touches (without ended ones or phase overrides)
		// ────────────────────────────────────────────────
		private static Dictionary<int, Touch> ComputeCurrentSimulatedTouches(Dictionary<int, Touch> previous)
		{
			var current = new Dictionary<int, Touch>();

			Vector2 mousePos = Input.mousePosition;
			Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 16f;
			float scroll = Input.GetAxis("Mouse ScrollWheel");

			// ── LMB: single finger ──
			if (Input.GetMouseButton(0))
			{
				current[0] = new Touch
				{
					fingerId = 0,
					position = mousePos,
					deltaPosition = mouseDelta,
					phase = GetPhase(0, previous),
					type = TouchType.Direct
				};
			}
			// ── RMB: secondary finger simulation ──
			else if (Input.GetMouseButton(1))
			{
				current[0] = new Touch
				{
					fingerId = 0,
					position = mousePos,
					deltaPosition = mouseDelta,
					phase = GetPhase(0, previous),
					type = TouchType.Indirect
				};

				TouchPhase phase1 = previous.ContainsKey(1) && previous[1].phase != TouchPhase.Ended
					? TouchPhase.Stationary
					: TouchPhase.Began;

				current[1] = new Touch
				{
					fingerId = 1,
					position = mousePos,
					deltaPosition = mouseDelta,
					phase = phase1,
					tapCount = 0,
					type = TouchType.Indirect
				};
			}

			// ── Scroll wheel: two-finger pinch ──
			if (Mathf.Abs(scroll) > 0.001f)
			{
				float pinchScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / 4f;
				float scaledScroll = scroll * pinchScale;

				Vector2 center = mousePos;
				Vector2 deltaLeft = Vector2.left * scaledScroll;
				Vector2 deltaRight = Vector2.right * scaledScroll;

				Vector2 pos0 = center;
				Vector2 pos1 = center;

				if (scroll < 0) // zoom in → fingers move apart
				{
					pos0 = center - deltaLeft;
					pos1 = center - deltaRight;
				}

				current[0] = new Touch
				{
					fingerId = 0,
					position = pos0,
					deltaPosition = deltaLeft,
					phase = previous.ContainsKey(0) ? TouchPhase.Moved : TouchPhase.Began,
					type = TouchType.Indirect
				};

				current[1] = new Touch
				{
					fingerId = 1,
					position = pos1,
					deltaPosition = deltaRight,
					phase = previous.ContainsKey(1) ? TouchPhase.Moved : TouchPhase.Began,
					type = TouchType.Indirect
				};
			}

			return current;
		}

		// ────────────────────────────────────────────────
		// Commit the current frame's simulated state to map (for next frame)
		// Called once per frame from LateUpdate
		// ────────────────────────────────────────────────
		internal static void CommitCurrentToMap()
		{
			map = ComputeCurrentSimulatedTouches(map);
		}

		private static bool IsExactlySimulatedPinch(Dictionary<int, Touch> touches)
		{
			if (touches.Count != 2) return false;
			if (!touches.ContainsKey(0) || !touches.ContainsKey(1)) return false;

			var d0 = touches[0].deltaPosition;
			var d1 = touches[1].deltaPosition;

			if (d0.sqrMagnitude < EPSILON * EPSILON) return false;

			Vector2 sum = d0 + d1;

			bool deltasCancel = sum.sqrMagnitude < EPSILON * EPSILON;
			bool oppositeSignsX = Mathf.Abs(d0.x + d1.x) < EPSILON;
			bool oppositeSignsY = Mathf.Abs(d0.y + d1.y) < EPSILON;
			bool nearZeroY = Mathf.Abs(d0.y) < EPSILON && Mathf.Abs(d1.y) < EPSILON;

			return deltasCancel && oppositeSignsX && oppositeSignsY && nearZeroY;
		}

		private static TouchPhase GetPhase(int id, Dictionary<int, Touch> oldMap)
		{
			if (!oldMap.ContainsKey(id)) return TouchPhase.Began;
			return oldMap[id].deltaPosition.sqrMagnitude > 0.0001f ? TouchPhase.Moved : TouchPhase.Stationary;
		}

		// Debug visualization — shows previous frame's positions
		public static void OnGUI()
		{
			foreach (var kvp in map)
			{
				Color col = kvp.Key == 0 ? new Color(1, 0, 0, 0.6f) : new Color(0, 0.7f, 1, 0.6f);
				DrawQuad(new Rect(kvp.Value.position.x - 10, Screen.height - kvp.Value.position.y - 10, 20, 20), col);
			}
		}

		private static void DrawQuad(Rect rect, Color color)
		{
			Texture2D tex = new Texture2D(1, 1);
			tex.SetPixel(0, 0, color);
			tex.Apply();
			GUI.skin.box.normal.background = tex;
			GUI.Box(rect, GUIContent.none);
		}
	}

	// Auto-created singleton
	internal class TouchCacheController : MonoBehaviour
	{
		private static TouchCacheController instance;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void CreateInstance()
		{
			if (instance != null) return;

			var go = new GameObject("[MultiTouchEmulator Controller]");
			go.hideFlags = HideFlags.HideAndDontSave;
			instance = go.AddComponent<TouchCacheController>();
			DontDestroyOnLoad(go);
		}

		private void LateUpdate()
		{
			MultiTouchEmulator.CommitCurrentToMap();
		}
	}
}