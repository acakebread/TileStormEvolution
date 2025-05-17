using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static CinemaCameraController;

public abstract class CinemaCameraBase
{
	// Shared constants
	protected const float CinemaTimeout = 5f;
	protected const float DefaultSequenceDuration = 8f;
	protected const float MinCameraHeight = 1.5f;
	protected const float MaxCameraHeight = 4f;
	protected const float MaxLookAtAngle = 20f;
	protected const float FovMin = 35f;
	protected const float FovMax = 55f;
	protected const int MaxPlayerPositions = 50;
	protected const float MinPoiDistanceFromPlayer = 4f;
	protected const float MinDistanceForNewPoi = 3f;
	protected const float EllipsoidMajorAxisScale = 1.5f;
	protected const float EllipsoidMinorAxisScale = 1.5f;
	protected const float MinOrbitRadius = 2f;
	protected const float MaxOrbitRadius = 8f;
	protected const float OrbitTargetDistanceThreshold = 0.5f;
	protected const float TargetFPS = 60f;
	protected const float ProjectionSmoothingRate = 16f;
	protected const float MinPathLength = 2f;
	protected const float verticalOffset = 0.5f;
	protected const bool useSplines = true;
	protected const bool debugVisualizeBezier = false;

	// Shared state
	protected float sequenceTimer;
	protected Transform playerTransform;
	protected List<Vector3> waypoints = new List<Vector3>();
	protected List<Vector3> playerPositions = new List<Vector3>();
	protected Vector3 originSrc;
	protected Vector3 originDst;
	protected Vector3 targetSrc;
	protected Vector3 targetDst;
	protected float currentFovMax;
	protected Vector2 mapExtentsMin;
	protected Vector2 mapExtentsMax;
	protected bool isOrbit;
	protected Vector2 endTargetOffset;
	protected float orbitStartAngle;
	protected float orbitEndAngle;
	protected Vector3 lastPlayerPos;
	protected Vector3 smoothedProjectedOffset;
	protected Vector3 orbitCenter;
	protected float orbitHeightSrc;
	protected float orbitHeightDst;
	protected float currentOrbitRadius;
	protected Vector3 controlPoint;
	protected float currentSequenceDuration;


	protected CinemaMode currentMode; // NEW: Track current mode

	// Public properties
	public float CinemaTimeoutDuration => CinemaTimeout;

	// Calculate minimum orbit radius based on height and MaxLookAtAngle
	protected float CalculateMinOrbitRadius(float cameraHeight, float targetY)
	{
		float heightDiff = cameraHeight - (targetY + verticalOffset);
		if (heightDiff <= 0f) return MaxOrbitRadius;
		float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
		return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
	}

	public virtual void Reset()
	{
		sequenceTimer = 0f;
		playerTransform = null;
		waypoints.Clear();
		playerPositions.Clear();
		originSrc = Vector3.zero;
		targetSrc = Vector3.zero;
		originDst = Vector3.zero;
		targetDst = Vector3.zero;
		currentFovMax = FovMax;
		mapExtentsMin = new Vector2(float.MaxValue, float.MaxValue);
		mapExtentsMax = new Vector2(float.MinValue, float.MinValue);
		isOrbit = false;
		endTargetOffset = Vector2.zero;
		orbitStartAngle = 0f;
		orbitEndAngle = 0f;
		lastPlayerPos = Vector3.zero;
		smoothedProjectedOffset = Vector3.zero;
		orbitCenter = Vector3.zero;
		orbitHeightSrc = 0f;
		orbitHeightDst = 0f;
		controlPoint = Vector3.zero;
		currentSequenceDuration = DefaultSequenceDuration;
	}

	public void SetWaypoints(List<Vector3> newWaypoints)
	{
		waypoints = newWaypoints?.Where(p => p != Vector3.zero && Vector3.Distance(p, Vector3.zero) > 0.1f).ToList() ?? new List<Vector3>();
		UpdateMapExtents();
	}

