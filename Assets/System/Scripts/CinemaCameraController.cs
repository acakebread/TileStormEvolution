using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CinemaCameraController
{
	// Cinema-specific constants
	private const float CinemaTimeout = 5f;
	private const float SequenceDuration = 8f;
	private const float PauseDuration = 1f;
	private const float MinCameraHeight = 1.5f;
	private const float MaxCameraHeight = 4f;
	private const float MaxLookAtAngle = 20f;
	private const float FovMin = 35f;
	private const float FovMax = 55f;
	private const int MaxPlayerPositions = 50;
	private const float MinPoiDistanceFromPlayer = 4f;
	private const float MinDistanceForNewPoi = 3f;
	private const float EllipsoidMajorAxisScale = 1.5f;
	private const float EllipsoidMinorAxisScale = 1.5f;
	private const float MinOrbitRadius = 2f;
	private const float MaxOrbitRadius = 8f;
	private const float OrbitTargetDistanceThreshold = 0.5f;
	private const float TargetFPS = 60f;
	private const float ProjectionSmoothingRate = 16f;
	private const float MinPathLength = 2f;
	private const float verticalOffset = 0.5f;

	// State
	private float sequenceTimer;
	private float pauseTimer;
	private Vector3 playerPos;
	private List<Vector3> waypoints = new List<Vector3>();
	private List<Vector3> playerPositions = new List<Vector3>();
	private Vector3 originSrc;
	private Vector3 originDst;
	private Vector3 targetSrc;
	private Vector3 targetDst;
	private float currentFovMax;
	private Vector2 mapExtentsMin;
	private Vector2 mapExtentsMax;
	private bool isOrbit;
	private Vector2 endTargetOffset;
	private float orbitStartAngle;
	private float orbitEndAngle;
	private Vector3 lastPlayerPos;
	private Vector3 smoothedProjectedOffset;
	private Vector3 orbitCenter;
	private float orbitHeightSrc;
	private float orbitHeightDst;
	private float currentOrbitRadius;

	// Public properties
	public float CinemaTimeoutDuration => CinemaTimeout;

	// Calculate minimum orbit radius based on height and MaxLookAtAngle
	private float CalculateMinOrbitRadius(float cameraHeight, float targetY)
	{
		float heightDiff = cameraHeight - (targetY + verticalOffset);
		if (heightDiff <= 0f) return MaxOrbitRadius;
		float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
		return Mathf.Max(MinOrbitRadius, heightDiff / Mathf.Tan(maxPitchRad));
	}

	public void Reset()
	{
		sequenceTimer = 0f;
		pauseTimer = 0f;
		playerPos = Vector3.zero;
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
	}

	public void SetWaypoints(List<Vector3> newWaypoints)
	{
		waypoints = newWaypoints?.Where(p => p != Vector3.zero && Vector3.Distance(p, Vector3.zero) > 0.1f).ToList() ?? new List<Vector3>();
		UpdateMapExtents();
	}

	public void UpdatePlayerPosition(Vector3 position)
	{
		playerPos = position;
		if (IsFarEnough(position))
		{
			playerPositions.Add(position);
			if (playerPositions.Count > MaxPlayerPositions)
				playerPositions.RemoveAt(0);
		}
		UpdateMapExtents();
	}

	private void UpdateMapExtents()
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

		if (playerPos != Vector3.zero)
		{
			mapExtentsMin.x = Mathf.Min(mapExtentsMin.x, playerPos.x);
			mapExtentsMin.y = Mathf.Min(mapExtentsMin.y, playerPos.z);
			mapExtentsMax.x = Mathf.Max(mapExtentsMax.x, playerPos.x);
			mapExtentsMax.y = Mathf.Max(mapExtentsMax.y, playerPos.z);
		}
	}

	public void StartNewCinemaSequence(Vector3 playerPos, List<Vector3> waypoints)
	{
		sequenceTimer = 0f;
		pauseTimer = 0f;
		this.playerPos = playerPos;
		this.waypoints = new List<Vector3>(waypoints);
		lastPlayerPos = playerPos;
		smoothedProjectedOffset = Vector3.zero;
		orbitCenter = Vector3.zero;

		// Select start POI (66% chance for POI, 33% for player)
		Vector3 startPoi;
		if (Random.value < 0.66f && waypoints.Count > 0)
		{
			var validPois = waypoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)) >= MinPoiDistanceFromPlayer).ToList();
			startPoi = validPois.Count > 0 ? validPois[Random.Range(0, validPois.Count)] : playerPos;
		}
		else
		{
			startPoi = playerPos;
		}

		// Set targets
		targetSrc = new Vector3(startPoi.x, verticalOffset, startPoi.z);
		endTargetOffset = Random.insideUnitCircle * 0.5f;
		targetDst = new Vector3(playerPos.x + endTargetOffset.x, verticalOffset, playerPos.z + endTargetOffset.y);

		// Check if targets are effectively the same (for orbit behavior)
		isOrbit = Vector2.Distance(new Vector2(targetSrc.x, targetSrc.z), new Vector2(targetDst.x, targetDst.z)) <= OrbitTargetDistanceThreshold;

		if (isOrbit)
		{
			orbitCenter = targetDst;
			targetSrc = targetDst;
			orbitStartAngle = Random.Range(0f, 360f);

			// Randomize orbit heights
			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight);
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight);

			// Calculate orbit radius based on the stricter height to ensure pitch constraint
			float minRadiusSrc = CalculateMinOrbitRadius(orbitHeightSrc, targetDst.y);
			float minRadiusDst = CalculateMinOrbitRadius(orbitHeightDst, targetDst.y);
			float minRadius = Mathf.Max(minRadiusSrc, minRadiusDst);
			currentOrbitRadius = Random.Range(Mathf.Max(minRadius, MinOrbitRadius), MaxOrbitRadius);

			// Generate positions directly
			originSrc = SampleOrbitPosition(orbitCenter, orbitStartAngle, 0f);
			float maxDelta = Mathf.Lerp(360f, 180f, (currentOrbitRadius - MinOrbitRadius) / (MaxOrbitRadius - MinOrbitRadius));
			float delta = Random.Range(90f, maxDelta) * (Random.value < 0.5f ? 1f : -1f);
			orbitEndAngle = orbitStartAngle + delta;
			originDst = SampleOrbitPosition(orbitCenter, orbitEndAngle, 1f);
		}
		else
		{
			// Standard path with tangent-based sampling
			Vector3 targetPath = targetDst - targetSrc;
			float pathLength = Mathf.Max(targetPath.magnitude, MinPathLength);
			Vector3 pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere;
			Vector3 midPoint = (targetSrc + targetDst) / 2f;
			Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;

			// Define lozenge ellipsoid
			float lozengeMajor = (pathLength + 2f * MinOrbitRadius) / 2f;
			float lozengeMinor = MinOrbitRadius;

			// Sample tangent-based camera positions
			var (src, dst) = SampleCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor, targetSrc, targetDst);
			originSrc = src;
			originDst = dst;

			AdjustHeight(ref originSrc, targetSrc);
			AdjustHeight(ref originDst, targetDst);
		}

		UpdateMapExtents();
		currentFovMax = Random.value < 0.2f ? 60f : FovMax;
	}

	private (Vector3 src, Vector3 dst) SampleCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor, Vector3 targetSrc, Vector3 targetDst)
	{
		// Pick reference point outside lozenge (in XZ plane)
		float referenceDist = lozengeMinor * EllipsoidMinorAxisScale + 1f;
		Vector2 referenceXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * referenceDist * (Random.value < 0.5f ? 1f : -1f);

		// Compute tangent to lozenge ellipse in XZ plane
		Vector2 midPointXZ = new Vector2(midPoint.x, midPoint.z);
		Vector2 relativeXZ = referenceXZ - midPointXZ;
		float a = lozengeMajor;
		float b = lozengeMinor;
		// Tangent slope: dy/dx = -(b^2 * x) / (a^2 * y)
		float x = relativeXZ.x;
		float y = relativeXZ.y;
		float tangentSlope = -(b * b * x) / (a * a * y + 0.001f); // Avoid division by zero
		Vector2 tangentDir = new Vector2(1f, tangentSlope).normalized;

		// Project tangent to world space
		Vector3 tangent = tangentDir.x * pathDir + tangentDir.y * perpendicular;

		// Generate originSrc and originDst along tangent
		float offsetRange = lozengeMajor * EllipsoidMajorAxisScale;
		float offsetSrc = Random.Range(-offsetRange, 0f); // Bias toward targetSrc
		float offsetDst = Random.Range(0f, offsetRange);  // Bias toward targetDst
		Vector3 src = new Vector3(referenceXZ.x, 0f, referenceXZ.y) + tangent * offsetSrc;
		Vector3 dst = new Vector3(referenceXZ.x, 0f, referenceXZ.y) + tangent * offsetDst;

		// Assign random heights
		src.y = Random.Range(MinCameraHeight, MaxCameraHeight);
		dst.y = Random.Range(MinCameraHeight, MaxCameraHeight);

		// Ensure minimum radius from targets
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

	private bool IsValidCameraPath()
	{
		Vector3 startLookDir = (targetSrc - originSrc).normalized;
		Vector3 endLookDir = (targetDst - originDst).normalized;
		float startPitch = Vector3.Angle(startLookDir, Vector3.down) - 90f;
		float endPitch = Vector3.Angle(endLookDir, Vector3.down) - 90f;
		return startPitch <= MaxLookAtAngle && endPitch <= MaxLookAtAngle;
	}

	private bool IsFarEnough(Vector3 position)
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

	private void AdjustHeight(ref Vector3 position, Vector3 target)
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

		// Verify pitch
		Vector3 direction = (target - position).normalized;
		float pitch = Vector3.Angle(direction, Vector3.down) - 90f;
		if (pitch > MaxLookAtAngle)
		{
			float maxPitchRad = MaxLookAtAngle * Mathf.Deg2Rad;
			float idealHeight = target.y + verticalOffset + distXZ / Mathf.Tan(maxPitchRad);
			position.y = Mathf.Clamp(idealHeight, MinCameraHeight, MaxCameraHeight);
		}
	}

	public CameraController.CameraData GetCinemaData(CameraController.CameraData data)
	{
		data.originSrc = originSrc;
		data.targetSrc = targetSrc;
		data.smoothingRate = 64f;
		return data;
	}

	public CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera)
	{
		var delta = playerPos - lastPlayerPos;
		lastPlayerPos = playerPos;

		if (pauseTimer > 0f)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer > 0f)
				UpdateDataValues(originDst, targetDst);
			else
			{
				StartNewCinemaSequence(playerPos, waypoints);
				data = GetCinemaData(data);
			}
			return data;
		}

		sequenceTimer += Time.deltaTime;
		if (sequenceTimer >= SequenceDuration)
		{
			sequenceTimer = 0f;
			pauseTimer = PauseDuration;
			return data;
		}

		var t = SequenceDuration > 0 ? Mathf.Clamp01(sequenceTimer / SequenceDuration) : 1f;
		var easedT = SmoothingUtils.Ease(t);

		Vector3 targetProjectionOffset = delta * 2f;
		smoothedProjectedOffset.x = SmoothingUtils.Smooth(smoothedProjectedOffset.x, targetProjectionOffset.x, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.y = SmoothingUtils.Smooth(smoothedProjectedOffset.y, targetProjectionOffset.y, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.z = SmoothingUtils.Smooth(smoothedProjectedOffset.z, targetProjectionOffset.z, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);

		orbitCenter += delta;
		originDst += delta;
		targetDst += delta;

		Vector3 transOrigin;
		Vector3 transTarget;

		if (isOrbit)
		{
			targetSrc = new Vector3(playerPos.x + endTargetOffset.x, verticalOffset, playerPos.z + endTargetOffset.y);
			targetDst = targetSrc;
			transOrigin = SampleOrbitPosition(orbitCenter, Mathf.Lerp(orbitStartAngle, orbitEndAngle, easedT), easedT);
			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);
		}
		else
		{
			transOrigin = Vector3.Lerp(originSrc, originDst, easedT);
			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);
		}

		UpdateDataValues(transOrigin, transTarget);

		var fovT = SmoothingUtils.EasePingPong(sequenceTimer / SequenceDuration);
		data.fov = Mathf.Lerp(FovMin, currentFovMax, fovT);
		camera.fieldOfView = data.fov;

		data.smoothingRate = SmoothingUtils.Smooth(data.smoothingRate, 16f, SequenceDuration, Time.deltaTime, TargetFPS);

		return data;

		void UpdateDataValues(Vector3 originNew, Vector3 transTarget)
		{
			var followLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothingRate, Time.deltaTime, TargetFPS);
			data.originSrc = Vector3.Lerp(data.originSrc, originNew, followLerp);
			data.targetSrc = Vector3.Lerp(data.targetSrc, transTarget, followLerp);
		}
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