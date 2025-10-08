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
		[HideInInspector] public CameraData currentData; // Temporarily public
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
			restoreData = currentData = new CameraData
			{
				camera = cam,
				postProcessingCamera = null, // Will be set in OnEnableCamera if needed
				smoothing = CameraData.DefaultSmoothingRate,
				position = Vector3.zero,
				target = Vector3.zero,
				lerpedTarget = Vector3.zero,
				fieldOfView = cam != null ? cam.fieldOfView : 45f,
				shake = 0f,
				enablePostProcessing = true
			};
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
			if (CameraState.Editor != currentState && cameraSystem != null) // CameraState.Cinema != currentState &&
				restoreData = currentData;
			OnCameraDisable?.Invoke(currentState);
			cameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				//CameraState.Cinema => CreateCinemaCamera(),
				_ => cameraSystem
			};
			currentData = restoreData;
			currentData.camera = GetComponent<Camera>(); // Ensure camera reference is set
			currentData.postProcessingCamera = GetComponentInChildren<PostProcessingCameraController>(true)?.GetComponent<Camera>();
			if (value != currentState) previousState = currentState;
			currentState = value;
			//static CameraBase CreateCinemaCamera()
			//{
			//    return UnityEngine.Random.Range(0, 7) switch
			//    {
			//        0 or 1 or 2 => new CinemaCameraPath(),
			//        3 or 4 or 5 => new CinemaCameraOrbit(),
			//        //6 => new CinemaCameraDollyZoom(),
			//        _ => new CinemaCameraPath()
			//    };
			//}
			if (CameraState.Editor == currentState) cameraSystem.Start(ref currentData); // Only editor for now
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