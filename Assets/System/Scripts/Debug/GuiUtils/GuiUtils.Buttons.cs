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
				normal = { background = solidWhite, textColor = Color.white },
				hover = { background = solidBright, textColor = Color.white },
				active = { background = solidDark, textColor = Color.white },
				onNormal = { background = solidWhite },
				onHover = { background = solidBright },
				onActive = { background = solidDark }
			};
		}

		private class HoldState
		{
			public bool isPressed;
			public float nextFireTime;
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
			float initialDelay = 0f,      // Delay before repeating starts (after first immediate fire)
			float repeatInterval = 0.05f)
		{
			EnsureStyles();

			int id = GUIUtility.GetControlID(FocusType.Passive);
			var state = GUIUtility.GetStateObject(typeof(HoldState), id) as HoldState;
			if (state == null)
			{
				state = new HoldState();
				GUIUtility.GetStateObject(typeof(HoldState), id); // registers it
			}

			// Save original colors
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

				// First fire is immediate
				onRepeat?.Invoke();

				// Schedule first repeat after initial delay
				state.nextFireTime = Time.time + initialDelay;

				e.Use();
			}

			// Repeating while held down
			if (state.isPressed && GUIUtility.hotControl == id)
			{
				bool isEditorPaused = false;
#if UNITY_EDITOR
				isEditorPaused = UnityEditor.EditorApplication.isPaused;
#endif

				// Only allow repeats when the editor is NOT paused
				if (!isEditorPaused && Time.time >= state.nextFireTime)
				{
					result = true;
					onRepeat?.Invoke();
					state.nextFireTime = Time.time + repeatInterval;
				}
			}

			// Release
			if (mouseUp && GUIUtility.hotControl == id)
			{
				GUIUtility.hotControl = 0;
				state.isPressed = false;
			}

			// Restore colors
			GUI.color = oldColor;
			GUI.backgroundColor = oldBg;
			GUI.contentColor = oldContent;

			return result;
		}
	}
}