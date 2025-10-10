using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraOrbit : CameraBase
	{
		private const float VerticalOffset = 0.5f;
		private const float MinOrbitRadius = 2f;
		private const float MaxOrbitRadius = 8f;
		private const float FovMin = 35f;
		private const float FovMax = 55f;
		private const float MinCameraHeight = 1f;
		private const float MaxCameraHeight = 3f;
		private const float MaxLookAtAngle = 20f;
		private const float SmoothingRate = 16f;

		private float orbitHeightSrc;
		private float orbitHeightDst;
		private float currentOrbitRadius;
		private float orbitStartAngle;
		private float orbitEndAngle;
		private float currentFovMax;

		public override void Awake()
		{
			base.Awake();
			data.smoothing = CameraData.DefaultSmoothingRate;
			data.shake = 0f;
			data.fieldOfView = 45f;
			data.postProcessingEnabled = true;

			var playerTransform = base.playerTransform?.Invoke();
			if (null == playerTransform || null == data.camera) return;

			InitializeCinemaSequence();

			sequenceDuration = DefaultSequenceDuration + Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;
			lastPlayerPos = nextPlayerPos = playerTransform.position;

			orbitStartAngle = Random.Range(0f, 360f);
			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, data.target.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, data.target.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			data.lerpedTarget = data.target = playerTransform.position + Vector3.up * VerticalOffset;
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = Random.Range(120f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			data.origin = data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);
			data.lerpedOrigin = data.origin;

			data.shake = 1f;
		}

		public override void Update()
		{
			base.Update();
			if (!UpdateCinemaSequence()) return;

			if (sequenceTimer > 0f)
			{
				var easedSequenceTimer = SmoothingUtils.Ease(sequenceDuration > 0
					? 1f - Mathf.Clamp01(sequenceTimer / sequenceDuration)
					: 1f);

				data.target = nextPlayerPos + Vector3.up * VerticalOffset;
				data.origin = data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, easedSequenceTimer);
				data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / sequenceDuration));
			}

			//update camera lerping
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingRate, sequenceDuration, Time.deltaTime, CameraData.TargetFPS);

			ApplyProjection();
		}

		private float CalculateMinOrbitRadius(float cameraHeight, float targetY)
		{
			var heightDiff = cameraHeight - (targetY + VerticalOffset);
			if (heightDiff <= 0f) return MaxOrbitRadius;
			var maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
			return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
		}

		private Vector3 SampleOrbitPosition(float angleSrc, float angleDst, float easedT)
		{
			var angleRad = Mathf.Lerp(angleSrc * Mathf.Deg2Rad, angleDst * Mathf.Deg2Rad, easedT);
			var position = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * currentOrbitRadius;
			position.y = Mathf.Lerp(orbitHeightSrc, orbitHeightDst, SmoothingUtils.Ease(easedT));
			position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);
			return position;
		}
	}
}