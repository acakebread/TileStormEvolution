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
	private const float MaxLookAtAngle = 20f; // Maximum pitch angle
	private const float FovMin = 35f;
	private const float FovMax = 55f;
	private const int MaxPlayerPositions = 50;
	private const float MinPoiDistanceFromPlayer = 4f;
	private const float MinDistanceForNewPoi = 3f;
	private const float EllipsoidMajorAxisScale = 1.5f;
	private const float EllipsoidMinorAxisScale = 1.0f;
	private const int MaxPositionSampleAttempts = 10;
	private const float MinOrbitRadius = 2f; // Minimum orbit radius
	private const float MaxOrbitRadius = 8f; // Maximum orbit radius
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
		if (heightDiff <= 0f) return MaxOrbitRadius; // Avoid division by zero or negative height
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
		isOrbit = true; // Vector2.Distance(new Vector2(targetSrc.x, targetSrc.z), new Vector2(targetDst.x, targetDst.z)) <= OrbitTargetDistanceThreshold;

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
			float delta = Random.Range(90f, maxDelta) * (Random.value < 0.5f ? 1f : -1f); // Random direction
			orbitEndAngle = orbitStartAngle + delta;
			originDst = SampleOrbitPosition(orbitCenter, orbitEndAngle, 1f);
		}
		else
		{
			// Standard path with ellipsoid sampling
			Vector3 targetPath = targetDst - targetSrc;
			float pathLength = Mathf.Max(targetPath.magnitude, MinPathLength);
			Vector3 pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere;
			Vector3 midPoint = (targetSrc + targetDst) / 2f;
			Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;

			originSrc = SampleCameraPosition(midPoint, pathDir, perpendicular, pathLength, startPoi, true);
			AdjustHeight(ref originSrc, targetSrc);

			originDst = SampleCameraPosition(midPoint, pathDir, perpendicular, pathLength, playerPos, false);
			AdjustHeight(ref originDst, targetDst);
		}

		UpdateMapExtents();
		currentFovMax = Random.value < 0.2f ? 60f : FovMax;
	}

	private Vector3 SampleCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float pathLength, Vector3 target, bool isStart)
	{
		float majorAxis = pathLength * EllipsoidMajorAxisScale;
		float minorAxis = pathLength * EllipsoidMinorAxisScale;
		float heightAxis = MaxCameraHeight - MinCameraHeight;

		for (int i = 0; i < MaxPositionSampleAttempts; i++)
		{
			Vector3 position = SampleEllipsoidPosition(midPoint, pathDir, perpendicular, majorAxis, minorAxis, heightAxis);
			float minRadius = CalculateMinOrbitRadius(position.y, target.y);
			if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(target.x, target.z)) < minRadius)
				continue;

			if (isStart)
			{
				originSrc = position;
				if (IsValidStartPosition(position) && IsValidCameraPath())
					return position;
			}
			else
			{
				originDst = position;
				if (IsValidCameraPath())
					return position;
			}
		}

		// Fallback: Offset from target by dynamic radius
		Vector2 targetXZ = new Vector2(target.x, target.z);
		float fallbackHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
		float fallbackRadius = CalculateMinOrbitRadius(fallbackHeight, target.y);
		Vector2 offsetDir = Random.insideUnitCircle.normalized * fallbackRadius;
		Vector2 fallbackXZ = targetXZ + offsetDir;
		return new Vector3(fallbackXZ.x, fallbackHeight, fallbackXZ.y);
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

	private static Vector3 SampleEllipsoidPosition(Vector3 center, Vector3 majorAxisDir, Vector3 minorAxisDir, float majorAxis, float minorAxis, float heightAxis)
	{
		Vector3 unitSpherePoint = Random.insideUnitSphere;
		float x = unitSpherePoint.x * majorAxis;
		float y = unitSpherePoint.y * heightAxis / 2f;
		float z = unitSpherePoint.z * minorAxis;
		Vector3 position = center + majorAxisDir * x + Vector3.up * y + minorAxisDir * z;
		position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);
		return position;
	}

	private bool IsValidStartPosition(Vector3 position)
	{
		Vector2 posXZ = new Vector2(position.x, position.z);
		Vector2 startTargetXZ = new Vector2(targetSrc.x, targetSrc.z);
		Vector2 endTargetXZ = new Vector2(targetDst.x, targetDst.z);
		Vector2 toStartTarget = startTargetXZ - posXZ;
		Vector2 toEndTarget = endTargetXZ - posXZ;
		float dot = Vector2.Dot(toStartTarget.normalized, toEndTarget.normalized);
		return dot >= 0f;
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
}