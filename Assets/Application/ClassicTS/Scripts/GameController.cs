using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class GameController : MonoBehaviour
	{
		public MapManager mapManager { get; private set; }
		private GestureController gestureController;
		private EggbotController eggbotController;
		private CameraController cameraController;

		private const float CinemaTimeoutDuration = 5f;
		private bool enableAutoCinema;
		public bool CinemaEnabled => enableAutoCinema;

		public void SetAutoCinema(bool allow = true) => enableAutoCinema = allow;

		private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
			gestureController = gameObject.GetComponent<GestureController>();
			gestureController.OnMapUpdated += CheckDisableDrag;

			cameraController = Camera.main?.GetComponent<CameraController>();
			if (cameraController == null && Camera.main != null)
				cameraController = Camera.main.gameObject.AddComponent<CameraController>();
		}

		private void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			SetAutoCinema(PreviewSettings.LaunchInCinemaMode);
			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
			cameraController.OnCameraUpdate += OnCameraUpdate;
		}

		private void Update()
		{
			if (null != eggbotController) eggbotController.UpdateEggbot(mapManager);
			HandleCinemaRestart();
		}

		private void HandleCinemaRestart()
		{
			if (cameraController == null) return;

			var cameraSystem = cameraController.CameraSystem;
			if (cameraSystem == null) return;

			bool startCinema =
				cameraController.CurrentState == CameraState.Cinema
					? cameraSystem.HasCompleted
					: CinemaEnabled && Time.time - cameraController.LastRefreshTime > CinemaTimeoutDuration;

			if (!startCinema) return;

			var playerTransform = cameraSystem.playerTransform;
			var points = cameraSystem.focusPoints;

			cameraController.SetMode(CameraState.Cinema);
			cameraSystem.playerTransform = playerTransform;
			cameraSystem.focusPoints = points;
			cameraSystem.Start();
		}

		private void OnCameraUpdate(CameraState state)
		{
			switch (state)
			{
				case CameraState.Follow:
				case CameraState.Cinema:
					cameraController.SetPlayer(eggbotController != null ? eggbotController.transform : null);
					break;
			}
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName))
				mapName = mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);

			if (mapName == null) return;

			var currentMap = string.IsNullOrEmpty(mapName)
				? DatabaseLoader.Maps.FirstOrDefault()
				: DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);

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

			if (eggbotController != null) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (eggbotController != null)
			{
				eggbotController.Initialise(mapManager);
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}

			cameraController.Reset();
			cameraController.SetMode(CameraState.Follow);
			cameraController.SetPlayer(eggbotController.transform);
			cameraController.SetFocusPoints(mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList());

			var srcPos = new Vector3(0f, 14f, -14f);
			var dstPos = Vector3.zero;

			if (mapManager.Waypoints != null && mapManager.Waypoints.Length != 0)
			{
				dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
				srcPos += dstPos;

				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.bCamera)
				{
					if (firstWaypoint.vSrc != null) srcPos = firstWaypoint.vSrc.ToVector3();
					if (firstWaypoint.vDst != null) dstPos = firstWaypoint.vDst.ToVector3();
				}
			}

			cameraController.SetOrigin(srcPos, true);
			cameraController.SetTarget(dstPos, true);

			if (eggbotController != null)
			{
				var postProcessingCameraController = FindFirstObjectByType<PostProcessingCameraController>(FindObjectsInactive.Include);
				if (postProcessingCameraController != null)
					postProcessingCameraController.target = eggbotController.transform;
			}

			gestureController.Initialise(mapManager);
			if (PreviewSettings.DebugMode) Camera.main.fieldOfView = 45;
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (cameraController.CurrentState == CameraState.Cinema) return;
			if (eggbotController == null) return;
			if (mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (mapManager.Waypoints.Length - 1 == waypointIndex || waypointIndex == 0) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
			{
				cameraController.SetMode(CameraState.Follow);
				cameraController.SetPlayer(eggbotController.transform);
				return;
			}

			cameraController.SetMode(CameraState.Preset);
			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			cameraController.SetOrigin(origin);
			var target = waypoint.vDst != null && waypoint.vDst.IsValidVector()
				? waypoint.vDst.ToVector3()
				: mapManager.TileWorldPosition(waypoint.nTile);
			cameraController.SetTarget(target);
			gestureController.enabled = true;
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (cameraController.CurrentState == CameraState.Cinema) return;
			if (eggbotController == null) return;
			cameraController.SetMode(CameraState.Follow);
			cameraController.SetPlayer(eggbotController.transform);
		}

		private void OnLevelCompleted() { }

		private void CheckDisableDrag(IMapManager imap)
		{
			if (eggbotController != null && eggbotController.NavDirection(imap) != 0)
				gestureController.enabled = false;
		}

		private void OnDestroy()
		{
			if (eggbotController == null) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
			gestureController.OnMapUpdated -= CheckDisableDrag;
			cameraController.OnCameraUpdate -= OnCameraUpdate;
		}
	}
}
