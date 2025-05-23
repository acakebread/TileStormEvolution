using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class CameraController
	{
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
		public static bool CinemaActive => currentState == CameraState.Cinema;

		// Constants
		private const float CinemaTimeoutDuration = 5f;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		// Internal
		private static CameraData restoreData;
		private static CameraState currentState = CameraState.Absent;
		private static CameraState previousState = CameraState.Absent;
		private static bool enableAutoCinema;
		private static float lastRefreshTime;
		private static Bounds mapBounds;
		private static CameraBase cameraSystem;
		private static Transform playerTransform;
		private static List<Vector3> focusPoints = new();

		public static void Reset(Camera camera = null)
		{
			// Initialize only once
			if (null == cameraSystem)
			{
				restoreData = new CameraData
				{
					smoothing = CameraData.DefaultSmoothingRate,
					fieldOfView = (null != camera ? camera : Camera.main).fieldOfView
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

			// Use dictionary or array for camera system instantiation if more types are added
			cameraSystem = value switch
			{
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				CameraState.Cinema => CreateCinemaCamera(),
				_ => cameraSystem // Fallback to current system
			};

			cameraSystem.cameraData = restoreData;
			cameraSystem.playerTransform = playerTransform;
			cameraSystem.focusPoints = focusPoints;
			cameraSystem.Start();

			if (value != currentState) previousState = currentState;//for exiting cinema camera system
			currentState = value;

			static CameraBase CreateCinemaCamera()
			{
				return Random.Range(0, 7) switch
				{
					0 or 1 or 2 => new CinemaCameraPath(),
					3 or 4 or 5 => new CinemaCameraOrbit(),
					6 => new CinemaCameraDollyZoom(),
					_ => new CinemaCameraPath() // Default fallback
				};
			}
		}

		public static void Update()
		{
			if (null == cameraSystem) return;

			if (enableAutoCinema && currentState != CameraState.Cinema && Time.time - lastRefreshTime > CinemaTimeoutDuration)
				SetMode(CameraState.Cinema);

			if (!cameraSystem.Update() && CameraState.Cinema == currentState)
			{
				SetMode(CameraState.Cinema);
				cameraSystem.Update();
			}
		}

		public static void Project(Camera camera = null) => cameraSystem.Project(camera);

		public static void Refresh(float time)
		{
			lastRefreshTime = time;
			if (currentState == CameraState.Cinema)
				SetMode(previousState);
		}

		public static void SetOrigin(Vector3 value) => cameraSystem.SetOrigin(value);
		public static void SetTarget(Vector3 value) => cameraSystem.SetTarget(value);

		public static void SetPlayer(Transform value)
		{
			playerTransform = value;
			cameraSystem.playerTransform = value;
			if (null != value && SpatialBucketSystem.CanAddPoint(value.position))
			{
				var pos = value.position;
				mapBounds.Encapsulate(pos);
				focusPoints.Add(pos);
				SpatialBucketSystem.AddPoint(pos);

				if (focusPoints.Count > MaxFocusPoints)
				{
					SpatialBucketSystem.RemovePoint(focusPoints[0]);
					focusPoints.RemoveAt(0);
				}
			}
			cameraSystem.focusPoints = focusPoints;
		}

		public static void SetFocusPoints(List<Vector3> points)
		{
			focusPoints.Clear();
			SpatialBucketSystem.Clear();
			if (null == points || 0 == points.Count) return;

			focusPoints.AddRange(points);
			mapBounds = new Bounds(points[0], Vector3.zero);
			foreach (var point in points)
			{
				mapBounds.Encapsulate(point);
				if (SpatialBucketSystem.CanAddPoint(point))
					SpatialBucketSystem.AddPoint(point);
			}
			cameraSystem.focusPoints = focusPoints;
		}
	}
}