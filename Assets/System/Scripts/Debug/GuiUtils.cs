using System;
using System.Collections.Generic;
using System.Linq;
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

		private class HoldState
		{
			public bool isPressed;
			public float nextFireTime;
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
				const float WIDTH = 260f;
				const float ITEM_HEIGHT = 26f;
				const float SPACER_DEFAULT = 8f;
				const float TITLE_HEIGHT = 34f;
				const float PADDING_BOTTOM = 8f;

				// Calculate total height
				float height = TITLE_HEIGHT;
				foreach (var it in items)
					height += (it.label == null) ? (it.spacerHeight > 0 ? it.spacerHeight : SPACER_DEFAULT) : ITEM_HEIGHT;
				height += PADDING_BOTTOM;

				// === AUTO POSITIONING: Center horizontally, place above cursor ===
				float x = screenPos.x - WIDTH * 0.5f;  // center horizontally
				float y = screenPos.y - height - 10f; // place above cursor with small gap

				// Clamp to screen bounds to avoid going off-screen
				if (x < 10f) x = 10f;
				if (x + WIDTH > Screen.width - 10f) x = Screen.width - WIDTH - 10f;
				if (y < 10f) y = screenPos.y + 20f; // if no room above, place below instead
				if (y + height > Screen.height - 10f) y = Screen.height - height - 10f;

				var rect = new Rect(x, y, WIDTH, height);

				// 1. Draw the window background
				GUI.Box(rect, GUIContent.none, GUI.skin.window);

				// 2. Draw the title
				var titleStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold,
					fontSize = 14
				};
				titleStyle.normal.textColor = Color.white;

				Rect titleRect = new Rect(rect.x, rect.y + 5, rect.width, 24f);
				GUI.Label(titleRect, title, titleStyle);

				// 3. Draw items
				float yOffset = rect.y + TITLE_HEIGHT;
				foreach (var item in items)
				{
					if (item.label == null)
					{
						yOffset += item.spacerHeight > 0 ? item.spacerHeight : SPACER_DEFAULT;
						continue;
					}

					var btnRect = new Rect(rect.x + 8f, yOffset, rect.width - 16f, ITEM_HEIGHT - 2f);

					var oldColor = GUI.color;
					if (item.colorOverride.HasValue)
						GUI.color = item.colorOverride.Value;

					if (GUI.Button(btnRect, item.label))
					{
						GUI.color = oldColor;
						item.action?.Invoke();
						return true; // closed
					}

					GUI.color = oldColor;
					yOffset += ITEM_HEIGHT;
				}

				// Click outside closes
				if (Event.current.type == EventType.MouseDown && !rect.Contains(Event.current.mousePosition))
					return true;

				return false;
			}
		}

		public struct ListViewItem
		{
			public string Label;
			public Action OnClick;
			public bool IsSelected;

			public ListViewItem(string label, Action onClick, bool selected = false)
			{
				Label = label;
				OnClick = onClick;
				IsSelected = selected;
			}
		}

		public struct ListViewButton
		{
			public string Label;
			public Action OnClick;
			public Color? ColorOverride;
			public bool Enabled;

			public ListViewButton(string label, Action onClick, Color? color = null, bool enabled = true)
			{
				Label = label;
				OnClick = onClick;
				ColorOverride = color;
				Enabled = enabled;
			}
		}

		public class ListView
		{
			private Vector2 scrollPos;
			private GUIStyle leftButton;

			public List<ListViewItem> Items { get; private set; } = new();

			public void SetItems(IEnumerable<ListViewItem> items)
			{
				Items = items.ToList();
			}

			public void Clear() => Items.Clear();

			public void AddItem(ListViewItem item) => Items.Add(item);

			public void Draw(Rect rect)
			{
				if (leftButton == null)
				{
					leftButton = new GUIStyle(GUI.skin.button)
					{
						alignment = TextAnchor.MiddleLeft,
						padding = new RectOffset(12, 4, 4, 4)
					};
				}

				float itemHeight = 32f;
				float scrollbarWidth = 12f;

				Rect viewRect = new Rect(rect.x, rect.y + 8, rect.width, rect.height - 8);
				Rect contentRect = new Rect(0, 0, viewRect.width - scrollbarWidth - 6, Items.Count * itemHeight);

				scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect, false, true);

				float y = 0;
				foreach (var item in Items)
				{
					GUI.backgroundColor = item.IsSelected ?
						new Color(0.3f, 0.8f, 1f, 0.9f) :
						Color.white;

					if (GUI.Button(new Rect(0, y, contentRect.width, itemHeight - 4), item.Label, leftButton))
						item.OnClick?.Invoke();

					GUI.backgroundColor = Color.white;
					y += itemHeight;
				}

				GUI.EndScrollView();
			}
		}

		public class AutoHidePanel
		{
			public float CurrentWidth { get; private set; }
			public bool IsMouseOver { get; private set; }

			private float collapsedWidth;
			private float expandedWidth;
			private float autoHideDelay;
			private float animDuration;
			private float targetWidth;
			private float animVel;
			private float exitTime;

			public ListView List { get; private set; } = new();
			public List<ListViewButton> Buttons { get; private set; } = new();

			private string footnote;
			public Vector2 DefaultPosition { get; private set; } = new(0f, 40f);

			public AutoHidePanel(float collapsed, float expanded, float delay, float animDur, Vector2? defaultPos = null)
			{
				collapsedWidth = collapsed;
				expandedWidth = expanded;
				autoHideDelay = delay;
				animDuration = animDur;
				CurrentWidth = collapsedWidth;
				targetWidth = collapsedWidth;

				if (defaultPos.HasValue)
					DefaultPosition = defaultPos.Value;
			}

			public void Update()
			{
				if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) return;

				float detectX = Screen.width - CurrentWidth - 10f;
				Rect detectRect = new Rect(detectX, 20f, CurrentWidth + 20f, Screen.height - 40f);

				Vector2 mp = Input.mousePosition;
				mp.y = Screen.height - mp.y;

				bool wasOver = IsMouseOver;
				IsMouseOver = detectRect.Contains(mp);

				if (IsMouseOver && !wasOver) { targetWidth = expandedWidth; exitTime = 0; }
				if (!IsMouseOver && wasOver) exitTime = Time.time;
				if (!IsMouseOver && exitTime > 0 && Time.time - exitTime >= autoHideDelay)
				{
					targetWidth = collapsedWidth;
					exitTime = 0;
				}

				CurrentWidth = Mathf.SmoothDamp(CurrentWidth, targetWidth, ref animVel, animDuration);
			}

			public Rect GetPanelRect(float margin = 20f)
			{
				return new Rect(
					Screen.width - CurrentWidth - 10f,
					DefaultPosition.y,
					CurrentWidth,
					Screen.height - DefaultPosition.y - margin
				);
			}

			public void SetFootnote(string text)
			{
				footnote = text;
			}

			public void Draw()
			{
				Rect panel = GetPanelRect();
				GUI.Box(panel, GUIContent.none);

				// Reserve space for buttons + optional footnote
				float buttonRowHeight = Buttons.Count > 0 ? 40f : 0f;
				float footnoteHeight = !string.IsNullOrEmpty(footnote) ? 28f : 0f;

				// Draw the ListView
				Rect listRect = new Rect(
					panel.x + 6f,                  // move 6 pixels right
					panel.y,
					panel.width - 6f,              // shrink width by 6 so it fits
					panel.height - buttonRowHeight - 6f - footnoteHeight
				);

				List.Draw(listRect);

				// Draw buttons at bottom
				float y = panel.y + listRect.height + 4f;
				Rect btnRect = new Rect(panel.x + 6f, y, panel.width - 12f, 36f);

				GUILayout.BeginArea(new Rect(btnRect.x, btnRect.y, btnRect.width, btnRect.height));
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();

				foreach (var btn in Buttons)
				{
					var oldColor = GUI.color;
					if (btn.ColorOverride.HasValue) GUI.color = btn.ColorOverride.Value;

					GUI.enabled = btn.Enabled;
					if (GUILayout.Button(btn.Label, GUILayout.Width(100), GUILayout.Height(30)))
						btn.OnClick?.Invoke();
					GUI.enabled = true;

					GUI.color = oldColor;
				}

				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.EndArea();

				// Draw footnote below buttons
				if (!string.IsNullOrEmpty(footnote))
				{
					Rect footRect = new Rect(panel.x + 6f, panel.y + panel.height - footnoteHeight - 4f, panel.width - 12f, footnoteHeight);
					GUI.Label(footRect, footnote, new GUIStyle(GUI.skin.label)
					{
						alignment = TextAnchor.MiddleCenter,
						fontSize = 10
					});
				}
			}

			public void ForceExpand()
			{
				targetWidth = expandedWidth;
				exitTime = 0;
			}

			public bool IsGuiActive() => GUIUtility.hotControl != 0 || IsMouseOver;
		}
	}
}