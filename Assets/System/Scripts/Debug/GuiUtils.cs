using UnityEngine;

namespace MassiveHadronLtd
{
    public static class GuiUtils
    {
		private static GUIStyle coloredButtonStyle;
		private static Texture2D solidWhite;
		private static Texture2D solidDark;
		private static Texture2D solidBright;

		private static void InitStaticTextures()
		{
			solidWhite = TextureUtils.MakeTex(1, 1, Color.white);
			solidDark = TextureUtils.MakeTex(1, 1, new Color(0.75f, 0.75f, 0.75f));
			solidBright = TextureUtils.MakeTex(1, 1, new Color(1.15f, 1.15f, 1.15f));
		}
		private static void EnsureStyles()
		{
			if (coloredButtonStyle != null) return;
			if (solidWhite == null) InitStaticTextures();

			coloredButtonStyle = new GUIStyle();

			coloredButtonStyle.font = GUI.skin.button.font;
			if (coloredButtonStyle.font == null)
				coloredButtonStyle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

			coloredButtonStyle.fontSize = 12;
			coloredButtonStyle.fontStyle = FontStyle.Bold;
			coloredButtonStyle.alignment = TextAnchor.MiddleCenter;

			// Proper button look
			coloredButtonStyle.border = new RectOffset(8, 8, 8, 8);
			coloredButtonStyle.padding = new RectOffset(4, 4, 4, 4);

			// Backgrounds
			coloredButtonStyle.normal.background = solidWhite;
			coloredButtonStyle.hover.background = solidBright;
			coloredButtonStyle.active.background = solidDark;
			coloredButtonStyle.onNormal.background = solidWhite;
			coloredButtonStyle.onHover.background = solidBright;
			coloredButtonStyle.onActive.background = solidDark;

			// TEXT — NOW IT WILL ACTUALLY SHOW (WHITE, NOT BLACK)
			Color textCol = Color.white;
			coloredButtonStyle.normal.textColor = textCol;
			coloredButtonStyle.hover.textColor = textCol;
			coloredButtonStyle.active.textColor = textCol;
			coloredButtonStyle.focused.textColor = textCol;
			coloredButtonStyle.onNormal.textColor = textCol;
			coloredButtonStyle.onHover.textColor = textCol;
			coloredButtonStyle.onActive.textColor = textCol;
			coloredButtonStyle.onFocused.textColor = textCol;
		}

		public static void ColoredButton(Rect r, string text, Color col, System.Action onClick)
		{
			EnsureStyles();

			// Save everything
			Color prevColor = GUI.color;
			Color prevContentColor = GUI.contentColor;
			Color prevBackground = GUI.backgroundColor;

			// Set background tint (this colors the button)
			GUI.backgroundColor = col;

			// CRITICAL: Force content color to white so text stays white
			GUI.contentColor = Color.white;

			// GUI.color must be white or it will still tint the text!
			GUI.color = Color.white;

			if (GUI.Button(r, text, coloredButtonStyle))
				onClick?.Invoke();

			// Restore everything
			GUI.color = prevColor;
			GUI.contentColor = prevContentColor;
			GUI.backgroundColor = prevBackground;
		}



		public static void ColoredRepeatButton(
			Rect rect,
			string text,
			Color color,
			System.Action onRepeat = null,
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
		}

		// Don't forget this class (add if missing)
		private class HoldState
		{
			public float nextFireTime;
			public bool isPressed;
		}
	}
}