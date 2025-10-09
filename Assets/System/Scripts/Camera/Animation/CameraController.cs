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
		public CameraState RestoreState => restoreState;
		public CameraState CurrentState => currentState;

		// Internal
		private CameraBase cameraSystem;
		private CameraData restoreData;
		private CameraData currentData;
		private CameraState restoreState = CameraState.Absent;
		private CameraState currentState = CameraState.Absent;
		private Bounds mapBounds;
		private readonly List<Vector3> focusPoints = new();

		// Constants
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		private static CameraBase CreateCinemaCamera()
		{
			return UnityEngine.Random.Range(0, 7) switch
			{
				0 or 1 or 2 => new CameraPath(),
				3 or 4 or 5 => new CameraOrbit(),
				//6 => new CameraDollyZoom(),
				_ => new CameraOrbit()
			};
		}

		private void Awake()
		{
			var cam = GetComponent<Camera>();
			restoreData = new CameraData(cam);
			currentData = new CameraData(cam);

			var postProcessingCameraController = GetComponentInChildren<PostProcessingCameraController>(true);
			if (postProcessingCameraController != null)
			{
				currentData.postProcessingCameraController = postProcessingCameraController;
				restoreData.postProcessingCameraController = postProcessingCameraController;
			}

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
			//if (cameraSystem is CameraFollow) currentData.target = value.position;
			if (null != cameraSystem?.playerTransform) UpdateFocusPoints(cameraSystem.playerTransform.position);
		}

		public void SetMode(CameraState value)
		{
			if (CameraState.Editor != currentState && CameraState.Cinema != currentState && cameraSystem != null)
				restoreData.CopyFrom(currentData);

			OnCameraDisable?.Invoke(currentState);

			cameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				CameraState.Cinema => CreateCinemaCamera(),
				_ => cameraSystem
			};

			currentData.CopyFrom(restoreData);

			if (value != currentState) restoreState = currentState;
			currentState = value;

			if (CameraState.Editor == currentState) cameraSystem.Start(ref currentData);
			OnCameraEnable?.Invoke(currentState);
		}

		private void Update()
		{
			OnCameraUpdate?.Invoke(currentState);
			if (null != cameraSystem?.playerTransform) UpdateFocusPoints(cameraSystem.playerTransform.position);
			cameraSystem?.Update(ref currentData);
		}

		private void UpdateFocusPoints(Vector3 position)
		{
			if (SpatialBucketSystem.CanAddPoint(position))
			{
				mapBounds.Encapsulate(position);
				focusPoints.Add(position);
				SpatialBucketSystem.AddPoint(position);
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
				SpatialBucketSystem.TryAddPoint(point);
			}
			cameraSystem.focusPoints = focusPoints;
		}
	}
}