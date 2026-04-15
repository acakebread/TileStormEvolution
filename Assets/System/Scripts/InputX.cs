//#define MOBILE   // Comment this out for pure desktop/WebGL builds (no legacy input)

using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using InputSystem = UnityEngine.InputSystem;
using UnityEngine.InputSystem;

namespace MassiveHadronLtd
{
	public static class InputX
	{
		private const float WEBGL_MOUSE_SCALE = 0.50f;

		private static float ApplyMouseScale(float value)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
            return value * WEBGL_MOUSE_SCALE;
#else
			return value;
#endif
		}

		private static Vector2 ApplyMouseScale(Vector2 value)
		{
#if UNITY_WEBGL && !UNITY_EDITOR
            return value * WEBGL_MOUSE_SCALE;
#else
			return value;
#endif
		}

		// ===================================================================
		// Key API (New Input System)
		// ===================================================================
		public static bool GetKeyDown(Key key) => key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
		public static bool GetKey(Key key) => key != Key.None && Keyboard.current != null && Keyboard.current[key].isPressed;
		public static bool GetKeyUp(Key key) => key != Key.None && Keyboard.current != null && Keyboard.current[key].wasReleasedThisFrame;

		public static bool GetKeyDown(KeyCode key) => GetKeyDown(key.ToKey());
		public static bool GetKey(KeyCode key) => GetKey(key.ToKey());
		public static bool GetKeyUp(KeyCode key) => GetKeyUp(key.ToKey());

		// Central touch / mouse source
		public static Touch[] touches => Application.isMobilePlatform || Application.isConsolePlatform
			? Input.touches
			: MultiTouchEmulator.touches;

		public static int touchCount => Application.isMobilePlatform || Application.isConsolePlatform
			? Input.touchCount
			: MultiTouchEmulator.touches.Length;

		public static Vector3 mousePosition => getMousePosition;

		public static bool GetMouseButtonDown(int button)
		{
			var result = getMouseButtonDown(button);
			if (result)
			{
				mouseDownPos = mousePosition;
				staticClick = true;
			}
			return result;
		}

		public static bool GetMouseButton(int button)
		{
			var result = getMouseButton(button);
			if (result && Vector3.Distance(mousePosition, mouseDownPos) >= CLICK_THRESHOLD)
				staticClick = false;
			return result;
		}

		public static bool GetMouseButtonUp(int button) => getMouseButtonUp(button);

		public static float GetAxis(string axisName)
		{
			var result = getAxisUnscaled(axisName);
			if (Mathf.Abs(result) > 0.01f && axisName == "Mouse ScrollWheel")
				staticClick = false;
			return ApplyMouseScale(result);
		}

		//public static float GetAxisRaw(string axisName) => GetAxis(axisName);

		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);

		public static bool staticClick = false;
		private static Vector3 mouseDownPos;
		private const float CLICK_THRESHOLD = 3f;

		// Hold support
		private static readonly Dictionary<int, HoldState> holdStates = new();

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
			// Your original mobile hold logic (using Input.touches)
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

				if (touch.phase == UnityEngine.TouchPhase.Began)
				{
					st.isHeld = false;
					st.startTime = Time.time;
				}
				else if (touch.phase == UnityEngine.TouchPhase.Moved || touch.phase == UnityEngine.TouchPhase.Stationary)
				{
					if (!st.isHeld && Time.time - st.startTime >= HOLD_THRESHOLD)
						st.isHeld = true;
				}
				else if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
				{
					holdStates.Remove(btn);
				}
			}
