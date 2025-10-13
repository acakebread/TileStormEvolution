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

		private bool isPlayerMode => CameraMode.Preset == cameraController.CurrentMode || CameraMode.Follow == cameraController.CurrentMode;

		private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
			gestureController = gameObject.GetComponent<GestureController>();
			gestureController.OnMapUpdated += CheckDisableDrag;

			cameraController = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
			if (null == cameraController && null != Camera.main) cameraController = Camera.main.gameObject.AddComponent<CameraController>();
		}

		private void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
		}

		private void Update()
		{
			if (null != eggbotController) eggbotController.UpdateEggbot(mapManager);
			CinemaUpdate();
		}

		private void CinemaUpdate()
		{
			if (null == cameraController || null == cameraController.cameraSystem) return;

			bool startCinema = cameraController.CurrentMode == CameraMode.Cinema && cameraController.cameraSystem.HasCompleted && Time.time - timeStart > CinemaTimeoutDuration;
			if (!startCinema) return;

			timeStart = Time.time;
			cameraController.SetCameraMode(CameraMode.Cinema);
		}

		public void SetPreviewMode(PreviewMode mode, bool forceCinema = false)
		{
			if (mode == PreviewMode.Cinema)
				timeStart = forceCinema ? Time.time - CinemaTimeoutDuration : Time.time;

			CameraMode cameraMode = mode switch
			{
				PreviewMode.Editor => CameraMode.Editor,
				PreviewMode.Cinema => CameraMode.Cinema,
				PreviewMode.Player => cameraController.GetStateForMode(CameraMode.Preset).cameraMode, // Gets current mode of playerState
				_ => CameraMode.Absent
			};

			cameraController.SetCameraMode(cameraMode);
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName)) mapName = mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);
			if (mapName == null) return;

			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			PreviewSettings.LoadMapName = currentMap.name;
			PlayerPrefs.SetString("LastLoadedMap", currentMap.name);
			PlayerPrefs.Save();

			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.szMusic);

			if (null != mapManager) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);
			mapManager.SetupWaypoints(currentMap);

			gestureController.Initialise(mapManager);
			timeStart = Time.time;

			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (null != eggbotController)
			{
				eggbotController.Initialise(mapManager);
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}

			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);

			if (null != mapManager.Waypoints && mapManager.Waypoints.Length > 0 && mapManager.Waypoints[0].bCamera)
			{
				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.vSrc != null) srcPos = firstWaypoint.vSrc.ToVector3();
				if (firstWaypoint.vDst != null) dstPos = firstWaypoint.vDst.ToVector3();
			}

			var editorState = new CameraState { cameraMode = CameraMode.Editor, data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos } };
			var playerState = new CameraState { cameraMode = CameraMode.Follow, data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos }, target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero, origin = () => srcPos };
			var cinemaState = new CameraState { cameraMode = CameraMode.Cinema, data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos }, target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero };

			cameraController.RegisterState(editorState);
			cameraController.RegisterState(playerState);
			cameraController.RegisterState(cinemaState);

			Func <IReadOnlyList<Vector3>> focusFunc = () =>
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
			if (null != eggbotController && null != ppController)
				ppController.dofTarget = eggbotController.transform;

			CameraMode initialMode = PreviewSettings.CurrentMode switch
			{
				PreviewMode.Editor => CameraMode.Editor,
				PreviewMode.Cinema => CameraMode.Cinema,
				PreviewMode.Player => CameraMode.Follow, // Default to Follow for Player mode
				_ => CameraMode.Absent
			};
			cameraController.SetCameraMode(initialMode);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (null == eggbotController || null == mapManager || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (mapManager.Waypoints.Length - 1 == waypointIndex || waypointIndex == 0) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (null == waypoint.vSrc || !waypoint.vSrc.IsValidVector())
			{
				cameraController.SetCameraMode(CameraMode.Follow, isPlayerMode);
				return;
			}

			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			var target = waypoint.vDst != null && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			// Update playerState indirectly via CameraController
			CameraState playerState = cameraController.GetStateForMode(CameraMode.Preset);
			playerState.origin = () => origin;
			playerState.target = () => target;
			playerState.cameraMode = CameraMode.Preset;

			cameraController.SetCameraMode(CameraMode.Preset, isPlayerMode);
			gestureController.enabled = isPlayerMode;
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (null == eggbotController) return;

			// Update playerState indirectly via CameraController
			CameraState playerState = cameraController.GetStateForMode(CameraMode.Follow);
			playerState.target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero;
			playerState.cameraMode = CameraMode.Follow;

			cameraController.SetCameraMode(CameraMode.Follow, isPlayerMode);
		}

		private void OnLevelCompleted() { }

		private void CheckDisableDrag(IMapManager imap)
		{
			if (null != eggbotController && eggbotController.NavDirection(imap) != 0)
				gestureController.enabled = false;
		}

		private void OnDestroy()
		{
			gestureController.OnMapUpdated -= CheckDisableDrag;
			if (null == eggbotController) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
		}
	}
}