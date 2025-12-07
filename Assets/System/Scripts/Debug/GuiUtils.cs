using System.Collections.Generic;
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
				if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) return;
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

			public Rect GetRect(float topOffset = 40f, float bottomMargin = 20f)
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

		public static class PopupConfirm
		{
			private static readonly GUIStyle centeredStyle;
			private static readonly GUIStyle titleStyle;

			static PopupConfirm()
			{
				centeredStyle = new GUIStyle(EditorStyles.label)
				{
					alignment = TextAnchor.MiddleCenter,
					wordWrap = true,
					fontSize = 12,
					normal = { textColor = Color.white },
					hover = { textColor = Color.white },
					active = { textColor = Color.white },
					focused = { textColor = Color.white }
				};

				titleStyle = new GUIStyle(EditorStyles.boldLabel)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 16,
					normal = { textColor = Color.white },
					hover = { textColor = Color.white },
					active = { textColor = Color.white },
					focused = { textColor = Color.white },
					margin = new RectOffset(0, 0, 6, 10)
				};
			}

			public static bool Show(
				Vector2 screenPos,
				Vector2 size,
				string title,
				string message = null,
				string yesText = "Yes",
				string noText = "No",
				Color? titleColor = null,
				System.Action onYes = null)
			{
				var rect = new Rect(screenPos.x, screenPos.y, size.x, size.y);
				bool result = false;

				// This is literally how Unity does all its popups
				GUI.Box(rect, "", GUI.skin.window);

				GUILayout.BeginArea(rect);
				{
					GUILayout.Space(12);

					GUI.color = titleColor ?? new Color(0.3f, 0.9f, 1f);
					GUILayout.Label(title, titleStyle);
					GUI.color = Color.white;

					if (!string.IsNullOrEmpty(message))
						GUILayout.Label(message, centeredStyle);

					GUILayout.FlexibleSpace();

					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();

					GUI.backgroundColor = new Color(0.2f, 0.95f, 0.2f);
					if (GUILayout.Button(yesText, GUILayout.Width(90), GUILayout.Height(30)))
					{
						onYes?.Invoke();
						result = true;
					}

					GUILayout.Space(16);

					GUI.backgroundColor = new Color(0.95f, 0.25f, 0.25f);
					if (GUILayout.Button(noText, GUILayout.Width(90), GUILayout.Height(30)))
						result = true;

					GUI.backgroundColor = Color.white;
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();

					GUILayout.Space(14);
				}
				GUILayout.EndArea();

				return result;
			}
		}

		public static class PopupMenu
		{
			public static bool Show(Vector2 screenPos, Vector2 size, string title, string[] options, System.Action<int> onSelect)
			{
				var rect = new Rect(screenPos.x, screenPos.y, size.x, size.y);

				if (Event.current.type == EventType.MouseDown && !rect.Contains(Event.current.mousePosition))
				{
					onSelect?.Invoke(-1);
					return true;
				}

				GUI.Box(rect, title, GUI.skin.window);

				for (int i = 0; i < options.Length; i++)
				{
					if (GUI.Button(new Rect(rect.x + 8, rect.y + 30 + i * 26, rect.width - 16, 24), options[i]))
					{
						onSelect?.Invoke(i);
						return true;
					}
				}

				return false; // keep open
			}
		}

		// ─────────────────────────────────────────────────────────────────────────────
		// PURE GUI POPUP — knows nothing about MapAttachment, View, Emitter, etc.
		// ─────────────────────────────────────────────────────────────────────────────
		public static class PopupAttachmentAdd
		{
			public static bool Show(
				Vector2 screenPos,
				int tileIndex,
				System.Action<int> onSelect) // 0=Emitter, 1=View, 2=Pickup, -1=Cancel
			{
				var options = new[] { "Add Emitter", "Add View", "Add Pickup", "Cancel" };

				return PopupMenu.Show(screenPos, new Vector2(260, 30 + options.Length * 26),
					$"Add to Tile {tileIndex}", options, i =>
					{
						onSelect?.Invoke(i < 3 ? i : -1); // -1 for Cancel
					});
			}
		}

		public static class PopupAttachmentDelete
		{
			public static bool Show(
				Vector2 screenPos,
				int tileIndex,
				int attachmentCount,
				System.Action<int> onSelect) // 0..count-1 = single delete, count = delete all (only if >1), count+1 = cancel
			{
				if (attachmentCount == 0) return true;

				var options = new List<string>();

				// Always show individual deletes
				for (int i = 0; i < attachmentCount; i++)
					options.Add($"Delete attachment {i + 1}");

				// Only show "Delete All" if more than one
				int deleteAllIndex = -1;
				if (attachmentCount > 1)
				{
					deleteAllIndex = options.Count;
					options.Add("Delete All on this tile");
				}

				options.Add("Cancel");
				int cancelIndex = options.Count - 1;

				return PopupMenu.Show(screenPos, new Vector2(320, 30 + options.Count * 26),
					$"Tile {tileIndex} — {attachmentCount} attachment(s)",
					options.ToArray(), i =>
					{
						if (i < attachmentCount)
							onSelect?.Invoke(i);                    // single delete
						else if (i == deleteAllIndex)
							onSelect?.Invoke(-2);                   // special code for "delete all"
						else if (i == cancelIndex)
							onSelect?.Invoke(-1);                   // cancel
					});
			}
		}
	}
}