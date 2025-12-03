using UnityEngine;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		private PlaceholderUI placeholderUI;
		private MainController mainController;

		public void Awake()
		{
			placeholderUI = gameObject.AddComponent<PlaceholderUI>();
			mainController = GetComponent<MainController>();

			// Subscribe to UI events
			placeholderUI.OnModeChanged += HandleModeChanged;
			placeholderUI.OnChangeMapRequested += HandleChangeMap;
			placeholderUI.OnPresetRequested += () => mainController.Preset();
			placeholderUI.OnScrambleRequested += () => mainController.Scramble();
			placeholderUI.OnSolveRequested += () => mainController.Solve();
		}

		private void HandleModeChanged(PreviewMode mode)
		{
			PreviewSettings.CurrentMode = mode;
			mainController.SetPreviewMode(mode);
		}

		private void HandleChangeMap(int delta)
		{
			var maps = ResourceManager.Maps;
			if (maps == null || maps.Count == 0) return;

			// Find current index the old-school way — works on ANY IList<T>
			int currentIndex = -1;
			for (int i = 0; i < maps.Count; i++)
			{
				if (maps[i]?.name == PreviewSettings.LoadMapName)
				{
					currentIndex = i;
					break;
				}
			}

			// If not found, default to 0
			if (currentIndex == -1) currentIndex = 0;

			if (delta != 0)
			{
				currentIndex = (currentIndex + delta + maps.Count) % maps.Count;
				PreviewSettings.LoadMapName = maps[currentIndex].name;
			}

			mainController.LoadMap();
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
		}
	}
}