//#define MOBILE

using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class InputX
	{
		public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
		public static bool GetKey(KeyCode key) => Input.GetKey(key);
		public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);

		// ────────────────────────────────────────────────
		// Central touch source — this is the only place that decides real vs emulated
		// ────────────────────────────────────────────────
		public static Touch[] touches => Application.isMobilePlatform || Application.isConsolePlatform ? Input.touches : MultiTouchEmulator.touches;
		public static int touchCount => Application.isMobilePlatform || Application.isConsolePlatform ? Input.touchCount : MultiTouchEmulator.touches.Length;

		public static Vector3 mousePosition => getMousePosition;

		public static bool GetMouseButtonDown(int button)
		{
			var result = getMouseButtonDown(button);
			if (result)
			{
				mouseDownPos = mousePosition;
				mouseMovedBeyondThreshold = false;
			}
			return result;
		}

		public static bool GetMouseButton(int button)
		{
			var result = getMouseButton(button);
			if (result && Vector3.Distance(mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
				mouseMovedBeyondThreshold = true;
			return result;
		}

		public static bool GetMouseButtonUp(int button)
		{
			var result = getMouseButtonUp(button);
			return result;
		}

		public static float GetAxis(string axisName)
		{
			var result = getAxis(axisName);
			if (Mathf.Abs(result) > 0.01f && axisName == "Mouse ScrollWheel")
				mouseMovedBeyondThreshold = true;
			return result;
		}

		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);
		public static bool mouseMovedBeyondThreshold = false;

		private static Vector3 mouseDownPos;
		private const float CLICK_THRESHOLD = 3f;

		// ─── Long hold support ──────────────────────────────────────────────────

		private static readonly Dictionary<int, HoldState> holdStates = new Dictionary<int, HoldState>();

		private class HoldState
		{
			public bool isHeld;
			public float startTime;
		}

		private const float HOLD_THRESHOLD = 0.25f;

		internal static void UpdateHoldStates()
		{
			var active = new HashSet<int>();

#if MOBILE
			foreach (var touch in Input.touches)
			{
				int btn = -1;
				if (touch.fingerId == 0) btn = 0;
				else if (touch.fingerId == 1) btn = 1;
				if (btn < 0) continue;

				active.Add(btn);

				if (!holdStates.TryGetValue(btn, out var st))
				{
					st = new HoldState();
					holdStates[btn] = st;
				}

				if (touch.phase == TouchPhase.Began)
				{
					st.isHeld = false;
					st.startTime = Time.time;
				}
				else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
				{
					if (!st.isHeld && Time.time - st.startTime >= HOLD_THRESHOLD)
					{
						st.isHeld = true;
					}
				}
				else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
				{
					holdStates.Remove(btn);
				}
			}
#else
			for (int b = 0; b < 3; b++)
			{
				if (Input.GetMouseButton(b))
				{
					active.Add(b);

					if (!holdStates.TryGetValue(b, out var st))
					{
						st = new HoldState();
						holdStates[b] = st;
					}

					if (Input.GetMouseButtonDown(b))
					{
						st.isHeld = false;
						st.startTime = Time.time;
					}
					else if (!st.isHeld && Time.time - st.startTime >= HOLD_THRESHOLD)
					{
						st.isHeld = true;
					}
				}
			}
#endif

			// Remove released buttons
			foreach (var kv in holdStates.ToList())
			{
				if (!active.Contains(kv.Key))
					holdStates.Remove(kv.Key);
			}
		}

		public static bool GetMouseButtonHeld(int button)
		{
			if (holdStates.TryGetValue(button, out var state))
			{
				return state.isHeld;
			}
			return false;
		}

		// Optional future escape hatch if stale holds become a problem
		public static void CancelHold(int button)
		{
			holdStates.Remove(button);
		}

		// ────────────────────────────────────────────────
		// Platform-specific implementations (unchanged)
		// ────────────────────────────────────────────────

#if MOBILE

		private static Vector3 getMousePosition
		{
			get
			{
				var ts = touches;
				if (ts.Length == 0)
					return Input.mousePosition;
				if (ts.Length == 1)
					return ts[0].position;
				Vector2 sum = Vector2.zero;
				foreach (var t in ts)
					sum += t.position;
				return sum / ts.Length;
			}
		}

		private static bool getMouseButtonDown(int button)
		{
			var ts = touches;
			if (0 == ts.Length) return false;
			if (LooksLikeActiveScroll(ts)) return false;

			if (button == 0)
			{
				return ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Began)
					   && !ts.Any(t => t.fingerId == 1);
			}

			if (button == 1)
			{
				return ts.Any(t => t.fingerId == 1 && (t.phase == TouchPhase.Began /* || t.phase == TouchPhase.Stationary */ ));
			}

			return false;
		}

		private static bool getMouseButton(int button)
		{
			var ts = touches;
			if (0 == ts.Length) return false;

			if (LooksLikeActiveScroll(ts))
				return true;

			if (button == 0)
			{
				return ts.Any(t => t.fingerId == 0 && (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
					   && !ts.Any(t => t.fingerId == 1);
			}

			if (button == 1)
			{
				return ts.Any(t => t.fingerId == 1 && (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary));
			}

			return false;
		}

		private static bool getMouseButtonUp(int button)
		{
			var ts = touches;
			if (0 == ts.Length) return false;
			if (LooksLikeActiveScroll(ts))
				return false;

			if (button == 0)
			{
				if (ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended) && !ts.Any(t => t.fingerId == 1))
					return true;
			}

			if (button == 1)
			{
				if (ts.Any(t => t.fingerId == 1 && t.phase == TouchPhase.Ended))
					return true;
			}

			return false;
		}

		private static bool LooksLikeActiveScroll(Touch[] ts)
		{
			if (ts.Length != 2) return false;

			var t0 = ts.FirstOrDefault(t => t.fingerId == 0);
			var t1 = ts.FirstOrDefault(t => t.fingerId == 1);

			if (t0.Equals(default(Touch)) || t1.Equals(default(Touch))) return false;

			bool bothActive = (t0.phase == TouchPhase.Began || t0.phase == TouchPhase.Moved) &&
							  (t1.phase == TouchPhase.Began || t1.phase == TouchPhase.Moved);

			if (bothActive)
			{
				Vector2 sumDelta = t0.deltaPosition + t1.deltaPosition;

				if (sumDelta.sqrMagnitude > 1e-5f) return false;
				if (t0.deltaPosition.sqrMagnitude < 1e-5f) return false;
				if (Mathf.Abs(t0.deltaPosition.y) > 1e-4f || Mathf.Abs(t1.deltaPosition.y) > 1e-4f)
					return false;

				return true;
			}

			bool bothEndedSamePos =
				t0.phase == TouchPhase.Ended &&
				t1.phase == TouchPhase.Ended &&
				Vector2.Distance(t0.position, t1.position) < 0.01f &&
				Mathf.Approximately(Vector2.SqrMagnitude(t1.deltaPosition - t0.deltaPosition), 1f);

			if (bothEndedSamePos) return true;

			return false;
		}

		private static float getAxis(string axisName)
		{
			if (axisName != "Mouse ScrollWheel")
			{
				var mouseDelta = GetMouseDelta();
				if (axisName == "Mouse X")
					return mouseDelta.x;
				if (axisName == "Mouse Y")
					return mouseDelta.y;
			}

			var ts = touches;
			if (ts.Length != 2)
				return 0;

			var t0 = ts.FirstOrDefault(t => t.fingerId == 0);
			var t1 = ts.FirstOrDefault(t => t.fingerId == 1);

			if (!t0.IsValid() || !t1.IsValid()) return 0f;

			Vector2 prev0 = t0.position;
			Vector2 prev1 = t1.position;
			Vector2 curr0 = t0.position + t0.deltaPosition;
			Vector2 curr1 = t1.position + t1.deltaPosition;

			float distPrev = Vector2.Distance(prev0, prev1);
			float distCurr = Vector2.Distance(curr0, curr1);

			float deltaDist = distCurr - distPrev;

			float scrollSensitivity = 1f / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
			return deltaDist * scrollSensitivity;

			static Vector3 GetMouseDelta()
			{
				var ts = touches;
				if (ts.Length == 0)
					return Vector3.zero;
				if (ts.Length == 1)
					return ts[0].deltaPosition;
				Vector2 sum = Vector2.zero;
				foreach (var t in ts)
					sum += t.deltaPosition;
				return sum / ts.Length;
			}
		}

		private static bool IsValid(this Touch t) => t.phase != TouchPhase.Canceled && t.phase != TouchPhase.Ended;

		public const float TOUCH_LOOK_COMPENSATION_SCALAR = 1f;
		public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 2f;

#else
		private static Vector3 getMousePosition => Input.mousePosition;
		private static bool getMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
		private static bool getMouseButton(int button) => Input.GetMouseButton(button);
		private static bool getMouseButtonUp(int button) => Input.GetMouseButtonUp(button);
		private static float getAxis(string axisName) => Input.GetAxis(axisName);

		public const float TOUCH_LOOK_COMPENSATION_SCALAR = 1f;
		public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 1f;
#endif
	}

	internal class InputXController : MonoBehaviour
	{
		private static InputXController instance;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void CreateInstance()
		{
			if (instance != null) return;

			var go = new GameObject("[InputX Controller]");
			go.hideFlags = HideFlags.HideAndDontSave;
			instance = go.AddComponent<InputXController>();
			DontDestroyOnLoad(go);
		}

		private void LateUpdate()
		{
			InputX.UpdateHoldStates();
			MultiTouchEmulator.CommitCurrentToMap();
		}
	}
}

