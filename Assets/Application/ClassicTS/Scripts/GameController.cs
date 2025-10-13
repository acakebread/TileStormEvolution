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

		// CameraController properties
		private CameraController cameraController;

		private CameraMode restoreMode = CameraMode.Absent;
		private CameraMode currentMode = CameraMode.Absent;

		private class CameraState
		{
			public CameraData data;
			public Func<Vector3> origin;
			public Func<Vector3> target;
			public Func<IReadOnlyList<Vector3>> focusPoints;
		}

		private CameraState editorState;
		private CameraState playerState;
		private CameraState cinemaState;

		private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
			gestureController = gameObject.GetComponent<GestureController>();
			gestureController.OnMapUpdated += CheckDisableDrag;

			cameraController = null != Camera.main ? Camera.main.GetComponent<CameraController>() : null;
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

		private void SetCameraMode(CameraMode value)
		{
			currentMode = value;

			var state = value switch
			{
				CameraMode.Cinema => cinemaState,
				CameraMode.Editor => editorState,
				CameraMode.Preset => playerState,
				CameraMode.Follow => playerState,
				_ => null
			};

			cameraController.SetMode(value);

			if (null != state)
			{
				cameraController.cameraSystem.data = state.data;
				cameraController.cameraSystem.originFunc = state.origin;
				cameraController.cameraSystem.targetFunc = state.target;
				cameraController.cameraSystem.focusPointsFunc = state.focusPoints;
			}

			cameraController.Initialise();
		}

		private void CinemaUpdate()
		{
			if (null == cameraController || null == cameraController.cameraSystem) return;

			bool startCinema = currentMode == CameraMode.Cinema && cameraController.cameraSystem.HasCompleted && Time.time - timeStart > CinemaTimeoutDuration;
			if (!startCinema) return;

			timeStart = Time.time;
			SetCameraMode(CameraMode.Cinema);
		}

		public void SetPreviewMode(PreviewMode mode, bool forceCinema = false)
		{
			if (currentMode == CameraMode.Preset || currentMode == CameraMode.Follow) { restoreMode = currentMode; }
			CameraMode camMode = mode switch
			{
				PreviewMode.Editor => CameraMode.Editor,
				PreviewMode.Cinema => CameraMode.Cinema,
				_ => restoreMode
			};

			if (mode == PreviewMode.Cinema)
				timeStart = forceCinema ? Time.time - CinemaTimeoutDuration : Time.time;

			SetCameraMode(camMode);
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName)) mapName = mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);
			if (null == mapName) return;

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
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);// Classic TS default for no waypoints

			if (null != mapManager.Waypoints && 0 != mapManager.Waypoints.Length && true == mapManager.Waypoints[0].bCamera)
			{
				var firstWaypoint = mapManager.Waypoints[0];
				if (null != firstWaypoint.vSrc) srcPos = firstWaypoint.vSrc.ToVector3();
				if (null != firstWaypoint.vDst) dstPos = firstWaypoint.vDst.ToVector3();
			}

			var editorCameraData = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos };
			var playerCameraData = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos };
			var cinemaCameraData = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos };

			editorState = new CameraState { data = editorCameraData };
			playerState = new CameraState { data = playerCameraData, target = () => null != eggbotController && null != eggbotController.transform ? eggbotController.transform.position : Vector3.zero, origin = () => srcPos };
			cinemaState = new CameraState { data = cinemaCameraData, target = () => null != eggbotController && null != eggbotController.transform ? eggbotController.transform.position : Vector3.zero };

			Func<IReadOnlyList<Vector3>> focusFunc = () =>
			{
				var waypoints = mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList();
				spatialSystem.SetPoints(waypoints);

				focusFunc = () =>
				{
					if (null != eggbotController && null != eggbotController?.transform)
						spatialSystem.TryAddPoint(eggbotController.transform.position);
					return spatialSystem.Points;
				};

				return spatialSystem.Points;
			};

			cinemaState.focusPoints = () => focusFunc();

			var ppController = cameraController.GetComponentInChildren<PostProcessingCameraController>(true);
			if (null != eggbotController && null != ppController)
				ppController.dofTarget = eggbotController.transform;

			SetCameraMode(PreviewSettings.CurrentMode == PreviewMode.Editor ? CameraMode.Editor : PreviewSettings.CurrentMode == PreviewMode.Cinema ? CameraMode.Cinema : CameraMode.Follow);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (null == eggbotController) return;
			if (null == mapManager || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (mapManager.Waypoints.Length - 1 == waypointIndex || waypointIndex == 0) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
			{
				playerState.target = () => null != eggbotController && null != eggbotController.transform ? eggbotController.transform.position : Vector3.zero;

				if (CameraMode.Cinema == currentMode || CameraMode.Editor == currentMode)
				{
					restoreMode = CameraMode.Follow;
					return;
				}

				SetCameraMode(CameraMode.Follow);
				return;
			}

			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			playerState.origin = () => origin;
			playerState.target = () => target;

			if (CameraMode.Cinema == currentMode || CameraMode.Editor == currentMode)
			{
				restoreMode = CameraMode.Preset;
				return;
			}
			SetCameraMode(CameraMode.Preset);
			gestureController.enabled = true;
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (null == eggbotController) return;

			playerState.target = () => null != eggbotController && null != eggbotController.transform ? eggbotController.transform.position : Vector3.zero;

			if (CameraMode.Cinema == currentMode || CameraMode.Editor == currentMode)
			{
				restoreMode = CameraMode.Follow;
				return;
			}
			SetCameraMode(CameraMode.Follow);
		}

		private void OnLevelCompleted() { }

		private void CheckDisableDrag(IMapManager imap)
		{
			if (null != eggbotController && 0 != eggbotController.NavDirection(imap))
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