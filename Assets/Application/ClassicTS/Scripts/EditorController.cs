using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.Rendering;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		public IMapEdit iMap;

		private EditorControllerMovement activeMode;
		//private EditorControllerView viewMode;
		private EditorControllerPlacement placementMode;
		private EditorControllerAttachment attachmentMode;

		private enum EditorMode { Placement, Attachment }//private enum EditorMode { View, Placement, Attachment }
		private EditorMode? currentMode = null;

		private bool gridEnabled = true;
		private bool postProcessingEnabled = false;

		public bool GridEnabled { get => gridEnabled; set => OnGridLinesToggled(value); }
		public bool PostProcessingEnabled { get => postProcessingEnabled; set => OnPostProcessingToggled(value); }

		private Volume getVolume(GameObject root) => root.GetComponentInChildren<Volume>(true);

		// UI state
		private float panelYoffset = 10f;
		private const float margin = 10f;
		private const float spacing = 6f;
		private const float buttonWidth = 130f;
		private const float buttonHeight = 28f;

		private MainCameraController mainCameraController { get { TryGetComponent<MainCameraController>(out var controller); return controller; } }
		private GameCameraEditor gameCameraEditor { get { if (null != mainCameraController && mainCameraController.activeSystem is GameCameraEditor editorCam) return editorCam; return null; } }

		private void Awake()
		{
			panelYoffset = PlaceholderUI.PanelBottomY;

			// Modes
			//viewMode = new EditorControllerView(this);
			placementMode = new EditorControllerPlacement(this);
			attachmentMode = new EditorControllerAttachment(this); 
			SetEditorMode(EditorMode.Placement);//default
		}

		private System.Action _unsubscribeMapAction;

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;
			iMap.OnMapEdited += OnMapEdited;// Subscribe to map changes
			_unsubscribeMapAction = () => iMap.OnMapEdited -= OnMapEdited;
			if (!isActiveAndEnabled) return;
			UpdateGridLines(gridEnabled);
			activeMode?.OnMapLoaded();
			EnableEggbot(false);
		}

		public void Reset()
		{
			_unsubscribeMapAction?.Invoke();
			_unsubscribeMapAction = null;
		}

		private void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
			{
				controller.SetCameraSystem(CameraModeRegistry.Editor, false);
				controller.UpdateGestureControllerState();
			}
			UpdateGridLines(gridEnabled);
			UpdatePostProcessing(postProcessingEnabled);
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
				var distance  = (cameraEditor.controller.transform.position - Map.CameraToWorld(cameraEditor.camera)).magnitude;
				VolumeUtils.SetDepthOfFieldDistance(volume, Mathf.Max(Mathf.Min(distance, cameraEditor.controller.transform.position.y * 3f), 1f));
			}
		}

		private void OnGUI()
		{
			DrawMainUI((currentMode ?? EditorMode.Placement).ToString(), gridEnabled);
			activeMode?.OnGUI();
			//MultiTouchEmulator.OnGUI();//for debugging touch emulator
		}

		public void OnApplicationFocus(bool hasFocus) => activeMode?.OnApplicationFocus(hasFocus);

		private void OnDestroy()
		{
			GridLinesUtil.Hide();
			if (null != iMap) iMap.OnMapEdited -= OnMapEdited;
			//viewMode?.OnDestroy();
			placementMode?.OnDestroy();
			attachmentMode?.OnDestroy();
		}

		private void EnableEggbot(bool value)
		{
			var eggbotController = GetComponentInChildren<EggbotController>(true);
			if (null != eggbotController) eggbotController.gameObject.SetActive(value);
		}

		private void UpdateGridLines(bool enabled = true) => GridLinesUtil.Show(transform, null != iMap ? iMap.Width : 32, null != iMap ? iMap.Height : 32, gridEnabled = enabled, offset: null!=iMap ? iMap.TileRenderPosition(0) + new Vector3(-0.5f, 0f, -0.5f) : Vector3.zero);
		private void UpdatePostProcessing(bool enabled = true)
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
		private void OnPostProcessingToggled(bool value) => UpdatePostProcessing(postProcessingEnabled = value);

		private void SetEditorMode(EditorMode newMode)
		{
			if (currentMode == newMode) return;
			activeMode?.OnDisable();
			currentMode = newMode;
			activeMode = newMode switch
			{
				//EditorMode.View => viewMode,
				EditorMode.Placement => placementMode,
				EditorMode.Attachment => attachmentMode,
				_ => placementMode//viewMode
			};
			activeMode?.OnEnable();
		}

		// ===================================================================
		// Map actions
		// ===================================================================

		private void OnMapEdited(Map map, bool resized, Vector3 originDelta)
		{
			if (map == null) return;
			ResourceManager.ApplyMapChanges(map);
			if (!resized) return;
			if (gridEnabled) GridLinesUtil.UpdateSize(map.width, map.height);
			if (Vector3.zero == originDelta) return;
		}

		// ===================================================================
		// UI & Input Detection
		// ===================================================================

		private int numItems = 0;
		public bool IsMouseOverGui() => new Rect(margin, panelYoffset + spacing, buttonWidth + 20f, (buttonHeight + spacing) * numItems).Contains(new Vector3(InputX.mousePosition.x, Screen.height - InputX.mousePosition.y, InputX.mousePosition.z));

		private void DrawMainUI(string mode, bool gridVisible)
		{
			Color prevContentColor = GUI.contentColor;

			var ct = 0;
			var y = panelYoffset + spacing;
			//if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), ApplicationSettings.RemapGeometry ? "Remap" : "Classic", new Color(0.45f, 0.25f, 0.25f))) ApplicationSettings.RemapGeometry = !ApplicationSettings.RemapGeometry;

			//if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), gridVisible ? "Hide Grid" : "Show Grid", new Color(0.25f, 0.75f, 0.25f))) OnGridLinesToggled(!gridVisible);

			//if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), dofEnabled ? "Disable DOF" : "Enable DOF", new Color(0.25f, 0.75f, 0.25f))) OnDofToggled(!dofEnabled);

			//if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Database Editor", new Color(0.6f, 0.3f, 0.8f))) UIController.OpenPanel<DatabaseEditorPanel>();

			//if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Definition Editor", new Color(0.6f, 0.3f, 0.8f))) UIController.OpenPanel<DefinitionEditorPanel>();

			//if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Effect Editor", new Color(0.6f, 0.3f, 0.8f))) UIController.OpenPanel<EffectEditorPanel>();

			//if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Model Editor", new Color(0.6f, 0.3f, 0.8f))) UIController.OpenPanel<ModelEditorPanel>();//placeholder

			//GUI.contentColor = mode == "View" ? Color.cyan : Color.white;
			//if (GUI.Button(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "View")) SetEditorMode(EditorMode.View);

			GUI.contentColor = mode == "Placement" ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Placement")) SetEditorMode(EditorMode.Placement);

			GUI.contentColor = (currentMode ?? EditorMode.Placement) == EditorMode.Attachment ? Color.cyan : Color.white;
			if (GUI.Button(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Attachments")) SetEditorMode(EditorMode.Attachment);

			GUI.contentColor = prevContentColor;

			numItems = ct;

//			var mainController = GetComponent<MainController>();
//#if UNITY_EDITOR
//			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Import Map", new Color(0.2f, 0.6f, 1f)))
//				mainController.ImportMapAsAtomic();

			//			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Export Map", new Color(0.8f, 0.2f, 0.2f)))
			//				mainController.ExportMapAsAtomic();
			//#endif

			//			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "(Re)Load Database", new Color(0.2f, 0.6f, 1f)))
			//				mainController.LoadDatabase();

			//#if UNITY_EDITOR
			//			if (GuiUtils.ColoredButton(new Rect(margin, y + ct++ * (buttonHeight + spacing), buttonWidth, buttonHeight), "Save Database", new Color(0.8f, 0.2f, 0.2f)))
			//				mainController.SaveDatabase();
			//#endif
		}

		//private void OnPostRender() => activeMode?.OnPostRender();
	}
}
