using UnityEngine;

namespace MassiveHadronLtd
{
	public class CinemaCameraOrbit : CinemaCameraBase
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

		private Vector3 originSrc { get => cameraData.originSrc; set => cameraData.originSrc = value; }
		private Vector3 originDst { get => cameraData.originDst; set => cameraData.originDst = value; }
		private Vector3 targetSrc { get => cameraData.targetSrc; set => cameraData.targetSrc = value; }
		private Vector3 targetDst { get => cameraData.targetDst; set => cameraData.targetDst = value; }
		private float fieldOfView { get => cameraData.fieldOfView; set => cameraData.fieldOfView = value; }
		private float smoothing { get => cameraData.smoothing; set => cameraData.smoothing = value; }
		private float shake { get => cameraData.shake; set => cameraData.shake = value; }

		protected override void StartCinemaSequence()
		{
			if (null == playerTransform) return;

			shake = 1f;
			targetSrc = targetDst = playerTransform.position + Vector3.up * VerticalOffset;
			orbitStartAngle = Random.Range(0f, 360f);

			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, targetDst.y);
			var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, targetDst.y);
			var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			originDst = originSrc = targetSrc + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, 0f);
			var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			var delta = Random.Range(120f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;

			fieldOfView = FovMin;
			currentFovMax = Random.value < 0.2f ? 60f : FovMax;

			float CalculateMinOrbitRadius(float cameraHeight, float targetY)
			{
				var heightDiff = cameraHeight - (targetY + VerticalOffset);
				if (heightDiff <= 0f) return MaxOrbitRadius;
				var maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
				return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
			}
		}

		protected override void UpdateCinemaSequence(float easedSequenceTimer)
		{
			//update target
			targetDst = predictedPlayerPosition + Vector3.up * VerticalOffset;

			//update camera dest position and FOV
			originDst = targetDst + SampleOrbitPosition(orbitStartAngle, orbitEndAngle, easedSequenceTimer);
			fieldOfView = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration)); ;

			//update camera lerping
			smoothing = SmoothingUtils.Smooth(smoothing, SmoothingRate, currentSequenceDuration, Time.deltaTime, CameraData.TargetFPS);
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