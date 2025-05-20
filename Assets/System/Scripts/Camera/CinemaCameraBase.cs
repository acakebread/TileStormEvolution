using UnityEngine;

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
	private Vector3 lastPlayerPos;

	protected CinemaCameraController cinemaCameraController;
	protected Transform playerTransform => cinemaCameraController.playerTransform;
	protected Vector3 originSrc { get => cinemaCameraController.cameraData.originSrc; set => cinemaCameraController.cameraData.originSrc = value; }
	protected Vector3 originDst { get => cinemaCameraController.cameraData.originDst; set => cinemaCameraController.cameraData.originDst = value; }
	protected Vector3 targetSrc { get => cinemaCameraController.cameraData.targetSrc; set => cinemaCameraController.cameraData.targetSrc = value; }
	protected Vector3 targetDst { get => cinemaCameraController.cameraData.targetDst; set => cinemaCameraController.cameraData.targetDst = value; }
	protected float fieldOfView { get => cinemaCameraController.cameraData.fieldOfView; set => cinemaCameraController.cameraData.fieldOfView = value; }

	public void StartSequence(CinemaCameraController _controller)
	{
		cinemaCameraController = _controller;
		currentSequenceDuration = DefaultSequenceDuration;
		sequenceTimer = pauseTimer = 0f;
		lastPlayerPos = predictedPlayerPosition = playerTransform.position;
		Start();
	}

	protected abstract void Start();

	public bool UpdateSequence()
	{
		if (playerTransform == null)
			return false;

		// Track player movement
		var posDelta = playerTransform.position - lastPlayerPos;
		lastPlayerPos = playerTransform.position;

		// Handle pause state
		if (pauseTimer > 0f)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer <= 0f) return false;
			cinemaCameraController.UpdateCameraData();
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

		// Smooth projected player position
		predictedPlayerPosition = SmoothingUtils.SmoothVector(predictedPlayerPosition, playerTransform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CinemaCameraController.TargetFPS);

		// Compute eased time
		var t = currentSequenceDuration > 0 ? Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f;
		var easedT = SmoothingUtils.Ease(t);

		// Compute mode-specific positions and FOV
		UpdateSequenceBespoke(easedT, posDelta);

		// Update camera data
		cinemaCameraController.UpdateCameraData();

		return true;
	}

	protected abstract void UpdateSequenceBespoke(float easedT, Vector3 playerDelta);
}