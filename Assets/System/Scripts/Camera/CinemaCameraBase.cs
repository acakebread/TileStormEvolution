using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public abstract class CinemaCameraBase
{
	// Shared constants
	protected const float TargetFPS = 60f;
	protected const int MaxPlayerPositions = 50;
	protected const float PauseDuration = 1.5f;
	protected const float DefaultSequenceDuration = 8f;
	protected const float MinCameraHeight = 1.5f;
	protected const float MaxCameraHeight = 4f;
	protected const float MinDistanceForNewFocusPoint = 3f;
	protected const float ProjectionSmoothingRate = 16f;
	protected const float VerticalOffset = 0.5f;

	// Enum for cinematic modes
	protected enum CinemaMode
	{
		Orbit = 0,
		Path = 1,
		DollyZoom = 2
	}

	// Shared state - world data
	protected List<Vector3> focusPoints = new();
	protected List<Vector3> playerPositions = new();
	protected Vector2 mapExtentsMin;
	protected Vector2 mapExtentsMax;
	protected Transform playerTransform;
	protected Vector3 lastPlayerPos;

	// Shared state - camera data
	private static CameraController.CameraData controllerData;
	protected Vector3 originSrc { get => controllerData.originSrc; set => controllerData.originSrc = value; }
	protected Vector3 originDst { get => controllerData.originDst; set => controllerData.originDst = value; }
	protected Vector3 targetSrc { get => controllerData.targetSrc; set => controllerData.targetSrc = value; }
	protected Vector3 targetDst { get => controllerData.targetDst; set => controllerData.targetDst = value; }

	protected Vector3 smoothedProjectedOffset;
	protected Vector2 endTargetOffset;//not used

	// Shared state - sequence data
	protected float pauseTimer;
	protected CinemaMode currentMode;
	protected float sequenceTimer;
	protected float currentSequenceDuration;

	public CinemaCameraBase() => Reset();

	public void SetFocusPoints(List<Vector3> points)
	{
		focusPoints = points?.Where(p => p != Vector3.zero && Vector3.Distance(p, Vector3.zero) > 0.1f).ToList() ?? new List<Vector3>();
		UpdateMapExtents();
	}

	public void UpdatePlayerTransform(Transform transform)
	{
		if (null == playerTransform)
		{
			playerTransform = transform;
			lastPlayerPos = playerTransform.position;
		}

		if (playerTransform != null && IsFarEnough(playerTransform.position))
		{
			playerPositions.Add(playerTransform.position);
			if (playerPositions.Count > MaxPlayerPositions)
				playerPositions.RemoveAt(0);
		}
		UpdateMapExtents();

		bool IsFarEnough(Vector3 position)
		{
			foreach (var fp in focusPoints)
			{
				if (Vector3.Distance(position, fp) < MinDistanceForNewFocusPoint)
					return false;
			}
			foreach (var pp in playerPositions)
			{
				if (Vector3.Distance(position, pp) < MinDistanceForNewFocusPoint)
					return false;
			}
			return true;
		}
	}

	protected void UpdateMapExtents()
	{
		mapExtentsMin = new Vector2(float.MaxValue, float.MaxValue);
		mapExtentsMax = new Vector2(float.MinValue, float.MinValue);

		if (focusPoints.Count > 0)
		{
			mapExtentsMin.x = focusPoints.Min(p => p.x);
			mapExtentsMin.y = focusPoints.Min(p => p.z);
			mapExtentsMax.x = focusPoints.Max(p => p.x);
			mapExtentsMax.y = focusPoints.Max(p => p.z);
		}

		if (playerTransform != null)
		{
			Vector3 pos = playerTransform.position;
			mapExtentsMin.x = Mathf.Min(mapExtentsMin.x, pos.x);
			mapExtentsMin.y = Mathf.Min(mapExtentsMin.y, pos.z);
			mapExtentsMax.x = Mathf.Max(mapExtentsMax.x, pos.x);
			mapExtentsMax.y = Mathf.Max(mapExtentsMax.y, pos.z);
		}
	}

	public CameraController.CameraData CameraData => controllerData;

	protected CameraController.CameraData UpdateCameraData(Vector3 originDst, Vector3 targetDst, float fov)
	{
		var interpolate = SmoothingUtils.Smooth(0f, 1f, controllerData.smoothingRate, Time.deltaTime, TargetFPS);
		controllerData.originSrc = Vector3.Lerp(controllerData.originSrc, originDst, interpolate);
		controllerData.targetSrc = Vector3.Lerp(controllerData.targetSrc, targetDst, interpolate);
		controllerData.fov = fov;
		return controllerData;
	}

	public virtual void Reset()
	{
		currentSequenceDuration = DefaultSequenceDuration;
		sequenceTimer = 0f;
		pauseTimer = 0f;

		controllerData = new CameraController.CameraData
		{
			smoothingRate = 64f,//default smoothing rate
			originSrc = Vector3.zero,
			originDst = Vector3.zero,
			targetSrc = Vector3.zero,
			targetDst = Vector3.zero,
			fov = 45f
		};

		endTargetOffset = Vector2.zero;
		smoothedProjectedOffset = Vector3.zero;

		//ToDo extract to controller
		playerTransform = null;
		lastPlayerPos = Vector3.zero;
		playerPositions.Clear();
		focusPoints.Clear();
		mapExtentsMin = new Vector2(float.MaxValue, float.MaxValue);
		mapExtentsMax = new Vector2(float.MinValue, float.MinValue);
	}

	public virtual void StartSequence(Transform transform, List<Vector3> points) { UpdatePlayerTransform(transform); SetFocusPoints(points); }

	public CameraController.CameraData UpdateSequence(Camera camera, out bool shouldContinue) => UpdateSequenceCore(camera, out shouldContinue);

	protected virtual CameraController.CameraData UpdateSequenceCore(Camera camera, out bool shouldContinue)
	{
		shouldContinue = true;

		if (playerTransform == null)
			return controllerData;

		// Track player movement
		var delta = playerTransform.position - lastPlayerPos;
		lastPlayerPos = playerTransform.position;

		// Handle pause state
		if (pauseTimer > 0f)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer > 0f)
				return UpdateCameraData(originDst, targetDst, controllerData.fov);
			else
			{
				shouldContinue = false;
				return CameraData;
			}
		}

		// Update sequence timer
		sequenceTimer += Time.deltaTime;
		if (sequenceTimer >= currentSequenceDuration)
		{
			sequenceTimer = 0f;
			pauseTimer = PauseDuration;
			return controllerData;
		}

		// Compute eased time
		var t = currentSequenceDuration > 0 ? Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f;
		var easedT = SmoothingUtils.Ease(t);

		// Smooth projected offset
		Vector3 targetProjectionOffset = delta * 2f;
		smoothedProjectedOffset.x = SmoothingUtils.Smooth(smoothedProjectedOffset.x, targetProjectionOffset.x, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.y = SmoothingUtils.Smooth(smoothedProjectedOffset.y, targetProjectionOffset.y, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.z = SmoothingUtils.Smooth(smoothedProjectedOffset.z, targetProjectionOffset.z, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);

		// Compute mode-specific positions and FOV
		(Vector3 transOrigin, Vector3 transTarget, float fov) = ComputeSequencePositionsAndFov(easedT, delta);

		// Update camera data
		UpdateCameraData(transOrigin, transTarget, fov);
		controllerData.smoothingRate = SmoothingUtils.Smooth(controllerData.smoothingRate, 16f, currentSequenceDuration, Time.deltaTime, TargetFPS);
		return controllerData;
	}

	protected abstract (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta);
}