	public void UpdatePlayerTransform(Transform transform)
	{
		playerTransform = transform;
		if (playerTransform != null && IsFarEnough(playerTransform.position))
		{
			playerPositions.Add(playerTransform.position);
			if (playerPositions.Count > MaxPlayerPositions)
				playerPositions.RemoveAt(0);
		}
		UpdateMapExtents();
	}

	protected void UpdateMapExtents()
	{
		mapExtentsMin = new Vector2(float.MaxValue, float.MaxValue);
		mapExtentsMax = new Vector2(float.MinValue, float.MinValue);

		if (waypoints.Count > 0)
		{
			mapExtentsMin.x = waypoints.Min(p => p.x);
			mapExtentsMin.y = waypoints.Min(p => p.z);
			mapExtentsMax.x = waypoints.Max(p => p.x);
			mapExtentsMax.y = waypoints.Max(p => p.z);
		}

		if (playerTransform != null)
		{
			Vector3 pos = playerTransform.position;
			mapExtentsMin.x = Mathf.Min(mapExtentsMin.x, pos.x);
			mapExtentsMin.y = Mathf.Min(mapExtentsMin.y, pos.z);
			mapExtentsMax.x = Mathf.Max(mapExtentsMax.x, pos.x);
			mapExtentsMax.y = Mathf.Max(mapExtentsMax.y, pos.z);
		}
	}

	public abstract void StartNewCinemaSequence(Vector3 playerPos, List<Vector3> waypoints);

