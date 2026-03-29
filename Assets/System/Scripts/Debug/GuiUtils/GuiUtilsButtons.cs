using System;
using UnityEngine;

namespace MassiveHadronLtd
{
    public static partial class GuiUtils
    {
		// ─────────────────────────────────────────────────────────────────────
		// Colored Button & Repeat Button (runtime-safe)
		// ─────────────────────────────────────────────────────────────────────
		private static Texture2D solidWhite;
		private static Texture2D solidDark;
		private static Texture2D solidBright;
		private static GUIStyle coloredButtonStyle;

		private static void EnsureStyles()
		{
			if (coloredButtonStyle != null) return;

			solidWhite = MakeTex(2, 2, Color.white);
			solidDark = MakeTex(2, 2, new Color(0.75f, 0.75f, 0.75f));
			solidBright = MakeTex(2, 2, new Color(1.15f, 1.15f, 1.15f));

			coloredButtonStyle = new GUIStyle(GUI.skin.button)
			{
				fontStyle = FontStyle.Bold,
				fontSize = 13,
				alignment = TextAnchor.MiddleCenter,
				border = new RectOffset(8, 8, 8, 8),
				padding = new RectOffset(4, 4, 4, 4),
				normal = { background = solidWhite },
				hover = { background = solidBright },
				active = { background = solidDark },
				onNormal = { background = solidWhite },
				onHover = { background = solidBright },
				onActive = { background = solidDark }
			};

			// Important: set text color globally for the style
			coloredButtonStyle.normal.textColor = Color.white;
			coloredButtonStyle.hover.textColor = Color.white;
			coloredButtonStyle.active.textColor = Color.white;
			coloredButtonStyle.onNormal.textColor = Color.white;
			// etc. if needed
		}

		private class HoldState
		{
			public bool isPressed;
			public float nextFireTime;
		}

		public static bool ColoredButton(Rect r, string text, Color col, Action onClick = null)
		{
			EnsureStyles();

			Color prev = GUI.backgroundColor;
			GUI.backgroundColor = col;                    // Only tint — don't touch content/color

			bool clicked = GUI.Button(r, text, coloredButtonStyle);
			if (clicked) onClick?.Invoke();

			GUI.backgroundColor = prev;
			return clicked;
		}

		public static bool ColoredRepeatButton(
			Rect rect,
			string text,
			Color color,
			Action onRepeat = null,
			float initialDelay = 0f,
			float repeatInterval = 0.05f)
		{
			EnsureStyles();

			int id = GUIUtility.GetControlID(FocusType.Passive);
			var state = GUIUtility.GetStateObject(typeof(HoldState), id) as HoldState;
			if (state == null)
			{
				state = new HoldState();
				GUIUtility.GetStateObject(typeof(HoldState), id);
			}

			Color prevBg = GUI.backgroundColor;
			GUI.backgroundColor = color;

			Event e = Event.current;
			bool mouseDown = e.type == EventType.MouseDown && rect.Contains(e.mousePosition);
			bool mouseUp = e.type == EventType.MouseUp && GUIUtility.hotControl == id;

			bool result = false;

			// Draw the button with normal tint — lets hover work, brief press flash, etc.
			GUI.Button(rect, text, coloredButtonStyle);

			// === VISUAL FEEDBACK FOR HELD STATE ===
			if (state.isPressed && GUIUtility.hotControl == id)
			{
				// Draw a semi-transparent dark overlay when held
				Color overlay = new Color(0f, 0f, 0f, 0.35f); // dark overlay
				GUI.color = overlay;
				GUI.DrawTexture(rect, Texture2D.whiteTexture);
				GUI.color = Color.white;
				result = true;
			}

			// === INPUT LOGIC ===
			if (mouseDown)
			{
				GUIUtility.hotControl = id;
				state.isPressed = true;
				onRepeat?.Invoke();
				state.nextFireTime = Time.time + initialDelay;
				e.Use();
			}
			else
			{
				if (state.isPressed && GUIUtility.hotControl == id)
				{
#if UNITY_EDITOR
					bool isEditorPaused = UnityEditor.EditorApplication.isPaused;
					if (!isEditorPaused && Time.time >= state.nextFireTime)
					{
						result = true;
						onRepeat?.Invoke();
						state.nextFireTime = Time.time + repeatInterval;
					}
#else
					if (Time.time >= state.nextFireTime)
					{
						result = true;
						onRepeat?.Invoke();
						state.nextFireTime = Time.time + repeatInterval;
					}
#endif
				}
			}

			if (mouseUp)
			{
				GUIUtility.hotControl = 0;
				state.isPressed = false;
			}

			GUI.backgroundColor = prevBg;

			return result;
		}
	}
}