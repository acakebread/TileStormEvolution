using UnityEngine;
using MassiveHadronLtd;

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

			OptionsPanel.onMusicToggle += value => PlayMusic(value);
		}

		private void HandleModeChanged(ApplicationMode mode)
		{
			ApplicationSettings.CurrentMode = mode;
			mainController.SetPreviewMode(mode);
		}

		private System.Action _unsubscribeMapAction;

		public void Initialise(IMapEdit iMap)
		{
			if (isActiveAndEnabled && ApplicationSettings.Music) AudioManager.PlayMusic(iMap.Music, loop: true);
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Follow, true);
			controller.SetCameraSystem(CameraModeRegistry.Path, true);

			iMap.OnMapEdited += OnMapEdited;
			_unsubscribeMapAction = () => iMap.OnMapEdited -= OnMapEdited;
		}

		public void Reset()
		{
			_unsubscribeMapAction?.Invoke();
			_unsubscribeMapAction = null;
		}

		private void OnMapEdited(IMapEdit iMap, bool resized, Vector3 delta)
		{
			if (!isActiveAndEnabled) AudioManager.StopMusic();
		}

		void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
				controller.UpdateGestureControllerState();

			// Music
			//AudioManager.PlayMusic(MainController.CurrentMap?.Music, loop: true);
			//AudioManager.PlayMusic(MusicAssets.Find(currentMap.music));

			PlayMusic(ApplicationSettings.Music);

			//possibly move here
			//if (null != eggbotController) DestroyImmediate(eggbotController.gameObject);
			//eggbotController = EggbotController.Instantiate(currentMap.character, transform);
			//if (null != eggbotController) eggbotController.Initialise(mapManager);
		}

		private void PlayMusic(bool value)
		{
			if (value && isActiveAndEnabled)
				AudioManager.PlayMusic(MainController.CurrentMap?.Music, loop: true);
			else
				AudioManager.StopMusic();
		}

		void OnDisable() => AudioManager.StopMusic();
	}
}