using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class CameraController
	{
		// Shared state
		public enum CameraState
		{
			Absent,
			Static,
			Preset,
			Follow,
			Cinema
		}

		// Public properties
		public static bool CinemaEnabled => enableAutoCinema;
		public static bool CinemaActive => CameraState.Cinema == currentState;
		public static List<Vector3> focusPoints = new();
		public static Transform playerTransform;

		// Constants
		private const float CinemaTimeoutDuration = 5f;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		// Internal
		private static CameraData restoreData;
		private static Camera mainCamera = Camera.main;
		private static CameraState currentState = CameraState.Absent;
		private static CameraState previousState = CameraState.Absent;
		private static bool enableAutoCinema;
		private static float lastRefreshTime;

		private static Bounds mapBounds;
		private static CameraBase cameraSystem;

		// Reset all systems
		public static void Reset()//called on load new map
		{
			if (null == cameraSystem)//workaround for absence of initialise pattern
			{
				restoreData = new CameraData
				{
					camera = mainCamera,
					smoothing = CameraData.DefaultSmoothingRate,
					originSrc = Vector3.zero,
					targetSrc = Vector3.zero,
					originDst = Vector3.zero,
					targetDst = Vector3.zero,
					fieldOfView = mainCamera.fieldOfView,
					shake = 0f
				};
			}

			lastRefreshTime = Time.time;
			playerTransform = null;
			focusPoints.Clear();
			mapBounds = new Bounds(Vector3.zero, Vector3.zero);
			SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint);
			SetMode(CameraState.Static);
		}

		public static void SetAutoCinema(bool allow = true) => enableAutoCinema = allow;

		public static void SetMode(CameraState value)
		{
			if (CameraState.Cinema != currentState && null != cameraSystem) 
				restoreData = cameraSystem.cameraData;

			switch (value)
			{
				case CameraState.Static:
					cameraSystem = new CameraStatic();
					break;

				case CameraState.Preset:
					cameraSystem = new CameraPreset();
					break;

				case CameraState.Follow:
					cameraSystem = new CameraFollow();
					break;

				case CameraState.Cinema:
					switch (Random.Range(0, 7))
					{
						case 0: case 1: case 2: cameraSystem = new CinemaCameraPath(); break;
						case 3: case 4: case 5: cameraSystem = new CinemaCameraOrbit(); break;
						case 6: cameraSystem = new CinemaCameraDollyZoom(); break;
					}
					break;
			}

			cameraSystem.cameraData = restoreData;
			cameraSystem.Start();

			if (value != currentState) previousState = currentState;
			currentState = value;
		}

		public static void Update()
		{
			if (mainCamera == null) return;

			if (enableAutoCinema && currentState != CameraState.Cinema && Time.time - lastRefreshTime > CinemaTimeoutDuration)
			{
				Debug.Log("Auto-switching to Cinema mode");
				SetMode(CameraState.Cinema);
			}

			switch (currentState)
			{
				case CameraState.Static:
					break;

				case CameraState.Preset:
					cameraSystem.Update();
					break;

				case CameraState.Follow:
					cameraSystem.Update();
					break;

				case CameraState.Cinema:
					if (cameraSystem == null || !cameraSystem.Update())
						SetMode(CameraState.Cinema);
					break;
			}

			var cameraData = cameraSystem.cameraData;
			mainCamera.transform.position = cameraData.originSrc;
			var direction = cameraData.targetSrc - cameraData.originSrc;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				mainCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			mainCamera.fieldOfView = cameraData.fieldOfView;
			CameraUtils.ApplyCameraShake(mainCamera, cameraData.shake);
		}

		public static void Refresh(float time)
		{
			lastRefreshTime = time;
			if (currentState == CameraState.Cinema)
				SetMode(previousState);
		}

		public static void SetOrigin(Vector3 value)
		{
			cameraSystem.cameraData.originDst = value;
			if (currentState == CameraState.Static)
				cameraSystem.cameraData.originSrc = value;
		}

		public static void SetTarget(Vector3 value)
		{
			cameraSystem.cameraData.targetDst = value;
			if (currentState == CameraState.Static)
				cameraSystem.cameraData.targetSrc = value;
		}

		public static void SetPlayer(Transform transform)
		{
			playerTransform = transform;
			if (currentState == CameraState.Follow)
				cameraSystem.cameraData.targetDst = transform.position;
			if (currentState == CameraState.Cinema)
				UpdatePlayerTransform(transform);

			static void UpdatePlayerTransform(Transform transform)
			{
				playerTransform = transform;
				if (null == playerTransform || !SpatialBucketSystem.CanAddPoint(playerTransform.position))
					return;

				var pos = playerTransform.position;
				mapBounds.Encapsulate(pos);
				focusPoints.Add(pos);
				SpatialBucketSystem.AddPoint(pos);

				if (focusPoints.Count > MaxFocusPoints)
				{
					SpatialBucketSystem.RemovePoint(focusPoints[0]);
					focusPoints.RemoveAt(0);
				}
			}
		}

		public static void SetFocusPoints(List<Vector3> points)
		{
			focusPoints = points ?? new List<Vector3>();
			SpatialBucketSystem.Clear();
			if (focusPoints.Count <= 0) return;
			mapBounds = new Bounds(focusPoints[0], Vector3.zero);
			foreach (var point in focusPoints)
			{
				mapBounds.Encapsulate(point);
				if (SpatialBucketSystem.CanAddPoint(point))
					SpatialBucketSystem.AddPoint(point);
			}
		}
	}
}
