// Copyright 2019 massivehadron.com ltd. ... (your header)

using UnityEngine;
using InputSystem = UnityEngine.InputSystem;   // ← Aliased
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class MultiTouchEmulator
	{
		private static Dictionary<int, Touch> map = new();
		private const float EPSILON = 1e-6f;

		public static Touch[] touches
		{
			get
			{
				var old = map;
				var current = ComputeCurrentSimulatedTouches(old);
				var result = new List<Touch>();

				bool wasSimulatedPinch = old != null && IsExactlySimulatedPinch(old);

				if (wasSimulatedPinch && !current.ContainsKey(0) && !current.ContainsKey(1))
				{
					Vector2 finalPos = InputSystem.Mouse.current?.position.ReadValue() ?? Vector2.zero;
					const float arbitraryDelta = 0.5f;
					Vector2 arbitraryLeft = Vector2.left * arbitraryDelta;
					Vector2 arbitraryRight = Vector2.right * arbitraryDelta;

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

				if (old != null)
				{
					foreach (var kvp in old)
					{
						if (!current.ContainsKey(kvp.Key))
						{
							var ended = kvp.Value;
							ended.phase = UnityEngine.TouchPhase.Ended;   // ← Fixed
							result.Add(ended);
						}
					}
				}

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

		private static Dictionary<int, Touch> ComputeCurrentSimulatedTouches(Dictionary<int, Touch> previous)
		{
			var current = new Dictionary<int, Touch>();

			if (InputSystem.Mouse.current == null) return current;

			Vector2 mousePos = InputSystem.Mouse.current.position.ReadValue();
			Vector2 mouseDelta = InputSystem.Mouse.current.delta.ReadValue() * 16f;
			float scroll = InputSystem.Mouse.current.scroll.ReadValue().y;

			if (InputSystem.Mouse.current.leftButton.isPressed)
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
			else if (InputSystem.Mouse.current.rightButton.isPressed)
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

			if (Mathf.Abs(scroll) > 0.001f)
			{
				var pinchScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / 4f;
				var scaledScroll = scroll * pinchScale;
				var center = mousePos;

				var deltaLeft = Vector2.left * scaledScroll;
				var deltaRight = Vector2.right * scaledScroll;

				var pos0 = center;
				var pos1 = center;

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

			var sum = d0 + d1;
			var deltasCancel = sum.sqrMagnitude < EPSILON * EPSILON;
			var oppositeSignsX = Mathf.Abs(d0.x + d1.x) < EPSILON;
			var oppositeSignsY = Mathf.Abs(d0.y + d1.y) < EPSILON;
			var nearZeroY = Mathf.Abs(d0.y) < EPSILON && Mathf.Abs(d1.y) < EPSILON;

			return deltasCancel && oppositeSignsX && oppositeSignsY && nearZeroY;
		}

		private static UnityEngine.TouchPhase GetPhase(int id, Dictionary<int, Touch> oldMap)   // ← Also fixed return type
		{
			if (!oldMap.ContainsKey(id)) return UnityEngine.TouchPhase.Began;
			return oldMap[id].deltaPosition.sqrMagnitude > 0.0001f ? UnityEngine.TouchPhase.Moved : UnityEngine.TouchPhase.Stationary;
		}

		// OnGUI and DrawQuad remain unchanged
		public static void OnGUI() { /* your original OnGUI */ }
		private static void DrawQuad(Rect rect, Color color) { /* your original DrawQuad */ }
	}

	// You can delete TouchCacheController if InputXController already calls CommitCurrentToMap()
}