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
	private const float MaxLookAtAngle = 45f;
	private const float FovMin = 40f;
	private const float FovMax = 50f;
	private const int MaxPlayerPositions = 50;
	private const float MinXZOffset = 2f;
	private const float MinPoiDistanceFromPlayer = 4f;
	private const float MinDistanceForNewPoi = 3f;
	private const float EllipsoidMajorAxisScale = 1.5f;
	private const float EllipsoidMinorAxisScale = 1.0f;
	private const int MaxPositionSampleAttempts = 10;
	private const float OrbitRadius = 5f; // Radius for orbiting and endOrigin positioning
	private const float OrbitTargetDistanceThreshold = 0.5f; // Threshold to detect same target (player)
	private const float OriginSmoothingFactor = 0.1f; // Smoothing for endOrigin updates

	// State
	private float sequenceTimer;
	private float pauseTimer;
	private Vector3 playerPos;
	private List<Vector3> waypoints = new List<Vector3>(); // Set via SetWaypoints
	private List<Vector3> playerPositions = new List<Vector3>();
	private Vector3 startOrigin;
	private Vector3 startTarget;
	private Vector3 endOrigin;
	private Vector3 endTarget;
	private float currentFovMax;
	private Vector2 mapExtentsMin;
	private Vector2 mapExtentsMax;
	private bool isOrbit; // Tracks if sequence is an orbit around player
	private Vector2 endTargetOffset; // Cached offset for endTarget
	private float orbitStartAngle; // Cached start angle for orbit
	private float orbitEndAngle; // Cached end angle for orbit
	private Vector3 baseStartOrigin; // Cached initial origins for interpolation
	private Vector3 baseEndOrigin;
	private Vector3 lastPlayerPos; // Tracks last player position for delta

	// Public properties
	public float CinemaTimeoutDuration => CinemaTimeout;

	public void Reset()
	{
		sequenceTimer = 0f;
		pauseTimer = 0f;
		playerPos = Vector3.zero;
		waypoints.Clear();
		playerPositions.Clear();
		startOrigin = Vector3.zero;
		startTarget = Vector3.zero;
		endOrigin = Vector3.zero;
		endTarget = Vector3.zero;
		currentFovMax = FovMax;
		mapExtentsMin = new Vector2(float.MaxValue, float.MaxValue);
		mapExtentsMax = new Vector2(float.MinValue, float.MinValue);
		isOrbit = false;
		endTargetOffset = Vector2.zero;
		orbitStartAngle = 0f;
		orbitEndAngle = 0f;
		baseStartOrigin = Vector3.zero;
		baseEndOrigin = Vector3.zero;
		lastPlayerPos = Vector3.zero;
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

	public CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera)
	{
		if (pauseTimer > 0)
		{
			pauseTimer -= Time.deltaTime;
			if (pauseTimer <= 0)
				StartNewCinemaSequence(playerPos, waypoints);
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
		float t = sequenceTimer / SequenceDuration;
		float easedT = Sigmoid(t, 6f);
		data.origin = Vector3.Lerp(baseStartOrigin, baseEndOrigin, easedT);
		data.target = Vector3.Lerp(startTarget, endTarget, easedT);

		// Apply dynamic offset based on player movement
		Vector3 playerDelta = playerPos - lastPlayerPos;
		if (isOrbit)
		{
			// Update targets to current player position
			Vector3 center = new Vector3(playerPos.x + endTargetOffset.x, 0.75f, playerPos.z + endTargetOffset.y);
			startTarget = center;
			endTarget = center;
			// Shift interpolated origin and target by player delta
			data.origin += playerDelta;
			data.target = center; // Override to ensure focus on player
								  // Update origins for validation
			startOrigin = baseStartOrigin + (playerPos - baseEndOrigin);
			endOrigin = baseEndOrigin + (playerPos - baseEndOrigin);
		}
		else
		{
			// Update endTarget
			endTarget = new Vector3(playerPos.x + endTargetOffset.x, 0.75f, playerPos.z + endTargetOffset.y);
			// Smoothly adjust endOrigin for validation
			Vector3 newEndOrigin = endTarget + (baseEndOrigin - endTarget).normalized * OrbitRadius;
			newEndOrigin.y = baseEndOrigin.y; // Retain initial height
			endOrigin = Vector3.Lerp(endOrigin, newEndOrigin, OriginSmoothingFactor);
			// Apply delta to origin, scale by easedT to affect end more
			data.origin += playerDelta * easedT;
			data.target = Vector3.Lerp(startTarget, endTarget, easedT);
		}

		// Enforce pitch and offset constraints on final position
		AdjustHeight(ref data.origin, data.target);
		EnsureMinimumOffset(ref data.origin, data.target);

		// Update FOV
		float fovT = Mathf.PingPong(sequenceTimer, SequenceDuration / 2f) / (SequenceDuration / 2f);
		data.fov = Mathf.Lerp(FovMin, currentFovMax, fovT);
		camera.fieldOfView = data.fov;

		lastPlayerPos = playerPos;
		return data;

		static float Sigmoid(float t, float k = 6f) => 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));
	}

	public void StartNewCinemaSequence(Vector3 playerPos, List<Vector3> waypoints)
	{
		sequenceTimer = 0f;
		pauseTimer = 0f;
		this.playerPos = playerPos;
		this.waypoints = new List<Vector3>(waypoints); // Set waypoints for this sequence
		lastPlayerPos = playerPos;

		// Select start POI
		Vector3 startPoi = SelectStartPoi(playerPos, waypoints);

		// Set targets
		startTarget = new Vector3(startPoi.x, Random.Range(0.5f, 1f), startPoi.z);
		endTargetOffset = Random.insideUnitCircle * 0.5f;
		endTarget = new Vector3(playerPos.x + endTargetOffset.x, 0.75f, playerPos.z + endTargetOffset.y);

		// Check if both targets are effectively the player (for orbit behavior)
		isOrbit = Vector2.Distance(new Vector2(startTarget.x, startTarget.z), new Vector2(endTarget.x, endTarget.z)) <= OrbitTargetDistanceThreshold;

		if (isOrbit)
		{
			// Orbit around the player
			Vector3 center = (startTarget + endTarget) / 2f; // Average for stability
			orbitStartAngle = Random.Range(0f, 360f);
			orbitEndAngle = orbitStartAngle + Random.Range(90f, 180f); // Ensure distinct positions

			startOrigin = SampleOrbitPosition(center, orbitStartAngle);
			AdjustHeight(ref startOrigin, startTarget);
			EnsureMinimumOffset(ref startOrigin, startPoi);

			endOrigin = SampleOrbitPosition(center, orbitEndAngle);
			AdjustHeight(ref endOrigin, endTarget);
			EnsureMinimumOffset(ref endOrigin, playerPos);
		}
		else
		{
			// Standard path with ellipsoid sampling
			Vector3 targetPath = endTarget - startTarget;
			float pathLength = targetPath.magnitude;
			Vector3 pathDir = targetPath.normalized;
			Vector3 midPoint = (startTarget + endTarget) / 2f;
			Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;

			startOrigin = SampleCameraPosition(midPoint, pathDir, perpendicular, pathLength, startPoi, isStart: true);
			AdjustHeight(ref startOrigin, startTarget);
			EnsureMinimumOffset(ref startOrigin, startPoi);

			endOrigin = SampleCameraPosition(midPoint, pathDir, perpendicular, pathLength, playerPos, isStart: false);
			AdjustHeight(ref endOrigin, endTarget);
			EnsureMinimumOffset(ref endOrigin, playerPos);
		}

		// Cache base origins for interpolation
		baseStartOrigin = startOrigin;
		baseEndOrigin = endTarget; // Use endTarget as reference for orbit shift
		UpdateMapExtents();

		// Set FOV max (occasionally wider)
		currentFovMax = Random.value < 0.2f ? 60f : FovMax;
	}

	private Vector3 SelectStartPoi(Vector3 playerPos, List<Vector3> pois)
	{
		if (pois.Count == 0)
			return playerPos != Vector3.zero ? playerPos : Vector3.zero;

		var validPois = pois
			.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)) >= MinPoiDistanceFromPlayer)
			.ToList();

		if (validPois.Count > 0)
			return validPois[Random.Range(0, validPois.Count)];

		var farthestPoi = pois
			.Select(p => (p, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z))))
			.OrderByDescending(d => d.Item2)
			.FirstOrDefault().p;

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
			if (isStart)
			{
				startOrigin = position; // Temporarily set for IsValidCameraPath
				if (IsValidStartPosition(position) && IsValidCameraPath())
					return position;
			}
			else
			{
				endOrigin = position; // Temporarily set for IsValidCameraPath
				if (IsValidCameraPath())
					return position;
			}
		}

		// Fallback position
		Vector2 fallbackXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * (pathLength * 0.5f);
		float fallbackHeight = (MinCameraHeight + MaxCameraHeight) / 2f;
		return new Vector3(fallbackXZ.x, fallbackHeight, fallbackXZ.y);
	}

	private Vector3 SampleOrbitPosition(Vector3 center, float angleDegrees)
	{
		// Sample a position on a circular orbit around the center
		float angleRad = angleDegrees * Mathf.Deg2Rad;
		Vector3 offset = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad)) * OrbitRadius;
		Vector3 position = center + offset;
		position.y = (MinCameraHeight + MaxCameraHeight) / 2f; // Fixed height for stability
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
		Vector2 startTargetXZ = new Vector2(startTarget.x, startTarget.z);
		Vector2 endTargetXZ = new Vector2(endTarget.x, endTarget.z);
		Vector2 toStartTarget = startTargetXZ - posXZ;
		Vector2 toEndTarget = endTargetXZ - posXZ;
		float dot = Vector2.Dot(toStartTarget.normalized, toEndTarget.normalized);
		return dot >= 0f;
	}

	private bool IsValidCameraPath()
	{
		Vector3 startLookDir = (startTarget - startOrigin).normalized;
		Vector3 endLookDir = (endTarget - endOrigin).normalized;
		float dot = Vector3.Dot(startLookDir, endLookDir);
		return dot >= -0.7f; // Relaxed for dynamic updates
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
			position.y = Mathf.Min(idealHeight, MaxCameraHeight);
		}
	}
}