#else
    // Desktop path - use GetMouseButtonDown / GetMouseButton
    for (int b = 0; b < 3; b++)
    {
        if (getMouseButton(b))
        {
            active.Add(b);
            if (!holdStates.TryGetValue(b, out var st))
            {
                st = new HoldState();
                holdStates[b] = st;
            }
            if (getMouseButtonDown(b))
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

			// Cleanup inactive holds
			foreach (var kv in holdStates.ToList())
			{
				if (!active.Contains(kv.Key))
					holdStates.Remove(kv.Key);
			}

			if (!getMouseButton(0) && !getMouseButton(1))
			{
				mouseDownPos = mousePosition;
				staticClick = true;
			}
		}

		public static bool GetMouseButtonHeld(int button)
		{
			return holdStates.TryGetValue(button, out var state) && state.isHeld;
		}

		public static void CancelHold(int button) => holdStates.Remove(button);

		// ────────────────────────────────────────────────
		// Platform-specific implementations
		// ────────────────────────────────────────────────
#if MOBILE
        private static Vector3 getMousePosition
        {
            get
            {
                var ts = touches;
				if (ts.Length == 0) return Mouse.current?.position.ReadValue() ?? Vector3.zero; //if (ts.Length == 0) return Vector3.zero;
                if (ts.Length == 1) return ts[0].position;
                Vector2 sum = Vector2.zero;
                foreach (var t in ts) sum += t.position;
                return sum / ts.Length;
            }
        }

        private static bool getMouseButtonDown(int button)
        {
            var ts = touches;
            if (ts.Length == 0) return false;
            if (LooksLikeActiveScroll(ts)) return false;
            if (button == 0)
                return ts.Any(t => t.fingerId == 0 && t.phase == UnityEngine.TouchPhase.Began) && !ts.Any(t => t.fingerId == 1);
            if (button == 1)
                return ts.Any(t => t.fingerId == 1 && t.phase == UnityEngine.TouchPhase.Began);
            return false;
        }

        private static bool getMouseButton(int button)
        {
            var ts = touches;
            if (ts.Length == 0) return false;
            if (LooksLikeActiveScroll(ts)) return true;
            if (button == 0)
                return ts.Any(t => t.fingerId == 0 && (t.phase == UnityEngine.TouchPhase.Began || t.phase == UnityEngine.TouchPhase.Moved || t.phase == UnityEngine.TouchPhase.Stationary))
                       && !ts.Any(t => t.fingerId == 1);
            if (button == 1)
                return ts.Any(t => t.fingerId == 1 && (t.phase == UnityEngine.TouchPhase.Began || t.phase == UnityEngine.TouchPhase.Moved || t.phase == UnityEngine.TouchPhase.Stationary));
            return false;
        }

        private static bool getMouseButtonUp(int button)
        {
            var ts = touches;
            if (ts.Length == 0) return false;
            if (LooksLikeActiveScroll(ts)) return false;
            if (button == 0)
                return ts.Any(t => t.fingerId == 0 && t.phase == UnityEngine.TouchPhase.Ended) && !ts.Any(t => t.fingerId == 1);
            if (button == 1)
                return ts.Any(t => t.fingerId == 1 && t.phase == UnityEngine.TouchPhase.Ended);
            return false;
        }

        private static bool LooksLikeActiveScroll(Touch[] ts)
        {
            if (ts.Length != 2) return false;
            var t0 = ts.FirstOrDefault(t => t.fingerId == 0);
            var t1 = ts.FirstOrDefault(t => t.fingerId == 1);
            if (t0.Equals(default(Touch)) || t1.Equals(default(Touch))) return false;

            bool bothActive = (t0.phase == UnityEngine.TouchPhase.Began || t0.phase == UnityEngine.TouchPhase.Moved) &&
                              (t1.phase == UnityEngine.TouchPhase.Began || t1.phase == UnityEngine.TouchPhase.Moved);

            if (bothActive)
            {
                Vector2 sumDelta = t0.deltaPosition + t1.deltaPosition;
                if (sumDelta.sqrMagnitude > 1e-5f) return false;
                if (t0.deltaPosition.sqrMagnitude < 1e-5f) return false;
                if (Mathf.Abs(t0.deltaPosition.y) > 1e-4f || Mathf.Abs(t1.deltaPosition.y) > 1e-4f)
                    return false;
                return true;
            }

            bool bothEndedSamePos = t0.phase == UnityEngine.TouchPhase.Ended &&
                                    t1.phase == UnityEngine.TouchPhase.Ended &&
                                    Vector2.Distance(t0.position, t1.position) < 0.01f &&
                                    Mathf.Approximately(Vector2.SqrMagnitude(t1.deltaPosition - t0.deltaPosition), 1f);
            return bothEndedSamePos;
        }

        private static float getAxisUnscaled(string axisName)
        {
            if (axisName != "Mouse ScrollWheel")
            {
                var mouseDelta = GetMouseDelta();
                if (axisName == "Mouse X") return mouseDelta.x;
                if (axisName == "Mouse Y") return mouseDelta.y;
            }

            var ts = touches;
            if (ts.Length != 2) return 0f;
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
        }

        private static bool IsValid(this Touch t) => t.phase != UnityEngine.TouchPhase.Canceled && t.phase != UnityEngine.TouchPhase.Ended;

        public static Vector2 GetMouseDelta()
        {
            var ts = touches;
            if (ts.Length == 0) return Vector2.zero;
            if (ts.Length == 1) return ts[0].deltaPosition;

            Vector2 sum = Vector2.zero;
            foreach (var t in ts)
                sum += t.deltaPosition;
            return ApplyMouseScale(sum / ts.Length);
        }

        public const float TOUCH_LOOK_COMPENSATION_SCALAR = 1f;
        public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 1f;

#else
		// Desktop / WebGL - Pure New Input System (no legacy code)
		private static Vector3 getMousePosition => Mouse.current?.position.ReadValue() ?? Vector3.zero;

		private static bool getMouseButtonDown(int button)
		{
			if (Mouse.current == null) return false;
			return button switch
			{
				0 => Mouse.current.leftButton.wasPressedThisFrame,
				1 => Mouse.current.rightButton.wasPressedThisFrame,
				2 => Mouse.current.middleButton.wasPressedThisFrame,
				_ => false
			};
		}

		private static bool getMouseButton(int button)
		{
			if (Mouse.current == null) return false;
			return button switch
			{
				0 => Mouse.current.leftButton.isPressed,
				1 => Mouse.current.rightButton.isPressed,
				2 => Mouse.current.middleButton.isPressed,
				_ => false
			};
		}

		private static bool getMouseButtonUp(int button)
		{
			if (Mouse.current == null) return false;
			return button switch
			{
				0 => Mouse.current.leftButton.wasReleasedThisFrame,
				1 => Mouse.current.rightButton.wasReleasedThisFrame,
				2 => Mouse.current.middleButton.wasReleasedThisFrame,
				_ => false
			};
		}

		private static float getAxisUnscaled(string axisName)
		{
			if (Mouse.current == null) return 0f;

			if (axisName == "Mouse X")
				return Mouse.current.delta.ReadValue().x;

			if (axisName == "Mouse Y")
				return Mouse.current.delta.ReadValue().y;

			if (axisName == "Mouse ScrollWheel")
				return Mouse.current.scroll.ReadValue().y;

			return 0f;
		}

		public static Vector2 GetMouseDelta()
		{
			if (Mouse.current == null) return Vector2.zero;
			return ApplyMouseScale(Mouse.current.delta.ReadValue());
		}

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
			var go = new GameObject("[InputX Controller]") { hideFlags = HideFlags.HideAndDontSave };
			instance = go.AddComponent<InputXController>();
			DontDestroyOnLoad(go);
		}

		private void LateUpdate()
		{
			InputX.UpdateHoldStates();
			//MultiTouchEmulator.CommitCurrentToMap();
		}
	}
}