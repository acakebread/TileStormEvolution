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
		private CameraData restoreData;
		public CameraData currentData;//temporarily public
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
				smoothing = CameraData.DefaultSmoothingRate,
				originSrc = Vector3.zero,
				originDst = Vector3.zero,
				targetSrc = Vector3.zero,
				targetDst = Vector3.zero,
				fieldOfView = null != cam ? cam.fieldOfView : 45f,
				shake = 0f,
				enablePostProcessing = true
			};

			if (null == cam) Debug.LogError("CameraController requires a Camera component on the same GameObject.");
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

		public void SetOrigin(Vector3 value, bool both = false) => cameraSystem?.SetOrigin(ref currentData, value, both);
		public void SetTarget(Vector3 value, bool both = false) => cameraSystem?.SetTarget(ref currentData, value, both);

		public void SetPlayer(Transform value)
		{
			cameraSystem.playerTransform = value;
			if (cameraSystem is CameraFollow)
				currentData.targetDst = value.position;
			UpdateFocusPoints();
		}

		public void SetMode(CameraState value)
		{
			if (CameraState.Cinema != currentState && CameraState.Editor != currentState && null != cameraSystem)
				restoreData = currentData;

			cameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				CameraState.Cinema => CreateCinemaCamera(),
				_ => cameraSystem
			};

			currentData = restoreData;

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
			if (CameraState.Editor == currentState) cameraSystem.Start(ref currentData);//only editor for now
			OnCameraEnable?.Invoke(currentState);
		}

		private void Update()
		{
			OnCameraUpdate?.Invoke(currentState);
			UpdateFocusPoints();
			cameraSystem?.Update(ref currentData);
			cameraSystem?.Project(ref currentData, GetComponent<Camera>());
		}

		private void UpdateFocusPoints()
		{
			if (null == cameraSystem.playerTransform) return;
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
