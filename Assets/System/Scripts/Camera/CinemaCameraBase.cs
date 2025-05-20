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

	private CinemaCameraController cinemaCameraController;
	protected Transform playerTransform => cinemaCameraController.playerTransform;
	protected Vector3 originSrc { get => cinemaCameraController.cameraData.originSrc; set => cinemaCameraController.cameraData.originSrc = value; }
	protected Vector3 originDst { get => cinemaCameraController.cameraData.originDst; set => cinemaCameraController.cameraData.originDst = value; }
	protected Vector3 targetSrc { get => cinemaCameraController.cameraData.targetSrc; set => cinemaCameraController.cameraData.targetSrc = value; }
	protected Vector3 targetDst { get => cinemaCameraController.cameraData.targetDst; set => cinemaCameraController.cameraData.targetDst = value; }
	protected float fieldOfView { get => cinemaCameraController.cameraData.fieldOfView; set => cinemaCameraController.cameraData.fieldOfView = value; }
	protected float smoothing { get => cinemaCameraController.cameraData.smoothing; set => cinemaCameraController.cameraData.smoothing = value; }
	protected List<Vector3> focusPoints => cinemaCameraController.focusPoints;

	public void StartSequence(CinemaCameraController controller)
	{
		sequenceTimer = pauseTimer = 0f;//disable sequence by default
		if (null == controller.playerTransform) return;
		cinemaCameraController = controller;
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
		predictedPlayerPosition = SmoothingUtils.SmoothVector(predictedPlayerPosition, playerTransform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CinemaCameraController.TargetFPS);

		// Compute eased time
		var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f);

		// Compute mode-specific positions and FOV
		UpdateSequence(easedSequenceTimer);

		lastPlayerPos = playerTransform.position;
		UpdateCameraData();
		return true;

		void UpdateCameraData()
		{
			var interpolate = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, CinemaCameraController.TargetFPS);
			originSrc = Vector3.Lerp(originSrc, originDst, interpolate);
			targetSrc = Vector3.Lerp(targetSrc, targetDst, interpolate);
			//fovSrc = Mathf.Lerp(fovSrc, fovDst, interpolate); ToDo initialise FOV in StartSequence and lerp
		}
	}

	protected abstract void UpdateSequence(float easedSequenceTimer);
}