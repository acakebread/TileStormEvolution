using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.Rendering;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		public IMapManager iMapManager => mapManager;
		private MapManager mapManager;

		private EditorControllerMovement activeMode;
		private EditorControllerDrag dragMode;
		private EditorControllerPaint paintMode;
		private EditorControllerAttachment attachmentMode;

		private enum EditorMode { Drag, Paint, Attachment }
		private EditorMode? currentMode = null;

		private bool gridEnabled = true;
		private bool dofEnabled = false;
		private Volume getVolume(GameObject root) => root.GetComponentInChildren<Volume>(true);

		// UI state
		private float panelYoffset = 10f;
		private const float margin = 10f;
		private const float spacing = 10f;
		private const float buttonWidth = 135f;
		private const float buttonHeight = 30f;

		private MainCameraController mainCameraController { get { TryGetComponent<MainCameraController>(out var controller); return controller; } }
		private GameCameraEditor gameCameraEditor { get { if (null != mainCameraController && mainCameraController.activeSystem is GameCameraEditor editorCam) return editorCam; return null; } }

		//private DefinitionEditor definitionEditor => FindAnyObjectByType<DefinitionEditor>();
		private DefinitionEditor _definitionEditor = null;
		private DefinitionEditor definitionEditor { get { if (null == _definitionEditor) _definitionEditor = gameObject.AddComponent<DefinitionEditor>(); return _definitionEditor; } }

		private void Awake()
		{
			panelYoffset = PlaceholderUI.PanelBottomY;

			// Modes
			dragMode = new EditorControllerDrag(this);
			paintMode = new EditorControllerPaint(this);
			attachmentMode = new EditorControllerAttachment(this); 
			SetEditorMode(EditorMode.Drag);//default
		}

		public void Initialise(MapManager map)
		{
			mapManager = map;
			mapManager.OnMapEdited += HandleMapEdited;// Subscribe to map changes
			if (!isActiveAndEnabled) return;
			UpdateGridLines(gridEnabled);
			activeMode?.OnMapLoaded();
			EnableEggbot(false);
		}

		private void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
			{
				controller.SetCameraSystem(CameraModeRegistry.Editor, false);
				controller.UpdateGestureControllerState();
			}
			UpdateGridLines(gridEnabled);
			UpdateDOF(dofEnabled);
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
			activeMode?.Update();

			var cameraEditor = gameCameraEditor;
			if (null != cameraEditor)
			{
				var volume = getVolume(cameraEditor.controller.gameObject);
				var distance  = (cameraEditor.controller.transform.position - MapManager.CameraToWorld(cameraEditor.camera)).magnitude;
				VolumeUtils.SetDepthOfFieldDistance(volume, Mathf.Max(Mathf.Min(distance, cameraEditor.controller.transform.position.y * 3f), 1f));
			}
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
			attachmentMode?.OnDestroy();
		}

		private void EnableEggbot(bool value)
		{
			var eggbotController = GetComponentInChildren<EggbotController>(true);
			if (null != eggbotController) eggbotController.gameObject.SetActive(value);
		}

		private void UpdateGridLines(bool enabled = true) => GridLinesUtil.Show(transform, mapManager ? mapManager.Width : 32, mapManager ? mapManager.Height : 32, gridEnabled = enabled);
		private void UpdateDOF(bool enabled = true)
		{
			if (null != gameCameraEditor)
			{
				var volume = getVolume(gameCameraEditor.controller.gameObject);
				volume.enabled = enabled;
				VolumeUtils.EnableDepthOfField(volume, enabled);
				VolumeUtils.SetDepthOfFieldDistance(volume, 8f);
			}
		}

		private void OnGridLinesToggled(bool value) => UpdateGridLines(gridEnabled = value);
		private void OnDofToggled(bool value) => UpdateDOF(dofEnabled = value);

		private void SetEditorMode(EditorMode newMode)
		{
			if (currentMode == newMode) return;
			activeMode?.OnDisable();
			currentMode = newMode;
			activeMode = newMode switch
			{
				EditorMode.Drag => dragMode,
				EditorMode.Paint => paintMode,
				EditorMode.Attachment => attachmentMode,
				_ => dragMode
			};
			activeMode?.OnEnable();
		}

		// ===================================================================
		// Map actions
		// ===================================================================

		private void HandleMapEdited(IMapManager mapManager,bool resized, Vector3 originDelta)
		{
			if (mapManager == null) return;
			ResourceManager.ApplyMapChanges(mapManager.CurrentMap);
			if (!resized) return;
			if (gridEnabled) GridLinesUtil.UpdateSize(mapManager.Width, mapManager.Height);
			if (Vector3.zero == originDelta) return;
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			if (controller.activeSystem is GameCameraEditor editorCam)
				editorCam.camera.transform.position += originDelta;
		}

		// ===================================================================
		// UI & Input Detection
		// ===================================================================

		public bool IsMouseOverGui() => new Rect(margin, panelYoffset + spacing, buttonWidth + 20f, buttonHeight * 9 + spacing * 9).Contains(new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, Input.mousePosition.z));

		private void DrawMainUI(string mode, bool gridVisible)
		{
			Color prevContentColor = GUI.contentColor;

			var ct = 0;
			var y = panelYoffset + spacing;
			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), PreviewSettings.RemapGeometry ? "Remap" : "Classic", new Color(0.45f, 0.25f, 0.25f))) PreviewSettings.RemapGeometry = !PreviewSettings.RemapGeometry;

			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), gridVisible ? "Hide Grid" : "Show Grid", new Color(0.25f, 0.75f, 0.25f))) OnGridLinesToggled(!gridVisible);

			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), dofEnabled ? "Disable DOF" : "Enable DOF", new Color(0.25f, 0.75f, 0.25f))) OnDofToggled(!dofEnabled);

			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Definition Editor", new Color(0.6f, 0.3f, 0.8f))) UIController.OpenPanel<DefinitionEditorPanel>();

			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Effect Editor", new Color(0.6f, 0.3f, 0.8f))) UIController.OpenPanel<EffectEditorPanel>();

			GUI.contentColor = mode == "Drag" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Drag")) SetEditorMode(EditorMode.Drag);

			GUI.contentColor = mode == "Paint" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Paint")) SetEditorMode(EditorMode.Paint);

			GUI.contentColor = (currentMode ?? EditorMode.Drag) == EditorMode.Attachment ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Attachments")) SetEditorMode(EditorMode.Attachment);

			GUI.contentColor = prevContentColor;

			var mainController = GetComponent<MainController>();
			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f)))
				mainController.ImportMapAsAtomic();

			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f)))
				mainController.ExportMapAsAtomic();

			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "(Re)Load Database", new Color(0.2f, 0.6f, 1f)))
				mainController.LoadDatabase();

			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f)))
				mainController.SaveDatabase();
		}
	}
}
