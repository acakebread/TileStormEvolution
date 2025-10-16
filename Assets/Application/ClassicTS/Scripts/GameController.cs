using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class GameController : MonoBehaviour
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private CameraController cameraController;
		private GestureController gestureController;
		private CameraStateController cameraStateController;
		private PostProcessingCameraController postProcessingController;
		private bool gestureControllerEnabled = true;
		private const float CinemaTimeoutDuration = 5f;
		private float cinemaTimer;

		private bool GestureControllerEnabled
		{
			set { gestureControllerEnabled = value; UpdateGestureControllerState(); }
		}

		private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
			gestureController = GetComponent<GestureController>();
			cameraStateController = gameObject.AddComponent<TilestormCameraStateController>();
			((TilestormCameraStateController)cameraStateController).OnWaypointReachedForGestures += OnWaypointGesturesEnable;
			gestureController.OnMapUpdated += CheckDisableDrag;
		}

		private void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
		}

		private void Update()
		{
			if (eggbotController != null) eggbotController.UpdateEggbot(mapManager);
			CinemaUpdate();
		}

		private void CinemaUpdate()
		{
			if (cameraController == null || PreviewMode.Cinema != PreviewSettings.CurrentMode || !cameraController.HasCompleted || Time.time - cinemaTimer <= CinemaTimeoutDuration) return;
			cinemaTimer = Time.time;
			cameraController.SetCameraMode(CameraMode.Cinema);
		}

		public void SetPreviewMode(PreviewMode mode, bool forceCinema = false)
		{
			if (mode == PreviewMode.Cinema)
				cinemaTimer = Time.time - (forceCinema ? CinemaTimeoutDuration : 0);
			if (cameraController != null)
			{
				var cameraMode = mode switch
				{
					PreviewMode.Editor => CameraMode.Editor,
					PreviewMode.Cinema => CameraMode.Cinema,
					PreviewMode.Player => CameraMode.Preset,
					PreviewMode.Direct => CameraMode.Direct,
					_ => CameraMode.Absent
				};
				var state = cameraController.GetStateForMode(cameraMode);
				if (state != null) cameraController.SetCameraMode(state.mode);
			}
			UpdateGestureControllerState();
		}

		public void LoadMap(string mapName = null)
		{
			mapName ??= mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);
			if (mapName == null) return;

			var currentMap = DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName) ?? DatabaseLoader.Maps.FirstOrDefault();
			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			PreviewSettings.LoadMapName = currentMap.name;
			PlayerPrefs.SetString("LastLoadedMap", currentMap.name);
			PlayerPrefs.Save();

			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.szMusic);

			if (mapManager != null) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);
			mapManager.SetupWaypoints(currentMap);

			gestureController.Initialise(mapManager);
			GestureControllerEnabled = false;
			cinemaTimer = Time.time;

			if (eggbotController != null) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (eggbotController != null)
			{
				eggbotController.Initialise(mapManager);
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}

			cameraController = EnsureCameraController();
			if (cameraController != null)
			{
				var initialMode = PreviewSettings.CurrentMode switch
				{
					PreviewMode.Editor => CameraMode.Editor,
					PreviewMode.Cinema => CameraMode.Cinema,
					PreviewMode.Player => CameraMode.Follow,
					_ => CameraMode.Follow
				};
				((TilestormCameraStateController)cameraStateController).Initialise(mapManager, eggbotController, cameraController, initialMode);
			}

			postProcessingController = EnsurePostProcessingController();
			if (postProcessingController != null && eggbotController != null)
				postProcessingController.dofTarget = eggbotController.transform;
		}

		public void Scramble() => mapManager?.Scramble();

		public void Solve()
		{
			if (mapManager != null) mapManager.Solve();
			GestureControllerEnabled = false;
		}

		private void OnLevelCompleted() { }

		private void CheckDisableDrag(IMapManager imap)
		{
			if (eggbotController != null && eggbotController.NavDirection(imap) != 0)
				GestureControllerEnabled = false;
		}

		private void UpdateGestureControllerState()
		{
			gestureController.enabled = gestureControllerEnabled && PreviewMode.Player == PreviewSettings.CurrentMode;
		}

		private void OnWaypointGesturesEnable(bool value)
		{
			GestureControllerEnabled = value;
		}

		private void OnDestroy()
		{
			gestureController.OnMapUpdated -= CheckDisableDrag;
			if (eggbotController != null)
				eggbotController.OnLevelCompleted -= OnLevelCompleted;
			if (cameraStateController != null)
				((TilestormCameraStateController)cameraStateController).OnWaypointReachedForGestures -= OnWaypointGesturesEnable;
		}

		private CameraController EnsureCameraController()
		{
			if (Camera.main == null)
			{
				Debug.LogWarning("Cannot create CameraController: Camera.main is null");
				return null;
			}

			var controller = Camera.main.GetComponent<CameraController>();
			if (controller == null)
			{
				controller = Camera.main.gameObject.AddComponent<CameraController>();
				Debug.Log("Created CameraController on Camera.main");
			}
			return controller;
		}

		private PostProcessingCameraController EnsurePostProcessingController()
		{
			if (Camera.main == null)
			{
				Debug.LogWarning("Cannot create PostProcessingCameraController: Camera.main is null");
				return null;
			}

			var ppController = Camera.main.GetComponentInChildren<PostProcessingCameraController>(true);
			if (ppController == null)
			{
				var ppObject = new GameObject("PostProcessing");
				ppObject.transform.SetParent(Camera.main.transform, false);
				ppController = ppObject.AddComponent<PostProcessingCameraController>();
				Debug.Log("Created PostProcessingCameraController on Camera.main");
			}
			return ppController;
		}
	}
}