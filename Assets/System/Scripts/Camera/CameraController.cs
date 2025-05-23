using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public enum CameraState
	{
		Absent,
		Static,
		Preset,
		Follow,
		Cinema
	}

	public static class CameraController
	{
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
		private static readonly List<Vector3> focusPoints = new();

		public static void Start(Camera camera = null)
		{
			restoreData = new CameraData
			{
				smoothing = CameraData.DefaultSmoothingRate,
				originSrc = Vector3.zero,
				originDst = Vector3.zero,
				targetSrc = Vector3.zero,
				targetDst = Vector3.zero,
				fieldOfView = (null != camera ? camera : Camera.main).fieldOfView,
				shake = 0f
			};
		}

		public static void Reset()
		{
			lastRefreshTime = Time.time;
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

		public static void SystemStart() => cameraSystem.Start();//optional force start - not required in normal use

		public static void Update()
		{
			if (null == cameraSystem) return;
			cameraSystem.Update();

			var startCinema = CameraState.Cinema == currentState ? cameraSystem.HasCompleted : enableAutoCinema && Time.time - lastRefreshTime > CinemaTimeoutDuration;
			if (true == startCinema)
			{
				var playerTransform = cameraSystem.playerTransform;
				var points = cameraSystem.focusPoints;
				SetMode(CameraState.Cinema);//restart cinema in new sub mode
				cameraSystem.playerTransform = playerTransform;
				cameraSystem.focusPoints = points;
			}
		}

		public static void Project(Camera camera = null) => cameraSystem.Project(camera);

		public static void Refresh(float time)
		{
			lastRefreshTime = time;
			if (currentState == CameraState.Cinema)
				SetMode(previousState);
		}

		public static void SetOrigin(Vector3 value, bool both = false) => cameraSystem.SetOrigin(value, both);
		public static void SetTarget(Vector3 value, bool both = false) => cameraSystem.SetTarget(value, both);

		public static void SetPlayer(Transform value)
		{
			cameraSystem.playerTransform = value;
			if (null == value) return;
			if (SpatialBucketSystem.CanAddPoint(value.position))
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