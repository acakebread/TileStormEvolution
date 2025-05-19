using UnityEngine;

public class CinemaCameraOrbit : CinemaCameraBase
{
	private const float MinOrbitRadius = 2f;
	private const float MaxOrbitRadius = 8f;
	private const float FovMin = 35f;
	private const float FovMax = 55f;
	protected const float MaxLookAtAngle = 20f;

	private Vector3 orbitCenter;
	private float orbitHeightSrc;
	private float orbitHeightDst;
	private float currentOrbitRadius;
	private float orbitStartAngle;
	private float orbitEndAngle;
	private float currentFovMax;

	public override void Reset()
	{
		base.Reset();
		currentFovMax = FovMax;
		orbitCenter = Vector3.zero;
		orbitHeightSrc = 0f;
		orbitHeightDst = 0f;
		currentOrbitRadius = 0f;
		orbitStartAngle = 0f;
		orbitEndAngle = 0f;
	}

	public override void StartSequence(CinemaCameraController _controller)
	{
		base.StartSequence(_controller);
		if (null == playerTransform)
			return;

		targetSrc = targetDst = orbitCenter = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);
		orbitStartAngle = Random.Range(0f, 360f);

		orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
		orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

		var minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, targetDst.y);
		var minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, targetDst.y);
		var minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
		currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

		originSrc = SampleOrbitPosition(orbitCenter, orbitStartAngle, 0f);
		var maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
		var delta = Random.Range(90f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
		orbitEndAngle = orbitStartAngle + delta;
		originDst = SampleOrbitPosition(orbitCenter, orbitEndAngle, 1f);

		currentFovMax = Random.value < 0.2f ? 60f : FovMax;

		float CalculateMinOrbitRadius(float cameraHeight, float targetY)
		{
			var heightDiff = cameraHeight - (targetY + VerticalOffset);
			if (heightDiff <= 0f) return MaxOrbitRadius;
			var maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
			return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
		}
	}

	protected override (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta)
	{
		orbitCenter += playerDelta;
		targetDst = orbitCenter + smoothedProjectedOffset;
		targetSrc = Vector3.Lerp(targetSrc, targetDst, SmoothingUtils.Smooth(0.5f, 1f, Time.deltaTime, CinemaCameraController.TargetFPS));

		var transOrigin = SampleOrbitPosition(orbitCenter, Mathf.Lerp(orbitStartAngle, orbitEndAngle, easedT), easedT);
		var transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);

		var fov = Mathf.Lerp(FovMin, currentFovMax, SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration));

		originDst = SampleOrbitPosition(orbitCenter, orbitEndAngle, 1f);//= transOrigin;
		return (transOrigin, transTarget, fov);
	}

	private Vector3 SampleOrbitPosition(Vector3 center, float angleDegrees, float easedT)
	{
		var angleRad = angleDegrees * Mathf.Deg2Rad;
		var offset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * currentOrbitRadius;
		var position = center + offset;
		position.y = Mathf.Lerp(orbitHeightSrc, orbitHeightDst, SmoothingUtils.Ease(easedT));
		position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);
		return position;
	}
}