using UnityEngine;
using MassiveHadronLtd;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class MainCameraController : CameraController
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private SpatialBucketSystem spatialSystem;
		private GestureController gestureController;
		private PostProcessingCameraController postProcessingController;

		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;
		private const float CinemaTimeoutDuration = 5f;
		private float cinemaTimer = 0f;
		public void ResetCinemaTimer(bool forceCinema = false) => cinemaTimer = Time.time - (forceCinema ? CinemaTimeoutDuration : 0);

		private bool gestureControllerEnabled = true;
		public event Action<bool> OnWaypointReachedForGestures;

		private CameraBase cameraSystem => activeSystem;

		public string PreviewModeToCameraMode(PreviewMode mode) => mode switch
		{
			PreviewMode.Editor => CameraModeRegistry.Editor,
			PreviewMode.Player => CameraModeRegistry.Follow,
			PreviewMode.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => CameraModeRegistry.Orbit, _ => CameraModeRegistry.Path },
			_ => CameraModeRegistry.Absent
		};

		protected override void Awake()
		{
			base.Awake();
			gestureController = GetComponent<GestureController>();
			OnWaypointReachedForGestures += OnWaypointGesturesEnable;
			gestureController.OnMapUpdated += CheckDisableDrag;
		}

		private void CheckDisableDrag(IMapManager imap)
		{
			if (eggbotController != null && eggbotController.NavDirection(imap) != 0)
				GestureControllerEnabled = false;
		}

		public void OnMapSolved() => GestureControllerEnabled = false;

		private bool GestureControllerEnabled { set { gestureControllerEnabled = value; UpdateGestureControllerState(); } }

		public void UpdateGestureControllerState() => gestureController.enabled = gestureControllerEnabled && PreviewMode.Player == PreviewSettings.CurrentMode;

		private void OnWaypointGesturesEnable(bool value) => GestureControllerEnabled = value;

		public void Initialise(MapManager map, EggbotController eggbot)
		{
			mapManager = map ?? throw new ArgumentNullException(nameof(map));
			gestureController.Initialise(Camera.main, mapManager);
			eggbotController = eggbot;
			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);
			if (eggbotController != null)
			{
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
				eggbotController.OnPuzzleSolved += HandlePuzzleSolved;
			}
			var initialMode = PreviewModeToCameraMode(PreviewSettings.CurrentMode);
			Initialise(initialMode ?? CameraModeRegistry.Preset);
			GestureControllerEnabled = false;
			UpdateGestureControllerState();

			if (postProcessingController != null && eggbotController != null)
				postProcessingController.dofTarget = eggbotController.transform;
		}

		protected override (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions()
		{
			if (mapManager == null)
				return (new Vector3(0f, 14f, -14f), Vector3.zero);

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);

			if (mapManager.Waypoints?.Length > 0 && mapManager.Waypoints[0].IsCamera())
			{
				var firstWaypoint = mapManager.Waypoints[0];
				var tilePos = mapManager.TileWorldPositionNoOrigin(firstWaypoint.nTile);

				srcPos = firstWaypoint.GetVSrc() + tilePos;
				dstPos = firstWaypoint.GetVDst() + tilePos;
			}

			return (srcPos, dstPos);
		}

		protected Func<Vector3> GetTargetPosition() => () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero;

		protected Func<IReadOnlyList<Vector3>> GetFocusPoints()
		{
			if (mapManager == null) return () => Array.Empty<Vector3>();

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

			return focusFunc;
		}

		protected override void SetupCameras()
		{
			base.SetupCameras();
			var camera = Camera.main;

			if (camera == null)
			{
				Debug.LogWarning("Cannot setup camera configs: Camera is null");
				return;
			}

			postProcessingController = InitialisePostProcessingController(camera);

			static PostProcessingCameraController InitialisePostProcessingController(Camera camera)
			{
				var ppController = camera.GetComponentInChildren<PostProcessingCameraController>(true);
				if (null == ppController)
				{
					var ppObject = new GameObject("PostProcessing");
					ppObject.transform.SetParent(camera.transform, false);
					ppController = ppObject.AddComponent<PostProcessingCameraController>();
					Debug.Log("Created PostProcessingCameraController on camera");
				}
				return ppController;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();

			RegisterCamera(new GameCameraEditor(camera) { iorigin = srcPos, itarget = dstPos }, CameraModeRegistry.Editor);
			RegisterCamera(new GameCameraFollow(camera) { iorigin = srcPos, itarget = dstPos, targetFn = GetTargetPosition() }, CameraModeRegistry.Follow);
			RegisterCamera(new GameCameraPreset(camera) { iorigin = srcPos, itarget = dstPos, originFn = () => srcPos, targetFn = GetTargetPosition() }, CameraModeRegistry.Preset);
			RegisterCamera(new GameCameraOrbit(camera) { iorigin = srcPos, itarget = dstPos, targetFn = GetTargetPosition() }, CameraModeRegistry.Orbit);
			RegisterCamera(new GameCameraPath(camera) { iorigin = srcPos, itarget = dstPos, pointsFn = GetFocusPoints(), targetFn = GetTargetPosition() }, CameraModeRegistry.Path);

			GameModes.RegisterAllModes(RegisterMode);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (eggbotController == null || mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length)
				return;

			if (waypointIndex == 0 || waypointIndex == mapManager.Waypoints.Length - 1)
				return;

			var waypoint = mapManager.Waypoints[waypointIndex];

			if (!waypoint.IsCamera())
			{
				SetCameraSystem(CameraModeRegistry.Follow, true);
				return;
			}

			var presetCam = (GameCameraPreset)CameraSystems[CameraModeRegistry.Preset];
			var tilePos = mapManager.TileWorldPositionNoOrigin(waypoint.nTile);
			presetCam.originFn = () => waypoint.GetVSrc() + tilePos;
			presetCam.targetFn = () => waypoint.GetVDst() + tilePos;

			SetCameraSystem(CameraModeRegistry.Preset, true);
			OnWaypointReachedForGestures?.Invoke(true);
		}

		private void HandlePuzzleSolved(int waypointIndex)
		{
			if (null == eggbotController) return;
			SetCameraSystem(CameraModeRegistry.Follow, true);
		}

		private void OnLevelCompleted() { }

		private void UpdateCinemaMode()
		{
			if (!IsCinemaCamera() || !HasCompleted() || Time.time < cinemaTimer + CinemaTimeoutDuration) return;
			cinemaTimer = Time.time;
			SetCameraSystem(UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => CameraModeRegistry.Orbit, _ => CameraModeRegistry.Path });

			bool HasCompleted() => cameraSystem is GameCameraOrbit orbit ? orbit.HasCompleted : cameraSystem is GameCameraPath path && path.HasCompleted;
			bool IsCinemaCamera() => cameraSystem is GameCameraOrbit || cameraSystem is GameCameraPath;
		}

		protected override void Update()
		{
			base.Update();
			UpdateCinemaMode();
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			OnWaypointReachedForGestures -= OnWaypointGesturesEnable;
			gestureController.OnMapUpdated -= CheckDisableDrag;
			if (eggbotController == null) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= HandlePuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
		}
	}
}