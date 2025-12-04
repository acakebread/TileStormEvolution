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

		public event Action<string> OnModeChanged;
		public event Action<bool> OnGridLinesToggled;
		public event Action OnSaveDatabaseRequested;
		public event Action OnReloadDatabaseRequested;
		public event Action OnExportMapRequested;
		public event Action OnImportMapRequested;

		private EditorController editorController;

		private void Start()
		{
			editorController = GetComponent<EditorController>();

			if (TryGetComponent<PlaceholderUI>(out var placeholderUI))
				panelYoffset = placeholderUI.GetPanelBottomY();
			else
				panelYoffset = 10f;
		}

		public bool IsGuiControlActive()
			=> GUIUtility.hotControl != 0 || (EventSystem.current && EventSystem.current.IsPointerOverGameObject());

		public bool IsMouseOverGui()
		{
			Vector2 mousePos = Input.mousePosition;
			mousePos.y = Screen.height - mousePos.y;

			// Left column buttons
			float leftY = panelYoffset + spacing;
			if (new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(mousePos))
				return true;

			// Right side panel (only when not in Drag mode)
			if (editorController.CurrentMode != EditorController.EditorMode.Drag)
			{
				float detectW = 340f;
				var rect = new Rect(Screen.width - detectW - 10f, 20f, detectW, Screen.height - 40f);
				return rect.Contains(mousePos);
			}

			return false;
		}

		public void DrawMainUI(string mode, bool gridVisible)
		{
			float y = panelYoffset + spacing;

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
	}
}