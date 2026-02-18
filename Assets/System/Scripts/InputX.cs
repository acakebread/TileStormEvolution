#if UNITY_IOS || UNITY_ANDROID

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
			{
				return Input.touches;
			}

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
				{
					return Input.mousePosition;
				}
				if (ts.Length == 1)
				{
					return ts[0].position;
				}
				// 2+ touches → average position (good for orbit + pinch)
				Vector2 sum = Vector2.zero;
				foreach (var t in ts)
				{
					sum += t.position;
				}
				return sum / ts.Length;
			}
		}

		public static bool GetMouseButtonDown(int button)
		{
			var ts = GetTouches();

			if (button == 0) // left ~ primary touch began
			{
				//return ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Began);
				return ts.Length == 1 && ts[0].fingerId == 0 && ts[0].phase == TouchPhase.Began;
			}

			if (button == 1) // right ~ secondary touch began (or two touches appeared)
			{
				// Typical right-mouse emulation gives two touches at Began together
				var beganTouches = ts.Where(t => t.phase == TouchPhase.Began).ToArray();
				return beganTouches.Length >= 1 && beganTouches.All(t => t.phase == TouchPhase.Began);
			}

			return false;
		}

		public static bool GetMouseButton(int button)
		{
			//do not attempt to emulate RMB right now
			if (button == 1)
				return Input.GetMouseButton(button);
			var ts = GetTouches();
			if (ts.Length == 1)
				return ts[0].phase == TouchPhase.Moved || ts[0].phase == TouchPhase.Stationary;
			return false;
		}

		//public static bool GetMouseButton(int button)
		//{
		//	var ts = GetTouches();

		//	if (button == 0) // left ~ primary touch active
		//	{
		//		return ts.Any(t => t.fingerId == 0 &&
		//						  (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary));
		//	}

		//	if (button == 1) // right ~ secondary touch active (or two similar touches)
		//	{
		//		var active = ts.Where(t => t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary).ToArray();
		//		if (active.Length == 0) return false;

		//		if (active.Length == 1)
		//		{
		//			return active[0].fingerId == 1;
		//		}

		//		// Two touches → check they are roughly at same place (right mouse style)
		//		if (active.Length >= 2)
		//		{
		//			var pos0 = active[0].position;
		//			bool allClose = active.All(t => Vector2.Distance(t.position, pos0) < 40f);
		//			return allClose;
		//		}
		//	}

		//	return false;
		//}

		public static bool GetMouseButtonUp(int button)
		{
			var ts = GetTouches();

			if (button == 0)
			{
				return ts.Length == 1 && ts[0].fingerId == 0 && ts[0].phase == TouchPhase.Ended;
				//return ts.Any(t => t.fingerId == 0 && t.phase == TouchPhase.Ended);
			}

			if (button == 1)
			{
				return ts.Length == 2 && ts[0].fingerId == 0 && ts[0].phase != TouchPhase.Ended && ts[1].fingerId == 1 && ts[1].phase == TouchPhase.Ended;


				//if (ts.Any(t => t.fingerId == 1 && t.phase == TouchPhase.Ended) ||
				//	   (ts.Length == 0 && Input.GetMouseButtonUp(1)))
				//	return true;

				//return ts.Any(t => t.fingerId == 1 && t.phase == TouchPhase.Ended) ||
				//	   (ts.Length == 0 && Input.GetMouseButtonUp(1)); // fallback for clean release
			}

			return false;
		}

		// ────────────────────────────────────────────────
		// Scroll wheel emulation is the tricky part
		// We try to reconstruct something close to original scroll from pinch movement
		// ────────────────────────────────────────────────
		public static float GetAxis(string axisName)
		{
			if (axisName != "Mouse ScrollWheel")
			{
				return Input.GetAxis(axisName);
			}

			var ts = GetTouches();
			if (ts.Length != 2)
			{
				// No pinch emulation active → use real scroll
				return Input.GetAxis("Mouse ScrollWheel");
			}

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
			// Tune multiplier to match how much legacy code expects per notch
			const float scrollSensitivity = 1f;// 0.1f; // ≈ 3–4× typical mouse notch

			return deltaDist * scrollSensitivity;
		}

		// Helpers
		private static bool IsValid(this Touch t) => t.phase != TouchPhase.Canceled && t.phase != TouchPhase.Ended;

		public static bool mouseInsideWindow =>
			new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);

		public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
		public static bool GetKey(KeyCode key) => Input.GetKey(key);
		public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);
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
	}
}

#endif