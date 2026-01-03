using UnityEngine;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		private PlaceholderUI placeholderUI;
		private MainController mainController => GetComponent<MainController>();

		public void Awake()
		{
			placeholderUI = gameObject.AddComponent<PlaceholderUI>();

			// Subscribe to UI events
			placeholderUI.OnModeChanged += HandleModeChanged;
			placeholderUI.OnChangeMapRequested += mainController.HandleChangeMap;
			placeholderUI.OnPresetRequested += () => mainController.Preset();
			placeholderUI.OnScrambleRequested += () => mainController.Scramble();
			placeholderUI.OnSolveRequested += () => mainController.Solve();
		}

		private void HandleModeChanged(PreviewMode mode)
		{
			PreviewSettings.CurrentMode = mode;
			mainController.SetPreviewMode(mode);
		}

		public void Initialise()
		{
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Follow, true);
			controller.SetCameraSystem(CameraModeRegistry.Path, true);
		}

		void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
				controller.UpdateGestureControllerState();

			//possibly move here
			//if (null != eggbotController) DestroyImmediate(eggbotController.gameObject);
			//eggbotController = EggbotController.Instantiate(currentMap.character, transform);
			//if (null != eggbotController) eggbotController.Initialise(mapManager);
		}
	}
}