	protected (Vector3 src, Vector3 dst, Vector3 ctrl) SampleSplineCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, Vector3 targetSrc, Vector3 targetDst)
	{
		float referenceDist = lozengeMinor * EllipsoidMinorAxisScale + 1f;
		Vector2 referenceXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * referenceDist * (Random.value < 0.5f ? 1f : -1f);

		float srcHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
		float dstHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
		float baseHeight = (srcHeight + dstHeight) / 2f;

		float maxHeightDeviation = 1.5f;
		float heightDeviation = Random.Range(0f, maxHeightDeviation);
		float controlHeight = baseHeight + heightDeviation;
		controlHeight = Mathf.Clamp(controlHeight, MinCameraHeight, MaxCameraHeight);

		Vector3 control = new Vector3(referenceXZ.x, controlHeight, referenceXZ.y);

		Vector2 midPointXZ = new Vector2(midPoint.x, midPoint.z);
		Vector2 relativeXZ = referenceXZ - midPointXZ;
		float a = lozengeMajor;
		float b = lozengeMinor;
		float scale = Mathf.Sqrt((a * a * b * b) / (b * b * relativeXZ.x * relativeXZ.x + a * a * relativeXZ.y * relativeXZ.y));
		Vector2 tangencyXZ = midPointXZ + relativeXZ * scale;
		Vector3 tangencyPoint = new Vector3(tangencyXZ.x, control.y, tangencyXZ.y);

		float x = tangencyXZ.x - midPointXZ.x;
		float y = tangencyXZ.y - midPointXZ.y;
		float tangentSlope = -(b * b * x) / (a * a * y + 0.001f);
		Vector2 tangentDir = new Vector2(1f, tangentSlope).normalized;
		Vector3 tangent = tangentDir.x * pathDir + tangentDir.y * perpendicular;

		float offsetRange = lozengeMajor * EllipsoidMajorAxisScale;
		float offsetSrc = Random.Range(-offsetRange, -offsetRange / 2f);
		float offsetDst = Random.Range(offsetRange / 2f, offsetRange);
		Vector3 initialSrc = tangencyPoint + tangent * offsetSrc;
		Vector3 initialDst = tangencyPoint + tangent * offsetDst;

		initialSrc.y = srcHeight;
		initialDst.y = dstHeight;

		float extensionFactor = Random.Range(0f, 0.25f);
		float srcExtension = Mathf.Abs(offsetSrc) * extensionFactor;
		float dstExtension = offsetDst * extensionFactor;
		Vector3 extendedSrc = tangencyPoint + tangent * (offsetSrc - srcExtension);
		Vector3 extendedDst = tangencyPoint + tangent * (offsetDst + dstExtension);

		extendedSrc.y = srcHeight;
		extendedDst.y = dstHeight;

		float minRadiusSrc = CalculateMinOrbitRadius(extendedSrc.y, targetSrc.y);
		float minRadiusDst = CalculateMinOrbitRadius(extendedDst.y, targetDst.y);
		Vector2 srcXZ = new Vector2(extendedSrc.x, extendedSrc.z);
		Vector2 dstXZ = new Vector2(extendedDst.x, extendedDst.z);
		Vector2 targetSrcXZ = new Vector2(targetSrc.x, targetSrc.z);
		Vector2 targetDstXZ = new Vector2(targetDst.x, targetDst.z);

		if (Vector2.Distance(srcXZ, targetSrcXZ) < minRadiusSrc)
		{
			Vector2 dir = (srcXZ - targetSrcXZ).normalized;
			srcXZ = targetSrcXZ + dir * minRadiusSrc;
			extendedSrc.x = srcXZ.x;
			extendedSrc.z = srcXZ.y;
		}
		if (Vector2.Distance(dstXZ, targetDstXZ) < minRadiusDst)
		{
			Vector2 dir = (dstXZ - targetDstXZ).normalized;
			dstXZ = targetDstXZ + dir * minRadiusDst;
			extendedDst.x = dstXZ.x;
			extendedDst.z = dstXZ.y;
		}

		control.y = controlHeight;
		return (extendedSrc, extendedDst, control);
	}

	protected (Vector3 src, Vector3 dst) SampleTangentCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, Vector3 targetSrc, Vector3 targetDst)
	{
		float referenceDist = lozengeMinor * EllipsoidMinorAxisScale + 1f;
		Vector2 referenceXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * referenceDist * (Random.value < 0.5f ? 1f : -1f);

		Vector2 midPointXZ = new Vector2(midPoint.x, midPoint.z);
		Vector2 relativeXZ = referenceXZ - midPointXZ;
		float a = lozengeMajor;
		float b = lozengeMinor;
		float x = relativeXZ.x;
		float y = relativeXZ.y;
		float tangentSlope = -(b * b * x) / (a * a * y + 0.001f);
		Vector2 tangentDir = new Vector2(1f, tangentSlope).normalized;

		Vector3 tangent = tangentDir.x * pathDir + tangentDir.y * perpendicular;

		float offsetRange = lozengeMajor * EllipsoidMajorAxisScale;
		float offsetSrc = Random.Range(-offsetRange, 0f);
		float offsetDst = Random.Range(0f, offsetRange);
		Vector3 src = new Vector3(referenceXZ.x, 0f, referenceXZ.y) + tangent * offsetSrc;
		Vector3 dst = new Vector3(referenceXZ.x, 0f, referenceXZ.y) + tangent * offsetDst;

		src.y = Random.Range(MinCameraHeight, MaxCameraHeight);
		dst.y = Random.Range(MinCameraHeight, MaxCameraHeight);

		float minRadiusSrc = CalculateMinOrbitRadius(src.y, targetSrc.y);
		float minRadiusDst = CalculateMinOrbitRadius(dst.y, targetDst.y);
		Vector2 srcXZ = new Vector2(src.x, src.z);
		Vector2 dstXZ = new Vector2(dst.x, dst.z);
		Vector2 targetSrcXZ = new Vector2(targetSrc.x, targetSrc.z);
		Vector2 targetDstXZ = new Vector2(targetDst.x, targetDst.z);

		if (Vector2.Distance(srcXZ, targetSrcXZ) < minRadiusSrc)
		{
			Vector2 dir = (srcXZ - targetSrcXZ).normalized;
			srcXZ = targetSrcXZ + dir * minRadiusSrc;
			src.x = srcXZ.x;
			src.z = srcXZ.y;
		}
		if (Vector2.Distance(dstXZ, targetDstXZ) < minRadiusDst)
		{
			Vector2 dir = (dstXZ - targetDstXZ).normalized;
			dstXZ = targetDstXZ + dir * minRadiusDst;
			dst.x = dstXZ.x;
			dst.z = dstXZ.y;
		}

		return (src, dst);
	}

	protected bool IsValidCameraPath()
	{
		Vector3 startLookDir = (targetSrc - originSrc).normalized;
		Vector3 endLookDir = (targetDst - originDst).normalized;
		float startPitch = Vector3.Angle(startLookDir, Vector3.down) - 90f;
		float endPitch = Vector3.Angle(endLookDir, Vector3.down) - 90f;
		return startPitch <= MaxLookAtAngle && endPitch <= MaxLookAtAngle;
	}

	protected bool IsFarEnough(Vector3 position)
	{
		foreach (var wp in waypoints)
		{
			if (Vector3.Distance(position, wp) < MinDistanceForNewPoi)
				return false;
		}
		foreach (var pp in playerPositions)
		{
			if (Vector3.Distance(position, pp) < MinDistanceForNewPoi)
				return false;
		}
		return true;
	}

	protected void AdjustHeight(ref Vector3 position, Vector3 target)
	{
		float minRadius = CalculateMinOrbitRadius(position.y, target.y);
		Vector2 positionXZ = new Vector2(position.x, position.z);
		Vector2 targetXZ = new Vector2(target.x, target.z);
		float distXZ = Vector2.Distance(positionXZ, targetXZ);

		if (distXZ < minRadius)
		{
			Vector2 directionXZ = (positionXZ - targetXZ).normalized;
			positionXZ = targetXZ + directionXZ * minRadius;
			position.x = positionXZ.x;
			position.z = positionXZ.y;
		}

		Vector3 direction = (target - position).normalized;
		float pitch = Vector3.Angle(direction, Vector3.down) - 90f;
		if (pitch > MaxLookAtAngle)
		{
			float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
			float idealHeight = target.y + verticalOffset + distXZ / Mathf.Tan(maxPitchRad);
			position.y = Mathf.Clamp(idealHeight, MinCameraHeight, MaxCameraHeight);
		}
	}

	protected Vector3 EvaluateQuadraticBezier(float t, Vector3 p0, Vector3 p1, Vector3 p2)
	{
		float u = 1f - t;
		return u * u * p0 + 2f * u * t * p1 + t * t * p2;
	}

	protected void VisualizeBezierPath(Vector3 p0, Vector3 p1, Vector3 p2)
	{
		const int segments = 20;
		Vector3 prevPoint = p0;
		for (int i = 1; i <= segments; i++)
		{
			float t = i / (float)segments;
			Vector3 point = EvaluateQuadraticBezier(t, p0, p1, p2);
			Debug.DrawLine(prevPoint, point, Color.magenta, 0.1f);
			prevPoint = point;
		}
	}

	public CameraController.CameraData GetCinemaData(CameraController.CameraData data)
	{
		data.originSrc = originSrc;
		data.targetSrc = targetSrc;
		data.smoothingRate = 64f;
		return data;
	}

	public abstract CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera);

	protected Vector3 SampleOrbitPosition(Vector3 center, float angleDegrees, float easedT)
	{
		float angleRad = angleDegrees * Mathf.Deg2Rad;
		Vector3 offset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * currentOrbitRadius;
		Vector3 position = center + offset;
		position.y = Mathf.Lerp(orbitHeightSrc, orbitHeightDst, SmoothingUtils.Ease(easedT));
		position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);
		return position;
	}
}