////#define MOBILE

//using UnityEngine;
//using System.Linq;

//namespace MassiveHadronLtd
//{
//	public static class InputX
//	{
//		public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
//		public static bool GetKey(KeyCode key) => Input.GetKey(key);
//		public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);

//		// ────────────────────────────────────────────────
//		// Central touch source — this is the only place that decides real vs emulated
//		// ────────────────────────────────────────────────
//		public static Touch[] touches => Application.isMobilePlatform || Application.isConsolePlatform ? Input.touches : MultiTouchEmulator.touches;// Editor / standalone / anything else → use emulator
//		public static int touchCount => Application.isMobilePlatform || Application.isConsolePlatform ? Input.touchCount : MultiTouchEmulator.touches.Length;// Editor / standalone / anything else → use emulator

//		public static Vector3 mousePosition => getMousePosition;
//		public static bool GetMouseButtonDown(int button)
//		{
//			var result = getMouseButtonDown(button);
//			if (result)
//			{
//				mouseDownPos = mousePosition;
//				mouseMovedBeyondThreshold = false;
//			}
//			return result;
//		}
//		public static bool GetMouseButton(int button)
//		{
//			var result = getMouseButton(button);
//			if (result && Vector3.Distance(mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
//				mouseMovedBeyondThreshold = true;
//			return result;
//		}
//		public static bool GetMouseButtonUp(int button)
//		{
//			var result = getMouseButtonUp(button);
//			return result;
//		}
//		public static float GetAxis(string axisName)
//		{
//			var result = getAxis(axisName);
//			if (Mathf.Abs(result) > 0.01f && axisName == "Mouse ScrollWheel")
//				mouseMovedBeyondThreshold = true;
//			return result;
//		}

