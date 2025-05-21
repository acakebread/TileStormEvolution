using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class CinemaController
	{
		// Environment constants
		public const float TargetFPS = 60f;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		// Shared state - world data
		public static Transform playerTransform;
		public static List<Vector3> focusPoints = new();
		private static Bounds mapBounds;
		private static CinemaCameraBase cameraSystem;

		static CinemaController() => Reset();

		public static void Reset() // Called when a new map is loaded
		{
			cameraSystem = null;
			playerTransform = null;
			focusPoints.Clear();
			mapBounds = new Bounds(Vector3.zero, Vector3.zero); // Initialize empty bounds
			SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint); // Initialize bucket system
		}

		public static void SetFocusPoints(List<Vector3> points)
		{
			focusPoints = points ?? new List<Vector3>();
			SpatialBucketSystem.Clear(); // Clear buckets when focus points change
			if (focusPoints.Count <= 0) return;
			mapBounds = new Bounds(focusPoints[0], Vector3.zero); // Initialize bounds
			foreach (var point in focusPoints)
			{
				mapBounds.Encapsulate(point); // Expand bounds
				if (SpatialBucketSystem.CanAddPoint(point))
					SpatialBucketSystem.AddPoint(point);
			}
		}

		public static void UpdatePlayerTransform(Transform transform)
		{
			playerTransform = transform;
			if (playerTransform == null || !SpatialBucketSystem.CanAddPoint(playerTransform.position)) return;

			var pos = playerTransform.position;
			mapBounds.Encapsulate(pos); // Expand bounds
			focusPoints.Add(pos);
			SpatialBucketSystem.AddPoint(pos); // Add to bucket system
			if (focusPoints.Count <= MaxFocusPoints) return;
			SpatialBucketSystem.RemovePoint(focusPoints[0]); // Remove oldest from bucket system
			focusPoints.RemoveAt(0);
		}

		public static void CreateCinemaSequence()
		{
			switch (Random.Range(0, 7))
			{
				case 0: case 1: case 2: cameraSystem = new CinemaCameraPath(); break;
				case 3: case 4: case 5: cameraSystem = new CinemaCameraOrbit(); break;
				case 6: cameraSystem = new CinemaCameraDollyZoom(); break;
			}
			//cameraSystem = new CinemaCameraDollyZoom();
		}

		public static void StartCinemaSequence() { if (cameraSystem != null) cameraSystem.StartSequence(); }

		public static bool UpdateCinemaMode() => cameraSystem != null && cameraSystem.Update();

		public static CameraData GetCameraData() => cameraSystem.cameraData;

		public static Bounds GetMapBounds() => mapBounds;
	}
}





//using UnityEngine;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public static class CameraController
//	{
//		public enum CameraState
//		{
//			Static,
//			Preset,
//			Follow,
//			Cinema
//		}

//		// Public properties
//		public static bool CinemaEnabled => enableAutoCinema;
//		public static bool CinemaActive => CameraState.Cinema == currentState;

//		// Common behavior constants
//		private const float TargetFPS = 60f;
//		private const float DefaultSmoothingRate = 64f;
//		private const float CinemaTimeoutDuration = 5f;

//		// State-specific constants
//		private static class FollowConfig
//		{
//			public const float SmoothingNa = 8f;
//			public const float SmoothingNb = 64f;
//			public const float IdealDistance = 14f;
//			public const float IdealDistanceHorizontalScale = 1.4f;
//		}

//		private static class PresetConfig
//		{
//			public const float SmoothingN = 32f;
//		}

//		// Shared state - camera data//made public for now
//		public static CameraData cameraData;

//		// Internal state
//		private static CameraState currentState = CameraState.Static;
//		private static CameraState previousState = CameraState.Static;
//		private static CameraData backupData;
//		private static Camera mainCamera = Camera.main;
//		private static Transform playerTransform;
//		private static bool enableAutoCinema;
//		private static float lastRefreshTime;
//		private static List<Vector3> focusPoints = new();

//		public static void Reset()
//		{
//			CinemaController.Reset();
//			playerTransform = null;
//			focusPoints.Clear();
//			lastRefreshTime = Time.time;
//		}

//		// Initialization
//		public static void Initialize()
//		{
//			if (mainCamera == null) return;
//			cameraData = new CameraData
//			{
//				smoothing = DefaultSmoothingRate,
//				originSrc = Vector3.zero,
//				targetSrc = Vector3.zero,
//				originDst = Vector3.zero,
//				targetDst = Vector3.zero,
//				fieldOfView = mainCamera.fieldOfView,
//				shake = 0f
//			};
//		}

//		// Cinema controls
//		public static void SetAutoCinema(bool allow = true) => enableAutoCinema = allow;

