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
	protected Vector3 originSrc;
	protected Vector3 originDst;
	protected Vector3 targetSrc;
	protected Vector3 targetDst;
	protected Vector2 endTargetOffset;
	protected Vector3 smoothedProjectedOffset;

	// Shared state - sequence data
	protected float pauseTimer;
	protected CinemaMode currentMode;
	protected float sequenceTimer;
	protected float currentSequenceDuration;

	public CinemaCameraBase() 
	{
		Debug.Log("instantiate " + this);
	}

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

	public CameraController.CameraData CreateCameraData(CameraController.CameraData data)
	{
		data.originSrc = originSrc;
		data.targetSrc = targetSrc;
		data.smoothingRate = 64f;
		return data;
	}

	protected CameraController.CameraData UpdateCameraData(CameraController.CameraData data, Vector3 originDst, Vector3 targetDst)
	{
		var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothingRate, Time.deltaTime, TargetFPS);
		data.originSrc = Vector3.Lerp(data.originSrc, originDst, interpolate);
		data.targetSrc = Vector3.Lerp(data.targetSrc, targetDst, interpolate);
		return data;
	}

	public virtual void Reset()
	{
		currentSequenceDuration = DefaultSequenceDuration;
		sequenceTimer = 0f;
		pauseTimer = 0f;

		playerTransform = null;
		lastPlayerPos = Vector3.zero;
		playerPositions.Clear();
		focusPoints.Clear();
		mapExtentsMin = new Vector2(float.MaxValue, float.MaxValue);
		mapExtentsMax = new Vector2(float.MinValue, float.MinValue);
		originSrc = Vector3.zero;
		targetSrc = Vector3.zero;
		originDst = Vector3.zero;
		targetDst = Vector3.zero;
		endTargetOffset = Vector2.zero;
		smoothedProjectedOffset = Vector3.zero;
	}

	public virtual void StartSequence() { if (null != playerTransform) lastPlayerPos = playerTransform.position; }

	public CameraController.CameraData UpdateSequence(CameraController.CameraData data, Camera camera)
	{
		bool shouldContinue;
		return UpdateSequenceCore(data, camera, out shouldContinue);
	}

	protected virtual CameraController.CameraData UpdateSequenceCore(CameraController.CameraData data, Camera camera, out bool shouldContinue)
	{
		shouldContinue = true;

		if (playerTransform == null)
			return data;

		// Track player movement
		var delta = playerTransform.position - lastPlayerPos;
		lastPlayerPos = playerTransform.position;

		// Handle pause state
		if (pauseTimer > 0f)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer > 0f)
				return UpdateCameraData(data, originDst, targetDst);
			else
			{
				StartSequence();
				return CreateCameraData(data);
			}
		}

		// Update sequence timer
		sequenceTimer += Time.deltaTime;
		if (sequenceTimer >= currentSequenceDuration)
		{
			sequenceTimer = 0f;
			pauseTimer = PauseDuration;
			shouldContinue = false;
			return data;
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
		data = UpdateCameraData(data, transOrigin, transTarget);
		data.smoothingRate = SmoothingUtils.Smooth(data.smoothingRate, 16f, currentSequenceDuration, Time.deltaTime, TargetFPS);
		data.fov = fov;

		return data;
	}

	protected abstract (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta);
}