using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using System;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class GameController : MonoBehaviour
	{
		public MapManager mapManager { get; private set; }
		private GestureController gestureController;
		private EggbotController eggbotController;
		private SpatialBucketSystem spatialSystem;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;
		private const float CinemaTimeoutDuration = 5f;
		private float timeStart;

		private CameraController cameraController;
		private CameraState editorState;
		private CameraState playerState;
		private CameraState cinemaState;

		private bool isPlayerMode => cameraController.CurrentMode == CameraMode.Preset || cameraController.CurrentMode == CameraMode.Follow;

		private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
			gestureController = gameObject.GetComponent<GestureController>();
			gestureController.OnMapUpdated += CheckDisableDrag;

			cameraController = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
			if (cameraController == null && Camera.main != null) cameraController = Camera.main.gameObject.AddComponent<CameraController>();
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
			if (cameraController == null || cameraController.cameraSystem == null) return;

			bool startCinema = cameraController.CurrentMode == CameraMode.Cinema && cameraController.cameraSystem.HasCompleted && Time.time - timeStart > CinemaTimeoutDuration;
			if (!startCinema) return;

			timeStart = Time.time;
			cameraController.SetCameraMode(CameraMode.Cinema, cinemaState);
		}

		public void SetPreviewMode(PreviewMode mode, bool forceCinema = false)
		{
			if (isPlayerMode)
				playerState.cameraMode = cameraController.CurrentMode;

			CameraState state = mode switch
			{
				PreviewMode.Editor => editorState,
				PreviewMode.Cinema => cinemaState,
				PreviewMode.Player => playerState,
				_ => null
			};

			CameraMode camMode = state?.cameraMode ?? CameraMode.Absent;

			if (camMode == CameraMode.Cinema)
				timeStart = forceCinema ? Time.time - CinemaTimeoutDuration : Time.time;

			cameraController.SetCameraMode(camMode, state);
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName)) mapName = mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);
			if (mapName == null) return;

			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
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
			timeStart = Time.time;

			if (eggbotController != null) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (eggbotController != null)
			{
				eggbotController.Initialise(mapManager);
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}

			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);

			if (mapManager.Waypoints != null && mapManager.Waypoints.Length > 0 && mapManager.Waypoints[0].bCamera)
			{
				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.vSrc != null) srcPos = firstWaypoint.vSrc.ToVector3();
				if (firstWaypoint.vDst != null) dstPos = firstWaypoint.vDst.ToVector3();
			}

			var editorCameraData = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos };
			var playerCameraData = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos };
			var cinemaCameraData = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos };

			editorState = new CameraState { cameraMode = CameraMode.Editor, data = editorCameraData };
			playerState = new CameraState
			{
				cameraMode = CameraMode.Follow,
				data = playerCameraData,
				target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero,
				origin = () => srcPos
			};
			cinemaState = new CameraState
			{
				cameraMode = CameraMode.Cinema,
				data = cinemaCameraData,
				target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero
			};

			Func<IReadOnlyList<Vector3>> focusFunc = () =>
			{
				var waypoints = mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList();
				spatialSystem.SetPoints(waypoints);

				focusFunc = () =>
				{
					if (eggbotController != null && eggbotController.transform != null)
						spatialSystem.TryAddPoint(eggbotController.transform.position);
					return spatialSystem.Points;
				};

				return spatialSystem.Points;
			};

			cinemaState.focusPoints = () => focusFunc();

			var ppController = cameraController.GetComponentInChildren<PostProcessingCameraController>(true);
			if (eggbotController != null && ppController != null)
				ppController.dofTarget = eggbotController.transform;

			cameraController.SetCameraMode(PreviewSettings.CurrentMode == PreviewMode.Editor ? CameraMode.Editor :
										  PreviewSettings.CurrentMode == PreviewMode.Cinema ? CameraMode.Cinema :
										  playerState.cameraMode, state: PreviewSettings.CurrentMode == PreviewMode.Editor ? editorState :
																		 PreviewSettings.CurrentMode == PreviewMode.Cinema ? cinemaState :
																		 playerState);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (eggbotController == null || mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (mapManager.Waypoints.Length - 1 == waypointIndex || waypointIndex == 0) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
			{
				playerState.target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero;
				playerState.cameraMode = CameraMode.Follow;

				if (!isPlayerMode)
					return;

				cameraController.SetCameraMode(CameraMode.Follow, playerState);
				return;
			}

			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			var target = waypoint.vDst != null && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			playerState.origin = () => origin;
			playerState.target = () => target;
			playerState.cameraMode = CameraMode.Preset;

			if (!isPlayerMode)
				return;

			cameraController.SetCameraMode(CameraMode.Preset, playerState);
			gestureController.enabled = true;
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (eggbotController == null) return;

			playerState.target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero;
			playerState.cameraMode = CameraMode.Follow;

			if (!isPlayerMode)
				return;

			cameraController.SetCameraMode(CameraMode.Follow, playerState);
		}

		private void OnLevelCompleted() { }

		private void CheckDisableDrag(IMapManager imap)
		{
			if (eggbotController != null && eggbotController.NavDirection(imap) != 0)
				gestureController.enabled = false;
		}

		private void OnDestroy()
		{
			gestureController.OnMapUpdated -= CheckDisableDrag;
			if (eggbotController == null) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
		}
	}
}