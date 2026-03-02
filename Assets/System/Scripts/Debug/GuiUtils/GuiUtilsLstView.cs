using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static partial class GuiUtils
	{
		public struct ListViewItem
		{
			public string Label;
			public Action<int> OnClick;
			public bool IsSelected;

			public ListViewItem(string label, Action<int> onClick = null, bool selected = false)
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
						item.OnClick?.Invoke(Items.IndexOf(item));

					GUI.backgroundColor = Color.white;
					y += itemHeight;
				}

				GUI.EndScrollView();
			}
		}

		public class AutoHidePanel
		{
			public float CurrentWidth { get; private set; }
			public bool IsMouseOver { get; set; }

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
				if (InputX.GetMouseButton(0) || InputX.GetMouseButton(1))
					return;

				float detectX = Screen.width - CurrentWidth - 10f;
				Rect detectRect = new Rect(detectX, 20f, CurrentWidth + 20f, Screen.height - 40f);

				Vector2 mp = InputX.mousePosition;
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

			public Rect GetPanelRect(float margin = 20f) => new Rect( Screen.width - CurrentWidth - 10f, DefaultPosition.y, CurrentWidth, Screen.height - DefaultPosition.y - margin );

			public void SetFootnote(string text) => footnote = text;

			public void Draw()
			{
				Update();

				if (IsMouseOver)
					MarkGuiActive();

				Rect panel = GetPanelRect();
				GUI.Box(panel, GUIContent.none);

				// Reserve space for buttons + optional footnote
				float buttonRowHeight = Buttons.Count > 0 ? 40f : 0f;
				float footnoteHeight = !string.IsNullOrEmpty(footnote) ? 28f : 0f;

				// Draw the ListView
				Rect listRect = new(panel.x + 6f, panel.y, panel.width - 6f, panel.height - buttonRowHeight - 6f - footnoteHeight);// move 6 pixels right and shrink width by 6 so it fits
				List.Draw(listRect);

				// Draw buttons at bottom
				float y = panel.y + listRect.height + 4f;
				Rect btnRect = new(panel.x + 6f, y, panel.width - 12f, 36f);

				GUILayout.BeginArea(new(btnRect.x, btnRect.y, btnRect.width, btnRect.height));
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
					Rect footRect = new(panel.x + 6f, panel.y + panel.height - footnoteHeight - 4f, panel.width - 12f, footnoteHeight);
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