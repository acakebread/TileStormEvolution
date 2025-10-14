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

		private Vector3 localOrigin; // Renamed to avoid conflict with helper property
		private Vector3 localTarget;

		public override bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

		public CameraOrbit(CameraState state) : base(state) { }

		public override void Start()
		{
			base.Start();
			data.smoothing = DefaultSmoothingRate;
			data.shake = 0f;
			data.fieldOfView = 45f;
			data.postProcessingEnabled = true;
			if (data?.camera == null) return;

			InitializeCinemaSequence();

			localTarget = target; // Use helper property 'target'

			sequenceDuration = DefaultSequenceDuration + Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;
			lastTarget = nextTarget = target; // Use helper property 'target'

			orbitStartAngle = Random.Range(0f, 360f);
			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, localTarget.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, localTarget.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			data.target = localTarget = target + Vector3.up * VerticalOffset; // Use helper property 'target'
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = Random.Range(120f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			data.fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			data.origin = localOrigin = localTarget + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);

			data.shake = 1f;
		}

		public override void Update()
		{
			base.Update();
			localTarget = target + Vector3.up * VerticalOffset; // Use helper property 'target'
			sequenceTimer -= Time.deltaTime;
			if (sequenceTimer > 0f)
			{
				var posDelta = localTarget - lastTarget;
				nextTarget = SmoothingUtils.SmoothVector(nextTarget, localTarget + posDelta * 2f, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
				lastTarget = localTarget;
			}
			else
				pauseTimer -= Time.deltaTime;

			if (sequenceTimer > 0f)
			{
				var easedSequenceTimer = SmoothingUtils.Ease(sequenceDuration > 0 ? 1f - Mathf.Clamp01(sequenceTimer / sequenceDuration) : 1f);
				localTarget = target + Vector3.up * VerticalOffset; // Use helper property 'target'
				localOrigin = localTarget + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, easedSequenceTimer);
				data.fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / sequenceDuration));
			}

			data.smoothing = SmoothingUtils.Smooth(data.smoothing, SmoothingRate, sequenceDuration, Time.deltaTime, TargetFPS);

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

		protected const float DefaultSmoothingRate = 64f;
		protected const float ProjectionSmoothingRate = 8f;
		protected const float DefaultSequenceDuration = 8f;
		protected const float DefaultPauseDuration = 1.5f;

		protected Vector3 lastTarget = Vector3.zero;
		protected Vector3 nextTarget = Vector3.zero;
		protected float sequenceDuration = DefaultSequenceDuration;
		protected float sequenceTimer = DefaultSequenceDuration;
		protected float pauseTimer = DefaultPauseDuration;

		protected virtual void InitializeCinemaSequence()
		{
			sequenceTimer = pauseTimer = 0f;

			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;

			lastTarget = nextTarget = target; // Use helper property 'target'
		}

		protected virtual void UpdateCinemaLerping()
		{
			var interpolate = SmoothingUtils.Smooth(0f, 1f, data.smoothing, Time.deltaTime, TargetFPS);
			data.origin = Vector3.Lerp(data.origin, localOrigin, interpolate);
			data.target = Vector3.Lerp(data.target, localTarget, interpolate);
		}
	}
}