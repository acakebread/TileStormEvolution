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

		private Vector3 origin;
		private Vector3 target;

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

		public override void Start()
		{
			base.Start();
			data.smoothing = CameraData.DefaultSmoothingRate;
			data.shake = 0f;
			data.fieldOfView = 45f;
			data.postProcessingEnabled = true;
			if (null == data.camera) return;

			InitializeCinemaSequence();

			target = targetFunc.Invoke();

			sequenceDuration = DefaultSequenceDuration + Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;
			lastTarget = nextTarget = targetFunc.Invoke();

			orbitStartAngle = Random.Range(0f, 360f);
			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, target.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, target.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			data.target = target = targetFunc.Invoke() + Vector3.up * VerticalOffset;
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = Random.Range(120f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			data.origin = origin = target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);

			data.shake = 1f;
		}

		public override void Update()
		{
			base.Update();
			target = targetFunc.Invoke();
			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer > 0f)
			{
				var posDelta = target - lastTarget;
				nextTarget = SmoothingUtils.SmoothVector(nextTarget, target + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, CameraData.TargetFPS);
				lastTarget = target;
			}
			else
				pauseTimer -= Time.deltaTime;

			if (sequenceTimer > 0f)
			{
				var easedSequenceTimer = SmoothingUtils.Ease(sequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / sequenceDuration) : 1f);
				target = targetFunc.Invoke() + Vector3.up * VerticalOffset;
				origin = target + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, easedSequenceTimer);
				data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / sequenceDuration));
			}

			//update camera smoothing
			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingRate, sequenceDuration, Time.deltaTime, CameraData.TargetFPS);

			UpdateCinemaLerping();
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

		// === Shared cinema constants ===
		protected const float ProjectionSmoothingRate = 8f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float DefaultPauseDuration = 1.5f;

		// === Shared cinema state ===
		protected Vector3 lastTarget = Vector3.zero;
		protected Vector3 nextTarget = Vector3.zero;//prediction
		protected float sequenceDuration = DefaultSequenceDuration;
		protected float sequenceTimer = DefaultSequenceDuration;
		protected float pauseTimer = DefaultPauseDuration;

		// === Shared cinema utilities ===
		protected virtual void InitializeCinemaSequence()
		{
			sequenceTimer = pauseTimer = 0f;

			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;

			lastTarget = nextTarget = targetFunc.Invoke();
		}

		protected virtual void UpdateCinemaLerping()
		{
			// Update camera lerping
			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, CameraData.TargetFPS);
			data.origin = Vector3.Lerp(data.origin, origin, interpolate);
			data.target = Vector3.Lerp(data.target, target, interpolate);
		}
	}
}
