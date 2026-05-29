using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		private PlaceholderUI placeholderUI;
		private MainController mainController => GetComponent<MainController>();
#if UNITY_WEBGL && !UNITY_EDITOR
		private bool pendingMusicPlayback;
		private string pendingMusicClipName;
#endif

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
			RequestMusicPlayback(iMap?.Music, ApplicationSettings.Music);
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
			{
				controller.UpdateGestureControllerState();
				controller.EnableEggbotTracking = true;
			}

			// Music
			//AudioManager.PlayMusic(MainController.CurrentMap?.Music, loop: true);
			//AudioManager.PlayMusic(MusicAssets.Find(currentMap.music));

			RequestMusicPlayback(MainController.CurrentMap?.Music, ApplicationSettings.Music);

			//possibly move here
			//if (null != eggbotController) DestroyImmediate(eggbotController.gameObject);
			//eggbotController = EggbotController.Instantiate(currentMap.character, transform);
			//if (null != eggbotController) eggbotController.Initialise(mapManager);
		}

		private void PlayMusic(bool value)
		{
			RequestMusicPlayback(MainController.CurrentMap?.Music, value);
		}

		private void RequestMusicPlayback(string clipName, bool value)
		{
			if (!value)
			{
#if UNITY_WEBGL && !UNITY_EDITOR
				pendingMusicPlayback = false;
				pendingMusicClipName = null;
#endif
				AudioManager.StopMusic();
				return;
			}

#if UNITY_WEBGL && !UNITY_EDITOR
			pendingMusicPlayback = true;
			pendingMusicClipName = clipName;
			if (isActiveAndEnabled && HasUserGesture())
			{
				pendingMusicPlayback = false;
				pendingMusicClipName = null;
				AudioManager.PlayMusic(clipName, loop: true);
			}
#else
			if (isActiveAndEnabled)
				AudioManager.PlayMusic(clipName, loop: true);
#endif
		}

		private void Update()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			if (!pendingMusicPlayback)
				return;

			if (!HasUserGesture())
				return;

			pendingMusicPlayback = false;
			var clipName = pendingMusicClipName ?? MainController.CurrentMap?.Music;
			pendingMusicClipName = null;
			AudioManager.PlayMusic(clipName, loop: true);
#endif
		}

		private static bool HasUserGesture()
		{
			return InputX.anyKeyDown ||
				   InputX.GetMouseButtonDown(0) ||
				   InputX.GetMouseButtonDown(1) ||
				   InputX.touchCount > 0;
		}

		void OnDisable()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			pendingMusicPlayback = false;
			pendingMusicClipName = null;
#endif
			AudioManager.StopMusic();
		}
	}
}
