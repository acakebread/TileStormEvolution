using UnityEngine;
using UnityEngine.EventSystems;
using System;
using MassiveHadronLtd;
using System.Linq;

namespace ClassicTilestorm
{
	public class PlaceholderEditorUI : MonoBehaviour
	{
		private float panelYoffset = 10f;

		private const float margin = 10f;
		private const float spacing = 10f;
		private const float buttonWidth = 135f;
		private const float buttonHeight = 30f;

		// ONE shared auto-hiding side panel — handles both Paint and Waypoint perfectly
		private readonly GuiUtils.AutoHidePanel sidePanel = new(collapsed: 120f, expanded: 340f, delay: 1f, animDur: 0.3f);

		// Separate scroll positions
		private Vector2 tileScrollPosition = Vector2.zero;
		private Vector2 waypointScrollPosition = Vector2.zero;

		// Paint tile list caching
		private float cachedContentHeight = -1f;
		private int cachedCount = -1;

		// Events
		public event Action<string> OnModeChanged;
		public event Action<bool> OnGridLinesToggled;
		public event Action OnSaveDatabaseRequested;
		public event Action OnReloadDatabaseRequested;
		public event Action OnExportMapRequested;
		public event Action OnImportMapRequested;
		public event Action<string> OnTileSelected;

		public event Action<int> OnWaypointSelected;
		public event Action OnWaypointAddRequested;
		public event Action<int> OnWaypointMoveUp;
		public event Action<int> OnWaypointMoveDown;
		public event Action<int> OnWaypointDelete;

		private void Start()
		{
			if (TryGetComponent<PlaceholderUI>(out var placeholderUI))
				panelYoffset = placeholderUI.GetPanelBottomY();
			else
				panelYoffset = 10f;
		}

		public bool IsGuiControlActive()
			=> sidePanel.IsGuiActive() || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		public bool IsMouseOverGui()
		{
			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			// Left button column
			float leftY = panelYoffset + spacing;
			if (new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(mousePos))
				return true;

			// Right side panel (uses expanded detection zone)
			return sidePanel.IsMouseOver;
		}

		public void DrawMainUI(string mode, bool gridVisible)
		{
			// This single line updates hover detection + animation for BOTH modes
			sidePanel.Update();

			float y = panelYoffset + spacing;

			// === Left Column Buttons ===
			GuiUtils.ColoredButton(
				new Rect(margin, y + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight),
				gridVisible ? "Hide Grid" : "Show Grid",
				new Color(0.25f, 0.75f, 0.25f),
				() => OnGridLinesToggled?.Invoke(!gridVisible));

			GUI.contentColor = mode == "Drag" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Drag"))
				OnModeChanged?.Invoke("Drag");

			GUI.contentColor = mode == "Paint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Paint"))
				OnModeChanged?.Invoke("Paint");

			GUI.contentColor = mode == "Waypoint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Waypoint"))
				OnModeChanged?.Invoke("Waypoint");

			GUI.contentColor = Color.white;

			GuiUtils.ColoredButton(new Rect(margin, y + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f), OnImportMapRequested);
			GuiUtils.ColoredButton(new Rect(margin, y + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f), OnExportMapRequested);
			GuiUtils.ColoredButton(new Rect(margin, y + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Reload Database", new Color(0.2f, 0.6f, 1f), OnReloadDatabaseRequested);
			GuiUtils.ColoredButton(new Rect(margin, y + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f), OnSaveDatabaseRequested);
		}

		public void DrawPaintUI(string selectedId)
		{
			var r = sidePanel.GetRect(panelYoffset + spacing, 20f);

			GUI.backgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
			GUI.Box(r, "Tile Selector");
			GUI.backgroundColor = Color.white;

			var view = new Rect(r.x + 10, r.y + 30, r.width - 20, r.height - 40);

			int count = ResourceManager.Definitions.Count;
			if (cachedCount != count)
			{
				cachedCount = count;
				cachedContentHeight = count * 40f;
			}

			tileScrollPosition = GUI.BeginScrollView(view, tileScrollPosition,
				new Rect(0, 0, r.width - 40, cachedContentHeight));

			int itemHeight = 40;
			int first = Mathf.FloorToInt(tileScrollPosition.y / itemHeight);
			int last = first + Mathf.CeilToInt(view.height / itemHeight) + 2;
			first = Mathf.Max(0, first);
			last = Mathf.Min(last, count);

			for (int i = first; i < last; i++)
			{
				var def = ResourceManager.Definitions.ElementAt(i);
				string label = $"{def.id} ({def.texture})";
				var btn = new Rect(0, i * itemHeight, r.width - 40, 35);

				if (def.id == selectedId) GUI.color = Color.green;
				if (GUI.Button(btn, label)) OnTileSelected?.Invoke(def.id);
				GUI.color = Color.white;
			}

			GUI.EndScrollView();
		}

		public void DrawWaypointUI(Waypoint[] waypoints, int selectedIndex = -1)
		{
			var r = sidePanel.GetRect(panelYoffset + spacing, 20f);

			GUI.backgroundColor = new Color(0.15f, 0.3f, 0.42f, 0.92f);
			GUI.Box(r, "");
			GUI.backgroundColor = Color.white;

			GUI.Label(new Rect(r.x + 10, r.y + 5, r.width - 20, 25), "Waypoints");

			if (waypoints == null || waypoints.Length == 0)
			{
				GUI.Label(new Rect(r.x + 10, r.y + 40, r.width - 20, 60),
					"No waypoints\nLeft-click map to add",
					new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
				return;
			}

			// Scrollable list
			var scrollRect = new Rect(r.x + 10, r.y + 35, r.width - 20, r.height - 100);
			waypointScrollPosition = GUI.BeginScrollView(scrollRect, waypointScrollPosition,
				new Rect(0, 0, r.width - 40, waypoints.Length * 46f));

			for (int i = 0; i < waypoints.Length; i++)
			{
				var wp = waypoints[i];
				string cam = wp.IsCamera() ? " [Cam]" : "";
				string label = $"WP{i:00}{cam} [{wp.tile}]";

				var btn = new Rect(0, i * 46f, r.width - 40, 42);
				GUI.color = (i == selectedIndex) ? new Color(0.3f, 0.8f, 1f) : Color.white;

				if (GUI.Button(btn, label))
					OnWaypointSelected?.Invoke(i);

				GUI.color = Color.white;
			}
			GUI.EndScrollView();

			// Move Up / Move Down buttons only
			float btnY = r.y + r.height - 50;

			GUI.enabled = selectedIndex > 0;
			if (GUI.Button(new Rect(r.x + 10, btnY, 100, 30), "Move Up"))
				OnWaypointMoveUp?.Invoke(selectedIndex);

			GUI.enabled = selectedIndex >= 0 && selectedIndex < waypoints.Length - 1;
			if (GUI.Button(new Rect(r.x + 120, btnY, 100, 30), "Move Down"))
				OnWaypointMoveDown?.Invoke(selectedIndex);

			GUI.enabled = true;
		}
	}
}