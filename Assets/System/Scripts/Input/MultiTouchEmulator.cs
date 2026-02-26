// Copyright 2019 massivehadron.com ltd. created 03/11/2019 by Andrew Cakebread
// Pinch-zoom emulation added 2025/2026
// Refactored 2026: removed frame cache, prevent mid-frame map overwrites, commit in LateUpdate

using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class MultiTouchEmulator
	{
		// Authoritative map — represents the PREVIOUS frame's simulated touches
		// Only updated once per frame at the end (in LateUpdate)
		private static Dictionary<int, Touch> map = new Dictionary<int, Touch>();

		// Tiny epsilon safe for float32 precision in Unity
		private const float EPSILON = 1e-6f;

		public static Touch[] touches
		{
			get
			{
				// Always recompute fresh — no caching
				Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 16f;
				Vector2 mousePos = Input.mousePosition;
				float scroll = Input.GetAxis("Mouse ScrollWheel");

				var old = map;  // previous frame state
				var current = new Dictionary<int, Touch>();

				// ── Normal single finger (LMB) ──
				if (Input.GetMouseButton(0))
				{
					current[0] = new Touch
					{
						fingerId = 0,
						position = mousePos,
						deltaPosition = mouseDelta,
						phase = GetPhase(0, old)
					};
				}
				// ── RMB secondary finger simulation ──
				else if (Input.GetMouseButton(1))
				{
					current[0] = new Touch
					{
						fingerId = 0,
						position = mousePos,
						deltaPosition = mouseDelta,
						phase = GetPhase(0, old),
						type = TouchType.Indirect
					};

					TouchPhase phase1 = TouchPhase.Began;
					if (old.ContainsKey(1) && old[1].phase != TouchPhase.Ended)
					{
						phase1 = TouchPhase.Stationary;
					}

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

				// ── Mouse wheel → simulated two-finger pinch ──
				if (Mathf.Abs(scroll) > 0.001f)
				{
					float pinchScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / 4f;
					float scaledScroll = scroll * pinchScale;

					Vector2 center = mousePos;
					Vector2 deltaLeft = Vector2.left * scaledScroll;
					Vector2 deltaRight = Vector2.right * scaledScroll;

					Vector2 pos0 = center;
					Vector2 pos1 = center;

					if (scroll < 0)   // scroll down → zoom in / fingers move apart
					{
						pos0 = center - deltaLeft;
						pos1 = center - deltaRight;
					}

					current[0] = new Touch
					{
						fingerId = 0,
						position = pos0,
						deltaPosition = deltaLeft,
						phase = old.ContainsKey(0) ? TouchPhase.Moved : TouchPhase.Began
					};

					current[1] = new Touch
					{
						fingerId = 1,
						position = pos1,
						deltaPosition = deltaRight,
						phase = old.ContainsKey(1) ? TouchPhase.Moved : TouchPhase.Began
					};
				}

				// ────────────────────────────────────────────────
				// Pinch end detection & cleanup — exact matching
				// ────────────────────────────────────────────────
				List<Touch> result = new List<Touch>();

				bool wasSimulatedPinch = old != null && IsExactlySimulatedPinch(old);

				if (wasSimulatedPinch && !current.ContainsKey(0) && !current.ContainsKey(1))
				{
					// Simulated pinch just ended → force arbitrary offset + small delta
					Vector2 finalPos = Input.mousePosition;

					const float arbitraryDelta = 0.5f;
					Vector2 arbitraryLeft = Vector2.left * arbitraryDelta;
					Vector2 arbitraryRight = Vector2.right * arbitraryDelta;

					var t0 = old[0];
					t0.position = finalPos;
					t0.deltaPosition = arbitraryLeft;

					var t1 = old[1];
					t1.position = finalPos;
					t1.deltaPosition = arbitraryRight;

					old[0] = t0;
					old[1] = t1;
					// phase will be corrected in the loop below → next frame Ended naturally
				}

				// ── Ended touches ──
				if (old != null)
				{
					foreach (var kvp in old)
					{
						if (!current.ContainsKey(kvp.Key))
						{
							result.Add(new Touch
							{
								fingerId = kvp.Key,
								position = kvp.Value.position,
								deltaPosition = kvp.Value.deltaPosition,
								phase = TouchPhase.Ended,
								type = kvp.Value.type,
								tapCount = kvp.Value.tapCount
							});
						}
					}
				}

				// ── Current touches ──
				foreach (var kvp in current)
				{
					TouchPhase phase = old != null && old.ContainsKey(kvp.Key)
						? (kvp.Value.deltaPosition.sqrMagnitude > 0.0001f ? TouchPhase.Moved : TouchPhase.Stationary)
						: TouchPhase.Began;

					result.Add(new Touch
					{
						fingerId = kvp.Key,
						position = kvp.Value.position,
						deltaPosition = kvp.Value.deltaPosition,
						phase = phase,
						type = kvp.Value.type,
						tapCount = kvp.Value.tapCount
					});
				}

				return result.ToArray();
			}
		}

		// ────────────────────────────────────────────────
		// Called once per frame in LateUpdate — commit current frame state to map
		// ────────────────────────────────────────────────
		internal static void CommitCurrentToMap()
		{
			map.Clear();

			// We need to know what the current frame's touches would be
			// → unfortunately we must recompute here (cheap operation)
			// Alternative: store current during first .touches call, but that complicates null checks

			Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 16f;
			Vector2 mousePos = Input.mousePosition;
			float scroll = Input.GetAxis("Mouse ScrollWheel");

			var old = map;  // actually previous-previous now — but we only care about keys/phases

			// Re-run same simulation logic (only filling map, no result list)
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
				map[0] = new Touch
				{
					fingerId = 0,
					position = mousePos,
					deltaPosition = mouseDelta,
					phase = GetPhase(0, old),
					type = TouchType.Indirect
				};

				TouchPhase phase1 = TouchPhase.Began;
				if (old.ContainsKey(1) && old[1].phase != TouchPhase.Ended)
					phase1 = TouchPhase.Stationary;

				map[1] = new Touch
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

		// ────────────────────────────────────────────────
		// Debug visualization (uses current map = previous frame)
		// ────────────────────────────────────────────────
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

	// Minimal auto-created singleton — now commits state at end of LateUpdate
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

//// Copyright 2019 massivehadron.com ltd. created 03/11/2019 by Andrew Cakebread
//// Pinch-zoom emulation added 2025/2026
//// Frame cache added 2026
//// Pinch end cleanup (snap to center + zero delta) added 2026
//// Exact matching for simulated pinch detection (minimal epsilon) - 2026

//using UnityEngine;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public static class MultiTouchEmulator
//	{
//		private static Dictionary<int, Touch> map = new Dictionary<int, Touch>();

//		// ────────────────────────────────────────────────
//		// Cache for touches array – now one frame only
//		// ────────────────────────────────────────────────
//		private static Touch[] cachedTouches;
//		private static bool cacheValidThisFrame = false;

//		// Tiny epsilon safe for float32 precision in Unity
//		private const float EPSILON = 1e-6f;

//		public static Touch[] touches
//		{
//			get
//			{
//				if (cacheValidThisFrame && cachedTouches != null)
//				{
//					return cachedTouches;
//				}

//				// ────────────────────────────────────────────────
//				// Full computation
//				// ────────────────────────────────────────────────
//				Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 16f;
//				Vector2 mousePos = Input.mousePosition;

//				Dictionary<int, Touch> old = map;
//				map = new Dictionary<int, Touch>();

//				float scroll = Input.GetAxis("Mouse ScrollWheel");

//				// ── Normal single finger (LMB) ──
//				if (Input.GetMouseButton(0))
//				{
//					map[0] = new Touch
//					{
//						fingerId = 0,
//						position = mousePos,
//						deltaPosition = mouseDelta,
//						phase = GetPhase(0, old)
//					};
//				}
//				// ── RMB secondary finger simulation ──
//				else if (Input.GetMouseButton(1))
//				{
//					map[0] = new Touch
//					{
//						fingerId = 0,
//						position = mousePos,
//						deltaPosition = mouseDelta,
//						phase = GetPhase(0, old),
//						type = TouchType.Indirect
//					};

//					TouchPhase phase1 = TouchPhase.Began;
//					if (old.ContainsKey(1) && old[1].phase != TouchPhase.Ended)
//					{
//						phase1 = TouchPhase.Stationary;
//					}

//					map[1] = new Touch
//					{
//						fingerId = 1,
//						position = mousePos,
//						deltaPosition = mouseDelta,
//						phase = phase1,
//						tapCount = 0,
//						type = TouchType.Indirect
//					};
//				}

//				// ── Mouse wheel → simulated two-finger pinch ──
//				if (Mathf.Abs(scroll) > 0.001f)
//				{
//					float pinchScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / 4f;
//					float scaledScroll = scroll * pinchScale;

//					Vector2 center = mousePos;
//					Vector2 deltaLeft = Vector2.left * scaledScroll;
//					Vector2 deltaRight = Vector2.right * scaledScroll;

//					Vector2 pos0 = center;
//					Vector2 pos1 = center;

//					if (scroll < 0)   // scroll down → zoom in / fingers move apart
//					{
//						pos0 = center - deltaLeft;
//						pos1 = center - deltaRight;
//					}

//					map[0] = new Touch
//					{
//						fingerId = 0,
//						position = pos0,
//						deltaPosition = deltaLeft,
//						phase = old.ContainsKey(0) ? TouchPhase.Moved : TouchPhase.Began
//					};

//					map[1] = new Touch
//					{
//						fingerId = 1,
//						position = pos1,
//						deltaPosition = deltaRight,
//						phase = old.ContainsKey(1) ? TouchPhase.Moved : TouchPhase.Began
//					};
//				}

//				// ────────────────────────────────────────────────
//				// Pinch end detection & cleanup — exact matching
//				// ────────────────────────────────────────────────
//				List<Touch> result = new List<Touch>();

//				bool wasSimulatedPinch = old != null && IsExactlySimulatedPinch(old);
//				//bool isSimulatedPinch = IsExactlySimulatedPinch(map);

//				//if (wasSimulatedPinch && !isSimulatedPinch && map.ContainsKey(0) && map.ContainsKey(1))
//				if (wasSimulatedPinch && false == map.ContainsKey(0) && false == map.ContainsKey(1))
//				{
//					// Simulated pinch just ended → force arbitrary offset positions  + zero delta, to allow for external simulaated scroll detection
//					Vector2 finalPos = Input.mousePosition;

//					const float arbitraryDelta = 0.5f;
//					Vector2 arbitraryLeft = Vector2.left * arbitraryDelta;
//					Vector2 arbitraryRight = Vector2.right * arbitraryDelta;

//					var t0 = old[0];
//					t0.position = finalPos;// + arbitraryLeft;
//					t0.deltaPosition = arbitraryLeft;// Vector2.zero;

//					var t1 = old[1];
//					t1.position = finalPos;// + arbitraryRight;
//					t1.deltaPosition = arbitraryRight;// Vector2.zero;

//					old[0] = t0;
//					old[1] = t1;
//					// phase corrected to Stationary in the loop below → next frame → Ended (natural)
//				}

//				// ── Ended touches ──
//				if (old != null)
//				{
//					foreach (var kvp in old)
//					{
//						if (!map.ContainsKey(kvp.Key))
//						{
//							result.Add(new Touch
//							{
//								fingerId = kvp.Key,
//								position = kvp.Value.position,
//								deltaPosition = kvp.Value.deltaPosition,//Vector2.zero,
//								phase = TouchPhase.Ended,
//								type = kvp.Value.type,
//								tapCount = kvp.Value.tapCount
//							});
//						}
//					}
//				}

//				// ── Current touches ──
//				foreach (var kvp in map)
//				{
//					TouchPhase phase = old != null && old.ContainsKey(kvp.Key)
//						? (kvp.Value.deltaPosition.sqrMagnitude > 0.0001f ? TouchPhase.Moved : TouchPhase.Stationary)
//						: TouchPhase.Began;

//					result.Add(new Touch
//					{
//						fingerId = kvp.Key,
//						position = kvp.Value.position,
//						deltaPosition = kvp.Value.deltaPosition,
//						phase = phase,
//						type = kvp.Value.type,
//						tapCount = kvp.Value.tapCount
//					});
//				}

//				// ────────────────────────────────────────────────
//				// Update cache
//				// ────────────────────────────────────────────────
//				cachedTouches = result.ToArray();
//				cacheValidThisFrame = true;

//				return cachedTouches;
//			}
//		}

//		private static bool IsExactlySimulatedPinch(Dictionary<int, Touch> touches)
//		{
//			if (touches.Count != 2) return false;
//			if (!touches.ContainsKey(0) || !touches.ContainsKey(1)) return false;

//			var d0 = touches[0].deltaPosition;
//			var d1 = touches[1].deltaPosition;

//			// Must have non-trivial movement this frame
//			if (d0.sqrMagnitude < EPSILON * EPSILON) return false;

//			Vector2 sum = d0 + d1;

//			// Core emulator invariants — deltas must almost perfectly cancel
//			bool deltasCancel = sum.sqrMagnitude < EPSILON * EPSILON;
//			bool oppositeSignsX = Mathf.Abs(d0.x + d1.x) < EPSILON;
//			bool oppositeSignsY = Mathf.Abs(d0.y + d1.y) < EPSILON;
//			bool nearZeroY = Mathf.Abs(d0.y) < EPSILON && Mathf.Abs(d1.y) < EPSILON;

//			// All must hold (your code only produces horizontal separation)
//			return deltasCancel && oppositeSignsX && oppositeSignsY && nearZeroY;
//		}

//		private static TouchPhase GetPhase(int id, Dictionary<int, Touch> oldMap)
//		{
//			if (!oldMap.ContainsKey(id)) return TouchPhase.Began;
//			return oldMap[id].deltaPosition.sqrMagnitude > 0.0001f ? TouchPhase.Moved : TouchPhase.Stationary;
//		}

//		// ────────────────────────────────────────────────
//		// Debug visualization (unchanged)
//		// ────────────────────────────────────────────────
//		public static void OnGUI()
//		{
//			foreach (var kvp in map)
//			{
//				Color col = kvp.Key == 0 ? new Color(1, 0, 0, 0.6f) : new Color(0, 0.7f, 1, 0.6f);
//				DrawQuad(new Rect(kvp.Value.position.x - 10, Screen.height - kvp.Value.position.y - 10, 20, 20), col);
//			}
//		}

//		private static void DrawQuad(Rect rect, Color color)
//		{
//			Texture2D tex = new Texture2D(1, 1);
//			tex.SetPixel(0, 0, color);
//			tex.Apply();
//			GUI.skin.box.normal.background = tex;
//			GUI.Box(rect, GUIContent.none);
//		}

//		// Called from LateUpdate by the controller
//		internal static void InvalidateCache()
//		{
//			cacheValidThisFrame = false;
//		}
//	}

//	// Minimal auto-created singleton that calls InvalidateCache() every LateUpdate
//	internal class TouchCacheController : MonoBehaviour
//	{
//		private static TouchCacheController instance;

//		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//		private static void CreateInstance()
//		{
//			if (instance != null) return;

//			var go = new GameObject("[MultiTouchEmulator Cache Controller]");
//			go.hideFlags = HideFlags.HideAndDontSave;
//			instance = go.AddComponent<TouchCacheController>();
//			DontDestroyOnLoad(go);
//		}

//		private void LateUpdate()
//		{
//			MultiTouchEmulator.InvalidateCache();
//		}
//	}
//}

//// Copyright 2019 massivehadron.com ltd. created 03/11/2019 by Andrew Cakebread
//// Pinch-zoom emulation added 2025/2026
//// Frame cache added 2026
//// Pinch end cleanup (snap to center + zero delta) added 2026
//// Exact matching for simulated pinch detection (minimal epsilon) - 2026

//using UnityEngine;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public static class MultiTouchEmulator
//	{
//		private static Dictionary<int, Touch> map = new Dictionary<int, Touch>();

//		// ────────────────────────────────────────────────
//		// Cache for touches array + timestamp
//		// ────────────────────────────────────────────────
//		private static Touch[] cachedTouches;
//		private static float lastComputeTime = -1f;
//		private const float CACHE_DURATION_SECONDS = 0.004f; // ~250 fps max sensible rate

//		// Tiny epsilon safe for float32 precision in Unity
//		private const float EPSILON = 1e-6f;

//		public static Touch[] touches
//		{
//			get
//			{
//				float now = Time.realtimeSinceStartup;

//				if (cachedTouches != null && now - lastComputeTime < CACHE_DURATION_SECONDS)
//					return cachedTouches;

//				// ────────────────────────────────────────────────
//				// Full computation
//				// ────────────────────────────────────────────────
//				Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 16f;
//				Vector2 mousePos = Input.mousePosition;

//				Dictionary<int, Touch> old = map;
//				map = new Dictionary<int, Touch>();

//				float scroll = Input.GetAxis("Mouse ScrollWheel");

//				// ── Normal single finger (LMB) ──
//				if (Input.GetMouseButton(0))
//				{
//					map[0] = new Touch
//					{
//						fingerId = 0,
//						position = mousePos,
//						deltaPosition = mouseDelta,
//						phase = GetPhase(0, old)
//					};
//				}
//				// ── RMB secondary finger simulation ──
//				else if (Input.GetMouseButton(1))
//				{
//					map[0] = new Touch
//					{
//						fingerId = 0,
//						position = mousePos,
//						deltaPosition = mouseDelta,
//						phase = GetPhase(0, old),
//						type = TouchType.Indirect
//					};

//					TouchPhase phase1 = TouchPhase.Began;
//					if (old.ContainsKey(1) && old[1].phase != TouchPhase.Ended)
//					{
//						phase1 = TouchPhase.Stationary;
//					}

//					map[1] = new Touch
//					{
//						fingerId = 1,
//						position = mousePos,
//						deltaPosition = mouseDelta,
//						phase = phase1,
//						tapCount = 0,
//						type = TouchType.Indirect
//					};
//				}

//				// ── Mouse wheel → simulated two-finger pinch ──
//				if (Mathf.Abs(scroll) > 0.001f)
//				{
//					float pinchScale = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height) / 4f;
//					float scaledScroll = scroll * pinchScale;

//					Vector2 center = mousePos;
//					Vector2 deltaLeft = Vector2.left * scaledScroll;
//					Vector2 deltaRight = Vector2.right * scaledScroll;

//					Vector2 pos0 = center;
//					Vector2 pos1 = center;

//					if (scroll < 0)   // scroll down → zoom in / fingers move apart
//					{
//						pos0 = center - deltaLeft;
//						pos1 = center - deltaRight;
//					}

//					map[0] = new Touch
//					{
//						fingerId = 0,
//						position = pos0,
//						deltaPosition = deltaLeft,
//						phase = old.ContainsKey(0) ? TouchPhase.Moved : TouchPhase.Began
//					};

//					map[1] = new Touch
//					{
//						fingerId = 1,
//						position = pos1,
//						deltaPosition = deltaRight,
//						phase = old.ContainsKey(1) ? TouchPhase.Moved : TouchPhase.Began
//					};
//				}

//				// ────────────────────────────────────────────────
//				// Pinch end detection & cleanup — exact matching
//				// ────────────────────────────────────────────────
//				List<Touch> result = new List<Touch>();

//				bool wasSimulatedPinch = old != null && IsExactlySimulatedPinch(old);
//				//bool isSimulatedPinch = IsExactlySimulatedPinch(map);

//				//if (wasSimulatedPinch && !isSimulatedPinch && map.ContainsKey(0) && map.ContainsKey(1))
//				if (wasSimulatedPinch && false == map.ContainsKey(0) && false == map.ContainsKey(1))
//				{
//					// Simulated pinch just ended → force arbitrary offset positions  + zero delta, to allow for external simulaated scroll detection
//					Vector2 finalPos = Input.mousePosition;

//					const float arbitraryDelta = 0.5f;
//					Vector2 arbitraryLeft = Vector2.left * arbitraryDelta;
//					Vector2 arbitraryRight = Vector2.right * arbitraryDelta;

//					var t0 = old[0];
//					t0.position = finalPos;// + arbitraryLeft;
//					t0.deltaPosition = arbitraryLeft;// Vector2.zero;

//					var t1 = old[1];
//					t1.position = finalPos;// + arbitraryRight;
//					t1.deltaPosition = arbitraryRight;// Vector2.zero;

//					old[0] = t0;
//					old[1] = t1;
//					// phase corrected to Stationary in the loop below → next frame → Ended (natural)
//				}

//				// ── Ended touches ──
//				if (old != null)
//				{
//					foreach (var kvp in old)
//					{
//						if (!map.ContainsKey(kvp.Key))
//						{
//							result.Add(new Touch
//							{
//								fingerId = kvp.Key,
//								position = kvp.Value.position,
//								deltaPosition = kvp.Value.deltaPosition,//Vector2.zero,
//								phase = TouchPhase.Ended,
//								type = kvp.Value.type,
//								tapCount = kvp.Value.tapCount
//							});
//						}
//					}
//				}

//				// ── Current touches ──
//				foreach (var kvp in map)
//				{
//					TouchPhase phase = old != null && old.ContainsKey(kvp.Key)
//						? (kvp.Value.deltaPosition.sqrMagnitude > 0.0001f ? TouchPhase.Moved : TouchPhase.Stationary)
//						: TouchPhase.Began;

//					result.Add(new Touch
//					{
//						fingerId = kvp.Key,
//						position = kvp.Value.position,
//						deltaPosition = kvp.Value.deltaPosition,
//						phase = phase,
//						type = kvp.Value.type,
//						tapCount = kvp.Value.tapCount
//					});
//				}

//				// ────────────────────────────────────────────────
//				// Update cache
//				// ────────────────────────────────────────────────
//				cachedTouches = result.ToArray();
//				lastComputeTime = now;

//				return cachedTouches;
//			}
//		}

//		private static bool IsExactlySimulatedPinch(Dictionary<int, Touch> touches)
//		{
//			if (touches.Count != 2) return false;
//			if (!touches.ContainsKey(0) || !touches.ContainsKey(1)) return false;

//			var d0 = touches[0].deltaPosition;
//			var d1 = touches[1].deltaPosition;

//			// Must have non-trivial movement this frame
//			if (d0.sqrMagnitude < EPSILON * EPSILON) return false;

//			Vector2 sum = d0 + d1;

//			// Core emulator invariants — deltas must almost perfectly cancel
//			bool deltasCancel = sum.sqrMagnitude < EPSILON * EPSILON;
//			bool oppositeSignsX = Mathf.Abs(d0.x + d1.x) < EPSILON;
//			bool oppositeSignsY = Mathf.Abs(d0.y + d1.y) < EPSILON;
//			bool nearZeroY = Mathf.Abs(d0.y) < EPSILON && Mathf.Abs(d1.y) < EPSILON;

//			// All must hold (your code only produces horizontal separation)
//			return deltasCancel && oppositeSignsX && oppositeSignsY && nearZeroY;
//		}

//		private static TouchPhase GetPhase(int id, Dictionary<int, Touch> oldMap)
//		{
//			if (!oldMap.ContainsKey(id)) return TouchPhase.Began;
//			return oldMap[id].deltaPosition.sqrMagnitude > 0.0001f ? TouchPhase.Moved : TouchPhase.Stationary;
//		}

//		// ────────────────────────────────────────────────
//		// Debug visualization (unchanged)
//		// ────────────────────────────────────────────────
//		public static void OnGUI()
//		{
//			foreach (var kvp in map)
//			{
//				Color col = kvp.Key == 0 ? new Color(1, 0, 0, 0.6f) : new Color(0, 0.7f, 1, 0.6f);
//				DrawQuad(new Rect(kvp.Value.position.x - 10, Screen.height - kvp.Value.position.y - 10, 20, 20), col);
//			}
//		}

//		private static void DrawQuad(Rect rect, Color color)
//		{
//			Texture2D tex = new Texture2D(1, 1);
//			tex.SetPixel(0, 0, color);
//			tex.Apply();
//			GUI.skin.box.normal.background = tex;
//			GUI.Box(rect, GUIContent.none);
//		}
//	}
//}