//		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);
//		public static bool mouseMovedBeyondThreshold = false;

//		private static Vector3 mouseDownPos;
//		private const float CLICK_THRESHOLD = 3f;

//#if MOBILE //#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)

//		// Mouse position: average of active touches, fallback to real mouse
//		private static Vector3 getMousePosition
//		{
//			get
//			{
//				var ts = touches;
//				if (ts.Length == 0)
//					return Input.mousePosition;
//				if (ts.Length == 1)
//					return ts[0].position;
//				// 2+ touches → average position (good for orbit + pinch)
//				Vector2 sum = Vector2.zero;
//				foreach (var t in ts)
//					sum += t.position;
//				return sum / ts.Length;
//			}
//		}

//		private static bool getMouseButtonDown(int button)
//		{
//			var ts = touches;
//			if (0 == ts.Length) return false;
//			if (LooksLikeActiveScroll(ts)) return false;  // ignore pure scroll frames for button events

//			if (button == 0)
//			{
//				// LMB: only finger 0 began, no finger 1
//				return ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Began)
//					   && !ts.Any(t => t.fingerId == 1);
//			}

//			if (button == 1)
//			{
//				// RMB down: finger 1 appeared (Began or first Stationary)
//				return ts.Any(t => t.fingerId == 1 && (t.phase == TouchPhase.Began /* || t.phase == TouchPhase.Stationary */ ));
//			}

