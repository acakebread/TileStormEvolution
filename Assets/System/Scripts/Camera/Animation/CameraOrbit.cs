using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraOrbit : CameraBase
	{
		protected const float PauseDuration = 1.5f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float ProjectionSmoothingRate = 8f;

		private const float VerticalOffset = 0.5f;
		private const float MinOrbitRadius = 2f;
		private const float MaxOrbitRadius = 8f;
		private const float FovMin = 35f;
		private const float FovMax = 55f;
		private const float MinCameraHeight = 1f;
		private const float MaxCameraHeight = 3f;
		private const float MaxLookAtAngle = 20f;
		private const float SmoothingRate = 16f;

		protected Vector3 predictedPlayerPosition = Vector3.zero;
		protected float pauseTimer;
		protected float sequenceTimer;
		protected float currentSequenceDuration;
		protected Vector3 lastPlayerPos;

		private float orbitHeightSrc;
		private float orbitHeightDst;
		private float currentOrbitRadius;
		private float orbitStartAngle;
		private float orbitEndAngle;
		private float currentFovMax;

		protected override void Start()
		{
			sequenceTimer = pauseTimer = 0f;
			if (playerTransform == null) return;

			_data.smoothing = CameraData.DefaultSmoothingRate;
			_data.fieldOfView = 45f;
			_data.shake = 0f;
			_data.enablePostProcessing = true;

			currentSequenceDuration = DefaultSequenceDuration + Random.Range(-2, 2);
			sequenceTimer = currentSequenceDuration;
			pauseTimer = PauseDuration;
			lastPlayerPos = predictedPlayerPosition = playerTransform.position;

			if (_data.camera == null || playerTransform == null) return;

			_data.shake = 1f;
			_data.target = playerTransform.position + Vector3.up * VerticalOffset;
			_data.lerpedTarget = _data.target;
			orbitStartAngle = Random.Range(0f, 360f);

			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, _data.target.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, _data.target.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			_data.position = _data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = Random.Range(120f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			_data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			UpdateOrbitSequence();
			_data.camera.transform.position = _data.position;
			_data.lerpedTarget = _data.target;
		}

		private float CalculateMinOrbitRadius(float cameraHeight, float targetY)
		{
			var heightDiff = cameraHeight - (targetY + VerticalOffset);
			if (heightDiff <= 0f) return MaxOrbitRadius;
			var maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
			return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
		}

		protected override void Update()
		{
			if (_data.camera == null || playerTransform == null) return;

			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer <= 0f)
			{
				if (pauseTimer <= 0f) return;
				pauseTimer -= Time.deltaTime;
			}
			else
			{
				var posDelta = playerTransform.position - lastPlayerPos;
				predictedPlayerPosition = SmoothingUtils.SmoothVector(predictedPlayerPosition, playerTransform.position + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CameraData.TargetFPS);

				var easedSequenceTimer = SmoothingUtils.Ease(currentSequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f);

				UpdateOrbitSequence();

				lastPlayerPos = playerTransform.position;
			}

			_data.smoothing = SmoothingUtils.Smooth(_data.smoothing, SmoothingRate, Time.deltaTime, CameraData.TargetFPS);
			var interpolate = SmoothingUtils.Smooth(0f, 1f, _data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			_data.camera.transform.position = Vector3.Lerp(_data.camera.transform.position, _data.position, interpolate);
			_data.lerpedTarget = Vector3.Lerp(_data.lerpedTarget, _data.target, interpolate);
		}

		private void UpdateOrbitSequence()
		{
			_data.target = predictedPlayerPosition + Vector3.up * VerticalOffset;
			_data.position = _data.target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration));
			_data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration));
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