using System;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameCameraOrbit : CameraBase
	{
		public Func<Vector3> targetFn;
		private Vector3 target => targetFn?.Invoke() ?? Vector3.zero;

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

		private Vector3 localOrigin;
		private Vector3 localTarget;

		public bool HasCompleted => sequenceTimer <= 0f && pauseTimer <= 0f;

		public GameCameraOrbit(Camera camera) : base(camera) { }

		public override void Start()
		{
			base.Start();
			smoothing = DefaultSmoothingRate;
			fieldOfView = 45f;
			postProcessingEnabled = true;
			if (camera == null) return;

			InitializeCinemaSequence();

			localTarget = target; // Use helper property 'target'

			sequenceDuration = DefaultSequenceDuration + UnityEngine.Random.Range(-2f, 2f);
			sequenceTimer = sequenceDuration;
			pauseTimer = DefaultPauseDuration;
			lastTarget = nextTarget = target; // Use helper property 'target'

			orbitStartAngle = UnityEngine.Random.Range(0f, 360f);
			orbitHeightSrc = UnityEngine.Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = UnityEngine.Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, localTarget.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, localTarget.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = UnityEngine.Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			itarget = localTarget = target + Vector3.up * VerticalOffset; // Use helper property 'target'
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = UnityEngine.Random.Range(120f, maxDelta) * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			fieldOfView = FovMin;
			currentFovMax = UnityEngine.Random.value < 0.2f ? 60f : FovMax;

			iorigin = localOrigin = localTarget + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);
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
				localTarget = target + Vector3.up * VerticalOffset;
				localOrigin = localTarget + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, easedSequenceTimer);
				fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / sequenceDuration));
			}

			smoothing = SmoothingUtils.Smooth(smoothing, SmoothingRate, sequenceDuration, Time.deltaTime, TargetFPS);

			UpdateCinemaLerping();
			OnRender();
		}

		protected override void OnRender()
		{
			if (camera == null) return;
			camera.transform.position = iorigin;
			var direction = itarget - iorigin;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = fieldOfView;
			CameraUtils.ApplyCameraShake(camera, 1f);
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
			var interpolate = SmoothingUtils.Smooth(0f, 1f, smoothing, Time.deltaTime, TargetFPS);
			iorigin = Vector3.Lerp(iorigin, localOrigin, interpolate);
			itarget = Vector3.Lerp(itarget, localTarget, interpolate);
		}
	}
}