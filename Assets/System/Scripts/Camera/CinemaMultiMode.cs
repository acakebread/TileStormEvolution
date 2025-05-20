////legacy implementation - to be removed
//using UnityEngine;
//using System.Linq;

//public class CinemaMultiMode : CinemaCameraBase
//{
//	private const float MinOrbitRadius = 2f;
//	private const float MaxOrbitRadius = 8f;
//	private const float OrbitTargetDistanceThreshold = 0.5f;
//	public static bool DEBUG_USE_SPLINES = true;
//	public static bool DEBUG_VISUALIZE_BEZIER = true;
//	private const float EllipsoidMajorAxisScale = 1.5f;
//	private const float EllipsoidMinorAxisScale = 1.5f;
//	private const float MinFocusPointDistanceFromPlayer = 4f;
//	private const float MinPathLength = 2f;
//	private const float FovMin = 35f;
//	private const float FovMax = 55f;
//	protected const float MaxLookAtAngle = 20f;

//	private Vector3 orbitCenter;
//	private float orbitHeightSrc;
//	private float orbitHeightDst;
//	private float currentOrbitRadius;
//	private float orbitStartAngle;
//	private float orbitEndAngle;
//	private float currentFovMax;
//	private Vector3 controlPoint;

//	// Enum for cinematic modes
//	private enum CinemaMode
//	{
//		Orbit = 0,
//		Path = 1,
//		DollyZoom = 2
//	}

//	private CinemaMode currentMode;

//	public override void Reset()
//	{
//		base.Reset();
//		controlPoint = Vector3.zero;
//		currentFovMax = FovMax;
//		orbitCenter = Vector3.zero;
//		orbitHeightSrc = 0f;
//		orbitHeightDst = 0f;
//		currentOrbitRadius = 0f;
//		orbitStartAngle = 0f;
//		orbitEndAngle = 0f;
//	}

//	public override void StartSequence(CinemaCameraController _controller)
//	{
//		base.StartSequence(_controller);
//		if (null == playerTransform)
//			return;

//		orbitCenter = Vector3.zero;
//		controlPoint = Vector3.zero;

//		currentMode = Random.value < 0.25f ? CinemaMode.Orbit : CinemaMode.Path;
//		currentSequenceDuration = DefaultSequenceDuration;

//		var startFocusPoint = playerTransform.position;
//		if (cinemaCameraController.focusPoints.Count > 0)
//		{
//			var validFocusPoint = cinemaCameraController.focusPoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) >= MinFocusPointDistanceFromPlayer).ToList();
//			if (validFocusPoint.Count > 0) startFocusPoint = validFocusPoint[Random.Range(0, validFocusPoint.Count)];
//		}

//		targetSrc = new Vector3(startFocusPoint.x, VerticalOffset, startFocusPoint.z);
//		targetDst = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);

//		if (Vector2.Distance(new Vector2(targetSrc.x, targetSrc.z), new Vector2(targetDst.x, targetDst.z)) <= OrbitTargetDistanceThreshold) currentMode = CinemaMode.Orbit;

//		if (currentMode == CinemaMode.Orbit)
//		{
//			orbitCenter = targetDst;
//			targetSrc = targetDst;
//			orbitStartAngle = Random.Range(0f, 360f);

//			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
//			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

//			float minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, targetDst.y);
//			float minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, targetDst.y);
//			float minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
//			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

//			originSrc = SampleOrbitPosition(orbitCenter, orbitStartAngle, 0f);
//			float maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
//			float delta = Random.Range(90f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
//			orbitEndAngle = orbitStartAngle + delta;
//			originDst = SampleOrbitPosition(orbitCenter, orbitEndAngle, 1f);
//		}
//		else
//		{
//			Vector3 targetPath = targetDst - targetSrc;
//			float pathLength = Mathf.Max(targetPath.magnitude, MinPathLength);
//			Vector3 pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere;
//			Vector3 midPoint = (targetSrc + targetDst) / 2f;
//			Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;

//			float lozengeMajor = (pathLength + 2f * MinOrbitRadius) / 2f;
//			float lozengeMinor = MinOrbitRadius;

//			if (DEBUG_USE_SPLINES)
//			{
//				var (src, dst, ctrl) = SampleSplineCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor, targetSrc, targetDst);
//				originSrc = src;
//				originDst = dst;
//				controlPoint = ctrl;