//		// State management
//		public static void SetMode(CameraState value)
//		{
//			if (value == CameraState.Cinema && currentState != CameraState.Cinema)
//			{
//				previousState = currentState;
//				backupData = cameraData;

//				CinemaController.Reset();
//				CinemaController.UpdatePlayerTransform(playerTransform);
//				CinemaController.SetFocusPoints(focusPoints);
//				CinemaController.CreateCinemaSequence();
//				CinemaController.StartCinemaSequence();
//				cameraData = CinemaController.GetCameraData();
//			}
//			else if (value != CameraState.Cinema && currentState == CameraState.Cinema)
//			{
//				previousState = currentState;
//				cameraData = backupData;
//				mainCamera.fieldOfView = cameraData.fieldOfView;
//			}
//			currentState = value;
//		}

//		public static void Refresh(float time)
//		{
//			lastRefreshTime = time;
//			if (currentState == CameraState.Cinema) SetMode(previousState);
//		}

//		// Position setters
//		public static void SetOrigin(Vector3 value)
//		{
//			cameraData.originDst = value;
//			if (currentState == CameraState.Static) cameraData.originSrc = value;
//		}

//		public static void SetTarget(Vector3 value)
//		{
//			cameraData.targetDst = value;
//			if (currentState == CameraState.Static) cameraData.targetSrc = value;
//		}

//		public static void SetPlayer(Transform transform)
//		{
//			playerTransform = transform;
//			if (currentState == CameraState.Follow) cameraData.targetDst = transform.position;
//			if (currentState == CameraState.Cinema) CinemaController.UpdatePlayerTransform(transform);
//		}

//		public static void SetFocusPoints(List<Vector3> points)
//		{
//			focusPoints = points;
//			if (currentState == CameraState.Cinema) CinemaController.SetFocusPoints(focusPoints);
//		}

//		// Update logic
//		public static void Update()
//		{
//			if (mainCamera == null) return;

//			if (enableAutoCinema && currentState != CameraState.Cinema && Time.time - lastRefreshTime > CinemaTimeoutDuration)
//			{
//				Debug.Log("Auto-switching to Cinema mode");
//				SetMode(CameraState.Cinema);
//			}

//			switch (currentState)
//			{
//				case CameraState.Static:
//					break;

//				case CameraState.Preset:
//					UpdatePresetMode();
//					break;

//				case CameraState.Follow:
//					UpdateFollowMode();
//					break;

//				case CameraState.Cinema:
//					if (false == CinemaController.UpdateCinemaMode())
//					{
//						CinemaController.CreateCinemaSequence();
//						CinemaController.StartCinemaSequence();
//					}
//					cameraData = CinemaController.GetCameraData();
//					break;
//			}

//			UpdateCamera();
//			CameraUtils.ApplyCameraShake(mainCamera, cameraData.shake);// Apply camera shake
//		}

//		private static void UpdatePresetMode()
//		{
//			cameraData.smoothing = SmoothingUtils.Smooth(cameraData.smoothing, PresetConfig.SmoothingN, Time.deltaTime, TargetFPS);
//			var presetLerp = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, TargetFPS);
//			cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, cameraData.originDst, presetLerp);
//			cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, cameraData.targetDst, presetLerp);
//		}

//		private static void UpdateFollowMode()
//		{
//			cameraData.smoothing = SmoothingUtils.Smooth(cameraData.smoothing, FollowConfig.SmoothingNa, FollowConfig.SmoothingNb, Time.deltaTime, TargetFPS);
//			var followLerp = SmoothingUtils.Smooth(0f, 1f, cameraData.smoothing, Time.deltaTime, TargetFPS);
//			cameraData.targetSrc = Vector3.Lerp(cameraData.targetSrc, cameraData.targetDst, followLerp);
//			var delta = cameraData.targetSrc - cameraData.originSrc;
//			var deltaHorizontal = (0f == delta.x && 0f == delta.z) ? mainCamera.transform.forward : new Vector3(delta.x, 0, delta.z);
//			deltaHorizontal.Normalize();
//			var idealPos = cameraData.targetSrc - deltaHorizontal * (FollowConfig.IdealDistance * FollowConfig.IdealDistanceHorizontalScale);
//			idealPos.y = cameraData.targetSrc.y + FollowConfig.IdealDistance;
//			cameraData.originSrc = Vector3.Lerp(cameraData.originSrc, idealPos, followLerp);
//		}

//		public static void UpdateCamera()
//		{
//			mainCamera.transform.position = cameraData.originSrc;
//			var direction = cameraData.targetSrc - cameraData.originSrc;
//			if (direction.sqrMagnitude > Mathf.Epsilon) mainCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
//			mainCamera.fieldOfView = cameraData.fieldOfView;
//		}
//	}
//}