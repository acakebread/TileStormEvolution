using UnityEngine;
using System.Linq;

public class CinemaCameraOrbit : CinemaCameraBase
{
	private const float MinOrbitRadius = 2f;
	private const float MaxOrbitRadius = 8f;
	private const float MinFocusPointDistanceFromPlayer = 4f;
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

	public override void StartSequence()
	{
		base.StartSequence();
		if (null == playerTransform)
			return;

		sequenceTimer = 0f;
		pauseTimer = 0f;
		lastPlayerPos = playerTransform.position;
		smoothedProjectedOffset = Vector3.zero;
		orbitCenter = Vector3.zero;

		currentMode = CinemaMode.Orbit;
		currentSequenceDuration = DefaultSequenceDuration;

		var startFocusPoint = playerTransform.position;
		if (focusPoints.Count > 0)
		{
			var validFocusPoint = focusPoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) >= MinFocusPointDistanceFromPlayer).ToList();
			if (validFocusPoint.Count > 0) startFocusPoint = validFocusPoint[Random.Range(0, validFocusPoint.Count)];
		}

		targetSrc = new Vector3(startFocusPoint.x, VerticalOffset, startFocusPoint.z);
		endTargetOffset = Vector3.zero;
		targetDst = new Vector3(playerTransform.position.x + endTargetOffset.x, VerticalOffset, playerTransform.position.z + endTargetOffset.y);

		orbitCenter = targetDst;
		targetSrc = targetDst;
		orbitStartAngle = Random.Range(0f, 360f);

		orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
		orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

		float minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, targetDst.y);
		float minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, targetDst.y);
		float minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
		currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

		originSrc = SampleOrbitPosition(orbitCenter, orbitStartAngle, 0f);
		float maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
		float delta = Random.Range(90f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
		orbitEndAngle = orbitStartAngle + delta;
		originDst = SampleOrbitPosition(orbitCenter, orbitEndAngle, 1f);

		UpdateMapExtents();
		currentFovMax = Random.value < 0.2f ? 60f : FovMax;
	}

	protected override (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta)
	{
		Vector3 transOrigin;
		Vector3 transTarget;
		float fov;

		targetSrc = new Vector3(playerTransform.position.x + endTargetOffset.x, VerticalOffset, playerTransform.position.z + endTargetOffset.y);
		targetDst = targetSrc;
		orbitCenter += playerDelta;
		transOrigin = SampleOrbitPosition(orbitCenter, Mathf.Lerp(orbitStartAngle, orbitEndAngle, easedT), easedT);
		transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);

		var fovT = SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration);
		fov = Mathf.Lerp(FovMin, currentFovMax, fovT);

		return (transOrigin, transTarget, fov);
	}

	private float CalculateMinOrbitRadius(float cameraHeight, float targetY)
	{
		float heightDiff = cameraHeight - (targetY + VerticalOffset);
		if (heightDiff <= 0f) return MaxOrbitRadius;
		float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
		return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
	}

	private Vector3 SampleOrbitPosition(Vector3 center, float angleDegrees, float easedT)
	{
		float angleRad = angleDegrees * Mathf.Deg2Rad;
		Vector3 offset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * currentOrbitRadius;
		Vector3 position = center + offset;
		position.y = Mathf.Lerp(orbitHeightSrc, orbitHeightDst, SmoothingUtils.Ease(easedT));
		position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);
		return position;
	}
}