//				originSrc = AdjustHeight(originSrc, targetSrc);
//				originDst = AdjustHeight(originDst, targetDst);
//				controlPoint = AdjustHeight(controlPoint, (targetSrc + targetDst) / 2f);
//			}
//			else
//			{
//				var (src, dst) = SampleTangentCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor, targetSrc, targetDst);
//				originSrc = src;
//				originDst = dst;

//				originSrc = AdjustHeight(originSrc, targetSrc);
//				originDst = AdjustHeight(originDst, targetDst);
//			}
//		}

//		currentFovMax = Random.value < 0.2f ? 60f : FovMax;
//	}

//	protected override (Vector3 transOrigin, Vector3 transTarget, float fov) ComputeSequencePositionsAndFov(float easedT, Vector3 playerDelta)
//	{
//		cinemaCameraController.cameraData.smoothing = SmoothingUtils.Smooth(cinemaCameraController.cameraData.smoothing, 16, currentSequenceDuration, Time.deltaTime, CinemaCameraController.TargetFPS);

//		Vector3 transOrigin;
//		Vector3 transTarget;
//		float fov;

//		if (currentMode == CinemaMode.Orbit)
//		{
//			targetSrc = new Vector3(playerTransform.position.x, VerticalOffset, playerTransform.position.z);
//			targetDst = targetSrc;
//			orbitCenter += playerDelta;
//			transOrigin = SampleOrbitPosition(orbitCenter, Mathf.Lerp(orbitStartAngle, orbitEndAngle, easedT), easedT);
//			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);

//			var fovT = SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration);
//			fov = Mathf.Lerp(FovMin, currentFovMax, fovT);
//		}
//		else
//		{
//			originDst += playerDelta;
//			targetDst += playerDelta;

//			if (DEBUG_USE_SPLINES)
//			{
//				controlPoint += playerDelta;
//				transOrigin = EvaluateQuadraticBezier(easedT, originSrc, controlPoint, originDst);
//				if (DEBUG_VISUALIZE_BEZIER)
//				{
//					VisualizeBezierPath(originSrc, controlPoint, originDst);
//					Debug.DrawLine(originSrc, controlPoint, Color.blue, 0.1f);
//					Debug.DrawLine(controlPoint, originDst, Color.blue, 0.1f);
//				}
//			}
//			else
//			{
//				transOrigin = Vector3.Lerp(originSrc, originDst, easedT);
//				if (DEBUG_VISUALIZE_BEZIER)
//				{
//					Debug.DrawLine(originSrc, originDst, Color.blue, 0.1f);
//				}
//			}

//			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);
//			var fovT = SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration);
//			fov = Mathf.Lerp(FovMin, currentFovMax, fovT);
//		}

//		originDst = transOrigin;
//		targetDst = transTarget;
//		cinemaCameraController.cameraData.fieldOfView = fov;

//		return (transOrigin, transTarget, fov);
//	}

//	private float CalculateMinOrbitRadius(float cameraHeight, float targetY)
//	{
//		float heightDiff = cameraHeight - (targetY + VerticalOffset);
//		if (heightDiff <= 0f) return MaxOrbitRadius;
//		float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
//		return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
//	}

//	private (Vector3 src, Vector3 dst, Vector3 ctrl) SampleSplineCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, Vector3 targetSrc, Vector3 targetDst)
//	{
//		float referenceDist = lozengeMinor * EllipsoidMinorAxisScale + 1f;
//		Vector2 referenceXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * referenceDist * (Random.value < 0.5f ? 1f : -1f);

//		float srcHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
//		float dstHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
//		float baseHeight = (srcHeight + dstHeight) / 2f;

//		float maxHeightDeviation = 1.5f;
//		float heightDeviation = Random.Range(0f, maxHeightDeviation);
//		float controlHeight = baseHeight + heightDeviation;
//		controlHeight = Mathf.Clamp(controlHeight, MinCameraHeight, MaxCameraHeight);

//		Vector3 control = new Vector3(referenceXZ.x, controlHeight, referenceXZ.y);

