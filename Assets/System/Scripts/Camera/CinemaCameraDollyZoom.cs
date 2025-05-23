using UnityEngine;

namespace MassiveHadronLtd
{
	public class CinemaCameraDollyZoom : CinemaCameraBase
	{
		// Dolly Zoom-specific constants
		private const float VerticalOffset = 0.5f;
		private const float DollyZoomSequenceDuration = 2f;
		private const float MaxDollyZoomDistance = 50f;
		private const float PlayerRadius = 1f;
		private const float MinFov = 1f;
		private const float MinCameraHeight = 0.25f;
		private const float MaxCameraHeight = 1f;

		// Dolly Zoom-specific state
		private Vector3 dollyZoomDirection;
		private float dollyZoomInitialDistance;

		private Vector3 originSrc { get => cameraData.originSrc; set => cameraData.originSrc = value; }
		private Vector3 originDst { get => cameraData.originDst; set => cameraData.originDst = value; }
		private Vector3 targetSrc { get => cameraData.targetSrc; set => cameraData.targetSrc = value; }
		private Vector3 targetDst { get => cameraData.targetDst; set => cameraData.targetDst = value; }
		private float fieldOfView { get => cameraData.fieldOfView; set => cameraData.fieldOfView = value; }
		private float smoothing { get => cameraData.smoothing; set => cameraData.smoothing = value; }
		private float shake { get => cameraData.shake; set => cameraData.shake = value; }

		protected override void StartCinemaSequence()
		{
			if (playerTransform == null) return;

			currentSequenceDuration = DollyZoomSequenceDuration;
			sequenceTimer = currentSequenceDuration;
			pauseTimer = 1f;
			shake = 0.02f;

			// Set target positions
			targetDst = targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);

			// Set initial camera position
			var dollyHeight = Random.Range(MinCameraHeight, MaxCameraHeight);// add vertical offset so camera tlts down slightly
			var initialDistance = Random.Range(2f, 3f);
			dollyZoomDirection = playerTransform.forward.normalized;
			dollyZoomInitialDistance = initialDistance;
			originSrc = targetSrc + dollyZoomDirection * initialDistance;
			originSrc += Vector3.up * dollyHeight;

			dollyZoomDirection = (originSrc - targetSrc).normalized;

			//// Set destination
			//var dollyZoomDistance = dollyZoomInitialDistance * 10f;
			//dollyZoomDistance = Mathf.Min(dollyZoomDistance, MaxDollyZoomDistance);
			//originDst = originSrc + dollyZoomDirection * dollyZoomDistance;
			//originDst = new Vector3(originDst.x, dollyHeight, originDst.z);
		}

		protected override void UpdateCinemaSequence(float easedSequenceTimer)
		{
			targetDst = targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);

			var maxDollyZoomDistance = Mathf.Min(dollyZoomInitialDistance * 10f, MaxDollyZoomDistance);
			var currentDollyDistance = Mathf.Lerp(dollyZoomInitialDistance, maxDollyZoomDistance, easedSequenceTimer);

			originDst = originSrc = targetSrc + dollyZoomDirection * currentDollyDistance;
			fieldOfView = CalculateFovForScreenCoverage(currentDollyDistance);

			//var playerDelta = playerTransform.position - lastPlayerPos;

			//// Update positions
			//targetDst = targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);
			//originSrc += playerDelta;

			//// Compute interpolated positions
			//originDst = originSrc = Vector3.Lerp(originSrc, originDst, easedSequenceTimer);

			//fieldOfView = CalculateFovForScreenCoverage((targetSrc - originSrc).magnitude);

			//// Dynamic FOV
			//var currentDistance = Mathf.Lerp(dollyZoomInitialDistance, dollyZoomInitialDistance * 10f, easedSequenceTimer);
			//currentDistance = Mathf.Min(currentDistance, MaxDollyZoomDistance);

			//fieldOfView = CalculateFovForScreenCoverage(currentDistance);
		}

		private float CalculateFovForScreenCoverage(float distance)
		{
			var halfPlayerHeight = PlayerRadius;
			var fov = 2f * Mathf.Atan(halfPlayerHeight / distance) * Mathf.Rad2Deg;
			return Mathf.Max(fov, MinFov);
		}
	}
}