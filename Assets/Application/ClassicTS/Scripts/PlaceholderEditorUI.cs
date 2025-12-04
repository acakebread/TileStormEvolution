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

		// === Shared Panel System (used by BOTH Paint and Waypoint modes) ===
		private float tileSelectorWidth = 120f;
		private const float fullWidth = 340f;         // Expanded width (both panels)
		private const float collapsedWidth = 120f;    // Collapsed width
		private float targetWidth = 120f;
		private float mouseExitTime = 0f;
		private bool isMouseOverTileSelector = false;
		private const float autoHideDelay = 1f;
		private float animationStartTime = 0f;
		private const float animationDuration = 0.3f;

		// Separate scroll positions for each panel
		private Vector2 tileScrollPosition = Vector2.zero;
		private Vector2 waypointScrollPosition = Vector2.zero;

		// Paint-specific caching
		private float cachedContentHeight = -1f;
		private int cachedCount = -1;

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

		public void Start()
		{
			TryGetComponent<PlaceholderUI>(out var placeholderUI);
			panelYoffset = placeholderUI ? placeholderUI.GetPanelBottomY() : 10f;
		}

		public bool IsGuiControlActive()
			=> GUIUtility.hotControl != 0 || isMouseOverTileSelector || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		public bool IsMouseInsideWindow()
		{
			var p = Input.mousePosition;
			return p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height;
		}

		public bool IsMouseOverGui()
		{
			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			// Left button panel
			float leftY = panelYoffset + spacing;
			if (new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(mousePos))
				return true;

			// Right panel — always detect using full width zone (prevents input blocking)
			float rx = Screen.width - fullWidth - margin;
			float ry = panelYoffset + spacing;
			float rh = Screen.height - ry - margin;
			return new Rect(rx, ry, fullWidth, rh).Contains(mousePos);
		}

		public void UpdateMode(string mode)
		{
			var wasOver = isMouseOverTileSelector;

			// Use maximum detection zone for both modes
			float detectionWidth = fullWidth;
			var x = Screen.width - detectionWidth - margin;
			var y = panelYoffset + spacing;
			var h = Screen.height - y - margin;

			Vector2 mp = Input.mousePosition;
			mp.y = Screen.height - mp.y;

			isMouseOverTileSelector = new Rect(x, y, detectionWidth, h).Contains(mp);

			// Expand on enter
			if (isMouseOverTileSelector && !wasOver)
			{
				targetWidth = fullWidth;
				animationStartTime = Time.time;
				mouseExitTime = 0f;
			}

			// Start collapse timer when leaving
			if (!isMouseOverTileSelector && wasOver && mouseExitTime == 0f)
			{
				mouseExitTime = Time.time;
			}

			// Collapse after delay
			if (!isMouseOverTileSelector && mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay)
			{
				targetWidth = collapsedWidth;
				animationStartTime = Time.time;
				mouseExitTime = 0f;
			}

			// Animate width
			float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
			tileSelectorWidth = Mathf.Lerp(tileSelectorWidth, targetWidth, t);
		}

		public void DrawMainUI(string mode, bool gridVisible)
		{
			float y = panelYoffset + spacing;

			Color prevColor = GUI.color;
			Color prevContentColor = GUI.contentColor;
			Color prevBgColor = GUI.backgroundColor;

			GuiUtils.ColoredButton(new Rect(margin, y + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight),
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

			GUI.color = Color.white;
			GUI.contentColor = Color.white;
			GUI.backgroundColor = Color.white;

			GuiUtils.ColoredButton(new Rect(margin, y + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f), OnImportMapRequested);
			GuiUtils.ColoredButton(new Rect(margin, y + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f), OnExportMapRequested);
			GuiUtils.ColoredButton(new Rect(margin, y + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Reload Database", new Color(0.2f, 0.6f, 1f), OnReloadDatabaseRequested);
			GuiUtils.ColoredButton(new Rect(margin, y + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f), OnSaveDatabaseRequested);

			GUI.color = prevColor;
			GUI.contentColor = prevContentColor;
			GUI.backgroundColor = prevBgColor;
		}

		public void DrawPaintUI(string selectedId)
		{
			var tx = Screen.width - tileSelectorWidth - margin;
			var ty = panelYoffset + spacing;
			var th = Screen.height - ty - margin;
			Rect selectorRect = new Rect(tx, ty, tileSelectorWidth, th);

			GUI.backgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
			GUI.Box(selectorRect, "Tile Selector");
			GUI.backgroundColor = Color.white;

			Rect view = new Rect(tx + 10, ty + 30, tileSelectorWidth - 20, th - 40);

			int count = ResourceManager.Definitions.Count;
			if (cachedCount != count)
			{
				cachedCount = count;
				cachedContentHeight = count * 40f;
			}

			tileScrollPosition = GUI.BeginScrollView(view, tileScrollPosition,
				new Rect(0, 0, tileSelectorWidth - 40, cachedContentHeight));

			int itemHeight = 40;
			int firstVisible = Mathf.FloorToInt(tileScrollPosition.y / itemHeight);
			int lastVisible = firstVisible + Mathf.CeilToInt(view.height / itemHeight) + 2;
			firstVisible = Mathf.Max(0, firstVisible);
			lastVisible = Mathf.Min(lastVisible, count);

			var definitions = ResourceManager.Definitions;

			for (int i = firstVisible; i < lastVisible; i++)
			{
				var def = definitions.ElementAt(i);
				string label = $"{def.id} ({def.texture})";

				Rect btn = new Rect(0, i * itemHeight, tileSelectorWidth - 40, 35);

				if (def.id == selectedId)
					GUI.color = Color.green;

				if (GUI.Button(btn, label))
					OnTileSelected?.Invoke(def.id);

				GUI.color = Color.white;
			}

			GUI.EndScrollView();
		}

		public void DrawWaypointUI(Waypoint[] waypoints, int selectedIndex = -1)
		{
			float x = Screen.width - tileSelectorWidth - margin;
			float y = panelYoffset + spacing;
			float h = Screen.height - y - margin;

			Rect panelRect = new Rect(x, y, tileSelectorWidth, h);

			GUI.backgroundColor = new Color(0.15f, 0.3f, 0.42f, 0.92f);
			GUI.Box(panelRect, "");
			GUI.backgroundColor = Color.white;

			GUI.Label(new Rect(x + 10, y + 5, tileSelectorWidth - 20, 25), "Waypoints");

			if (waypoints == null || waypoints.Length == 0)
			{
				GUI.Label(new Rect(x + 10, y + 40, tileSelectorWidth - 20, 50),
					"No waypoints\nClick map to add");
				return;
			}

			Rect scrollRect = new Rect(x + 10, y + 35, tileSelectorWidth - 20, h - 180);
			waypointScrollPosition = GUI.BeginScrollView(scrollRect, waypointScrollPosition,
				new Rect(0, 0, tileSelectorWidth - 40, waypoints.Length * 46f));

			for (int i = 0; i < waypoints.Length; i++)
			{
				var wp = waypoints[i];
				string cam = wp.IsCamera() ? " [Cam]" : "";
				string status = wp.tile < 0 ? " [UNPLACED]" : $" [{wp.tile}]";
				string label = $"{i:00}: WP{i}{status}{cam}";

				Rect r = new Rect(0, i * 46f, tileSelectorWidth - 40, 42);

				if (wp.tile < 0)
					GUI.color = new Color(1f, 0.6f, 0.2f);
				else if (i == selectedIndex)
					GUI.color = new Color(0.3f, 0.8f, 1f);
				else
					GUI.color = Color.white;

				if (GUI.Button(r, label))
					OnWaypointSelected?.Invoke(i);

				GUI.color = Color.white;
			}
			GUI.EndScrollView();

			float controlY = y + h - 120;

			GUI.enabled = selectedIndex > 0;
			if (GUI.Button(new Rect(x + 10, controlY, 100, 30), "Move Up"))
				OnWaypointMoveUp?.Invoke(selectedIndex);

			GUI.enabled = selectedIndex >= 0 && selectedIndex < waypoints.Length - 1;
			if (GUI.Button(new Rect(x + 120, controlY, 100, 30), "Move Down"))
				OnWaypointMoveDown?.Invoke(selectedIndex);

			GUI.enabled = true;
			controlY += 40;
			if (GUI.Button(new Rect(x + 10, controlY, tileSelectorWidth - 20, 32), "Add New"))
				OnWaypointAddRequested?.Invoke();

			controlY += 40;
			GUI.color = Color.red;
			if (GUI.Button(new Rect(x + 10, controlY, tileSelectorWidth - 20, 32), "Delete Selected"))
				OnWaypointDelete?.Invoke(selectedIndex);
			GUI.color = Color.white;
		}
	}
}