//		Vector2 midPointXZ = new Vector2(midPoint.x, midPoint.z);
//		Vector2 relativeXZ = referenceXZ - midPointXZ;
//		float a = lozengeMajor;
//		float b = lozengeMinor;
//		float scale = Mathf.Sqrt((a * a * b * b) / (b * b * relativeXZ.x * relativeXZ.x + a * a * relativeXZ.y * relativeXZ.y));
//		Vector2 tangencyXZ = midPointXZ + relativeXZ * scale;
//		Vector3 tangencyPoint = new Vector3(tangencyXZ.x, control.y, tangencyXZ.y);

//		float x = tangencyXZ.x - midPointXZ.x;
//		float y = tangencyXZ.y - midPointXZ.y;
//		float tangentSlope = -(b * b * x) / (a * a * y + 0.001f);
//		Vector2 tangentDir = new Vector2(1f, tangentSlope).normalized;
//		Vector3 tangent = tangentDir.x * pathDir + tangentDir.y * perpendicular;

//		float offsetRange = lozengeMajor * EllipsoidMajorAxisScale;
//		float offsetSrc = Random.Range(-offsetRange, -offsetRange / 2f);
//		float offsetDst = Random.Range(offsetRange / 2f, offsetRange);
//		Vector3 initialSrc = tangencyPoint + tangent * offsetSrc;
//		Vector3 initialDst = tangencyPoint + tangent * offsetDst;

//		initialSrc.y = srcHeight;
//		initialDst.y = dstHeight;

//		float extensionFactor = Random.Range(0f, 0.25f);
//		float srcExtension = Mathf.Abs(offsetSrc) * extensionFactor;
//		float dstExtension = offsetDst * extensionFactor;
//		Vector3 extendedSrc = tangencyPoint + tangent * (offsetSrc - srcExtension);
//		Vector3 extendedDst = tangencyPoint + tangent * (offsetDst + dstExtension);

//		extendedSrc.y = srcHeight;
//		extendedDst.y = dstHeight;

//		float minRadiusSrc = CalculateMinOrbitRadius(extendedSrc.y, targetSrc.y);
//		float minRadiusDst = CalculateMinOrbitRadius(extendedDst.y, targetDst.y);
//		Vector2 srcXZ = new Vector2(extendedSrc.x, extendedSrc.z);
//		Vector2 dstXZ = new Vector2(extendedDst.x, extendedDst.z);
//		Vector2 targetSrcXZ = new Vector2(targetSrc.x, targetSrc.z);
//		Vector2 targetDstXZ = new Vector2(targetDst.x, targetDst.z);

//		if (Vector2.Distance(srcXZ, targetSrcXZ) < minRadiusSrc)
//		{
//			Vector2 dir = (srcXZ - targetSrcXZ).normalized;
//			srcXZ = targetSrcXZ + dir * minRadiusSrc;
//			extendedSrc.x = srcXZ.x;
//			extendedSrc.z = srcXZ.y;
//		}
//		if (Vector2.Distance(dstXZ, targetDstXZ) < minRadiusDst)
//		{
//			Vector2 dir = (dstXZ - targetDstXZ).normalized;
//			dstXZ = targetDstXZ + dir * minRadiusDst;
//			extendedDst.x = dstXZ.x;
//			extendedDst.z = dstXZ.y;
//		}

//		control.y = controlHeight;
//		return (extendedSrc, extendedDst, control);
//	}

//	private (Vector3 src, Vector3 dst) SampleTangentCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, Vector3 targetSrc, Vector3 targetDst)
//	{
//		float referenceDist = lozengeMinor * EllipsoidMinorAxisScale + 1f;
//		Vector2 referenceXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * referenceDist * (Random.value < 0.5f ? 1f : -1f);

//		Vector2 midPointXZ = new Vector2(midPoint.x, midPoint.z);
//		Vector2 relativeXZ = referenceXZ - midPointXZ;
//		float a = lozengeMajor;
//		float b = lozengeMinor;
//		float x = relativeXZ.x;
//		float y = relativeXZ.y;
//		float tangentSlope = -(b * b * x) / (a * a * y + 0.001f);
//		Vector2 tangentDir = new Vector2(1f, tangentSlope).normalized;

//		Vector3 tangent = tangentDir.x * pathDir + tangentDir.y * perpendicular;

//		float offsetRange = lozengeMajor * EllipsoidMajorAxisScale;
//		float offsetSrc = Random.Range(-offsetRange, 0f);
//		float offsetDst = Random.Range(0f, offsetRange);
//		Vector3 src = new Vector3(referenceXZ.x, 0f, referenceXZ.y) + tangent * offsetSrc;
//		Vector3 dst = new Vector3(referenceXZ.x, 0f, referenceXZ.y) + tangent * offsetDst;

