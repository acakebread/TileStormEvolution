using UnityEngine;
using UnityEngine.UI;

namespace ClassicTilestorm
{
    public class OptionsPanel : UIPanel
	{
		[Header("UI References")]
		[SerializeField] private Button closeButton;
		[SerializeField] private Button LoadDatabaseButton;
		[SerializeField] private Button SaveDatabaseButton;
		[SerializeField] private Button ImportMapButton;
		[SerializeField] private Button ExportMapButton;

		[SerializeField] private Toggle gridLinesToggle;
		[SerializeField] private Toggle depthOfFieldToggle;
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
					gridLinesToggle.isOn = editorController.GridEnabled;
					gridLinesToggle.onValueChanged.AddListener(isOn => editorController.GridEnabled = isOn);
				}
				if (null != depthOfFieldToggle)
				{
					depthOfFieldToggle.isOn = editorController.DofEnabled;
					depthOfFieldToggle.onValueChanged.AddListener(isOn => editorController.DofEnabled = isOn);
				}
				if (null != remapAssetsToggle)
				{
					remapAssetsToggle.isOn = ApplicationSettings.RemapGeometry;
					remapAssetsToggle.onValueChanged.AddListener(isOn => ApplicationSettings.RemapGeometry = isOn);
				}
			}
		}
		//private MainCameraController mainCameraController { get { TryGetComponent<MainCameraController>(out var controller); return controller; } }
		//private GameCameraEditor gameCameraEditor { get { if (null != mainCameraController && mainCameraController.activeSystem is GameCameraEditor editorCam) return editorCam; return null; } }
	}
}