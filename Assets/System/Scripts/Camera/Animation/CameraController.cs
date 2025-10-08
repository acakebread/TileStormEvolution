using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public enum CameraState { Absent, Editor, Static, Preset, Follow, Cinema }
	public class CameraController : MonoBehaviour
	{
		// Public properties
		public event Action<CameraState> OnCameraEnable;
		public event Action<CameraState> OnCameraUpdate;
		public event Action<CameraState> OnCameraDisable;

		// Public accessors
		public CameraBase CameraSystem => cameraSystem;
		public CameraState PreviousState => previousState;
		public CameraState CurrentState => currentState;

		// Internal
		private CameraBase cameraSystem;
		private CameraData restoreState;
		[HideInInspector] public CameraAnimationData currentData;
		private CameraState currentState = CameraState.Absent;
		private CameraState previousState = CameraState.Absent;
		private Bounds mapBounds;
		private readonly List<Vector3> focusPoints = new();

		// Constants
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		private void Awake()
		{
			var cam = GetComponent<Camera>();
			currentData = new CameraAnimationData(cam)
			{
				postProcessingCameraController = GetComponentInChildren<PostProcessingCameraController>(true)
			};
			restoreState = new CameraData(cam);

			if (cam == null) Debug.LogError("CameraController requires a Camera component on the same GameObject.");
		}

		public void Reset()
		{
			cameraSystem = null;
			focusPoints.Clear();
			mapBounds = new Bounds(Vector3.zero, Vector3.zero);
			SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint);
			restoreState.smoothing = CameraData.DefaultSmoothingRate;
			SetMode(CameraState.Static);
		}

		public void SetPosition(Vector3 value, bool immediate = false) => cameraSystem?.SetPosition(ref currentData, value, immediate);
		public void SetTarget(Vector3 value, bool immediate = false) => cameraSystem?.SetTarget(ref currentData, value, immediate);

		public void SetPlayer(Transform value)
		{
			cameraSystem.playerTransform = value;
			if (cameraSystem is CameraFollow)
				currentData.target = value.position;
			UpdateFocusPoints();
		}

		public void SetMode(CameraState value)
		{
			if (CameraState.Cinema != currentState && CameraState.Editor != currentState && cameraSystem != null)
			{
				restoreState = new CameraData
				{
					target = currentData.target,
					smoothing = currentData.smoothing,
					fieldOfView = currentData.fieldOfView,
					shake = currentData.shake,
					enablePostProcessing = currentData.enablePostProcessing
				};
			}

			OnCameraDisable?.Invoke(currentState);

			cameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				CameraState.Cinema => new CameraOrbit(),
				_ => cameraSystem
			};

			if (value != CameraState.Cinema)
			{
				currentData = new CameraAnimationData(GetComponent<Camera>())
				{
					postProcessingCameraController = GetComponentInChildren<PostProcessingCameraController>(true),
					position = currentData.camera != null ? currentData.camera.transform.position : Vector3.zero,
					target = restoreState.target,
					lerpedTarget = restoreState.target,
					smoothing = restoreState.smoothing,
					fieldOfView = restoreState.fieldOfView,
					shake = restoreState.shake,
					enablePostProcessing = restoreState.enablePostProcessing
				};
				if (currentData.camera != null)
				{
					currentData.camera.transform.position = currentData.position;
				}
			}
			else
			{
				currentData = new CameraAnimationData(GetComponent<Camera>())
				{
					postProcessingCameraController = GetComponentInChildren<PostProcessingCameraController>(true),
					position = currentData.camera != null ? currentData.camera.transform.position : Vector3.zero,
					target = restoreState.target,
					lerpedTarget = restoreState.target,
					smoothing = CameraData.DefaultSmoothingRate,
					fieldOfView = restoreState.fieldOfView,
					shake = 0f,
					enablePostProcessing = true
				};
			}

			if (value != currentState) previousState = currentState;
			currentState = value;

//			if (CameraState.Editor == currentState) cameraSystem.Start(ref currentData);
			OnCameraEnable?.Invoke(currentState);
		}

		private void Update()
		{
			OnCameraUpdate?.Invoke(currentState);
			UpdateFocusPoints();
			cameraSystem?.Update(ref currentData);
		}

		private void UpdateFocusPoints()
		{
			if (cameraSystem?.playerTransform == null) return;
			if (SpatialBucketSystem.CanAddPoint(cameraSystem.playerTransform.position))
			{
				var pos = cameraSystem.playerTransform.position;
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
			if (points == null || points.Count == 0) return;

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