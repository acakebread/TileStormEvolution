#define MOBILE
#if MOBILE
//#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)

using UnityEngine;
using System.Linq;

namespace MassiveHadronLtd
{
	public static class InputX
	{
		// ────────────────────────────────────────────────
		// Central touch source — this is the only place that decides real vs emulated
		// ────────────────────────────────────────────────
		private static Touch[] GetTouches()
		{
			if (Application.isMobilePlatform || Application.isConsolePlatform)
				return Input.touches;
			// Editor / standalone / anything else → use emulator
			return MultiTouchEmulator.touches;
		}

		// ────────────────────────────────────────────────
		// Public API — everything else reads from GetTouches()
		// ────────────────────────────────────────────────
		public static int touchCount => GetTouches().Length;

		public static Touch[] touches => GetTouches();

		// Mouse position: average of active touches, fallback to real mouse
		public static Vector3 mousePosition
		{
			get
			{
				var ts = GetTouches();
				if (ts.Length == 0)
					return Input.mousePosition;
				if (ts.Length == 1)
					return ts[0].position;
				// 2+ touches → average position (good for orbit + pinch)
				Vector2 sum = Vector2.zero;
				foreach (var t in ts)
					sum += t.position;
				return sum / ts.Length;
			}
		}

		public static bool GetMouseButtonDown(int button)
		{
			var ts = GetTouches();
			if (0 == ts.Length) return false;
			if (LooksLikeActiveScroll(ts)) return false;  // ignore pure scroll frames for button events

			if (button == 0)
			{
				// LMB: only finger 0 began, no finger 1
				return ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Began)
					   && !ts.Any(t => t.fingerId == 1);
			}

			if (button == 1)
			{
				// RMB down: finger 1 appeared (Began or first Stationary)
				return ts.Any(t => t.fingerId == 1 && (t.phase == TouchPhase.Began /* || t.phase == TouchPhase.Stationary */ ));
			}

			return false;
		}

