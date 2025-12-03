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

		private float tileSelectorWidth = 120f;
		private const float fullWidth = 300f;
		private const float collapsedWidth = 120f;
		private float mouseExitTime = 0f;
		private bool isMouseOverTileSelector = false;
		private const float autoHideDelay = 1f;
		private float targetWidth = 120f;
		private float animationStartTime = 0f;
		private const float animationDuration = 0.3f;
		private Vector2 scrollPosition = Vector2.zero;

		public event Action<string> OnModeChanged;
		public event Action<bool> OnGridLinesToggled;
		public event Action OnSaveDatabaseRequested;
		public event Action OnReloadDatabaseRequested;
		public event Action OnExportMapRequested;
		public event Action OnImportMapRequested;
		public event Action<string> OnTileSelected;

		public event Action<int> OnWaypointSelected;        // User clicked a waypoint
		public event Action OnWaypointAddRequested;         // Add new waypoint button
		public event Action<int> OnWaypointMoveUp;
		public event Action<int> OnWaypointMoveDown;
		public event Action<int> OnWaypointDelete;
		private float waypointPanelWidth = 120f;
		private float waypointTargetWidth = 120f;
		private float waypointMouseExitTime = 0f;
		private bool isMouseOverWaypointPanel = false;
		private const float waypointAutoHideDelay = 1f;
		private Vector2 waypointScrollPosition = Vector2.zero;
		private float waypointAnimationStartTime = 0f;
		private const float waypointAnimationDuration = 0.3f;

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
			if (isMouseOverTileSelector) return true;

			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			float leftY = panelYoffset + spacing;
			if (new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(mousePos))
				return true;

			float tx = Screen.width - tileSelectorWidth - margin;
			float ty = panelYoffset + spacing;
			float th = Screen.height - ty - margin;
			return new Rect(tx, ty, tileSelectorWidth, th).Contains(mousePos);
		}

		public void UpdatePaintMode()
		{
			var mp = Input.mousePosition;
			mp.y = Screen.height - mp.y;

			var wasOver = isMouseOverTileSelector;

			var x = Screen.width - tileSelectorWidth - margin;
			var y = panelYoffset + spacing;
			var h = Screen.height - y - margin;

			isMouseOverTileSelector = new Rect(x, y, tileSelectorWidth, h).Contains(mp);

			// EXPAND: when mouse enters
			if (isMouseOverTileSelector && !wasOver)
			{
				targetWidth = fullWidth;
				animationStartTime = Time.time;
				mouseExitTime = 0f; // cancel any collapse timer
			}

			// COLLAPSE: start timer only once when mouse leaves
			if (!isMouseOverTileSelector && wasOver && mouseExitTime == 0f)
			{
				mouseExitTime = Time.time;
			}

			// After 1 second away → collapse
			if (!isMouseOverTileSelector && mouseExitTime > 0f && Time.time - mouseExitTime >= autoHideDelay)
			{
				targetWidth = collapsedWidth;
				animationStartTime = Time.time;
				mouseExitTime = 0f;
			}

			// Smooth animation
			var t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
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

		// Add these fields to your PlaceholderEditorUI class
		private float cachedContentHeight = -1f;
		private int cachedCount = -1;

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

			// Cache count and height
			int count = ResourceManager.Definitions.Count;
			if (cachedCount != count)
			{
				cachedCount = count;
				cachedContentHeight = count * 40f;
			}

			scrollPosition = GUI.BeginScrollView(view, scrollPosition,
				new Rect(0, 0, tileSelectorWidth - 40, cachedContentHeight));

			// Virtual scrolling: only draw visible items
			int itemHeight = 40;
			int firstVisible = Mathf.FloorToInt(scrollPosition.y / itemHeight);
			int lastVisible = firstVisible + Mathf.CeilToInt(view.height / itemHeight) + 2;
			firstVisible = Mathf.Max(0, firstVisible);
			lastVisible = Mathf.Min(lastVisible, count);

			var definitions = ResourceManager.Definitions;

			for (int i = firstVisible; i < lastVisible; i++)
			{
				// This works with IList<T> — use ElementAt (slightly slower but safe)
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
			const float margin = 10f;
			const float collapsedW = 120f;
			const float fullW = 340f;

			float x = Screen.width - waypointPanelWidth - margin;
			float y = panelYoffset + spacing;
			float h = Screen.height - y - margin;

			Rect panelRect = new Rect(x, y, waypointPanelWidth, h);

			// === Hover/expand logic (unchanged) ===
			Vector2 mp = Input.mousePosition;
			mp.y = Screen.height - mp.y;

			bool wasOver = isMouseOverWaypointPanel;
			isMouseOverWaypointPanel = panelRect.Contains(mp);

			if (isMouseOverWaypointPanel && !wasOver)
			{
				waypointTargetWidth = fullW;
				waypointAnimationStartTime = Time.time;
			}
			else if (!isMouseOverWaypointPanel && wasOver)
			{
				waypointMouseExitTime = Time.time;
			}

			if (!isMouseOverWaypointPanel && Time.time - waypointMouseExitTime > waypointAutoHideDelay)
				waypointTargetWidth = collapsedW;

			if (waypointPanelWidth != waypointTargetWidth)
			{
				float t = (Time.time - waypointAnimationStartTime) / waypointAnimationDuration;
				waypointPanelWidth = Mathf.Lerp(waypointPanelWidth, waypointTargetWidth, t);
				if (t >= 1f) waypointPanelWidth = waypointTargetWidth;
			}

			// === Draw panel ===
			GUI.backgroundColor = new Color(0.15f, 0.3f, 0.42f, 0.92f);
			GUI.Box(panelRect, "");
			GUI.backgroundColor = Color.white;

			GUI.Label(new Rect(x + 10, y + 5, waypointPanelWidth - 20, 25), "Waypoints");

			if (waypoints == null || waypoints.Length == 0)
			{
				GUI.Label(new Rect(x + 10, y + 40, waypointPanelWidth - 20, 50),
					"No waypoints\nClick map to add first one");
				return;
			}

			// === Scrollable list ===
			Rect scrollRect = new Rect(x + 10, y + 35, waypointPanelWidth - 20, h - 180);
			waypointScrollPosition = GUI.BeginScrollView(scrollRect, waypointScrollPosition,
				new Rect(0, 0, waypointPanelWidth - 40, waypoints.Length * 46f));

			for (int i = 0; i < waypoints.Length; i++)
			{
				var wp = waypoints[i];
				string name = string.IsNullOrEmpty(wp.name) ? "<unnamed>" : wp.name;
				string cam = wp.IsCamera() ? " [Cam]" : "";
				string status = wp.tile < 0 ? " [UNPLACED — click map]" : $" [{wp.tile}]";
				string label = $"{i:00}: {name}{status}{cam}";

				Rect r = new Rect(0, i * 46f, waypointPanelWidth - 40, 42);

				// Color logic: unplaced = orange, selected = blue, both = orange-blue mix
				if (wp.tile < 0)
					GUI.color = new Color(1f, 0.6f, 0.2f);      // Orange for unplaced
				else if (i == selectedIndex)
					GUI.color = new Color(0.3f, 0.8f, 1f);      // Blue for selected
				else
					GUI.color = Color.white;

				if (GUI.Button(r, label))
					OnWaypointSelected?.Invoke(i);

				GUI.color = Color.white;
			}
			GUI.EndScrollView();

			// === Control Panel ===
			float controlY = y + h - 140;

			// Selected info
			if (selectedIndex >= 0 && selectedIndex < waypoints.Length)
			{
				var selWp = waypoints[selectedIndex];
				GUI.Label(new Rect(x + 10, controlY, waypointPanelWidth - 20, 20),
					$"Selected: {selectedIndex} — {selWp.name ?? "<unnamed>"}");

				if (selWp.tile < 0)
				{
					GUI.color = new Color(1f, 0.7f, 0.3f);
					GUI.Label(new Rect(x + 10, controlY + 22, waypointPanelWidth - 20, 40),
						"This waypoint is unplaced.\nClick on the map to place it.");
					GUI.color = Color.white;
					controlY += 20;
				}
			}
			else
			{
				GUI.Label(new Rect(x + 10, controlY, waypointPanelWidth - 20, 20), "No waypoint selected");
			}

			controlY += 40;

			// Move buttons
			GUI.enabled = selectedIndex > 0;
			if (GUI.Button(new Rect(x + 10, controlY, 100, 30), "Move Up"))
				OnWaypointMoveUp?.Invoke(selectedIndex);

			GUI.enabled = selectedIndex >= 0 && selectedIndex < waypoints.Length - 1;
			if (GUI.Button(new Rect(x + 120, controlY, 100, 30), "Move Down"))
				OnWaypointMoveDown?.Invoke(selectedIndex);

			GUI.enabled = true;
			controlY += 38;

			// Delete
			GUI.color = new Color(1f, 0.4f, 0.4f);
			if (GUI.Button(new Rect(x + 10, controlY, waypointPanelWidth - 20, 32), "Delete Selected"))
				OnWaypointDelete?.Invoke(selectedIndex);
			GUI.color = Color.white;

			controlY += 40;

			// Add New
			if (GUI.Button(new Rect(x + 10, controlY, waypointPanelWidth - 20, 32), "Add New Waypoint"))
				OnWaypointAddRequested?.Invoke();
		}
	}
}