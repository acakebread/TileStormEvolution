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
		private GameCameraController cameraController;
		private GestureController gestureController;
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
			gestureController.OnMapUpdated += CheckDisableDrag;
			cameraController = EnsureCameraController(); // Ensure this is called early
			cameraController.OnWaypointReachedForGestures += OnWaypointGesturesEnable; // Subscribe to event
		}

		private void Start()
		{
			DatabaseSerializer.Init(PreviewSettings.DatabaseJsonFile, (asset) => { PreviewSettings.DatabaseJsonFile = asset; });
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
			cameraController.SetCameraMode(Random.Range(0, 7) switch { 0 or 1 or 2 => CameraMode.Orbit, _ => CameraMode.Path });
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
					PreviewMode.Cinema => CameraMode.Orbit,
					PreviewMode.Player => CameraMode.Preset,
					PreviewMode.Direct => CameraMode.Direct,
					_ => CameraMode.Absent
				};
				cameraController.SetCameraMode(cameraController.GetCurrentGroupMode(cameraMode));
				
			}
			UpdateGestureControllerState();
		}

		public void LoadMap(string mapName = null)
		{
			mapName ??= mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);
			if (mapName == null) return;

			var currentMap = DatabaseSerializer.Maps.FirstOrDefault(m => m.name == mapName) ?? DatabaseSerializer.Maps.FirstOrDefault();
			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseSerializer.Maps.Select(m => m.name))}");
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
					PreviewMode.Player => CameraMode.Follow,
					//PreviewMode.Cinema => CameraMode.Cinema,
					PreviewMode.Cinema => Random.Range(0, 7) switch { 0 or 1 or 2 => CameraMode.Orbit, _ => CameraMode.Path },
					_ => CameraMode.Follow
				};
				cameraController.Initialise(mapManager, eggbotController, initialMode);
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
			if (cameraController != null)
				cameraController.OnWaypointReachedForGestures -= OnWaypointGesturesEnable;
		}

		private GameCameraController EnsureCameraController()
		{
			if (Camera.main == null)
			{
				Debug.LogWarning("Cannot create GameCameraController: Camera.main is null");
				return null;
			}

			var controller = Camera.main.GetComponent<GameCameraController>();
			if (controller == null)
			{
				controller = Camera.main.gameObject.AddComponent<GameCameraController>();
				Debug.Log("Created GameCameraController on Camera.main");
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