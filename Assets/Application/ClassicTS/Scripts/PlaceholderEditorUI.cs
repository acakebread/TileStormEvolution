using UnityEngine;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class PlaceholderEditorUI : MonoBehaviour
	{
		private MapManager mapManager;

		private readonly float margin = 10f;
		private readonly float spacing = 10f;
		private readonly float buttonWidth = 135f;
		private readonly float buttonHeight = 30f;
		private float tileSelectorWidth = 120f;
		private readonly float fullWidth = 300f;
		private readonly float collapsedWidth = 120f;
		private float mouseExitTime = 0f;
		private bool isMouseOverTileSelector;
		private readonly float autoHideDelay = 1f;
		private float targetWidth;
		private float animationStartTime;
		private readonly float animationDuration = 0.3f;
		private Vector2 scrollPosition = Vector2.zero;

		private bool gridLinesEnabled = true;
		private string selectedDefinitionId = "tile_empty";

		public EditorController.EditorMode currentMode = EditorController.EditorMode.Drag;

		// Events — unchanged
		public event System.Action<EditorController.EditorMode> OnModeChanged;
		public event System.Action<bool> OnGridLinesToggled;
		public event System.Action OnSaveDatabaseRequested;
		public event System.Action OnReloadDatabaseRequested;
		public event System.Action OnExportMapRequested;
		public event System.Action OnImportMapRequested;
		public event System.Action OnResizeMapTestRequested;
		public event System.Action OnCropMapTestRequested;
		public event System.Action<Definition> OnTileSelected;

		public void Initialize(MapManager map) => mapManager = map;

		public bool IsGuiControlActive() => GUIUtility.hotControl != 0 || isMouseOverTileSelector || EventSystem.current.IsPointerOverGameObject();

		public bool IsMouseInsideWindow()
		{
			Vector2 p = Input.mousePosition;
			return p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height;
		}

		public bool IsMouseOverGui()
		{
			if (isMouseOverTileSelector) return true;

			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			float panelBottomY = GetPanelBottomY();
			float leftPanelX = margin;
			float leftPanelY = panelBottomY + spacing;
			float leftPanelWidth = buttonWidth + 20f;
			float leftPanelHeight = buttonHeight * 9 + spacing * 9;

			if (new Rect(leftPanelX, leftPanelY, leftPanelWidth, leftPanelHeight).Contains(mousePos))
				return true;

			if (currentMode == EditorController.EditorMode.Paint)
			{
				float tileSelectorX = Screen.width - tileSelectorWidth - margin;
				float tileSelectorY = panelBottomY + spacing;
				float tileSelectorHeight = Screen.height - tileSelectorY - margin;
				if (new Rect(tileSelectorX, tileSelectorY, tileSelectorWidth, tileSelectorHeight).Contains(mousePos))
					return true;
			}

			return false;
		}

		private void Update()
		{
			if (currentMode != EditorController.EditorMode.Paint) return;

			Vector2 mp = Input.mousePosition;
			mp.y = Screen.height - mp.y;

			bool wasOver = isMouseOverTileSelector;
			float x = Screen.width - tileSelectorWidth - margin;
			float y = GetPanelBottomY() + spacing;
			float h = Screen.height - y - margin;
			isMouseOverTileSelector = new Rect(x, y, tileSelectorWidth, h).Contains(mp);

			if (isMouseOverTileSelector)
			{
				if (targetWidth != fullWidth) { targetWidth = fullWidth; animationStartTime = Time.time; }
				mouseExitTime = 0f;
			}
			else if (wasOver)
			{
				mouseExitTime = Time.time;
				if (Time.time - mouseExitTime >= autoHideDelay && targetWidth != collapsedWidth)
				{
					targetWidth = collapsedWidth;
					animationStartTime = Time.time;
				}
			}

			float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
			tileSelectorWidth = Mathf.Lerp(tileSelectorWidth, targetWidth, t);
		}

		private void OnGUI()
		{
			if (!mapManager) return;

			float y = GetPanelBottomY() + spacing;

			// 0: Grid Toggle
			ColoredButton(new Rect(margin, y + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight),
				gridLinesEnabled ? "Hide Grid" : "Show Grid",
				new Color(0.5f, 0.5f, 0.5f),
				() => { gridLinesEnabled = !gridLinesEnabled; OnGridLinesToggled?.Invoke(gridLinesEnabled); });

			// 1 & 2: Mode buttons (highlighted with contentColor)
			GUI.contentColor = currentMode == EditorController.EditorMode.Drag ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Drag"))
			{
				currentMode = EditorController.EditorMode.Drag;
				OnModeChanged?.Invoke(EditorController.EditorMode.Drag);
				GeometryUtil.HideGhostTile();
			}

			GUI.contentColor = currentMode == EditorController.EditorMode.Paint ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Paint"))
			{
				currentMode = EditorController.EditorMode.Paint;
				OnModeChanged?.Invoke(EditorController.EditorMode.Paint);
				targetWidth = collapsedWidth;
				tileSelectorWidth = collapsedWidth;
				animationStartTime = Time.time;
			}
			GUI.contentColor = Color.white;

			// 3–8: Action buttons — exactly your original order
			ColoredButton(new Rect(margin, y + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Resize Test", new Color(0.2f, 0.6f, 1f), OnResizeMapTestRequested);
			ColoredButton(new Rect(margin, y + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Crop Test", new Color(0.2f, 0.6f, 1f), OnCropMapTestRequested);
			ColoredButton(new Rect(margin, y + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f), OnImportMapRequested);
			ColoredButton(new Rect(margin, y + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f), OnExportMapRequested);
			ColoredButton(new Rect(margin, y + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Reload Database", new Color(0.2f, 0.6f, 1f), OnReloadDatabaseRequested);
			ColoredButton(new Rect(margin, y + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f), OnSaveDatabaseRequested);

			// Tile selector — unchanged behaviour
			if (currentMode == EditorController.EditorMode.Paint)
			{
				float tx = Screen.width - tileSelectorWidth - margin;
				float ty = GetPanelBottomY() + spacing;
				float th = Screen.height - ty - margin;
				Rect selectorRect = new Rect(tx, ty, tileSelectorWidth, th);

				GUI.backgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
				GUI.Box(selectorRect, "Tile Selector");
				GUI.backgroundColor = Color.white;

				Rect viewRect = new Rect(tx + 10, ty + 30, tileSelectorWidth - 20, th - 40);
				scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, new Rect(0, 0, tileSelectorWidth - 40, ResourceManager.Definitions.Count * 40));

				for (int i = 0; i < ResourceManager.Definitions.Count; i++)
				{
					var def = ResourceManager.Definitions[i];
					string label = $"{def.id} ({def.texture})";
					Rect btn = new Rect(0, i * 40, tileSelectorWidth - 40, 35);

					if (def.id == selectedDefinitionId) GUI.color = Color.green;
					if (GUI.Button(btn, label))
					{
						selectedDefinitionId = def.id;
						OnTileSelected?.Invoke(def);
					}
					GUI.color = Color.white;
				}

				GUI.EndScrollView();
			}

			static void ColoredButton(Rect rect, string text, Color color, System.Action onClick)
			{
				GUI.backgroundColor = color;
				if (GUI.Button(rect, text)) onClick?.Invoke();
				GUI.backgroundColor = Color.white;
			}
		}

		private float GetPanelBottomY()
		{
			var ui = FindAnyObjectByType<PlaceholderUI>();
			return ui != null ? ui.GetPanelBottomY() : margin;
		}

		// Public helpers
		public void SetGridLinesEnabled(bool enabled) => gridLinesEnabled = enabled;
		public bool GetGridLinesEnabled() => gridLinesEnabled;
		public void SetSelectedDefinitionId(string id)
		{
			if (!string.IsNullOrEmpty(id)) selectedDefinitionId = id;
		}
	}
}