//			return false;
//		}

//		private static bool getMouseButton(int button)
//		{
//			var ts = touches;
//			if (0 == ts.Length) return false;
//			//if (LooksLikeActiveScroll(ts)) return false;  // ignore pure scroll frames for button events - update: skip this or we get rogue no mouse button held events

//			if (LooksLikeActiveScroll(ts))
//				return true;

//			if (button == 0)
//			{
//				// LMB held: finger 0 active, no finger 1 present
//				return ts.Any(t => t.fingerId == 0 && (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
//					   && !ts.Any(t => t.fingerId == 1);
//			}

//			if (button == 1)
//			{
//				// RMB held: finger 1 exists (marker) + finger 0 active
//				//var finger1 = ts.FirstOrDefault(t => t.fingerId == 1);
//				//bool hasMarker = finger1.fingerId == 1 && (finger1.phase == TouchPhase.Began || finger1.phase == TouchPhase.Stationary);
//				//bool finger0Active = ts.Any(t => t.fingerId == 0 && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary));
//				//return hasMarker && finger0Active;
//				return ts.Any(t => t.fingerId == 1 && (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary));
//			}

//			return false;
//		}

//		private static bool getMouseButtonUp(int button)
//		{
//			var ts = touches;
//			if (0 == ts.Length) return false;
//			if (LooksLikeActiveScroll(ts))
//				return false;  // ignore pure scroll frames for button events

//			if (button == 0)
//			{
//				//return ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended) && !ts.Any(t => t.fingerId == 1);
//				if (ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended) && !ts.Any(t => t.fingerId == 1))
//					return true;
//				//if (ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended))
//				//	return true;
//			}

//			if (button == 1)
//			{
//				// RMB up: finger 1 Ended this frame (marker disappeared)
//				//return ts.Any(t => t.fingerId == 1 && t.phase == TouchPhase.Ended);
//				if (ts.Any(t => t.fingerId == 1 && t.phase == TouchPhase.Ended))
//					return true;
//			}

//			return false;
//		}

//		private static bool LooksLikeActiveScroll(Touch[] ts)
//		{
//			if (ts.Length != 2) return false;

//			// Find the two touches — must be exactly finger 0 and 1
//			var t0 = ts.FirstOrDefault(t => t.fingerId == 0);
//			var t1 = ts.FirstOrDefault(t => t.fingerId == 1);

//			// Missing one of them → cannot be emulator pinch
//			if (t0.Equals(default(Touch)) || t1.Equals(default(Touch))) return false;

//			// ── Case 1: Active scroll frame ─────────────────────────────────────
//			bool bothActive = (t0.phase == TouchPhase.Began || t0.phase == TouchPhase.Moved) &&
//							  (t1.phase == TouchPhase.Began || t1.phase == TouchPhase.Moved);

//			if (bothActive)
//			{
//				Vector2 sumDelta = t0.deltaPosition + t1.deltaPosition;

//				// Deltas must almost perfectly cancel (emulator does exact opposite horizontal)
//				if (sumDelta.sqrMagnitude > 1e-5f) return false;

//				// Non-trivial movement (not stationary noise)
//				if (t0.deltaPosition.sqrMagnitude < 1e-5f) return false;

//				// Emulator only moves horizontally → y components near zero
//				if (Mathf.Abs(t0.deltaPosition.y) > 1e-4f || Mathf.Abs(t1.deltaPosition.y) > 1e-4f)
//					return false;

//				//// Optional but very strong: reconstructed center should match
//				//Vector2 center0 = t0.position + t0.deltaPosition;
//				//Vector2 center1 = t1.position + t1.deltaPosition;
//				//if (Vector2.Distance(center0, center1) > 0.01f) return false;

