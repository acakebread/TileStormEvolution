using UnityEngine;
using MassiveHadronLtd;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class GameCameraController : CameraController
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private SpatialBucketSystem spatialSystem;
		private GestureController gestureController;
		private PostProcessingCameraController postProcessingController;

		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;
		private const float CinemaTimeoutDuration = 5f;
		private float cinemaTimer = 0f; // Initialize to 0 instead of Time.time

		private bool gestureControllerEnabled = true;
		public event Action<bool> OnWaypointReachedForGestures;

		private CameraBase cameraSystem => currentSystem;
		private bool HasCompleted => cameraSystem is GameCameraOrbit orbit ? orbit.HasCompleted : cameraSystem is GameCameraPath path && path.HasCompleted;
		private bool IsCinemaCamera(CameraBase _cameraSystem) => _cameraSystem is GameCameraOrbit || _cameraSystem is GameCameraPath;

		public override Camera camera
		{
			get { return base.camera; }
			set
			{
				base.camera = value;
				postProcessingController = InitialisePostProcessingController();
			}
		}

		private PostProcessingCameraController InitialisePostProcessingController()
		{
			if (null == camera)
			{
				Debug.LogWarning("Cannot create PostProcessingCameraController: Camera is null");
				return null;
			}

			var ppController = camera.GetComponentInChildren<PostProcessingCameraController>(true);
			if (ppController == null)
			{
				var ppObject = new GameObject("PostProcessing");
				ppObject.transform.SetParent(camera.transform, false);
				ppController = ppObject.AddComponent<PostProcessingCameraController>();
				Debug.Log("Created PostProcessingCameraController on camera");
			}
			return ppController;
		}

		private void Awake()
		{
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

		private void OnWaypointGesturesEnable(bool value) => GestureControllerEnabled = value;

		private void UpdateGestureControllerState()
		{
			gestureController.enabled = gestureControllerEnabled && PreviewMode.Player == PreviewSettings.CurrentMode;
		}

		public void Initialise(MapManager map, EggbotController eggbot, string initialMode = null)
		{
			initialMode = initialMode ?? CameraModeRegistry.Follow;
			mapManager = map ?? throw new ArgumentNullException(nameof(map));
			gestureController.Initialise(mapManager);
			eggbotController = eggbot;
			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);
			if (eggbotController != null)
			{
				eggbotController.OnWaypointReached += HandleWaypointReached;
				eggbotController.OnPuzzleSolved += HandlePuzzleSolved;
			}
			base.Initialise(initialMode);
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

			if (mapManager.Waypoints?.Length > 0 && mapManager.Waypoints[0].bCamera)
			{
				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.vSrc != null) srcPos = firstWaypoint.vSrc.ToVector3();
				if (firstWaypoint.vDst != null) dstPos = firstWaypoint.vDst.ToVector3();
			}

			return (srcPos, dstPos);
		}

		protected Func<Vector3> GetTargetPosition()
		{
			return () => eggbotController != null && eggbotController.transform != null
				? eggbotController.transform.position
				: Vector3.zero;
		}

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
			if (camera == null)
			{
				Debug.LogWarning("Cannot setup camera configs: Camera is null");
				return;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();

			RegisterCamera(new GameCameraEditor(camera) { mapManager = this.mapManager }, CameraModeRegistry.Editor);
			RegisterCamera(new GameCameraDirect(camera) { iorigin = srcPos, itarget = dstPos }, CameraModeRegistry.Direct);
			RegisterCamera(new GameCameraFollow(camera) { iorigin = srcPos, itarget = dstPos, targetFn = GetTargetPosition() }, CameraModeRegistry.Follow);
			RegisterCamera(new GameCameraPreset(camera) { iorigin = srcPos, itarget = dstPos, originFn = () => srcPos, targetFn = GetTargetPosition() }, CameraModeRegistry.Preset);
			RegisterCamera(new GameCameraOrbit(camera) { iorigin = srcPos, itarget = dstPos, targetFn = GetTargetPosition() }, CameraModeRegistry.Orbit);
			RegisterCamera(new GameCameraPath(camera) { iorigin = srcPos, itarget = dstPos, pointsFn = GetFocusPoints(), targetFn = GetTargetPosition() }, CameraModeRegistry.Path);

			RegisterGroup("EDITOR", new[] { CameraModeRegistry.Editor });
			RegisterGroup("DIRECT", new[] { CameraModeRegistry.Direct });
			RegisterGroup("PLAYER", new[] { CameraModeRegistry.Follow, CameraModeRegistry.Preset });
			RegisterGroup("CINEMA", new[] { CameraModeRegistry.Path, CameraModeRegistry.Orbit });
		}

		private void HandleWaypointReached(int waypointIndex)
		{
			if (eggbotController == null || mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (waypointIndex == 0 || waypointIndex == mapManager.Waypoints.Length - 1) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
			{
				SetCameraMode(CameraModeRegistry.Follow, true);
				return;
			}

			((GameCameraPreset)CameraSystems[CameraModeRegistry.Preset]).originFn = () => waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			((GameCameraPreset)CameraSystems[CameraModeRegistry.Preset]).targetFn = () => waypoint.vDst != null && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			SetCameraMode(CameraModeRegistry.Preset, true);
			OnWaypointReachedForGestures?.Invoke(true);
		}

		private void HandlePuzzleSolved(int waypointIndex)
		{
			if (eggbotController == null) return;
			SetCameraMode(CameraModeRegistry.Follow, true);
		}

		protected void UpdateCinemaMode()
		{
			if (!IsCinemaCamera(cameraSystem) || !HasCompleted || Time.time < cinemaTimer + CinemaTimeoutDuration) return;
			cinemaTimer = Time.time;
			SetCameraMode(UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => CameraModeRegistry.Orbit, _ => CameraModeRegistry.Path });
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
			eggbotController.OnWaypointReached -= HandleWaypointReached;
			eggbotController.OnPuzzleSolved -= HandlePuzzleSolved;
		}

		public void ResetCinemaTimer(bool forceCinema = false)
		{
			cinemaTimer = Time.time - (forceCinema ? CinemaTimeoutDuration : 0);
		}
	}
}