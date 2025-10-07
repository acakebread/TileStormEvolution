using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public enum CameraState { Absent, Static, Preset, Follow, Cinema }

	public class CameraController : MonoBehaviour
	{
		// Public properties
		public event Action<CameraState> OnCameraUpdate;

		// Constants
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		// Internal
		private CameraData restoreData;
		private CameraState currentState = CameraState.Absent;
		private CameraState previousState = CameraState.Absent;
		private float lastRefreshTime;
		private Bounds mapBounds;
		private CameraBase cameraSystem;
		private readonly List<Vector3> focusPoints = new();

		public CameraBase CameraSystem => cameraSystem;
		public CameraState CurrentState => currentState;
		public float LastRefreshTime => lastRefreshTime;

		private void Awake()
		{
			Camera cam = GetComponent<Camera>();
			if (cam == null)
			{
				Debug.LogError("CameraController requires a Camera component on the same GameObject.");
				return;
			}

			restoreData = new CameraData
			{
				smoothing = CameraData.DefaultSmoothingRate,
				originSrc = Vector3.zero,
				originDst = Vector3.zero,
				targetSrc = Vector3.zero,
				targetDst = Vector3.zero,
				fieldOfView = cam.fieldOfView,
				shake = 0f
			};
		}

		public void Reset()
		{
			lastRefreshTime = Time.time;
			focusPoints.Clear();
			mapBounds = new Bounds(Vector3.zero, Vector3.zero);
			SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint);
			cameraSystem = null;
			restoreData.smoothing = CameraData.DefaultSmoothingRate;
			SetMode(CameraState.Static);
		}

		public void SetMode(CameraState value)
		{
			if (CameraState.Cinema != currentState && null != cameraSystem)
				restoreData = cameraSystem.cameraData;

			cameraSystem = value switch
			{
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				CameraState.Cinema => CreateCinemaCamera(),
				_ => cameraSystem
			};

			cameraSystem.cameraData = restoreData;

			if (value != currentState) previousState = currentState;
			currentState = value;

			static CameraBase CreateCinemaCamera()
			{
				return UnityEngine.Random.Range(0, 7) switch
				{
					0 or 1 or 2 => new CinemaCameraPath(),
					3 or 4 or 5 => new CinemaCameraOrbit(),
					//6 => new CinemaCameraDollyZoom(),//disable dolly mode for now because it's not great
					_ => new CinemaCameraPath()
				};
			}
		}

		private void Update()
		{
			if (ClassicTilestorm.PreviewSettings.DebugMode) return;
			if (null == cameraSystem) return;

			OnCameraUpdate?.Invoke(currentState);
			cameraSystem?.Update();
			cameraSystem?.Project(GetComponent<Camera>());
		}

		public void Refresh(float time)
		{
			lastRefreshTime = time;
			if (currentState == CameraState.Cinema)
				SetMode(previousState);
		}

		public void SetOrigin(Vector3 value, bool both = false) => cameraSystem?.SetOrigin(value, both);
		public void SetTarget(Vector3 value, bool both = false) => cameraSystem?.SetTarget(value, both);

		public void SetPlayer(Transform value)
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

		public void SetFocusPoints(List<Vector3> points)
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