//				return true;
//			}

//			//// ── Case 2: Pinch just ended this frame (snap happened) ──────────────
//			//bool bothSnappedEnd =
//			//	t0.deltaPosition == Vector2.zero &&
//			//	t1.deltaPosition == Vector2.zero &&
//			//	(t0.phase == TouchPhase.Stationary || t0.phase == TouchPhase.Moved) &&  // Moved possible if threshold borderline
//			//	(t1.phase == TouchPhase.Stationary || t1.phase == TouchPhase.Moved) &&
//			//	Vector2.Distance(t0.position, t1.position) < 0.01f;  // identical after snap

//			//if (bothSnappedEnd) return true;

//			// ── Case 3: The final Ended frame (both ended same frame, same pos) ──
//			bool bothEndedSamePos =
//				t0.phase == TouchPhase.Ended &&
//				t1.phase == TouchPhase.Ended &&
//				Vector2.Distance(t0.position, t1.position) < 0.01f &&
//				Mathf.Approximately(Vector2.SqrMagnitude(t1.deltaPosition - t0.deltaPosition), 1f);

//			if (bothEndedSamePos) return true;

//			// Anything else cannot come from the emulator → not fake scroll
//			return false;
//		}

//		// ────────────────────────────────────────────────
//		// Scroll wheel emulation is the tricky part
//		// We try to reconstruct something close to original scroll from pinch movement
//		// ────────────────────────────────────────────────
//		private static float getAxis(string axisName)
//		{
//			if (axisName != "Mouse ScrollWheel")
//			{
//				var mouseDelta = GetMouseDelta();
//				if (axisName == "Mouse X")
//					return mouseDelta.x;
//				if (axisName == "Mouse Y")
//					return mouseDelta.y;
//			}

//			var ts = touches;
//			if (ts.Length != 2)
//				return 0;

//			// Try to estimate scroll direction & magnitude from pinch distance change
//			var t0 = ts.FirstOrDefault(t => t.fingerId == 0);
//			var t1 = ts.FirstOrDefault(t => t.fingerId == 1);

//			if (!t0.IsValid() || !t1.IsValid()) return 0f;

//			Vector2 prev0 = t0.position;
//			Vector2 prev1 = t1.position;
//			Vector2 curr0 = t0.position + t0.deltaPosition;
//			Vector2 curr1 = t1.position + t1.deltaPosition;

//			float distPrev = Vector2.Distance(prev0, prev1);
//			float distCurr = Vector2.Distance(curr0, curr1);

//			float deltaDist = distCurr - distPrev;

//			// Map pinch-out → positive scroll (zoom in)
//			// pinch-in  → negative scroll (zoom out)
//			float scrollSensitivity = 1f / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
//			return deltaDist * scrollSensitivity;

//			static Vector3 GetMouseDelta()
//			{
//				var ts = touches;
//				if (ts.Length == 0)
//					return Vector3.zero;
//				if (ts.Length == 1)
//					return ts[0].deltaPosition;
//				// 2+ touches → average position (good for orbit + pinch)
//				Vector2 sum = Vector2.zero;
//				foreach (var t in ts)
//					sum += t.deltaPosition;
//				return sum / ts.Length;
//			}
//		}

//		// Helpers
//		private static bool IsValid(this Touch t) => t.phase != TouchPhase.Canceled && t.phase != TouchPhase.Ended;

//		public const float TOUCH_LOOK_COMPENSATION_SCALAR = 1f;//temporary workaround
//		public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 2f;//temporary workaround

//#else
//		private static Vector3 getMousePosition => Input.mousePosition;
//		private static bool getMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
//		private static bool getMouseButton(int button) => Input.GetMouseButton(button);
//		private static bool getMouseButtonUp(int button) => Input.GetMouseButtonUp(button);
//		private static float getAxis(string axisName) => Input.GetAxis(axisName);

//		public const float TOUCH_LOOK_COMPENSATION_SCALAR = 1f;//temporary workaround
//		public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 1f;//temporary workaround
//#endif
//	}
//}
