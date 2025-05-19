using UnityEngine;

public abstract class CinemaCameraBase
{
	// Shared constants
	protected const float PauseDuration = 1.5f;
	protected const float DefaultSequenceDuration = 8f;
	protected const float MinCameraHeight = 1.5f;
	protected const float MaxCameraHeight = 4f;
	protected const float ProjectionSmoothingRate = 16f;
	protected const float VerticalOffset = 0.5f;

	protected Vector3 smoothedProjectedOffset;

	// Shared state - sequence data
	protected float pauseTimer;
	protected float sequenceTimer;
	protected float currentSequenceDuration;

	public CinemaCameraBase() => Reset();

	public virtual void Reset()
	{
		currentSequenceDuration = DefaultSequenceDuration;
		sequenceTimer = pauseTimer = 0f;
		smoothedProjectedOffset = Vector3.zero;
	}

	//temporary workarounds
	protected CinemaCameraController cinemaCameraController;

	protected Transform playerTransform => cinemaCameraController.playerTransform;
	protected Vector3 originSrc { get => cinemaCameraController.cameraData.originSrc; set => cinemaCameraController.cameraData.originSrc = value; }
	protected Vector3 originDst { get => cinemaCameraController.cameraData.originDst; set => cinemaCameraController.cameraData.originDst = value; }
	protected Vector3 targetSrc { get => cinemaCameraController.cameraData.targetSrc; set => cinemaCameraController.cameraData.targetSrc = value; }
	protected Vector3 targetDst { get => cinemaCameraController.cameraData.targetDst; set => cinemaCameraController.cameraData.targetDst = value; }

	public virtual void StartSequence(CinemaCameraController _controller) 
	{ 
		cinemaCameraController = _controller;
		Reset();
	}

	public bool UpdateSequence()
	{
		if (playerTransform == null)
			return false;

		// Track player movement
		var delta = playerTransform.position - cinemaCameraController.lastPlayerPos;
		cinemaCameraController.lastPlayerPos = playerTransform.position;

		// Handle pause state
		if (pauseTimer > 0f)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer > 0f)
				cinemaCameraController.UpdateCameraData(originDst, targetDst, cinemaCameraController.cameraData.fieldOfView);
			else
				return false;
			return true;
		}

		// Update sequence timer
		sequenceTimer += Time.deltaTime;
		if (sequenceTimer >= currentSequenceDuration)
		{
			sequenceTimer = 0f;
			pauseTimer = PauseDuration;
			return true;
		}

		// Smooth projected offset
		Vector3 targetProjectionOffset = delta * 2f;
		smoothedProjectedOffset.x = SmoothingUtils.Smooth(smoothedProjectedOffset.x, targetProjectionOffset.x, ProjectionSmoothingRate, Time.deltaTime, CinemaCameraController.TargetFPS);
		smoothedProjectedOffset.y = SmoothingUtils.Smooth(smoothedProjectedOffset.y, targetProjectionOffset.y, ProjectionSmoothingRate, Time.deltaTime, CinemaCameraController.TargetFPS);
		smoothedProjectedOffset.z = SmoothingUtils.Smooth(smoothedProjectedOffset.z, targetProjectionOffset.z, ProjectionSmoothingRate, Time.deltaTime, CinemaCameraController.TargetFPS);

		// Compute eased time
		var t = currentSequenceDuration > 0 ? Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f;
		var easedT = SmoothingUtils.Ease(t);

		// Compute mode-specific positions and FOV
		(Vector3 transOrigin, Vector3 transTarget, float fov) = ComputeSequencePositionsAndFov(easedT, delta);

		// Update camera data
		cinemaCameraController.UpdateCameraData(transOrigin, transTarget, fov);
		cinemaCameraController.cameraData.smoothing = SmoothingUtils.Smooth(cinemaCameraController.cameraData.smoothing, 16f, currentSequenceDuration, Time.deltaTime, CinemaCameraController.TargetFPS);
		return true;
	}

	protected abstract (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta);
}