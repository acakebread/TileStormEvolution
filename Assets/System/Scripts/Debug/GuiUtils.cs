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
			float initialDelay = 0f,
			float repeatInterval = 0.05f)
		{
			EnsureStyles();

			int id = GUIUtility.GetControlID(FocusType.Passive);
			var state = GUIUtility.GetStateObject(typeof(HoldState), id) as HoldState ?? new HoldState();

			Color oldBg = GUI.backgroundColor;
			Color oldContent = GUI.contentColor;
			Color oldColor = GUI.color;

			GUI.backgroundColor = state.isPressed ? color * 0.8f : color;
			GUI.contentColor = Color.white;
			GUI.color = Color.white;

			GUI.Button(rect, text, coloredButtonStyle);

			Event e = Event.current;
			bool inRect = rect.Contains(e.mousePosition);
			bool fired = false;

			if (e.type == EventType.MouseDown && inRect && GUIUtility.hotControl == 0)
			{
				GUIUtility.hotControl = id;
				state.isPressed = true;
				onRepeat?.Invoke();
				fired = true;
				state.nextFireTime = Time.time + initialDelay;
				e.Use();
			}

			if (state.isPressed && GUIUtility.hotControl == id)
			{
				if (Time.time >= state.nextFireTime)
				{
					onRepeat?.Invoke();
					fired = true;
					state.nextFireTime = Time.time + repeatInterval;
				}
			}

			if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
			{
				GUIUtility.hotControl = 0;
				state.isPressed = false;
			}

			GUI.backgroundColor = oldBg;
			GUI.contentColor = oldContent;
			GUI.color = oldColor;

			return fired;
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
					UnityEngine.Debug.Log("Button clicked!");
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

		public static class PopupConfirm
		{
			public static bool Show(
				Vector2 screenPos,
				Vector2 size,
				string title,
				string message = null,
				string yesText = "Yes",
				string noText = "No",
				Color? titleColor = null,
				Action onYes = null)
			{
				Rect rect = new Rect(screenPos.x - size.x * 0.5f, screenPos.y - size.y * 0.5f, size.x, size.y);

				GUI.Box(rect, "", PopupStyles.window);
				GUILayout.BeginArea(rect);
				{
					GUILayout.Space(12);
					GUI.color = titleColor ?? new Color(0.3f, 0.9f, 1f);
					GUILayout.Label(title, PopupStyles.title);
					GUI.color = Color.white;

					if (!string.IsNullOrEmpty(message))
						GUILayout.Label(message, PopupStyles.message);

					GUILayout.FlexibleSpace();

					GUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();

					GUI.backgroundColor = new Color(0.2f, 0.95f, 0.2f);
					if (GUILayout.Button(yesText, PopupStyles.button, GUILayout.Width(90), GUILayout.Height(30)))
					{
						onYes?.Invoke();
						GUI.backgroundColor = Color.white;
						return true;
					}

					GUILayout.Space(16);

					GUI.backgroundColor = new Color(0.95f, 0.25f, 0.25f);
					if (GUILayout.Button(noText, PopupStyles.button, GUILayout.Width(90), GUILayout.Height(30)))
						return true;

					GUI.backgroundColor = Color.white;
					GUILayout.FlexibleSpace();
					GUILayout.EndHorizontal();

					GUILayout.Space(14);
				}
				GUILayout.EndArea();

				if (Event.current.type == EventType.MouseDown && !rect.Contains(Event.current.mousePosition))
					return true;

				return false;
			}
		}

		public static class PopupMenu
		{
			public static bool Show(Vector2 screenPos, Vector2 size, string title, string[] options, Action<int> onSelect)
			{
				Rect rect = new Rect(screenPos.x, screenPos.y, size.x, size.y);
				GUI.Box(rect, title, GUI.skin.window);

				for (int i = 0; i < options.Length; i++)
				{
					if (GUI.Button(new Rect(rect.x + 8, rect.y + 30 + i * 26, rect.width - 16, 24), options[i]))
					{
						onSelect?.Invoke(i);
						return true;
					}
				}

				if (Event.current.type == EventType.MouseDown && !rect.Contains(Event.current.mousePosition))
				{
					onSelect?.Invoke(-1);
					return true;
				}

				return false;
			}
		}

		public static class PopupAttachmentAdd
		{
			public static bool Show(Vector2 screenPos, int tileIndex, Action<int> onSelect)
			{
				var options = new[] { "Add Emitter", "Add View", "Add Pickup", "Cancel" };
				return PopupMenu.Show(screenPos, new Vector2(260, 30 + options.Length * 26),
					$"Add to Tile {tileIndex}", options, i => onSelect?.Invoke(i < 3 ? i : -1));
			}
		}

		public static class PopupAttachmentDelete
		{
			public static bool Show(Vector2 screenPos, int tileIndex, string[] text, Action<int> onSelect)
			{
				if (text.Length == 0) return true;

				var options = new List<string>();
				for (int i = 0; i < text.Length; i++)
					options.Add($"Delete {text[i]}");

				int deleteAllIndex = -1;
				if (text.Length > 1)
				{
					deleteAllIndex = options.Count;
					options.Add("Delete All on this tile");
				}
				options.Add("Cancel");

				return PopupMenu.Show(screenPos, new Vector2(320, 30 + options.Count * 26),
					$"Tile {tileIndex} — {text.Length} attachment(s)",
					options.ToArray(), i =>
					{
						if (i < text.Length) onSelect?.Invoke(i);
						else if (i == deleteAllIndex) onSelect?.Invoke(-2);
						else onSelect?.Invoke(-1);
					});
			}
		}
	}
}