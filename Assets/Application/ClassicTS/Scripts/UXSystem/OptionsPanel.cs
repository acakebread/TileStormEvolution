using MassiveHadronLtd;
using MassiveHadronLtd.FileBrowserUtil;
using System;
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
		[SerializeField] private Button ImportModelButton;

		[SerializeField] private Toggle difficultyToggle;
		[SerializeField] private Toggle musicToggle;
		[SerializeField] private Toggle gridLinesToggle;
		[SerializeField] private Toggle postProcessingToggle;

		[SerializeField] private Slider detailLevelSlider;
		[SerializeField] private TMP_Text detailLevelLabel;

		[SerializeField] private Toggle remapAssetsToggle;

		public static Action<bool> onDifficultyToggle;
		public static Action<bool> onMusicToggle;
		public static Action<bool> onGridlinesToggle;
		public static Action<int> onDetailLevelChanged;

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
					ImportMapButton.interactable = true;
				}
				if (null != ExportMapButton)
				{
					ExportMapButton.onClick.AddListener(() => mainController.ExportMapAsAtomic());
					ExportMapButton.interactable = true;
				}
				if (null != ImportModelButton)
				{
					ImportModelButton.onClick.AddListener(() =>
					{
#if UNITY_WEBGL && !UNITY_EDITOR
						string importRoot = RuntimeFileBrowser.GetDefaultRootFolder();
#else
						string importRoot = RuntimeFileBrowser.GetDefaultRootFolder();
#endif
						RuntimeFileBrowser.OpenObjFile(
							"Import Wavefront Model",
							path =>
							{
								var importedPath = WavefrontAssetImporter.ImportWavefrontModel(path);
								if (!string.IsNullOrEmpty(importedPath))
								{
									ClassicTilestorm.Assets.ModelAssets.RefreshRegistry(true);
									ClassicTilestorm.Assets.ProjectAssets.RefreshAllNameCaches();
								}
							},
							importRoot);
					});
					ImportModelButton.interactable = true;
				}
			}

			var editorController = FindAnyObjectByType<EditorController>(FindObjectsInactive.Include);
			if (null != editorController)
			{
				if (null != difficultyToggle)
				{
					difficultyToggle.isOn = ApplicationSettings.Difficulty;
					difficultyToggle.onValueChanged.AddListener(value => { ApplicationSettings.Difficulty = value; onDifficultyToggle?.Invoke(value); });
				}

				if (null != musicToggle)
				{
					musicToggle.isOn = ApplicationSettings.Music;
					musicToggle.onValueChanged.AddListener(value => { ApplicationSettings.Music = value; onMusicToggle?.Invoke(value); });
				}

				if (null != gridLinesToggle)
				{
					gridLinesToggle.isOn = ApplicationSettings.ShowEditorGrid;
					gridLinesToggle.onValueChanged.AddListener(value => { ApplicationSettings.ShowEditorGrid = value; onGridlinesToggle?.Invoke(value); });
				}

				// ─────────────── New detail level slider logic ───────────────
				if (null != detailLevelSlider)
				{
					detailLevelSlider.minValue = 0;
					detailLevelSlider.maxValue = 2;
					detailLevelSlider.wholeNumbers = true;

					// Load initial value
					int initialValue = ApplicationSettings.DetailLevel;
					//var cameraController = FindAnyObjectByType<MainCameraController>(FindObjectsInactive.Include);
					//if (null != cameraController)
					//	initialValue = cameraController.PostProcessingLevel;

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
			ApplicationSettings.DetailLevel = mode;

			//var cameraController = FindAnyObjectByType<MainCameraController>(FindObjectsInactive.Include);
			//if (null != cameraController)
			//	cameraController.PostProcessingLevel = mode;

			UpdateDetailLabel(mode);
			onDetailLevelChanged?.Invoke(mode);
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
	}
}
