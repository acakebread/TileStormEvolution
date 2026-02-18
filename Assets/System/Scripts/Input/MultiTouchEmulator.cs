// Copyright 2019 massivehadron.com ltd. created 03/11/2019 by Andrew Cakebread
// Pinch-zoom emulation added 2025/2026
// Frame cache added 2026

using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class MultiTouchEmulator
	{
		private static Dictionary<int, Touch> map = new Dictionary<int, Touch>();

		// ────────────────────────────────────────────────
		// Cache for touches array + timestamp
		// ────────────────────────────────────────────────
		private static Touch[] cachedTouches;
		private static float lastComputeTime = -1f;
		private const float CACHE_DURATION_SECONDS = 0.004f; // 4 ms

		public static Touch[] touches
		{
			get
			{
				float now = Time.realtimeSinceStartup;

				// If we have a recent cache and time hasn't advanced much → reuse
				if (cachedTouches != null && now - lastComputeTime < CACHE_DURATION_SECONDS)
				{
					return cachedTouches;
				}

				// ────────────────────────────────────────────────
				// Full computation (old code)
				// ────────────────────────────────────────────────
				Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
				Vector2 mousePos = Input.mousePosition;

				Dictionary<int, Touch> old = map;
				map = new Dictionary<int, Touch>();

				float scroll = Input.GetAxis("Mouse ScrollWheel");

				if (Input.GetMouseButton(0))
				{
					map[0] = new Touch
					{
						fingerId = 0,
						position = mousePos,
						deltaPosition = mouseDelta,
						phase = GetPhase(0, old)
					};
				}
				else if (Input.GetMouseButton(1))
				{
					Vector2 pos = Input.mousePosition;
					Vector2 delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

					TouchPhase sharedPhase = GetPhase(0, old);
					if (old.ContainsKey(1) && old[1].phase == TouchPhase.Began)
						sharedPhase = TouchPhase.Began;

					var baseTouch = new Touch
					{
						position = pos,
						deltaPosition = delta,
						phase = sharedPhase,
						tapCount = 0,
						type = TouchType.Indirect
					};

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
				else if (Mathf.Abs(scroll) > 0.001f)
				{
					float scaledScroll = scroll * 100f * Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);

					Vector2 center = mousePos;

					Vector2 deltaLeft = Vector2.left * scaledScroll;
					Vector2 deltaRight = Vector2.right * scaledScroll;

					Vector2 pos0 = center;
					Vector2 pos1 = center;

					if (scroll < 0)   // scroll down → zoom in / expand
					{
						pos0 = center - deltaLeft;
						pos1 = center - deltaRight;
					}

					map[0] = new Touch
					{
						fingerId = 0,
						position = pos0,
						deltaPosition = deltaLeft,
						phase = old.ContainsKey(0) ? TouchPhase.Moved : TouchPhase.Began
					};

					map[1] = new Touch
					{
						fingerId = 1,
						position = pos1,
						deltaPosition = deltaRight,
						phase = old.ContainsKey(1) ? TouchPhase.Moved : TouchPhase.Began
					};
				}

				// Build result list with proper phases
				List<Touch> result = new List<Touch>();

				// Ended
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

				// Current
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

				// ────────────────────────────────────────────────
				// Update cache
				// ────────────────────────────────────────────────
				cachedTouches = result.ToArray();
				lastComputeTime = now;

				return cachedTouches;
			}
		}

		private static TouchPhase GetPhase(int id, Dictionary<int, Touch> oldMap)
		{
			if (!oldMap.ContainsKey(id)) return TouchPhase.Began;
			return oldMap[id].deltaPosition.sqrMagnitude > 0.01f ? TouchPhase.Moved : TouchPhase.Stationary;
		}

		// OnGUI and DrawQuad unchanged...
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
}