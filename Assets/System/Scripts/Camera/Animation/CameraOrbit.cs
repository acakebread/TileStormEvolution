// CameraOrbit.cs
using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraOrbit : CameraBase
	{
		// Shared constants from CinemaCameraBase
		protected const float PauseDuration = 1.5f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float ProjectionSmoothingRate = 8f; //16f;

		// Constants from CinemaCameraOrbit
		private const float VerticalOffset = 0.5f;
		private const float MinOrbitRadius = 2f;
		private const float MaxOrbitRadius = 8f;
		private const float FovMin = 35f;
		private const float FovMax = 55f;
		private const float MinCameraHeight = 1f;
		private const float MaxCameraHeight = 3f;
		private const float MaxLookAtAngle = 20f;
		private const float SmoothingRate = 16f;

		// Shared state from CinemaCameraBase
		protected Vector3 predictedPlayerPosition = Vector3.zero;
		protected float pauseTimer;
		protected float sequenceTimer;
		protected float currentSequenceDuration;
		protected Vector3 lastPlayerPos;

		// Fields from CinemaCameraOrbit
		private float orbitHeightSrc;
		private float orbitHeightDst;
		private float currentOrbitRadius;
		private float orbitStartAngle;
		private float orbitEndAngle;
		private float currentFovMax;

		public override void Start(ref CameraData data)
		{
			base.Start(ref data);
			sequenceTimer = pauseTimer = 0f; // Disable sequence by default
			if (playerTransform == null) return;

			// Preserve existing data for seamless transition, but initialize cinema-specific fields
			data.smoothing = CameraData.DefaultSmoothingRate;
			data.fieldOfView = 45f;
			data.shake = 0f;
			data.enablePostProcessing = true;

			currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2, 2);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDuration;
			lastPlayerPos = predictedPlayerPosition = playerTransform.position;

			// Call what was StartCinemaSequence
			if (data.camera == null || playerTransform == null) return;

			data.shake = 1f;
			data.target = playerTransform.position + Vector3.up * VerticalOffset;
			data.lerpedTarget = data.target;
			orbitStartAngle = Random.Range(0f, 360f);

			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, data.target.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, data.target.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			data.position = data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = Random.Range(120f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			// Update immediately for t=0
			UpdateOrbitSequence(ref data, 0f);
			data.camera.transform.position = data.position;
			data.lerpedTarget = data.target;
		}

		private float CalculateMinOrbitRadius(float cameraHeight, float targetY)
		{
			var heightDiff = cameraHeight - (targetY + VerticalOffset);
			if (heightDiff <= 0f) return MaxOrbitRadius;
			var maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
			return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
		}

		public override void Update(ref CameraData data)
		{
			base.Update(ref data);
			if (data.camera == null || playerTransform == null) return;

			// Update sequence timer (from CinemaCameraBase Update)
			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer <= 0f)
			{
				// Handle pause state
				if (pauseTimer <= 0f) return;
				pauseTimer -= Time.deltaTime;
			}
			else
			{
				// Smooth projected player position
				var posDelta = playerTransform.position - lastPlayerPos;
				predictedPlayerPosition = SmoothingUtils.SmoothVector(predictedPlayerPosition, playerTransform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CameraData.TargetFPS);

				// Compute eased time
				var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f);

				// Compute mode-specific positions and FOV (from UpdateCinemaSequence)
				UpdateOrbitSequence(ref data, easedSequenceTimer);

				lastPlayerPos = playerTransform.position;
			}

			// Lerp positions (from CinemaCameraBase Update)
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingRate, Time.deltaTime, CameraData.TargetFPS);
			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.camera.transform.position = Vector3.Lerp(data.camera.transform.position, data.position, interpolate);
			data.lerpedTarget = Vector3.Lerp(data.lerpedTarget, data.target, interpolate);
		}

		private void UpdateOrbitSequence(ref CameraData data, float easedSequenceTimer)
		{
			// Update target
			data.target = predictedPlayerPosition + Vector3.up * VerticalOffset;

			// Update camera dest position and FOV
			data.position = data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, easedSequenceTimer);
			data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration));
		}

		private Vector3 SampleOrbitPosition(float angleSrc, float angleDst, float easedT)
		{
			var angleRad = Mathf.Lerp(angleSrc * Mathf.Deg2Rad, angleDst * Mathf.Deg2Rad, easedT);
			var position = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * currentOrbitRadius;
			position.y = Mathf.Lerp(orbitHeightSrc, orbitHeightDst, SmoothingUtils.Ease(easedT));
			position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);
			return position;
		}

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;
	}
}