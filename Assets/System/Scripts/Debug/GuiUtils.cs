using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GuiUtils
	{
		public static bool IsMouseInsideWindow()
		{
			var pos = Input.mousePosition;
			return pos.x >= 0 && pos.y >= 0 && pos.x < Screen.width && pos.y < Screen.height;
		}

		// ─────────────────────────────────────────────────────────────────────
		// Colored Button & Repeat Button (runtime-safe)
		// ─────────────────────────────────────────────────────────────────────
		private static Texture2D solidWhite;
		private static Texture2D solidDark;
		private static Texture2D solidBright;
		private static GUIStyle coloredButtonStyle;

		private static Texture2D MakeTex(int w, int h, Color col)
		{
			Color[] pix = new Color[w * h];
			for (int i = 0; i < pix.Length; i++) pix[i] = col;
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.SetPixels(pix);
			tex.Apply();
			return tex;
		}

		private static void EnsureStyles()
		{
			if (coloredButtonStyle != null) return;

			solidWhite = MakeTex(2, 2, Color.white);
			solidDark = MakeTex(2, 2, new Color(0.75f, 0.75f, 0.75f));
			solidBright = MakeTex(2, 2, new Color(1.15f, 1.15f, 1.15f));

			coloredButtonStyle = new GUIStyle(GUI.skin.button)
			{
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				border = new RectOffset(8, 8, 8, 8),
				padding = new RectOffset(4, 4, 4, 4),
				normal = { background = solidWhite, textColor = Color.white },
				hover = { background = solidBright, textColor = Color.white },
				active = { background = solidDark, textColor = Color.white },
				onNormal = { background = solidWhite },
				onHover = { background = solidBright },
				onActive = { background = solidDark }
			};
		}

		public static bool ColoredButton(Rect r, string text, Color col, Action onClick = null)
		{
			EnsureStyles();

			Color prevBg = GUI.backgroundColor;
			Color prevContent = GUI.contentColor;
			Color prevColor = GUI.color;

			GUI.backgroundColor = col;
			GUI.contentColor = Color.white;
			GUI.color = Color.white;

			bool clicked = GUI.Button(r, text, coloredButtonStyle);
			if (clicked) onClick?.Invoke();

			GUI.backgroundColor = prevBg;
			GUI.contentColor = prevContent;
			GUI.color = prevColor;

			return clicked;
		}

		public static bool ColoredRepeatButton(
			Rect rect,
			string text,
			Color color,
			Action onRepeat = null,
			float initialDelay = 0f,      // Delay BEFORE repeating (after first fire)
			float repeatInterval = 0.05f)
		{
			EnsureStyles();

			int id = GUIUtility.GetControlID(FocusType.Passive);
			var state = GUIUtility.GetStateObject(typeof(HoldState), id) as HoldState ?? new HoldState();

			// Save colors
			Color oldColor = GUI.color;
			Color oldBg = GUI.backgroundColor;
			Color oldContent = GUI.contentColor;

			GUI.backgroundColor = color;
			GUI.contentColor = Color.white;
			GUI.color = Color.white;

			Event e = Event.current;
			bool mouseDown = e.type == EventType.MouseDown && rect.Contains(e.mousePosition);
			bool mouseUp = e.type == EventType.MouseUp;

			// Visual feedback when held
			if (state.isPressed)
				GUI.backgroundColor = color * 0.8f;

			bool result = false;
			GUI.Button(rect, text, coloredButtonStyle);

			// === INPUT & FIRE LOGIC ===
			if (mouseDown)
			{
				GUIUtility.hotControl = id;
				state.isPressed = true;

				// FIRST FIRE: IMMEDIATE (this is the key fix!)
				onRepeat?.Invoke();

				// Schedule the NEXT fire after the initial delay
				state.nextFireTime = Time.time + initialDelay;
				e.Use(); // consume the event
			}

			// REPEATING WHILE HELD
			if (state.isPressed && GUIUtility.hotControl == id)
			{
				if (Time.time >= state.nextFireTime)
				{
					result = true;
					onRepeat?.Invoke();
					state.nextFireTime = Time.time + repeatInterval;
				}
			}

			// RELEASE
			if (mouseUp && GUIUtility.hotControl == id)
			{
				GUIUtility.hotControl = 0;
				state.isPressed = false;
			}

			// Restore
			GUI.color = oldColor;
			GUI.backgroundColor = oldBg;
			GUI.contentColor = oldContent;
			return result;
		}

		private class HoldState
		{
			public bool isPressed;
			public float nextFireTime;
		}

		// ─────────────────────────────────────────────────────────────────────
		// AutoHidePanel — EXACT original constructor signature restored
		// ─────────────────────────────────────────────────────────────────────
		public class AutoHidePanel
		{
			public float CurrentWidth { get; private set; }
			public bool IsMouseOver { get; private set; }

			private readonly float collapsedWidth;
			private readonly float expandedWidth;
			private readonly float autoHideDelay;
			private readonly float animationDuration;

			private float targetWidth;
			private float animationVelocity;
			private float mouseExitTime;

			private Rect detectionRect = new Rect();
			private Vector2 flippedMousePos;

			public AutoHidePanel(float collapsed = 120f, float expanded = 340f, float delay = 1f, float animDur = 0.25f)
			{
				collapsedWidth = collapsed;
				expandedWidth = expanded;
				autoHideDelay = delay;
				animationDuration = animDur;

				CurrentWidth = collapsedWidth;
				targetWidth = collapsedWidth;
			}

			public void Update(bool forceExpanded = false)
			{
				if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) return;

				bool wasOver = IsMouseOver;

				float detectX = Screen.width - CurrentWidth - 10f;
				detectionRect.x = detectX;
				detectionRect.y = 20f;
				detectionRect.width = CurrentWidth + 20f;
				detectionRect.height = Screen.height - 40f;

				flippedMousePos = Input.mousePosition;
				flippedMousePos.y = Screen.height - flippedMousePos.y;

				IsMouseOver = detectionRect.Contains(flippedMousePos) || forceExpanded;

				if (IsMouseOver && !wasOver)
				{
					targetWidth = expandedWidth;
					mouseExitTime = 0f;
				}

				if (!IsMouseOver && wasOver && mouseExitTime <= 0f)
					mouseExitTime = Time.time;

				if (!IsMouseOver && mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay)
				{
					targetWidth = collapsedWidth;
					mouseExitTime = 0f;
				}

				CurrentWidth = Mathf.SmoothDamp(CurrentWidth, targetWidth, ref animationVelocity, animationDuration);
			}

			public Rect GetRect(float topOffset = 40f, float bottomMargin = 20f)
			{
				float x = Screen.width - CurrentWidth - 10f;
				float y = topOffset;
				float h = Screen.height - y - bottomMargin;
				return new Rect(x, y, CurrentWidth, h);
			}

			public void DrawGUI()
			{
				Rect panelRect = GetRect();
				GUILayout.BeginArea(panelRect);
				GUILayout.BeginVertical(GUILayout.Width(panelRect.width));
				// Your content goes here — kept as-is for compatibility
				GUILayout.Box("Auto-Hide Panel", GUILayout.ExpandWidth(true));
				GUILayout.Space(10);
				if (GUILayout.Button("Test Button", GUILayout.Height(30)))
					Debug.Log("Button clicked!");
				for (int i = 0; i < 20; i++)
					GUILayout.Label($"Item {i + 1}");
				GUILayout.EndVertical();
				GUILayout.EndArea();
			}

			public void ForceExpand()
			{
				targetWidth = expandedWidth;
				mouseExitTime = 0f;
			}

			public bool IsGuiActive() => GUIUtility.hotControl != 0 || IsMouseOver;
		}

		// ─────────────────────────────────────────────────────────────────────
		// Popup System — EXACT original signatures restored
		// ─────────────────────────────────────────────────────────────────────
		private static class PopupStyles
		{
			internal static readonly GUIStyle window = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 10, 12) };
			internal static readonly GUIStyle title = new GUIStyle(GUI.skin.label)
			{
				fontSize = 16,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				margin = new RectOffset(0, 0, 6, 10)
			};
			internal static readonly GUIStyle message = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				wordWrap = true,
				fontSize = 12
			};
			internal static readonly GUIStyle button = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontStyle = FontStyle.Bold };

			static PopupStyles()
			{
				title.normal.textColor = message.normal.textColor = Color.white;
			}
		}

		public class PopupItem
		{
			public string label;
			public Action action;
			public Color? colorOverride;
			public int spacerHeight;

			// Primary ctor
			public PopupItem(string label, Action action = null, Color? colorOverride = null, int spacerHeight = 8)
			{
				this.label = label;
				this.action = action;
				this.colorOverride = colorOverride;
				this.spacerHeight = spacerHeight;
			}

			// Spacer helper
			public static PopupItem Spacer(int height = 8) => new PopupItem(null, null, null, height);
		}

		public static class PopupMenu
		{
			public static bool Show(Vector2 screenPos, string title, List<PopupItem> items)
			{
				float height = 34f;
				foreach (var it in items)
					height += (it.label == null) ? it.spacerHeight : 26f;

				var rect = new Rect(screenPos.x, screenPos.y, 260f, height);

				// 1. Draw the window background only
				GUI.Box(rect, GUIContent.none, GUI.skin.window);

				// 2. Draw the title manually (centered)
				var titleStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold
				};

				Rect titleRect = new Rect(rect.x, rect.y + 5, rect.width, 24f);
				GUI.Label(titleRect, title, titleStyle);

				float y = rect.y + 30f;
				foreach (var item in items)
				{
					if (item.label == null)
					{
						y += item.spacerHeight;
						continue;
					}

					var btnRect = new Rect(rect.x + 8f, y, rect.width - 16f, 24f);

					var oldColor = GUI.color;
					if (item.colorOverride.HasValue)
						GUI.color = item.colorOverride.Value;

					if (GUI.Button(btnRect, item.label))
					{
						GUI.color = oldColor;
						item.action?.Invoke();
						return true;
					}

					GUI.color = oldColor;
					y += 26f;
				}

				// Click outside closes popup
				if (Event.current.type == EventType.MouseDown && !rect.Contains(Event.current.mousePosition))
					return true;

				return false;
			}
		}
	}
}