using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CinemaCameraController
{
	// Cinema-specific constants
	private const float CinemaTimeout = 5f;
	private const float SequenceDuration = 8f;
	private const float PauseDuration = 1f;
	private const float MinCameraHeight = 2f;
	private const float MaxCameraHeight = 6f;
	private const float MaxLookAtAngle = 30f; // Maximum pitch angle, allowing pitches below
	private const float FovMin = 35f;
	private const float FovMax = 55f;
	private const int MaxPlayerPositions = 50;
	private const float MinXZOffset = 2f;
	private const float MinPoiDistanceFromPlayer = 4f;
	private const float MinDistanceForNewPoi = 3f;
	private const float EllipsoidMajorAxisScale = 1.5f;
	private const float EllipsoidMinorAxisScale = 1.0f;
	private const int MaxPositionSampleAttempts = 10;
	private const float OrbitRadius = 5f; // Radius for orbiting and endOrigin positioning
	private const float OrbitTargetDistanceThreshold = 0.5f; // Threshold to detect same target
	private const float TargetFPS = 60f; // Copied from CameraController - should make this common
	private const float ProjectionSmoothingRate = 16f; // Smoothing rate for dead-reckoned position
	private const float MinPathLength = 2f; // Minimum path length for non-orbit sampling

	// State
	private float sequenceTimer;
	private float pauseTimer;
	private Vector3 playerPos;
	private List<Vector3> waypoints = new List<Vector3>(); // Set via SetWaypoints
	private List<Vector3> playerPositions = new List<Vector3>();
	private Vector3 originSrc; // Initial calculated values
	private Vector3 originDst; // Initial calculated values
	private Vector3 targetSrc; // Initial calculated values
	private Vector3 targetDst; // Initial calculated values
	private float currentFovMax;
	private Vector2 mapExtentsMin;
	private Vector2 mapExtentsMax;
	private bool isOrbit; // Tracks if sequence is an orbit around player
	private Vector2 endTargetOffset; // Cached offset for endTarget
	private float orbitStartAngle; // Cached start angle for orbit
	private float orbitEndAngle; // Cached end angle for orbit
	private Vector3 lastPlayerPos; // Tracks last player position for delta
	private Vector3 startPlayerPos; // Tracks initial player position for sequence
	private Vector3 smoothedProjectedOffset; // Tracks smoothed dead-reckoned offset
	private Vector3 orbitCenter; // Tracks orbit center for dynamic updates
	private float orbitHeightSrc;
	private float orbitHeightDst;

	// Public properties
	public float CinemaTimeoutDuration => CinemaTimeout;

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
		startPlayerPos = Vector3.zero;
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

		// Add position to playerPositions if far enough from existing points
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
		// Calculate map extents for camera positioning
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
		this.waypoints = new List<Vector3>(waypoints); // Set waypoints for this sequence
		lastPlayerPos = playerPos;
		startPlayerPos = playerPos;
		smoothedProjectedOffset = Vector3.zero; // Reset projection offset
		orbitCenter = Vector3.zero; // Reset orbit center

		// Select start POI (66% chance for POI, 33% for player)
		Vector3 startPoi;
		if (Random.value < 0.66f && waypoints.Count > 0) // 2/3 chance for POI
		{
			var validPois = waypoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)) >= MinPoiDistanceFromPlayer).ToList();
			startPoi = validPois.Count > 0 ? validPois[Random.Range(0, validPois.Count)] : playerPos;
		}
		else
		{
			startPoi = playerPos; // 1/3 chance for player
		}

		// Set targets
		targetSrc = new Vector3(startPoi.x, Random.Range(0.5f, 1f), startPoi.z);
		endTargetOffset = Random.insideUnitCircle * 0.5f;
		targetDst = new Vector3(playerPos.x + endTargetOffset.x, 0.75f, playerPos.z + endTargetOffset.y);

		// Check if targets are effectively the same (for orbit behavior)
		isOrbit = Vector2.Distance(new Vector2(targetSrc.x, targetSrc.z), new Vector2(targetDst.x, targetDst.z)) <= OrbitTargetDistanceThreshold;

		if (isOrbit)
		{
			orbitCenter = targetDst; // Center on targetDst (player position)
			targetSrc = targetDst; // Both targets on player for orbit
			orbitStartAngle = Random.Range(0f, 360f);
			orbitEndAngle = orbitStartAngle + Random.Range(90f, 270f); // Ensure distinct positions

			orbitHeightSrc = Random.Range(MinCameraHeight, MaxCameraHeight); // Randomize src orbital height
			orbitHeightDst = Random.Range(MinCameraHeight, MaxCameraHeight); // Randomize dst orbital height

			originSrc = SampleOrbitPosition(orbitCenter, orbitStartAngle, 0f);
			AdjustHeight(ref originSrc, targetSrc);
			EnsureMinimumOffset(ref originSrc, targetSrc);

			originDst = SampleOrbitPosition(orbitCenter, orbitEndAngle, 1f);
			AdjustHeight(ref originDst, targetDst);
			EnsureMinimumOffset(ref originDst, targetDst);
		}
		else
		{
			// Standard path with ellipsoid sampling
			Vector3 targetPath = targetDst - targetSrc;
			float pathLength = Mathf.Max(targetPath.magnitude, MinPathLength); // Ensure minimum path length
			Vector3 pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere; // Fallback direction
			Vector3 midPoint = (targetSrc + targetDst) / 2f;
			Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;

			originSrc = SampleCameraPosition(midPoint, pathDir, perpendicular, pathLength, startPoi, isStart: true);
			AdjustHeight(ref originSrc, targetSrc);
			EnsureMinimumOffset(ref originSrc, startPoi);

			originDst = SampleCameraPosition(midPoint, pathDir, perpendicular, pathLength, playerPos, isStart: false);
			AdjustHeight(ref originDst, targetDst);
			EnsureMinimumOffset(ref originDst, playerPos);
		}

		UpdateMapExtents();

		// Set FOV max (occasionally wider)
		currentFovMax = Random.value < 0.2f ? 60f : FovMax;
	}

	private Vector3 SelectStartPoi(Vector3 playerPos, List<Vector3> pois)
	{
		if (pois.Count == 0)
			return playerPos != Vector3.zero ? playerPos : Vector3.zero;

		var validPois = pois.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)) >= MinPoiDistanceFromPlayer).ToList();

		if (validPois.Count > 0)
			return validPois[Random.Range(0, validPois.Count)];

		var farthestPoi = pois.Select(p => (p, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)))).OrderByDescending(d => d.Item2).FirstOrDefault().p;

		return farthestPoi != Vector3.zero ? farthestPoi : playerPos;
	}

	private Vector3 SampleCameraPosition(Vector3 midPoint, Vector3 pathDir, Vector3 perpendicular, float pathLength, Vector3 target, bool isStart)
	{
		float majorAxis = pathLength * EllipsoidMajorAxisScale;
		float minorAxis = pathLength * EllipsoidMinorAxisScale;
		float heightAxis = MaxCameraHeight - MinCameraHeight;

		// Try sampling a valid position
		for (int i = 0; i < MaxPositionSampleAttempts; i++)
		{
			Vector3 position = SampleEllipsoidPosition(midPoint, pathDir, perpendicular, majorAxis, minorAxis, heightAxis);
			// Ensure position is not too close to target in XZ plane
			if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(target.x, target.z)) < MinXZOffset)
				continue;
			if (isStart)
			{
				originSrc = position; // Temporarily set for IsValidCameraPath
				if (IsValidStartPosition(position) && IsValidCameraPath())
					return position;
			}
			else
			{
				originDst = position; // Temporarily set for IsValidCameraPath
				if (IsValidCameraPath())
					return position;
			}
		}

		// Fallback position: offset from target by OrbitRadius
		Vector2 targetXZ = new Vector2(target.x, target.z);
		Vector2 offsetDir = Random.insideUnitCircle.normalized * OrbitRadius;
		Vector2 fallbackXZ = targetXZ + offsetDir;
		float fallbackHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
		return new Vector3(fallbackXZ.x, fallbackHeight, fallbackXZ.y);
	}

	private Vector3 SampleOrbitPosition(Vector3 center, float angleDegrees, float easedT)
	{
		// Sample a position on a circular orbit around the center
		float angleRad = angleDegrees * Mathf.Deg2Rad;
		Vector3 offset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * OrbitRadius;
		Vector3 position = center + offset;
		// Interpolate height from orbitHeightSrc to orbitHeightDst
		position.y = Mathf.Lerp(orbitHeightSrc, orbitHeightDst, SmoothingUtils.Ease(easedT));
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
		float dot = Vector3.Dot(startLookDir, endLookDir);
		// Reject near-vertical look directions
		return dot >= -0.7f && startPitch < 80f && endPitch < 80f;
	}

	private void EnsureMinimumOffset(ref Vector3 position, Vector3 target)
	{
		Vector2 targetXZ = new Vector2(target.x, target.z);
		Vector2 positionXZ = new Vector2(position.x, position.z);
		Vector2 delta = positionXZ - targetXZ;
		if (delta.magnitude < MinXZOffset)
		{
			delta = delta.normalized * MinXZOffset;
			position.x = target.x + delta.x;
			position.z = target.z + delta.y;
		}
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

	private static void AdjustHeight(ref Vector3 position, Vector3 target)
	{
		Vector3 direction = (target - position).normalized;
		float pitch = Vector3.Angle(direction, Vector3.down) - 90f;
		if (pitch > MaxLookAtAngle)
		{
			float distXZ = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(target.x, target.z));
			float idealHeight = distXZ / Mathf.Tan(MaxLookAtAngle * Mathf.Deg2Rad) + target.y;
			position.y = Mathf.Clamp(idealHeight, MinCameraHeight, MaxCameraHeight);
		}
		// Ensure minimum height
		position.y = Mathf.Max(position.y, MinCameraHeight);
	}

	public CameraController.CameraData GetCinemaData(CameraController.CameraData data)
	{
		data.originSrc = originSrc;
		data.targetSrc = targetSrc;
		data.smoothingRate = 64f; // TODO: Define as constant
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

		// Compute base interpolation
		var t = SequenceDuration > 0 ? Mathf.Clamp01(sequenceTimer / SequenceDuration) : 1f;
		var easedT = SmoothingUtils.Ease(t);
		if (easedT < 0f) Debug.LogWarning("easedT < 0 !");

		// Smooth the dead-reckoned projection offset
		Vector3 targetProjectionOffset = delta * 2f;
		smoothedProjectedOffset.x = SmoothingUtils.Smooth(smoothedProjectedOffset.x, targetProjectionOffset.x, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.y = SmoothingUtils.Smooth(smoothedProjectedOffset.y, targetProjectionOffset.y, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.z = SmoothingUtils.Smooth(smoothedProjectedOffset.z, targetProjectionOffset.z, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);

		// Update positions (always tracking player for targetDst)
		orbitCenter += delta;
		originDst += delta;
		targetDst += delta;

		Vector3 transOrigin;
		Vector3 transTarget;

		if (isOrbit)
		{
			targetSrc = new Vector3(playerPos.x + endTargetOffset.x, 0.75f, playerPos.z + endTargetOffset.y);
			targetDst = targetSrc;
			transOrigin = SampleOrbitPosition(orbitCenter, Mathf.Lerp(orbitStartAngle, orbitEndAngle, easedT), easedT);
			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);
		}
		else
		{
			// Non-orbit: Standard path with ellipsoid sampling
			transOrigin = Vector3.Lerp(originSrc, originDst, easedT);
			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);
		}

		UpdateDataValues(transOrigin, transTarget);

		// Update FOV
		var fovT = SmoothingUtils.EasePingPong(sequenceTimer / SequenceDuration);
		data.fov = Mathf.Lerp(FovMin, currentFovMax, fovT);
		camera.fieldOfView = data.fov;

		data.smoothingRate = SmoothingUtils.Smooth(data.smoothingRate, 16f, SequenceDuration, Time.deltaTime, TargetFPS);

		return data;

		void UpdateDataValues(Vector3 originNew, Vector3 targetNew)
		{
			var followLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothingRate, Time.deltaTime, TargetFPS);
			if (followLerp < 0f) Debug.LogWarning("followLerp < 0 !");
			data.originSrc = Vector3.Lerp(data.originSrc, originNew, followLerp);
			data.targetSrc = Vector3.Lerp(data.targetSrc, targetNew, followLerp);
		}
	}
}