		public static bool GetMouseButton(int button)
		{
			var ts = GetTouches();
			if (0 == ts.Length) return false;
			//if (LooksLikeActiveScroll(ts)) return false;  // ignore pure scroll frames for button events - update: skip this or we get rogue no mouse button held events

			if (button == 0)
			{
				// LMB held: finger 0 active, no finger 1 present
				return ts.Any(t => t.fingerId == 0 && (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
					   && !ts.Any(t => t.fingerId == 1);
			}

			if (button == 1)
			{
				// RMB held: finger 1 exists (marker) + finger 0 active
				//var finger1 = ts.FirstOrDefault(t => t.fingerId == 1);
				//bool hasMarker = finger1.fingerId == 1 && (finger1.phase == TouchPhase.Began || finger1.phase == TouchPhase.Stationary);
				//bool finger0Active = ts.Any(t => t.fingerId == 0 && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary));
				//return hasMarker && finger0Active;
				return ts.Any(t => t.fingerId == 1 && (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary));
			}

			return false;
		}

		public static bool GetMouseButtonUp(int button)
		{
			var ts = GetTouches();
			if (0 == ts.Length) return false;
			if (LooksLikeActiveScroll(ts))
				return false;  // ignore pure scroll frames for button events

			if (button == 0)
			{
				//return ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended) && !ts.Any(t => t.fingerId == 1);
				if (ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended) && !ts.Any(t => t.fingerId == 1))
					return true;
				//if (ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended))
				//	return true;
			}

			if (button == 1)
			{
				// RMB up: finger 1 Ended this frame (marker disappeared)
				//return ts.Any(t => t.fingerId == 1 && t.phase == TouchPhase.Ended);
				if (ts.Any(t => t.fingerId == 1 && t.phase == TouchPhase.Ended))
					return true;
			}

			return false;
		}

		private static bool LooksLikeActiveScroll(Touch[] ts)
		{
			if (ts.Length != 2) return false;

			// Find the two touches — must be exactly finger 0 and 1
			var t0 = ts.FirstOrDefault(t => t.fingerId == 0);
			var t1 = ts.FirstOrDefault(t => t.fingerId == 1);

			// Missing one of them → cannot be emulator pinch
			if (t0.Equals(default(Touch)) || t1.Equals(default(Touch))) return false;

			// ── Case 1: Active scroll frame ─────────────────────────────────────
			bool bothActive = (t0.phase == TouchPhase.Began || t0.phase == TouchPhase.Moved) &&
							  (t1.phase == TouchPhase.Began || t1.phase == TouchPhase.Moved);

			if (bothActive)
			{
				Vector2 sumDelta = t0.deltaPosition + t1.deltaPosition;

				// Deltas must almost perfectly cancel (emulator does exact opposite horizontal)
				if (sumDelta.sqrMagnitude > 1e-5f) return false;

				// Non-trivial movement (not stationary noise)
				if (t0.deltaPosition.sqrMagnitude < 1e-5f) return false;

				// Emulator only moves horizontally → y components near zero
				if (Mathf.Abs(t0.deltaPosition.y) > 1e-4f || Mathf.Abs(t1.deltaPosition.y) > 1e-4f)
					return false;

				// Optional but very strong: reconstructed center should match
				Vector2 center0 = t0.position + t0.deltaPosition;
				Vector2 center1 = t1.position + t1.deltaPosition;
				if (Vector2.Distance(center0, center1) > 0.01f) return false;

				return true;
			}

			//// ── Case 2: Pinch just ended this frame (snap happened) ──────────────
			//bool bothSnappedEnd =
			//	t0.deltaPosition == Vector2.zero &&
			//	t1.deltaPosition == Vector2.zero &&
			//	(t0.phase == TouchPhase.Stationary || t0.phase == TouchPhase.Moved) &&  // Moved possible if threshold borderline
			//	(t1.phase == TouchPhase.Stationary || t1.phase == TouchPhase.Moved) &&
			//	Vector2.Distance(t0.position, t1.position) < 0.01f;  // identical after snap

			//if (bothSnappedEnd) return true;

			// ── Case 3: The final Ended frame (both ended same frame, same pos) ──
			bool bothEndedSamePos =
				t0.phase == TouchPhase.Ended &&
				t1.phase == TouchPhase.Ended &&
				Vector2.Distance(t0.position, t1.position) < 0.01f &&
				Mathf.Approximately(Vector2.SqrMagnitude(t1.deltaPosition - t0.deltaPosition), 1f);

			if (bothEndedSamePos) return true;

			// Anything else cannot come from the emulator → not fake scroll
			return false;
		}

		private static Vector3 mouseDelta
		{
			get
			{
				var ts = GetTouches();
				if (ts.Length == 0)
					return Vector3.zero;
				if (ts.Length == 1)
					return ts[0].deltaPosition;
				// 2+ touches → average position (good for orbit + pinch)
				Vector2 sum = Vector2.zero;
				foreach (var t in ts)
					sum += t.deltaPosition;
				return sum / ts.Length;
			}
		}

		// ────────────────────────────────────────────────
		// Scroll wheel emulation is the tricky part
		// We try to reconstruct something close to original scroll from pinch movement
		// ────────────────────────────────────────────────
		public static float GetAxis(string axisName)
		{
			//if (axisName != "Mouse ScrollWheel")
			//	return Input.GetAxis(axisName);// * TOUCH_PINCH_MOUSE_WHEEL_NOMALISE_RATIO / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);//normalise

			if (axisName != "Mouse ScrollWheel")
			{
				if (axisName == "Mouse X")
					return mouseDelta.x;
				if (axisName == "Mouse Y")
					return mouseDelta.y;
			}

			var ts = GetTouches();
			if (ts.Length != 2)
				return 0;

			// Try to estimate scroll direction & magnitude from pinch distance change
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

			// Map pinch-out → positive scroll (zoom in)
			// pinch-in  → negative scroll (zoom out)
			float scrollSensitivity = 1f / Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
			return deltaDist * scrollSensitivity;
		}

		// Helpers
		private static bool IsValid(this Touch t) => t.phase != TouchPhase.Canceled && t.phase != TouchPhase.Ended;

		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);

		public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
		public static bool GetKey(KeyCode key) => Input.GetKey(key);
		public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);

		public const float TOUCH_LOOK_COMPENSATION_SCALAR = 1f;//temporary workaround
		public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 2f;//temporary workaround
	}
}

#else

using UnityEngine;

namespace MassiveHadronLtd
{
	public static class InputX
	{
		public static Vector3 mousePosition => Input.mousePosition;
		public static bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
		public static bool GetMouseButton(int button) => Input.GetMouseButton(button);
		public static bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);

		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);

		public static int touchCount => Input.touchCount;
		public static Touch[] touches => Input.touches;
		public static float GetAxis(string axisName) => Input.GetAxis(axisName);

		public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
		public static bool GetKey(KeyCode key) => Input.GetKey(key);
		public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);

		public const float TOUCH_LOOK_COMPENSATION_SCALAR = 16f;//temporary workaround
		public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 1f;//temporary workaround
	}
}

#endif