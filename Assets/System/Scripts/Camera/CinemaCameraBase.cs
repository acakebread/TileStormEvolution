using UnityEngine;
using System.Collections.Generic;

public abstract class CinemaCameraBase
{
	// Shared constants
	protected const float PauseDuration = 1.5f;
	protected const float DefaultSequenceDuration = 8f;
	protected const float MinCameraHeight = 1.5f;
	protected const float MaxCameraHeight = 4f;
	protected const float ProjectionSmoothingRate = 8f;//16f;
	protected const float VerticalOffset = 0.5f;
	protected Vector3 predictedPlayerPosition = Vector3.zero;

	// Shared state - sequence data
	protected float pauseTimer;
	protected float sequenceTimer;
	protected float currentSequenceDuration;

	//temporary workarounds
	protected Vector3 lastPlayerPos;

	protected Transform playerTransform => CinemaController.playerTransform;
	protected Vector3 originSrc { get => CinemaController.cameraData.originSrc; set => CinemaController.cameraData.originSrc = value; }
	protected Vector3 originDst { get => CinemaController.cameraData.originDst; set => CinemaController.cameraData.originDst = value; }
	protected Vector3 targetSrc { get => CinemaController.cameraData.targetSrc; set => CinemaController.cameraData.targetSrc = value; }
	protected Vector3 targetDst { get => CinemaController.cameraData.targetDst; set => CinemaController.cameraData.targetDst = value; }
	protected float fieldOfView { get => CinemaController.cameraData.fieldOfView; set => CinemaController.cameraData.fieldOfView = value; }
	protected float smoothing { get => CinemaController.cameraData.smoothing; set => CinemaController.cameraData.smoothing = value; }
	protected List<Vector3> focusPoints => CinemaController.focusPoints;

	public void StartSequence()
	{
		sequenceTimer = pauseTimer = 0f;//disable sequence by default
		if (null == CinemaController.playerTransform) return;
		currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2, 2);
		sequenceTimer = currentSequenceDuration;
		pauseTimer = PauseDuration;
		lastPlayerPos = predictedPlayerPosition = playerTransform.position;
		Start();
	}

	protected abstract void Start();

	public bool Update()
	{
		// Update sequence timer
		sequenceTimer -= Time.deltaTime;
		if (sequenceTimer <= 0f)
		{
			// Handle pause state
			if (pauseTimer <= 0f) return false;
			pauseTimer -= Time.deltaTime;
			if (pauseTimer > 0f) UpdateCameraData();
			return pauseTimer > 0f;
		}

		// Smooth projected player position
		var posDelta = playerTransform.position - lastPlayerPos;
		predictedPlayerPosition = SmoothingUtils.SmoothVector(predictedPlayerPosition, playerTransform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CinemaController.TargetFPS);

		// Compute eased time
		var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f);

		// Compute mode-specific positions and FOV
		UpdateSequence(easedSequenceTimer);

		lastPlayerPos = playerTransform.position;
		UpdateCameraData();
		return true;

		void UpdateCameraData()
		{
			var interpolate = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, CinemaController.TargetFPS);
			originSrc = Vector3.Lerp(originSrc, originDst, interpolate);
			targetSrc = Vector3.Lerp(targetSrc, targetDst, interpolate);
			//fovSrc = Mathf.Lerp(fovSrc, fovDst, interpolate); ToDo initialise FOV in StartSequence and lerp
		}
	}

	protected abstract void UpdateSequence(float easedSequenceTimer);
}