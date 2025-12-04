using UnityEditor;
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

		public static bool ColoredButton(Rect r, string text, Color col, System.Action onClick = null)
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

			bool result;
			if (result = GUI.Button(r, text, coloredButtonStyle))
				onClick?.Invoke();

			// Restore everything
			GUI.color = prevColor;
			GUI.contentColor = prevContentColor;
			GUI.backgroundColor = prevBackground;
			return result;
		}

		public static bool ColoredRepeatButton(
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

			bool result =false;
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

		// Don't forget this class (add if missing)
		private class HoldState
		{
			public float nextFireTime;
			public bool isPressed;
		}

		//public class AutoHidePanel
		//{
		//	public float CurrentWidth { get; private set; }
		//	public bool IsMouseOver { get; private set; }

		//	private readonly float collapsedWidth;
		//	private readonly float expandedWidth;
		//	private readonly float autoHideDelay;
		//	private readonly float animationDuration;

		//	private float targetWidth;
		//	private float animationStartTime;
		//	private float mouseExitTime;

		//	public AutoHidePanel(float collapsed = 120f, float expanded = 340f, float delay = 1f, float animDur = 0.3f)
		//	{
		//		collapsedWidth = collapsed;
		//		expandedWidth = expanded;
		//		autoHideDelay = delay;
		//		animationDuration = animDur;

		//		CurrentWidth = collapsedWidth;
		//		targetWidth = collapsedWidth;
		//	}

		//	public void Update(bool forceExpanded = false)
		//	{
		//		var wasOver = IsMouseOver;

		//		// Always detect using expanded zone — this is the magic that makes it feel perfect
		//		float detectW = CurrentWidth;
		//		var rect = new Rect(Screen.width - detectW - 10f, 20f, detectW, Screen.height - 40f);
		//		Vector2 mp = Input.mousePosition;
		//		mp.y = Screen.height - mp.y;

		//		IsMouseOver = rect.Contains(mp) || forceExpanded;

		//		// Expand
		//		if (IsMouseOver && !wasOver)
		//		{
		//			targetWidth = expandedWidth;
		//			animationStartTime = Time.time;
		//			mouseExitTime = 0f;
		//		}

		//		// Start collapse timer
		//		if (!IsMouseOver && wasOver && mouseExitTime == 0f)
		//			mouseExitTime = Time.time;

		//		// Collapse after delay
		//		if (!IsMouseOver && mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay)
		//		{
		//			targetWidth = collapsedWidth;
		//			animationStartTime = Time.time;
		//			mouseExitTime = 0f;
		//		}

		//		// Animate
		//		float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
		//		CurrentWidth = Mathf.Lerp(CurrentWidth, targetWidth, t);
		//	}

		//	public Rect GetRect(float topOffset = 20f, float bottomMargin = 20f)
		//	{
		//		float x = Screen.width - CurrentWidth - 10f;
		//		float y = topOffset;
		//		float h = Screen.height - y - bottomMargin;
		//		return new Rect(x, y, CurrentWidth, h);
		//	}

		//	public bool IsGuiActive() => GUIUtility.hotControl != 0 || IsMouseOver;
		//}


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

			// Cached to avoid allocations
			private Rect detectionRect;
			private Vector2 flippedMousePos;

			public AutoHidePanel(float collapsed = 120f, float expanded = 340f, float delay = 1f, float animDur = 0.25f)
			{
				collapsedWidth = collapsed;
				expandedWidth = expanded;
				autoHideDelay = delay;
				animationDuration = animDur;

				CurrentWidth = collapsedWidth;
				targetWidth = collapsedWidth;
				detectionRect = new Rect();
			}

			public void Update(bool forceExpanded = false)
			{
				bool wasOver = IsMouseOver;

				// do not use expanded width!!!!
				float detectX = Screen.width - CurrentWidth - 10f;
				detectionRect.x = detectX;
				detectionRect.y = 20f;
				detectionRect.width = CurrentWidth + 20f;
				detectionRect.height = Screen.height - 40f;

				flippedMousePos = Input.mousePosition;
				flippedMousePos.y = Screen.height - flippedMousePos.y;

				IsMouseOver = detectionRect.Contains(flippedMousePos) || forceExpanded;

				// Expand instantly on enter
				if (IsMouseOver && !wasOver)
				{
					targetWidth = expandedWidth;
					mouseExitTime = 0f;
				}

				// Start timer when mouse leaves
				if (!IsMouseOver && wasOver && mouseExitTime <= 0f)
					mouseExitTime = Time.time;

				// Collapse after delay
				if (!IsMouseOver && mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay)
				{
					targetWidth = collapsedWidth;
					mouseExitTime = 0f;
				}

				// Smooth animation
				CurrentWidth = Mathf.SmoothDamp(CurrentWidth, targetWidth, ref animationVelocity, animationDuration);
			}

			public Rect GetRect(float topOffset = 20f, float bottomMargin = 20f)
			{
				float x = Screen.width - CurrentWidth - 10f;
				float y = topOffset;
				float h = Screen.height - y - bottomMargin;
				return new Rect(x, y, CurrentWidth, h);
			}

			/// <summary>
			/// Draw the panel content — this version GUARANTEES no horizontal scrollbar
			/// </summary>
			public void DrawGUI()
			{
				Rect panelRect = GetRect();

				// This is the key: BeginArea completely isolates layout → no scrollbars ever
				GUILayout.BeginArea(panelRect);

				GUILayout.BeginVertical(GUILayout.Width(panelRect.width));

				// YOUR ACTUAL PANEL CONTENT GOES HERE
				// Example (replace with your real GUI):
				GUILayout.Box("Auto-Hide Panel", GUILayout.ExpandWidth(true));
				GUILayout.Space(10);

				GUILayout.Label("This panel will never show a horizontal scrollbar!", EditorStyles.wordWrappedLabel);

				if (GUILayout.Button("Test Button", GUILayout.Height(30)))
					Debug.Log("Button clicked!");

				// Add as many controls as you want — no horizontal bar will appear
				for (int i = 0; i < 20; i++)
					GUILayout.Label($"Item {i + 1}");

				// END OF YOUR CONTENT

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
	}
}