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
		private IMapPlay iMap;
		private EggbotController eggbotController;
		private SpatialBucketSystem spatialSystem;
		private GestureController gestureController;
		private PostProcessingCameraController postProcessingController;

		private int postProcessingLevel = 1; 
		public int PostProcessingLevel { get => postProcessingLevel; set => postProcessingLevel= value; }

		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;
		private const float CinemaTimeoutDuration = 5f;
		private float cinemaTimer = 0f;
		public void ResetCinemaTimer(bool forceCinema = false) => cinemaTimer = Time.time - (forceCinema ? CinemaTimeoutDuration : 0);

		private bool gestureControllerEnabled = true;
		public event Action<bool> OnWaypointReachedForGestures;

		private CameraBase cameraSystem => activeSystem;

		public void SelectCameraSystem(string system, bool background = false)
		{
			SetCameraSystem(system, background);
			UpdateGestureControllerState();
		}

		public string PreviewModeToCameraMode(ApplicationMode mode) => mode switch
		{
			ApplicationMode.Editor => CameraModeRegistry.Editor,
			ApplicationMode.Player => CameraModeRegistry.Follow,
			ApplicationMode.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => CameraModeRegistry.Orbit, _ => CameraModeRegistry.Path },
			_ => CameraModeRegistry.Absent
		};

		protected override void Awake()
		{
			base.Awake();
			gestureController = GetComponent<GestureController>();
			OnWaypointReachedForGestures += OnWaypointGesturesEnable;
			gestureController.OnMapUpdated += CheckDisableDrag;
			PostProcessingLevel = 1;
		}

		private void CheckDisableDrag(IMapPlay imap)
		{
			if (eggbotController != null && eggbotController.NavDirection(imap) != 0)
				GestureControllerEnabled = false;
		}

		public void OnMapSolved() => GestureControllerEnabled = false;

		private bool GestureControllerEnabled { set { gestureControllerEnabled = value; UpdateGestureControllerState(); } }

		public void UpdateGestureControllerState() => gestureController.enabled = gestureControllerEnabled && ApplicationMode.Player == ApplicationSettings.CurrentMode;

		private void OnWaypointGesturesEnable(bool value) => GestureControllerEnabled = value;

		private Action _unsubscribeMapAction;

		public void Initialise(IMapEdit map, EggbotController eggbot)
		{
			iMap = map ?? throw new ArgumentNullException(nameof(map));
			gestureController.Initialise(Camera.main, iMap);
			eggbotController = eggbot;
			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);
			if (eggbotController != null)
			{
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
				eggbotController.OnPuzzleSolved += HandlePuzzleSolved;
			}
			var initialMode = PreviewModeToCameraMode(ApplicationSettings.CurrentMode);
			Initialise(initialMode ?? CameraModeRegistry.Preset);
			GestureControllerEnabled = false;
			UpdateGestureControllerState();

			if (postProcessingController != null && eggbotController != null)
				postProcessingController.dofTarget = eggbotController.transform;

			map.OnMapEdited += OnMapEdited;
			_unsubscribeMapAction = () => map.OnMapEdited -= OnMapEdited;
		}

		public void Reset()
		{
			_unsubscribeMapAction?.Invoke();
			_unsubscribeMapAction = null;
		}

		private void OnMapEdited(IMapEdit map, bool resized, Vector3 delta)
		{
			if (resized) AdjustAllCamerasForMapShift(delta);
		}

		protected override (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions()
		{
			if (iMap == null)
				return (new Vector3(0f, 14f, -14f), Vector3.zero);

			var dstPos = new Vector3(iMap.Width * 0.5f, 0f, iMap.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);

			var waypoints = iMap.GetWaypoints();
			if (waypoints.Length > 0)
			{
				var tile = waypoints[0].tile;
				// Use extension method — returns the View or null
				var view = iMap.GetAttachmentOfType<View>(tile);

				if (view != null)
				{
					var tilePos = iMap.TileRenderPosition(tile);
					srcPos = view.VSrc + tilePos;
					dstPos = view.VDst + tilePos;
				}
			}

			return (srcPos, dstPos);
		}

		protected Func<Vector3> GetTargetPosition() => () => eggbotController != null && eggbotController.transform != null ? eggbotController.transform.position : Vector3.zero;

		protected Func<IReadOnlyList<Vector3>> GetFocusPoints()
		{
			if (iMap == null) return () => Array.Empty<Vector3>();

			Func<IReadOnlyList<Vector3>> focusFunc = () =>
			{
				var waypoints = iMap.GetWaypoints().Select(w => iMap.TileRenderPosition(w.tile)).ToList();
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

			var postProcessingCamera = postProcessingController.GetComponent<Camera>();
			if (null != postProcessingCamera)//ensure clip planes are synchronised - important for depth of field
			{
				postProcessingCamera.nearClipPlane = camera.nearClipPlane;
				postProcessingCamera.farClipPlane = camera.farClipPlane;
			}

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

			RegisterCamera(new GameCameraEditor(camera) { iorigin = srcPos, itarget = dstPos, PostProcessingEnabled = PostProcessingLevel >= 2 }, CameraModeRegistry.Editor);
			RegisterCamera(new GameCameraFollow(camera) { iorigin = srcPos, itarget = GetTargetPosition().Invoke(), targetFn = GetTargetPosition(), PostProcessingEnabled = PostProcessingLevel >= 1 }, CameraModeRegistry.Follow);
			RegisterCamera(new GameCameraPreset(camera) { iorigin = srcPos, itarget = dstPos, originFn = () => srcPos, targetFn = GetTargetPosition(), PostProcessingEnabled = PostProcessingLevel >= 1 }, CameraModeRegistry.Preset);
			RegisterCamera(new GameCameraOrbit(camera) { iorigin = srcPos, itarget = dstPos, targetFn = GetTargetPosition(), PostProcessingEnabled = PostProcessingLevel >= 1 }, CameraModeRegistry.Orbit);
			RegisterCamera(new GameCameraPath(camera) { iorigin = srcPos, itarget = dstPos, pointsFn = GetFocusPoints(), targetFn = GetTargetPosition(), PostProcessingEnabled = PostProcessingLevel >= 1 }, CameraModeRegistry.Path);

			GameModes.RegisterAllModes(RegisterMode);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			var waypoints = iMap?.GetWaypoints();

			if (eggbotController == null || iMap == null ||
				waypointIndex < 0 || waypointIndex >= waypoints.Length)
				return;

			if (waypointIndex == 0 || waypointIndex == waypoints.Length - 1)
				return;

			var tile = iMap.GetWaypoint(waypointIndex).tile;

			// Use extension method
			var view = iMap.GetAttachmentOfType<View>(tile);

			if (view == null)
			{
				SetCameraSystem(CameraModeRegistry.Follow, true);
				return;
			}

			// Has camera settings (View)
			var presetCam = (GameCameraPreset)CameraSystems[CameraModeRegistry.Preset];

			presetCam.originFn = () => view.VSrc + iMap.TileRenderPosition(iMap.GetWaypoint(waypointIndex).tile);
			presetCam.targetFn = () => view.VDst + iMap.TileRenderPosition(iMap.GetWaypoint(waypointIndex).tile);

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
			UpdateEditorPostProcessing();
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

		//utils
		public void EnableEditorPostProcessing()
		{
			var gameCameraEditor = activeSystem is GameCameraEditor editor ? editor : null;
			if (gameCameraEditor != null)
			{
				var enabled = postProcessingLevel > 1;
				var volume = getVolume(gameCameraEditor.controller.gameObject);
				volume.enabled = enabled;
				VolumeUtils.EnableDepthOfField(volume, enabled);
				VolumeUtils.SetDepthOfFieldDistance(volume, 8f);
			}
			static UnityEngine.Rendering.Volume getVolume(GameObject root) => root.GetComponentInChildren<UnityEngine.Rendering.Volume>(true);
		}

		public void UpdateEditorPostProcessing()
		{
			var gameCameraEditor = activeSystem is GameCameraEditor editor ? editor : null;
			if (gameCameraEditor != null)
			{
				var volume = getVolume(gameCameraEditor.controller.gameObject);
				var distance = (gameCameraEditor.controller.transform.position - Map.CameraToWorld(gameCameraEditor.camera)).magnitude;
				VolumeUtils.SetDepthOfFieldDistance(volume, Mathf.Max(Mathf.Min(distance, gameCameraEditor.controller.transform.position.y * 3f), 1f));
			}
			static  UnityEngine.Rendering.Volume getVolume(GameObject root) => root.GetComponentInChildren<UnityEngine.Rendering.Volume>(true);
		}
	}
}