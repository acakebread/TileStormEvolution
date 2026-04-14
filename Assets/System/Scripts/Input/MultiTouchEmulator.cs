using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class MultiTouchEmulator
	{
		// Authoritative map — holds the PREVIOUS frame's simulated touches
		private static Dictionary<int, Touch> map = new();

		private const float EPSILON = 1e-6f;

		public static Touch[] touches
		{
			get
			{
				var old = map;
				var current = ComputeCurrentSimulatedTouches(old);

				var result = new List<Touch>();

				var wasSimulatedPinch = old != null && IsExactlySimulatedPinch(old);

				// Pinch end cleanup (modifies old if needed)
				if (wasSimulatedPinch && !current.ContainsKey(0) && !current.ContainsKey(1))
				{
					var finalPos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
					const float arbitraryDelta = 0.5f;
					var arbitraryLeft = Vector2.left * arbitraryDelta;
					var arbitraryRight = Vector2.right * arbitraryDelta;

					if (old.ContainsKey(0))
					{
						var t0 = old[0];
						t0.position = finalPos;
						t0.deltaPosition = arbitraryLeft;
						old[0] = t0;
					}
					if (old.ContainsKey(1))
					{
						var t1 = old[1];
						t1.position = finalPos;
						t1.deltaPosition = arbitraryRight;
						old[1] = t1;
					}
				}

				// Ended touches from previous frame
				if (old != null)
				{
					foreach (var kvp in old)
					{
						if (!current.ContainsKey(kvp.Key))
						{
							var ended = kvp.Value;
							ended.phase = UnityEngine.TouchPhase.Ended;
							result.Add(ended);
						}
					}
				}

				// Current frame touches with corrected phase
				foreach (var kvp in current)
				{
					var touch = kvp.Value;
					touch.phase = old.ContainsKey(kvp.Key)
						? (touch.deltaPosition.sqrMagnitude > 0.0001f ? UnityEngine.TouchPhase.Moved : UnityEngine.TouchPhase.Stationary)
						: UnityEngine.TouchPhase.Began;

					result.Add(touch);
				}

				return result.ToArray();
			}
		}

		// Core shared logic
		private static Dictionary<int, Touch> ComputeCurrentSimulatedTouches(Dictionary<int, Touch> previous)
		{
			var current = new Dictionary<int, Touch>();

			var mouse = Mouse.current;
			if (mouse == null) return current;

			var mousePos = mouse.position.ReadValue();
			var mouseDelta = mouse.delta.ReadValue();// * 16f;
			float scroll = mouse.scroll.ReadValue().y;

			// LMB: single finger
			if (mouse.leftButton.isPressed)
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
			// RMB: secondary finger simulation
			else if (mouse.rightButton.isPressed)
			{
				current[0] = new Touch
				{
					fingerId = 0,
					position = mousePos,
					deltaPosition = mouseDelta,
					phase = GetPhase(0, previous),
					type = TouchType.Indirect
				};

				UnityEngine.TouchPhase phase1 = previous.ContainsKey(1) && previous[1].phase != UnityEngine.TouchPhase.Ended
					? UnityEngine.TouchPhase.Stationary
					: UnityEngine.TouchPhase.Began;

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

			// Scroll wheel: two-finger pinch
			if (Mathf.Abs(scroll) > 0.001f)
			{
				float pinchScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / 4f;
				float scaledScroll = scroll * pinchScale;

				Vector2 center = mousePos;
				Vector2 deltaLeft = Vector2.left * scaledScroll;
				Vector2 deltaRight = Vector2.right * scaledScroll;

				Vector2 pos0 = center;
				Vector2 pos1 = center;

				if (scroll < 0)
				{
					pos0 = center - deltaLeft;
					pos1 = center - deltaRight;
				}

				current[0] = new Touch
				{
					fingerId = 0,
					position = pos0,
					deltaPosition = deltaLeft,
					phase = previous.ContainsKey(0) ? UnityEngine.TouchPhase.Moved : UnityEngine.TouchPhase.Began,
					type = TouchType.Indirect
				};

				current[1] = new Touch
				{
					fingerId = 1,
					position = pos1,
					deltaPosition = deltaRight,
					phase = previous.ContainsKey(1) ? UnityEngine.TouchPhase.Moved : UnityEngine.TouchPhase.Began,
					type = TouchType.Indirect
				};
			}

			return current;
		}

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

		private static UnityEngine.TouchPhase GetPhase(int id, Dictionary<int, Touch> oldMap)
		{
			if (!oldMap.ContainsKey(id)) return UnityEngine.TouchPhase.Began;
			return oldMap[id].deltaPosition.sqrMagnitude > 0.0001f ? UnityEngine.TouchPhase.Moved : UnityEngine.TouchPhase.Stationary;
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