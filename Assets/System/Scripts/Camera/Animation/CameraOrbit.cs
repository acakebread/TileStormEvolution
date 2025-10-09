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

		public override bool HasCompleted => base.HasCompleted;

		protected override void Start()
		{
			InitializeCinemaSequence();
			if (playerTransform == null || _data.camera == null) return;

			currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2f, 2f);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDurationDefault;
			lastPlayerPos = predictedPlayerPosition = playerTransform.position;

			orbitStartAngle = Random.Range(0f, 360f);
			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, _data.target.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, _data.target.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			_data.lerpedTarget = _data.target = playerTransform.position + Vector3.up * VerticalOffset;
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = Random.Range(120f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			_data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			_data.position = _data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);
			_data.lerpedPosition = _data.position;
		}

		protected override void Update()
		{
			if (!UpdateCinemaSequence()) return;

			var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0
				? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration)
				: 1f);

			_data.target = predictedPlayerPosition + Vector3.up * VerticalOffset;
			_data.position = _data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, easedSequenceTimer);
			_data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration));

			_data.smoothing = SmoothingUtils.Smooth(_data.smoothing, SmoothingRate, currentSequenceDuration, Time.deltaTime, CameraData.TargetFPS);
			var t = SmoothingUtils.Smooth(0f, 1f, _data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			_data.lerpedPosition = Vector3.Lerp(_data.lerpedPosition, _data.position, t);
			_data.lerpedTarget = Vector3.Lerp(_data.lerpedTarget, _data.target, t);
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
