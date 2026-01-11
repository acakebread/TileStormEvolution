using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		private PlaceholderUI placeholderUI;
		private MainController mainController => GetComponent<MainController>();
		private MapManager mapManager => null != mainController ? mainController.GetComponentInChildren<MapManager>(true) : null;

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

		private void HandleModeChanged(ApplicationMode mode)
		{
			ApplicationSettings.CurrentMode = mode;
			mainController.SetPreviewMode(mode);
		}

		public void Initialise(MapManager map)
		{
			if (isActiveAndEnabled) AudioManager.PlayMusic(map.CurrentMap.music, loop: true);
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Follow, true);
			controller.SetCameraSystem(CameraModeRegistry.Path, true);
		}

		void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
				controller.UpdateGestureControllerState();

			// Music
			if (null != mapManager) AudioManager.PlayMusic(mapManager.CurrentMap.music, loop: true);
			//AudioManager.PlayMusic(MusicAssets.Find(currentMap.music));


			//possibly move here
			//if (null != eggbotController) DestroyImmediate(eggbotController.gameObject);
			//eggbotController = EggbotController.Instantiate(currentMap.character, transform);
			//if (null != eggbotController) eggbotController.Initialise(mapManager);
		}

		void OnDisable() => AudioManager.StopMusic();
	}
}