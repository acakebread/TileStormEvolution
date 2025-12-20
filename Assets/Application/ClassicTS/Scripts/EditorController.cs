using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		public IMapManager iMapManager => mapManager;
		private MapManager mapManager;

		private EditorControllerMovement activeMode;
		private EditorControllerDrag dragMode;
		private EditorControllerPaint paintMode;
		private EditorControllerWaypoint waypointMode;
		private EditorControllerAttachment attachmentMode;

		private enum EditorMode { Drag, Paint, Waypoint, Attachment }
		private EditorMode? currentMode = null;

		private bool gridEnabled = true;

		// UI state
		private float panelYoffset = 10f;
		private const float margin = 10f;
		private const float spacing = 10f;
		private const float buttonWidth = 135f;
		private const float buttonHeight = 30f;

		private void Awake()
		{
			panelYoffset = PlaceholderUI.PanelBottomY;

			// Modes
			dragMode = new EditorControllerDrag(this);
			paintMode = new EditorControllerPaint(this);
			waypointMode = new EditorControllerWaypoint(this);
			attachmentMode = new EditorControllerAttachment(this); 
			SetEditorMode(EditorMode.Drag);//default
		}

		private void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
			{
				controller.SetCameraSystem(CameraModeRegistry.Editor, false);
				controller.UpdateGestureControllerState();
			}
			UpdateGridLines(gridEnabled);
			activeMode?.OnEnable();
			EnableEggbot(false);
		}

		private void OnDisable()
		{
			activeMode?.OnDisable();
			GridLinesUtil.Hide();
			EnableEggbot(true);
		}

		private void Update()
		{
			if (Input.GetMouseButtonUp(0) && GUIUtility.hotControl != 0) GUIUtility.hotControl = 0;
			activeMode?.Update();
		}

		private void OnGUI()
		{
			DrawMainUI((currentMode ?? EditorMode.Drag).ToString(), gridEnabled);
			activeMode?.OnGUI();
		}

		public void OnApplicationFocus(bool hasFocus) => activeMode?.OnApplicationFocus(hasFocus);

		private void OnDestroy()
		{
			GridLinesUtil.Hide();
			if (null != mapManager) mapManager.OnMapEdited -= HandleMapEdited;
			dragMode?.OnDestroy();
			paintMode?.OnDestroy();
			waypointMode?.OnDestroy();
			attachmentMode?.OnDestroy();
		}

		private EggbotController Eggbot() => GetComponentInChildren<EggbotController>(true);

		private void EnableEggbot(bool value)
		{
			var eggbotController = Eggbot();
			if (null != eggbotController) eggbotController.gameObject.SetActive(value);
		}

		public void Initialise(MapManager map)
		{
			mapManager = map;
			// Subscribe to map changes
			mapManager.OnMapEdited += HandleMapEdited;
			if (!isActiveAndEnabled) return;
			UpdateGridLines(gridEnabled);
			activeMode?.OnMapLoaded();
			EnableEggbot(false);
		}

		private void UpdateGridLines(bool enabled = true) => GridLinesUtil.Show(transform, mapManager ? mapManager.Width : 32, mapManager ? mapManager.Height : 32, gridEnabled = enabled);

		private void OnGridLinesToggled(bool value) => UpdateGridLines(gridEnabled = value);

		private void SetEditorMode(EditorMode newMode)
		{
			if (currentMode == newMode) return;
			activeMode?.OnDisable();
			currentMode = newMode;
			activeMode = newMode switch
			{
				EditorMode.Drag => dragMode,
				EditorMode.Paint => paintMode,
				EditorMode.Waypoint => waypointMode,
				EditorMode.Attachment => attachmentMode,
				_ => dragMode
			};
			activeMode?.OnEnable();
		}

		// ===================================================================
		// Map actions
		// ===================================================================

		private void HandleMapEdited(bool resized, Vector3 originDelta)
		{
			if (mapManager == null) return;
			ResourceManager.ApplyMapChanges(mapManager.CurrentMap);
			if (resized) OnMapResized(originDelta);
		}

		private void OnMapResized(Vector3 originDelta = default)
		{
			if (mapManager == null) return;

			if (gridEnabled && isActiveAndEnabled)
			{
				int width = mapManager.Width;
				int height = mapManager.Height;
				GridLinesUtil.UpdateSize(width, height);
			}
			if (Vector3.zero != originDelta)
			{
				if (TryGetComponent<MainCameraController>(out var controller))
				{
					if (controller.activeSystem is GameCameraEditor editorCam)
						editorCam.camera.transform.position += originDelta;
				}

				var eggbotController = Eggbot();
				if (null != eggbotController) eggbotController.OnMapOriginShift(mapManager, originDelta);
			}
		}

		// ===================================================================
		// UI & Input Detection
		// ===================================================================

		public bool IsMouseOverGui()
		{
			var leftY = panelYoffset + spacing;// Left column buttons
			return new Rect(margin, leftY, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, Input.mousePosition.z));
		}

		private void DrawMainUI(string mode, bool gridVisible)
		{
			Color prevContentColor = GUI.contentColor;

			var y = panelYoffset + spacing;
			if (GuiUtils.ColoredButton(new Rect(margin, y + 0 * (buttonHeight + spacing), buttonWidth, buttonHeight), gridVisible ? "Hide Grid" : "Show Grid", new Color(0.25f, 0.75f, 0.25f))) OnGridLinesToggled(!gridVisible);

			GUI.contentColor = mode == "Drag" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 1 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Drag")) SetEditorMode(EditorMode.Drag);

			GUI.contentColor = mode == "Paint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 2 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Paint")) SetEditorMode(EditorMode.Paint);

			GUI.contentColor = mode == "Waypoint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 3 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Waypoint")) SetEditorMode(EditorMode.Waypoint);

			GUI.contentColor = (currentMode ?? EditorMode.Drag) == EditorMode.Attachment ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + 4 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Attachments")) SetEditorMode(EditorMode.Attachment);

			GUI.contentColor = prevContentColor;

			var mainController = GetComponent<MainController>();
			if (GuiUtils.ColoredButton(new Rect(margin, y + 5 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f)))
				mainController.ImportMapAsAtomic();

			if (GuiUtils.ColoredButton(new Rect(margin, y + 6 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f)))
				mainController.ExportMapAsAtomic();

			if (GuiUtils.ColoredButton(new Rect(margin, y + 7 * (buttonHeight + spacing), buttonWidth, buttonHeight), "(Re)Load Database", new Color(0.2f, 0.6f, 1f)))
				mainController.LoadDatabase();

			if (GuiUtils.ColoredButton(new Rect(margin, y + 8 * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f)))
				mainController.SaveDatabase();
		}
	}
}
