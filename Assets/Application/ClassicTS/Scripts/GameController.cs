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
		private MapManager mapManager;
		private EggbotController eggbotController;
		private CameraController cameraController;

		//gesture control system respects modes
		private GestureController gestureController;
		private bool gestureControllerEnabled = true;
		private bool GestureControllerEnabled { set { gestureControllerEnabled = value; UpdateGestureControllerState(); } }
		private void UpdateGestureControllerState() => gestureController.enabled = gestureControllerEnabled && (PreviewMode.Player == PreviewSettings.CurrentMode);

		private SpatialBucketSystem spatialSystem;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		private const float CinemaTimeoutDuration = 5f;
		private float timeStart;

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
			if (null == cameraController) return;

			bool startCinema = PreviewMode.Cinema == PreviewSettings.CurrentMode && cameraController.HasCompleted && Time.time - timeStart > CinemaTimeoutDuration;
			if (!startCinema) return;

			timeStart = Time.time;
			cameraController.SetCameraMode(CameraMode.Cinema);
		}

		public void SetPreviewMode(PreviewMode mode, bool forceCinema = false)
		{
			if (mode == PreviewMode.Cinema)
				timeStart = Time.time - (forceCinema ? CinemaTimeoutDuration : 0);

			cameraController.SetCameraMode(cameraController.GetStateForMode(mode switch
			{
				PreviewMode.Editor => CameraMode.Editor,
				PreviewMode.Cinema => CameraMode.Cinema,
				PreviewMode.Player => CameraMode.Preset,
				_ => CameraMode.Absent
			}).mode);

			UpdateGestureControllerState();
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
			GestureControllerEnabled = false;
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

			// In LoadMap method, replace the state registration part
			var editorState = new CameraState
			{
				mode = CameraMode.Editor,
				data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos }
			};
			var playerState = new CameraState
			{
				mode = CameraMode.Follow,
				data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero,
				origin = () => srcPos
			};
			var cinemaState = new CameraState
			{
				mode = CameraMode.Cinema,
				data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero
			};

			cameraController.RegisterState(editorState, new[] { CameraMode.Editor });
			cameraController.RegisterState(playerState, new[] { CameraMode.Follow, CameraMode.Preset });
			cameraController.RegisterState(cinemaState, new[] { CameraMode.Cinema });

			Func <IReadOnlyList<Vector3>> focusFunc = () =>
			{
				var waypoints = mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList();
				spatialSystem.SetPoints(waypoints);

				focusFunc = () =>
				{
					if (null != eggbotController && null != eggbotController.transform)
						spatialSystem.TryAddPoint(eggbotController.transform.position);
					return spatialSystem.Points;
				};

				return spatialSystem.Points;
			};

			cinemaState.points = () => focusFunc();

			var ppController = cameraController.GetComponentInChildren<PostProcessingCameraController>(true);
			if (null != eggbotController && null != ppController)
				ppController.dofTarget = eggbotController.transform;

			var initialMode = PreviewSettings.CurrentMode switch
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
				cameraController.SetCameraMode(CameraMode.Follow, true);
				return;
			}

			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			var target = waypoint.vDst != null && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			var playerState = cameraController.GetStateForMode(CameraMode.Preset);// Update playerState indirectly via CameraController
			playerState.origin = () => origin;
			playerState.target = () => target;
			playerState.mode = CameraMode.Preset;

			GestureControllerEnabled = true;
			cameraController.SetCameraMode(CameraMode.Preset, true);
		}

		public void Scramble() => mapManager.Scramble();

		public void Solve()
		{
			mapManager.Solve();
			GestureControllerEnabled = false;
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (null == eggbotController) return;

			var playerState = cameraController.GetStateForMode(CameraMode.Follow);// Update playerState indirectly via CameraController
			playerState.target = () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero;
			playerState.mode = CameraMode.Follow;

			cameraController.SetCameraMode(CameraMode.Follow, true);
		}

		private void OnLevelCompleted() { }

		private void CheckDisableDrag(IMapManager imap)
		{
			if (null != eggbotController && eggbotController.NavDirection(imap) != 0)
				GestureControllerEnabled = false;
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