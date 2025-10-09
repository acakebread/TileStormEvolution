using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public enum CameraState { Absent, Editor, Static, Preset, Follow, Cinema }

	public class CameraController : MonoBehaviour
	{
		public Func<Transform> OnUpdatePlayer;
		public event Action<CameraState> OnCameraEnable;
		public event Action<CameraState> OnCameraUpdate;
		public event Action<CameraState> OnCameraDisable;

		public CameraBase CameraSystem => cameraSystem;
		public CameraState RestoreState => restoreState;
		public CameraState CurrentState => currentState;

		private CameraBase cameraSystem;
		private CameraData restoreData;
		private CameraState restoreState = CameraState.Absent;
		private CameraState currentState = CameraState.Absent;
		private Bounds mapBounds;
		private readonly List<Vector3> focusPoints = new();

		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		private static CameraBase CreateCinemaCamera()
		{
			return UnityEngine.Random.Range(0, 7) switch
			{
				0 or 1 or 2 => new CameraPath(),
				3 or 4 or 5 => new CameraOrbit(),
				_ => new CameraOrbit()
			};
		}

		private void Awake() => Reset();

		public void Reset()
		{
			cameraSystem = null;
			mapBounds = new Bounds(Vector3.zero, Vector3.zero);
			focusPoints.Clear();
			SpatialBucketSystem.Initialize(MinDistanceForNewFocusPoint);

			var cam = GetComponent<Camera>();
			restoreData = new CameraData(cam);

			var postProcessingCameraController = GetComponentInChildren<PostProcessingCameraController>(true);
			if (null != postProcessingCameraController)
			{
				restoreData.postProcessingCameraController = postProcessingCameraController;
			}
			else
				Debug.LogError("CameraController requires a Camera component on the same GameObject.");

			SetMode(CameraState.Static);
		}

		public void SetOrigin(Vector3 value, bool immediate = false) => cameraSystem?.SetOrigin(value, immediate);
		public void SetTarget(Vector3 value, bool immediate = false) => cameraSystem?.SetTarget(value, immediate);

		public void SetFocusPoints(List<Vector3> points)
		{
			mapBounds = new Bounds(points[0], Vector3.zero);
			focusPoints.Clear();
			SpatialBucketSystem.Clear();
			if (points == null || points.Count == 0) return;
			focusPoints.AddRange(points);
			foreach (var point in points)
			{
				mapBounds.Encapsulate(point);
				SpatialBucketSystem.TryAddPoint(point);
			}
			cameraSystem.focusPoints = focusPoints;
		}

		public void SetMode(CameraState value)
		{
			if (CameraState.Editor != currentState && CameraState.Cinema != currentState && cameraSystem != null)
				restoreData= cameraSystem.GetData(); //restoreData.CopyFrom(cameraSystem.GetData());
			var currentData = restoreData;

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

			cameraSystem.OnUpdatePlayer += OnUpdatePlayer;
			cameraSystem.OnUpdateFocusPoints += () => focusPoints;
			cameraSystem.SetData(currentData);

			if (value != currentState) restoreState = currentState;
			currentState = value;

			cameraSystem.Start(this);
			OnCameraEnable?.Invoke(currentState);
		}

		private void Update()
		{
			OnCameraUpdate?.Invoke(currentState);
			cameraSystem?.Update(this);
		}

		public void UpdateFocusPoints(Vector3 position)
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
	}
}
