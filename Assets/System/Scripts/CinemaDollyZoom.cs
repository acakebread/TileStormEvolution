using UnityEngine;

public class CinemaDollyZoom : CinemaCameraBase
{
	// Dolly Zoom-specific constants
	private const float DoolyZoomSequenceDuration = 1f;
	private const float MaxDollyZoomDistance = 50f;
	private const float PlayerRadius = 1f;
	private const float MinFov = 15f; // Minimum FOV for narrow end

	// Dolly Zoom-specific state
	private Vector3 dollyZoomDirection;
	private float dollyZoomInitialDistance;

	public override void Reset()
	{
		base.Reset();
		dollyZoomDirection = Vector3.zero;
		dollyZoomInitialDistance = 0f;
	}

	public override void StartSequence()
	{
		base.StartSequence();

		if (playerTransform == null)
			return;

		sequenceTimer = 0f;
		pauseTimer = 0f;
		lastPlayerPos = playerTransform.position;
		smoothedProjectedOffset = Vector3.zero;

		currentMode = CinemaMode.DollyZoom;
		currentSequenceDuration = DoolyZoomSequenceDuration;

		// Chief Brody (Dolly Zoom) mode
		targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);
		targetDst = targetSrc;

		// Set initial camera position in front of the player
		float height = Random.Range(MinCameraHeight, MaxCameraHeight);
		float initialDistance = Random.Range(2f, 3f); // 2–3m distance
		Vector3 forward = playerTransform.forward.normalized;
		Vector3 offset = forward * initialDistance; // Position in front of player's facing direction (positive Z)
		originSrc = targetSrc + offset;
		originSrc.y = height;

		// Calculate distance to maintain target size during zoom
		dollyZoomInitialDistance = initialDistance;
		float dollyZoomDistance = dollyZoomInitialDistance * 10f; // NEW: 10x initial distance for wider pull-back
		dollyZoomDistance = Mathf.Min(dollyZoomDistance, MaxDollyZoomDistance);

		// Set Dolly Zoom direction and destination
		dollyZoomDirection = forward; // Move backward along player's facing direction (toward positive Z)
		originDst = originSrc + dollyZoomDirection * dollyZoomDistance;
		originDst.y = height;

		UpdateMapExtents();
	}

	private float CalculateFovForScreenCoverage(float distance)
	{
		// Player diameter = 2 * PlayerRadius = 2
		// Want player height to be ~50% of screen height
		// fov = 2 * atan(halfPlayerHeight / distance)
		float halfPlayerHeight = PlayerRadius; // Half diameter (1m) for 50% screen height
		float fov = 2f * Mathf.Atan(halfPlayerHeight / distance) * Mathf.Rad2Deg;
		return Mathf.Max(fov, MinFov); // Ensure FOV doesn't go below 15°
	}

	public override CameraController.CameraData UpdateSequence(CameraController.CameraData data, Camera camera)
	{
		if (playerTransform == null)
			return data;

		var delta = playerTransform.position - lastPlayerPos;
		lastPlayerPos = playerTransform.position;

		if (pauseTimer > 0f)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer > 0f)
				data = UpdateCameraData(data, originDst, targetDst);
			else
			{
				StartSequence();
				data = CreateCameraData(data);
			}
			return data;
		}

		sequenceTimer += Time.deltaTime;
		if (sequenceTimer >= currentSequenceDuration)
		{
			sequenceTimer = 0f;
			pauseTimer = PauseDuration;
			return data;
		}

		var t = currentSequenceDuration > 0 ? Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f;
		var easedT = SmoothingUtils.Ease(t);

		Vector3 targetProjectionOffset = delta * 2f;
		smoothedProjectedOffset.x = SmoothingUtils.Smooth(smoothedProjectedOffset.x, targetProjectionOffset.x, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.y = SmoothingUtils.Smooth(smoothedProjectedOffset.y, targetProjectionOffset.y, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.z = SmoothingUtils.Smooth(smoothedProjectedOffset.z, targetProjectionOffset.z, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);

		targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);
		targetDst = targetSrc;
		originSrc += delta;
		originDst += delta;

		Vector3 transOrigin = Vector3.Lerp(originSrc, originDst, easedT);
		Vector3 transTarget = targetSrc;

		data = UpdateCameraData(data, transOrigin, transTarget);
		data.smoothingRate = SmoothingUtils.Smooth(data.smoothingRate, 16f, currentSequenceDuration, Time.deltaTime, TargetFPS);

		// Dynamic FOV based on distance
		float currentDistance = Mathf.Lerp(dollyZoomInitialDistance, dollyZoomInitialDistance * 10f, easedT);
		currentDistance = Mathf.Min(currentDistance, MaxDollyZoomDistance);
		float fov = CalculateFovForScreenCoverage(currentDistance);
		data.fov = fov;
		return data;
	}
}