//		src.y = Random.Range(MinCameraHeight, MaxCameraHeight);
//		dst.y = Random.Range(MinCameraHeight, MaxCameraHeight);

//		float minRadiusSrc = CalculateMinOrbitRadius(src.y, targetSrc.y);
//		float minRadiusDst = CalculateMinOrbitRadius(dst.y, targetDst.y);
//		Vector2 srcXZ = new Vector2(src.x, src.z);
//		Vector2 dstXZ = new Vector2(dst.x, dst.z);
//		Vector2 targetSrcXZ = new Vector2(targetSrc.x, targetSrc.z);
//		Vector2 targetDstXZ = new Vector2(targetDst.x, targetDst.z);

//		if (Vector2.Distance(srcXZ, targetSrcXZ) < minRadiusSrc)
//		{
//			Vector2 dir = (srcXZ - targetSrcXZ).normalized;
//			srcXZ = targetSrcXZ + dir * minRadiusSrc;
//			src.x = srcXZ.x;
//			src.z = srcXZ.y;
//		}
//		if (Vector2.Distance(dstXZ, targetDstXZ) < minRadiusDst)
//		{
//			Vector2 dir = (dstXZ - targetDstXZ).normalized;
//			dstXZ = targetDstXZ + dir * minRadiusDst;
//			dst.x = dstXZ.x;
//			dst.z = dstXZ.y;
//		}

//		return (src, dst);
//	}

//	private Vector3 SampleOrbitPosition(Vector3 center, float angleDegrees, float easedT)
//	{
//		float angleRad = angleDegrees * Mathf.Deg2Rad;
//		Vector3 offset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * currentOrbitRadius;
//		Vector3 position = center + offset;
//		position.y = Mathf.Lerp(orbitHeightSrc, orbitHeightDst, SmoothingUtils.Ease(easedT));
//		position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);
//		return position;
//	}

//	private void VisualizeBezierPath(Vector3 p0, Vector3 p1, Vector3 p2)
//	{
//		const int segments = 20;
//		Vector3 prevPoint = p0;
//		for (int i = 1; i <= segments; i++)
//		{
//			float t = i / (float)segments;
//			Vector3 point = EvaluateQuadraticBezier(t, p0, p1, p2);
//			Debug.DrawLine(prevPoint, point, Color.magenta, 0.1f);
//			prevPoint = point;
//		}
//	}

//	private Vector3 AdjustHeight(Vector3 position, Vector3 target)
//	{
//		float minRadius = CalculateMinOrbitRadius(position.y, target.y);
//		Vector2 positionXZ = new Vector2(position.x, position.z);
//		Vector2 targetXZ = new Vector2(target.x, target.z);
//		float distXZ = Vector2.Distance(positionXZ, targetXZ);

//		if (distXZ < minRadius)
//		{
//			Vector2 directionXZ = (positionXZ - targetXZ).normalized;
//			positionXZ = targetXZ + directionXZ * minRadius;
//			position.x = positionXZ.x;
//			position.z = positionXZ.y;
//		}

//		Vector3 direction = (target - position).normalized;
//		float pitch = Vector3.Angle(direction, Vector3.down) - 90f;
//		if (pitch > MaxLookAtAngle)
//		{
//			float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
//			float idealHeight = target.y + VerticalOffset + distXZ / Mathf.Tan(maxPitchRad);
//			position.y = Mathf.Clamp(idealHeight, MinCameraHeight, MaxCameraHeight);
//		}
//		return position;
//	}

//	private Vector3 EvaluateQuadraticBezier(float t, Vector3 p0, Vector3 p1, Vector3 p2)
//	{
//		float u = 1f - t;
//		return u * u * p0 + 2f * u * t * p1 + t * t * p2;
//	}
//}

////bool IsValidCameraPath()
////{
////	Vector3 startLookDir = (targetSrc - originSrc).normalized;
////	Vector3 endLookDir = (targetDst - originDst).normalized;
////	float startPitch = Vector3.Angle(startLookDir, Vector3.down) - 90f;
////	float endPitch = Vector3.Angle(endLookDir, Vector3.down) - 90f;
////	return startPitch <= MaxLookAtAngle && endPitch <= MaxLookAtAngle;
////}
