using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static partial class GuiUtils
	{
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

			public PopupItem(string label, Action action = null, Color? colorOverride = null, int spacerHeight = 8)
			{
				this.label = label;
				this.action = action;
				this.colorOverride = colorOverride;
				this.spacerHeight = spacerHeight;
			}

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
	}
}