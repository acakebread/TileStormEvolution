using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace ClassicTilestorm
{
	public class PlaceholderEditorUI : MonoBehaviour
	{
		private MapManager mapManager;
		private float panelBottomY = 10f;

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
		public event Action OnResizeMapTestRequested;
		public event Action OnCropMapTestRequested;
		public event Action<Definition> OnTileSelected;

		public void Initialize(MapManager map, float bottomY)
		{
			mapManager = map;
			panelBottomY = bottomY;
		}

		public bool IsGuiControlActive()
			=> GUIUtility.hotControl != 0 || isMouseOverTileSelector || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

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

			float leftY = panelBottomY + spacing;
			if (new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(mousePos))
				return true;

			if (mapManager?.CurrentMap != null)
			{
				float tx = Screen.width - tileSelectorWidth - margin;
				float ty = panelBottomY + spacing;
				float th = Screen.height - ty - margin;
				if (new Rect(tx, ty, tileSelectorWidth, th).Contains(mousePos))
					return true;
			}

			return false;
		}

		public void UpdatePaintMode()
		{
			Vector2 mp = Input.mousePosition;
			mp.y = Screen.height - mp.y;

			bool wasOver = isMouseOverTileSelector;

			float x = Screen.width - tileSelectorWidth - margin;
			float y = panelBottomY + spacing;
			float h = Screen.height - y - margin;

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
			float t = Mathf.Clamp01((Time.time - animationStartTime) / animationDuration);
			tileSelectorWidth = Mathf.Lerp(tileSelectorWidth, targetWidth, t);
		}

		private void ColoredButton(Rect r, string text, Color col, Action onClick)
		{
			GUI.backgroundColor = col;
			if (GUI.Button(r, text)) onClick?.Invoke();
			GUI.backgroundColor = Color.white;
		}

		public void DrawMainUI(string mode, bool gridVisible)
		{
			if (mapManager == null) return;

			float y = panelBottomY + spacing;

			ColoredButton(new Rect(margin, y + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight),
				gridVisible ? "Hide Grid" : "Show Grid",
				new Color(0.5f, 0.5f, 0.5f),
				() => OnGridLinesToggled?.Invoke(!gridVisible));

			GUI.contentColor = mode == "Drag" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Drag"))
				OnModeChanged?.Invoke("Drag");

			GUI.contentColor = mode == "Paint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Paint"))
				OnModeChanged?.Invoke("Paint");
			GUI.contentColor = Color.white;

			ColoredButton(new Rect(margin, y + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Resize Test", new Color(0.2f, 0.6f, 1f), OnResizeMapTestRequested);
			ColoredButton(new Rect(margin, y + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Crop Test", new Color(0.2f, 0.6f, 1f), OnCropMapTestRequested);
			ColoredButton(new Rect(margin, y + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f), OnImportMapRequested);
			ColoredButton(new Rect(margin, y + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f), OnExportMapRequested);
			ColoredButton(new Rect(margin, y + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Reload Database", new Color(0.2f, 0.6f, 1f), OnReloadDatabaseRequested);
			ColoredButton(new Rect(margin, y + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f), OnSaveDatabaseRequested);
		}

		public void DrawPaintUI(string selectedId)
		{
			if (mapManager == null) return;

			float tx = Screen.width - tileSelectorWidth - margin;
			float ty = panelBottomY + spacing;
			float th = Screen.height - ty - margin;
			Rect selectorRect = new Rect(tx, ty, tileSelectorWidth, th);

			GUI.backgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.75f);
			GUI.Box(selectorRect, "Tile Selector");
			GUI.backgroundColor = Color.white;

			Rect view = new Rect(tx + 10, ty + 30, tileSelectorWidth - 20, th - 40);
			scrollPosition = GUI.BeginScrollView(view, scrollPosition,
				new Rect(0, 0, tileSelectorWidth - 40, ResourceManager.Definitions.Count * 40));

			for (int i = 0; i < ResourceManager.Definitions.Count; i++)
			{
				var def = ResourceManager.Definitions[i];
				string label = $"{def.id} ({def.texture})";
				Rect btn = new Rect(0, i * 40, tileSelectorWidth - 40, 35);

				if (def.id == selectedId) GUI.color = Color.green;
				if (GUI.Button(btn, label)) OnTileSelected?.Invoke(def);
				GUI.color = Color.white;
			}
			GUI.EndScrollView();
		}
	}
}