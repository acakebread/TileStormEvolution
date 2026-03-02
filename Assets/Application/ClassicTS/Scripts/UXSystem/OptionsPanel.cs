using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
    public class OptionsPanel : UIPanel
	{
		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private Button DatabaseEditorButton;
		[SerializeField] private Button DefinitionEditorButton;
		[SerializeField] private Button LoadDatabaseButton;
		[SerializeField] private Button SaveDatabaseButton;
		[SerializeField] private Button ImportMapButton;
		[SerializeField] private Button ExportMapButton;

		[SerializeField] private Toggle gridLinesToggle;
		[SerializeField] private Toggle postProcessingToggle;

		[SerializeField] private Slider detailLevelSlider;          // ← new
		[SerializeField] private TMP_Text detailLevelLabel;           // optional – shows current text

		[SerializeField] private Toggle remapAssetsToggle;

		protected override void Awake()
		{
			base.Awake();
			InitializeUIReferences();
		}

		private void InitializeUIReferences()
		{
			if (null != closeButton)
				closeButton.onClick.AddListener(() => gameObject.SetActive(false));

			if (null != DatabaseEditorButton)
				DatabaseEditorButton.onClick.AddListener(() => UIController.OpenPanel<DatabaseEditorPanel>());
			if (null != DefinitionEditorButton)
				DefinitionEditorButton.onClick.AddListener(() => UIController.OpenPanel<DefinitionEditorPanel>());

			var mainController = FindAnyObjectByType<MainController>(FindObjectsInactive.Include);
			if (null != mainController)
			{
				if (null != LoadDatabaseButton)
					LoadDatabaseButton.onClick.AddListener(() => mainController.LoadDatabase());
				if (null != SaveDatabaseButton)
				{
					SaveDatabaseButton.onClick.AddListener(() => mainController.SaveDatabase());
					SaveDatabaseButton.interactable = Application.isEditor;
				}
				if (null != ImportMapButton)
				{
					ImportMapButton.onClick.AddListener(() => mainController.ImportMapAsAtomic());
					ImportMapButton.interactable = Application.isEditor;
				}
				if (null != ExportMapButton)
				{
					ExportMapButton.onClick.AddListener(() => mainController.ExportMapAsAtomic());
					ExportMapButton.interactable = Application.isEditor;
				}
			}

			var editorController = FindAnyObjectByType<EditorController>(FindObjectsInactive.Include);
			if (null != editorController)
			{
				if (null != gridLinesToggle)
				{
					gridLinesToggle.isOn = MassiveHadronLtd.GridLinesUtil.Enabled;
					gridLinesToggle.onValueChanged.AddListener(isOn => MassiveHadronLtd.GridLinesUtil.Enabled = isOn);
				}
				//if (null != postProcessingToggle)
				//{
				//	postProcessingToggle.isOn = editorController.PostProcessingEnabled;
				//	postProcessingToggle.onValueChanged.AddListener(isOn => editorController.PostProcessingEnabled = isOn);
				//}

				// ─────────────── New detail level slider logic ───────────────
				if (null != detailLevelSlider)
				{
					// Default to Game Only
					detailLevelSlider.minValue = 0;
					detailLevelSlider.maxValue = 2;
					detailLevelSlider.wholeNumbers = true;

					// Load initial value
					//bool ppGame = editorController.PostProcessingEnabled;
					//bool ppEditor = editorController.PostProcessingEnabled;/* decide how to load editor-specific setting – see note below */;

					//int initialValue = 0;
					//if (ppGame)
					//{
					//	initialValue = ppEditor ? 2 : 1;
					//}

					//bool ppGame = editorController.PostProcessingEnabled;
					//bool ppEditor = editorController.PostProcessingEnabled;/* decide how to load editor-specific setting – see note below */;

					int initialValue = 1;//default
					var cameraController = FindAnyObjectByType<MainCameraController>(FindObjectsInactive.Include);
					if (null != cameraController)
						initialValue = cameraController.PostProcessingLevel;

					detailLevelSlider.value = initialValue;

					// Optional: show current mode as text
					if (detailLevelLabel != null)
						UpdateDetailLabel(initialValue);

					detailLevelSlider.onValueChanged.AddListener(OnDetailLevelChanged);

					// Apply initial state
					OnDetailLevelChanged(initialValue);
				}

				if (null != remapAssetsToggle)
				{
					remapAssetsToggle.isOn = ApplicationSettings.RemapGeometry;
					remapAssetsToggle.onValueChanged.AddListener(isOn => ApplicationSettings.RemapGeometry = isOn);
				}
			}
		}

		private void OnDetailLevelChanged(float value)
		{
			int mode = Mathf.RoundToInt(value);

			var cameraController = FindAnyObjectByType<MainCameraController>(FindObjectsInactive.Include);
			if (null != cameraController)
				cameraController.PostProcessingLevel = mode;

			//var mainController = FindAnyObjectByType<MainController>(FindObjectsInactive.Include);
			//if (null != mainController)
			//	mainController.PostProcessingLevel = mode;

			//var editor = FindAnyObjectByType<EditorController>(FindObjectsInactive.Include);
			//if (null != editor)
			//	editor.PostProcessingEnabled = (mode >= 1);           // GameOnly or Game+Editor

			// Core logic: decide who gets post-processing

			// If your EditorController has a separate flag for editor-only post-processing:
			// editor.PostProcessingInEditorOnly = (mode == 2);

			// Or if it uses the same bool but only applies in editor context:
			// (most common approach → one bool, controlled by context)

			UpdateDetailLabel(mode);
		}

		private void UpdateDetailLabel(int mode)
		{
			if (detailLevelLabel == null) return;

			switch (mode)
			{
				case 0: detailLevelLabel.text = "Off"; break;
				case 1: detailLevelLabel.text = "Game Only"; break;
				case 2: detailLevelLabel.text = "Game + Editor"; break;
				default: detailLevelLabel.text = "—"; break;
			}
		}
		//private MainCameraController mainCameraController { get { TryGetComponent<MainCameraController>(out var controller); return controller; } }
		//private GameCameraEditor gameCameraEditor { get { if (null != mainCameraController && mainCameraController.activeSystem is GameCameraEditor editorCam) return editorCam; return null; } }
	}
}