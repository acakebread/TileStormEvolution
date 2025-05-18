using System.Collections.Generic;
using UnityEngine;

public class CinemaCameraDollyZoom : CinemaCameraBase
{
	// Dolly Zoom-specific constants
	private const float DollyZoomSequenceDuration = 1f;
	private const float MaxDollyZoomDistance = 50f;
	private const float PlayerRadius = 1f;
	private const float MinFov = 15f;

	// Dolly Zoom-specific state
	private Vector3 dollyZoomDirection;
	private float dollyZoomInitialDistance;

	public override void Reset()
	{
		base.Reset();
		dollyZoomDirection = Vector3.zero;
		dollyZoomInitialDistance = 0f;
	}

	public override void StartSequence(Transform transform, List<Vector3> points)
	{
		base.StartSequence(transform, points);
		if (playerTransform == null)
			return;

		sequenceTimer = 0f;
		pauseTimer = 0f;
		lastPlayerPos = playerTransform.position;
		smoothedProjectedOffset = Vector3.zero;

		currentMode = CinemaMode.DollyZoom;
		currentSequenceDuration = DollyZoomSequenceDuration;

		// Set target positions
		targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);
		targetDst = targetSrc;

		// Set initial camera position
		float height = Random.Range(MinCameraHeight, MaxCameraHeight);
		float initialDistance = Random.Range(2f, 3f);
		dollyZoomDirection = playerTransform.forward.normalized;
		dollyZoomInitialDistance = initialDistance;
		originSrc = targetSrc + dollyZoomDirection * initialDistance;
		originSrc.y = height;

		// Set destination
		float dollyZoomDistance = dollyZoomInitialDistance * 10f;
		dollyZoomDistance = Mathf.Min(dollyZoomDistance, MaxDollyZoomDistance);
		originDst = originSrc + dollyZoomDirection * dollyZoomDistance;
		originDst.y = height;

		UpdateMapExtents();
	}

	protected override (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta)
	{
		// Update positions
		targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);
		targetDst = targetSrc;
		originSrc += playerDelta;
		originDst += playerDelta;

		// Compute interpolated positions
		Vector3 transOrigin = Vector3.Lerp(originSrc, originDst, easedT);
		Vector3 transTarget = targetSrc;

		// Dynamic FOV
		float currentDistance = Mathf.Lerp(dollyZoomInitialDistance, dollyZoomInitialDistance * 10f, easedT);
		currentDistance = Mathf.Min(currentDistance, MaxDollyZoomDistance);
		float fov = CalculateFovForScreenCoverage(currentDistance);

		return (transOrigin, transTarget, fov);
	}

	private float CalculateFovForScreenCoverage(float distance)
	{
		float halfPlayerHeight = PlayerRadius;
		float fov = 2f * Mathf.Atan(halfPlayerHeight / distance) * Mathf.Rad2Deg;
		return Mathf.Max(fov, MinFov);
	}
}