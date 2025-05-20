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

	protected override void Start()
	{
		if (playerTransform == null) return;

		dollyZoomDirection = Vector3.zero;
		dollyZoomInitialDistance = 0f;

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
		originSrc = new Vector3(originSrc.x, height, originSrc.z);

		// Set destination
		float dollyZoomDistance = dollyZoomInitialDistance * 10f;
		dollyZoomDistance = Mathf.Min(dollyZoomDistance, MaxDollyZoomDistance);
		originDst = originSrc + dollyZoomDirection * dollyZoomDistance;
		originDst = new Vector3(originDst.x, height, originDst.z);
	}

	protected override void UpdateSequenceBespoke(float easedT, Vector3 playerDelta)
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
	}

	private float CalculateFovForScreenCoverage(float distance)
	{
		float halfPlayerHeight = PlayerRadius;
		float fov = 2f * Mathf.Atan(halfPlayerHeight / distance) * Mathf.Rad2Deg;
		return Mathf.Max(fov, MinFov);
	}
}