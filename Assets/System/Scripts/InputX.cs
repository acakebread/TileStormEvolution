//#define MOBILE
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using InputSystem = UnityEngine.InputSystem;

namespace MassiveHadronLtd
{
	public static class InputX
	{
		// ────────────────────────────────────────────────
		// Safe Keyboard methods (no more KeyControl or Menu errors)
		// ────────────────────────────────────────────────
		public static bool GetKeyDown(KeyCode key) => GetKeyControl(key)?.wasPressedThisFrame ?? false;
		public static bool GetKey(KeyCode key) => GetKeyControl(key)?.isPressed ?? false;
		public static bool GetKeyUp(KeyCode key) => GetKeyControl(key)?.wasReleasedThisFrame ?? false;

		private static InputSystem.Controls.KeyControl GetKeyControl(KeyCode keyCode)
		{
			if (InputSystem.Keyboard.current == null) return null;

			// Mouse buttons are not keyboard keys
			if (keyCode == KeyCode.Mouse0 || keyCode == KeyCode.Mouse1 || keyCode == KeyCode.Mouse2)
				return null;

			switch (keyCode)
			{
				case KeyCode.LeftArrow: return InputSystem.Keyboard.current.leftArrowKey;
				case KeyCode.RightArrow: return InputSystem.Keyboard.current.rightArrowKey;
				case KeyCode.UpArrow: return InputSystem.Keyboard.current.upArrowKey;
				case KeyCode.DownArrow: return InputSystem.Keyboard.current.downArrowKey;

				case KeyCode.LeftShift: return InputSystem.Keyboard.current.leftShiftKey;
				case KeyCode.RightShift: return InputSystem.Keyboard.current.rightShiftKey;
				case KeyCode.LeftControl: return InputSystem.Keyboard.current.leftCtrlKey;
				case KeyCode.RightControl: return InputSystem.Keyboard.current.rightCtrlKey;
				case KeyCode.LeftAlt: return InputSystem.Keyboard.current.leftAltKey;
				case KeyCode.RightAlt: return InputSystem.Keyboard.current.rightAltKey;

				case KeyCode.Space: return InputSystem.Keyboard.current.spaceKey;
				case KeyCode.Return: return InputSystem.Keyboard.current.enterKey;
				case KeyCode.Escape: return InputSystem.Keyboard.current.escapeKey;
				case KeyCode.Tab: return InputSystem.Keyboard.current.tabKey;
				case KeyCode.Backspace: return InputSystem.Keyboard.current.backspaceKey;
				case KeyCode.Delete: return InputSystem.Keyboard.current.deleteKey;
				case KeyCode.KeypadEnter: return InputSystem.Keyboard.current.numpadEnterKey;

				case KeyCode.W: return InputSystem.Keyboard.current.wKey;
				case KeyCode.S: return InputSystem.Keyboard.current.sKey;
				case KeyCode.A: return InputSystem.Keyboard.current.aKey;
				case KeyCode.D: return InputSystem.Keyboard.current.dKey;
				case KeyCode.Q: return InputSystem.Keyboard.current.qKey;
				case KeyCode.E: return InputSystem.Keyboard.current.eKey;

				// Add more if your game uses them (F-keys, Home, PageUp/Down, etc.)
				default:
					return null;   // safe fallback - treated as not pressed
			}
		}

		// ────────────────────────────────────────────────
		// Central touch / mouse source
		// ────────────────────────────────────────────────
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
			var result = getAxis(axisName);
			if (Mathf.Abs(result) > 0.01f && axisName == "Mouse ScrollWheel")
				staticClick = false;
			return result;
		}

		public static float GetAxisRaw(string axisName) => GetAxis(axisName);

		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);

		public static bool staticClick = false;
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
                if (ts.Length == 0) return Vector3.zero;
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

        private static float getAxis(string axisName)
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

            static Vector3 GetMouseDelta()
            {
                var ts = touches;
                if (ts.Length == 0) return Vector3.zero;
                if (ts.Length == 1) return ts[0].deltaPosition;
                Vector2 sum = Vector2.zero;
                foreach (var t in ts) sum += t.deltaPosition;
                return sum / ts.Length;
            }
        }

        private static bool IsValid(this Touch t) => t.phase != UnityEngine.TouchPhase.Canceled && t.phase != UnityEngine.TouchPhase.Ended;

        public const float TOUCH_LOOK_COMPENSATION_SCALAR = 1f;
        public const float TOUCH_SCROLL_COMPENSATION_SCALAR = 2f;

#else
		// Desktop - New Input System
		private static Vector3 getMousePosition => InputSystem.Mouse.current?.position.ReadValue() ?? Vector3.zero;

		private static bool getMouseButtonDown(int button)
		{
			if (InputSystem.Mouse.current == null) return false;
			return button switch
			{
				0 => InputSystem.Mouse.current.leftButton.wasPressedThisFrame,
				1 => InputSystem.Mouse.current.rightButton.wasPressedThisFrame,
				2 => InputSystem.Mouse.current.middleButton.wasPressedThisFrame,
				_ => false
			};
		}

		private static bool getMouseButton(int button)
		{
			if (InputSystem.Mouse.current == null) return false;
			return button switch
			{
				0 => InputSystem.Mouse.current.leftButton.isPressed,
				1 => InputSystem.Mouse.current.rightButton.isPressed,
				2 => InputSystem.Mouse.current.middleButton.isPressed,
				_ => false
			};
		}

		private static bool getMouseButtonUp(int button)
		{
			if (InputSystem.Mouse.current == null) return false;
			return button switch
			{
				0 => InputSystem.Mouse.current.leftButton.wasReleasedThisFrame,
				1 => InputSystem.Mouse.current.rightButton.wasReleasedThisFrame,
				2 => InputSystem.Mouse.current.middleButton.wasReleasedThisFrame,
				_ => false
			};
		}

		private static float getAxis(string axisName)
		{
			if (InputSystem.Mouse.current == null) return 0f;

			if (axisName == "Mouse X") return GetMouseDelta().x;
			if (axisName == "Mouse Y") return GetMouseDelta().y;
			if (axisName == "Mouse ScrollWheel")
				return InputSystem.Mouse.current.scroll.ReadValue().y;

			return 0f;
		}

		public static Vector2 GetMouseDelta() => InputSystem.Mouse.current?.delta.ReadValue() ?? Vector2.zero;

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