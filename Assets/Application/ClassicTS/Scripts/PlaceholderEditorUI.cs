using UnityEngine;
using UnityEngine.EventSystems;
using System;
using MassiveHadronLtd;

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
			GUI.contentColor = Color.white;

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
			scrollPosition = GUI.BeginScrollView(view, scrollPosition,
				new Rect(0, 0, tileSelectorWidth - 40, ResourceManager.Definitions.Count * 40));

			for (int i = 0; i < ResourceManager.Definitions.Count; i++)
			{
				var def = ResourceManager.Definitions[i];
				var label = $"{def.id} ({def.texture})";
				Rect btn = new(0, i * 40, tileSelectorWidth - 40, 35);

				if (def.id == selectedId) GUI.color = Color.green;
				if (GUI.Button(btn, label)) OnTileSelected?.Invoke(def.id);
				GUI.color = Color.white;
			}
			GUI.EndScrollView();
		}
	}
}