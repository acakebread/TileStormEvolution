// Copyright 2019 massivehadron.com ltd. created 03/11/2019 by Andrew Cakebread
// Pinch-zoom emulation added 2025/2026

using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class MultiTouchEmulator
	{
		private static Vector2 dualMousePosition; // used by shift+left logic
		private static Dictionary<int, Touch> map = new Dictionary<int, Touch>();

		// ────────────────────────────────────────────────
		// New pinch emulation settings
		// ────────────────────────────────────────────────
		private const float PinchBaseSeparationSpeed = 180f;   // pixels per second at scroll = 1
		private const float PinchMinSeparation = 40f;     // don't collapse to zero
		private static float currentPinchSeparation = 120f;    // starting / resting distance between fakes

		public static Touch[] touches
		{
			get
			{
				Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
				Vector2 mousePos = Input.mousePosition;

				Dictionary<int, Touch> old = map;
				map = new Dictionary<int, Touch>();

				float scroll = Input.GetAxis("Mouse ScrollWheel");

				bool anyButtonDown = Input.GetMouseButton(0) || Input.GetMouseButton(1);
				bool shiftOrCtrlDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
									   Input.GetKey(KeyCode.LeftControl);

				// ────────────────────────────────────────────────
				// Priority: existing button logic first (don't break old behaviour)
				// ────────────────────────────────────────────────
				if (Input.GetMouseButton(0))
				{
					if (shiftOrCtrlDown)
					{
						// Original two-finger simulation with shift
						map[1] = new Touch { fingerId = 1, position = mousePos, deltaPosition = mouseDelta, phase = GetPhase(1, old) };

						Vector2 fakeDelta = mouseDelta;
						if (Input.GetKey(KeyCode.LeftControl)) fakeDelta *= 0.66f;
						else fakeDelta *= -1f;

						dualMousePosition += fakeDelta;
						map[0] = new Touch { fingerId = 0, position = dualMousePosition, deltaPosition = fakeDelta, phase = GetPhase(0, old) };
					}
					else
					{
						// Normal left drag → only finger 0
						dualMousePosition = mousePos;
						map[0] = new Touch { fingerId = 0, position = mousePos, deltaPosition = mouseDelta, phase = GetPhase(0, old) };
					}
				}
				else if (Input.GetMouseButton(1))
				{
					Vector2 pos = Input.mousePosition;
					Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

					// Use the phase from whichever finger was already known, or default to Moved/Began
					TouchPhase sharedPhase = GetPhase(0, old); // or merge logic if you prefer
					if (old.ContainsKey(1) && old[1].phase == TouchPhase.Began)
						sharedPhase = TouchPhase.Began;

					var baseTouch = new Touch
					{
						position = pos,
						deltaPosition = delta,
						phase = sharedPhase,
						tapCount = 0,           // usually irrelevant for emulator
						type = TouchType.Indirect,   // safe fallback for mouse-emulated / indirect input
					};

					// Both fake touches get **exactly the same** position + delta
					// → average movement in test class = full mouse delta
					// → distance between them stays constant → no accidental zoom
					map[0] = new Touch
					{
						fingerId = 0,
						position = baseTouch.position,
						deltaPosition = baseTouch.deltaPosition,
						phase = baseTouch.phase,
						tapCount = baseTouch.tapCount,
						type = baseTouch.type
					};

					map[1] = new Touch
					{
						fingerId = 1,
						position = baseTouch.position,
						deltaPosition = baseTouch.deltaPosition,
						phase = baseTouch.phase,
						tapCount = baseTouch.tapCount,
						type = baseTouch.type
					};
				}
				else if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && Mathf.Abs(scroll) > 0.001f)
				{
					// Raw scroll value (typically ~ ±0.1 to ±0.5 per notch)
					float rawScroll = Input.GetAxis("Mouse ScrollWheel");

					// Pixels of separation change per scroll unit — tune this (higher = faster zoom both ways)
					// Try 80–200; makes ~1 notch ≈ 10–25 pixels of finger separation change
					const float pixelsPerScrollUnit = 140f;

					// Change in half-distance (since we offset both sides)
					float halfDelta = rawScroll * pixelsPerScrollUnit * 0.5f;

					// Accumulate separation (prevents collapse to zero, keeps it positive)
					currentPinchSeparation = Mathf.Max(40f, currentPinchSeparation + halfDelta * 2f);

					Vector2 center = Input.mousePosition;

					// Current half-separation
					float halfSep = currentPinchSeparation * 0.5f;

					// Deltas point outward (positive scroll) or inward (negative scroll)
					Vector2 deltaDir = Vector2.right * Mathf.Sign(rawScroll); // left = -right for symmetry

					// For expand (positive scroll): deltas = outward
					// For shrink (negative): deltas = inward = opposite of outward
					Vector2 deltaThisFrame = deltaDir * Mathf.Abs(halfDelta);

					// Touch 0 on left, moving left if shrinking / right if expanding? Wait — let's match your spec:
					// "pinch expand [...] deltas should be left and right from this value"
					// → touch0 delta = Vector2.left * something
					// → touch1 delta = Vector2.right * something

					// But to make shrink reverse: for negative scroll, reverse the direction of movement

					Vector2 offset = Vector2.right * halfSep;

					map[0] = new Touch
					{
						fingerId = 0,
						position = center - offset,
						deltaPosition = -deltaThisFrame,  // leftward for expand, rightward for shrink
						phase = old.ContainsKey(0) ? TouchPhase.Moved : TouchPhase.Began
					};

					map[1] = new Touch
					{
						fingerId = 1,
						position = center + offset,
						deltaPosition = deltaThisFrame,  // rightward for expand, leftward for shrink
						phase = old.ContainsKey(1) ? TouchPhase.Moved : TouchPhase.Began
					};
				}
				else if (!anyButtonDown && Input.GetKey(KeyCode.LeftControl))
				{
					// Original ctrl-hold stationary touch
					map[0] = new Touch { fingerId = 0, position = dualMousePosition, deltaPosition = Vector2.zero, phase = TouchPhase.Stationary };
				}

				// ────────────────────────────────────────────────
				// Build final list with proper Began / Ended / Moved phases
				// ────────────────────────────────────────────────
				List<Touch> result = new List<Touch>();

				// Ended touches (fingers that disappeared this frame)
				foreach (var kvp in old)
				{
					if (!map.ContainsKey(kvp.Key))
					{
						result.Add(new Touch
						{
							fingerId = kvp.Key,
							position = kvp.Value.position,
							deltaPosition = Vector2.zero,
							phase = TouchPhase.Ended
						});
					}
				}

				// Current touches
				foreach (var kvp in map)
				{
					TouchPhase phase = old.ContainsKey(kvp.Key)
						? (kvp.Value.deltaPosition.sqrMagnitude > 0.01f ? TouchPhase.Moved : TouchPhase.Stationary)
						: TouchPhase.Began;

					result.Add(new Touch
					{
						fingerId = kvp.Key,
						position = kvp.Value.position,
						deltaPosition = kvp.Value.deltaPosition,
						phase = phase
					});
				}

				return result.ToArray();
			}
		}

		private static Touch CreateFakeTouch(int id, Vector2 pos, Vector2 deltaThisFrame, Dictionary<int, Touch> old)
		{
			TouchPhase phase = old.ContainsKey(id)
				? (deltaThisFrame.sqrMagnitude > 0.01f ? TouchPhase.Moved : TouchPhase.Stationary)
				: TouchPhase.Began;

			return new Touch
			{
				fingerId = id,
				position = pos,
				deltaPosition = deltaThisFrame,
				phase = phase
			};
		}

		private static TouchPhase GetPhase(int id, Dictionary<int, Touch> oldMap)
		{
			if (!oldMap.ContainsKey(id)) return TouchPhase.Began;
			return oldMap[id].deltaPosition.sqrMagnitude > 0.01f ? TouchPhase.Moved : TouchPhase.Stationary;
		}

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

		// Optional: reset separation when no input for a while (can help UX)
		public static void ResetPinchSeparation()
		{
			currentPinchSeparation = 120f;
		}
	}
}