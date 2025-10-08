using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public enum CameraState { Absent, Editor, Static, Preset, Follow } //, Cinema }
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
		private CameraData restoreData;
		[HideInInspector] public CameraAnimationData currentData; // Temporarily public
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
			currentData = new CameraAnimationData(cam);
			restoreData = new CameraData(cam);

			if (cam == null) Debug.LogError("CameraController requires a Camera component on the same GameObject.");
		}

		public void Reset()
		{
			cameraSystem = null;
			focusPoints.Clear();
			mapBounds = new Bounds(Vector3.zero, Vector3.zero);
			SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint);
			restoreData.smoothing = CameraData.DefaultSmoothingRate;
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
			CameraAnimationData previousData = currentData;

			if (CameraState.Editor != currentState && cameraSystem != null)
			{
				restoreData.target = currentData.target;
				restoreData.smoothing = currentData.smoothing;
				restoreData.fieldOfView = currentData.fieldOfView;
				restoreData.shake = currentData.shake;
				restoreData.enablePostProcessing = currentData.enablePostProcessing;
			}

			OnCameraDisable?.Invoke(currentState);

			cameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				_ => cameraSystem
			};

			currentData = new CameraAnimationData(GetComponent<Camera>())
			{
				position = previousData.position,
				lerpedTarget = previousData.lerpedTarget,
				target = restoreData.target,
				smoothing = restoreData.smoothing,
				fieldOfView = restoreData.fieldOfView,
				shake = restoreData.shake,
				enablePostProcessing = restoreData.enablePostProcessing
			};

			if (value != currentState) previousState = currentState;
			currentState = value;

			if (CameraState.Editor == currentState) cameraSystem.Start(ref currentData);
			OnEnableCamera();
		}

		private void OnEnableCamera()
		{
			var postProcessingCameraController = GetComponentInChildren<PostProcessingCameraController>(true);
			if (postProcessingCameraController != null)
			{
				postProcessingCameraController.enabled = currentData.enablePostProcessing;
				currentData.postProcessingCamera = postProcessingCameraController.GetComponent<Camera>();
			}
			OnCameraEnable?.Invoke(currentState);
		}

		private void Update()
		{
			OnCameraUpdate?.Invoke(currentState);
			UpdateFocusPoints();
			cameraSystem?.Update(ref currentData);
			cameraSystem?.Project(ref currentData);
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