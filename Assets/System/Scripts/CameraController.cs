using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class CameraController
{
	public enum CameraState
	{
		Static,
		Follow,
		Preset,
		Cinema
	}

	public struct CameraData
	{
		public Vector3 origin;
		public Vector3 target;
		public Vector3 originDst;
		public Vector3 targetDst;
		public float smoothingRate;
		public float fov;

		public CameraData Copy()
		{
			return new CameraData
			{
				origin = origin,
				target = target,
				originDst = originDst,
				targetDst = targetDst,
				smoothingRate = smoothingRate,
				fov = fov
			};
		}

		public void Set(CameraData other)
		{
			origin = other.origin;
			target = other.target;
			originDst = other.originDst;
			targetDst = other.targetDst;
			smoothingRate = other.smoothingRate;
			fov = other.fov;
		}
	}

	public static CameraState State => state;

	// Configuration constants
	private const float DefaultSmoothingRate = 64f;
	private const float FollowSmoothingNa = 8f;
	private const float FollowSmoothingNb = 64f;
	private const float PresetSmoothingN = 32f;
	private const float IdealDistance = 14f;
	private const float IdealDistanceHorizontalScale = 1.4f;
	private const float TargetFPS = 60f;
	private const float CinemaTimeout = 5f;
	private const float CinemaSequenceDuration = 8f;
	private const float CinemaPauseDuration = 1f;
	private const float MinCameraHeight = 2f;
	private const float MaxCameraHeight = 6f;
	private const float MaxLookAtAngle = 45f;
	private const float CinemaFOVMin = 40f;
	private const float CinemaFOVMax = 50f;
	private const int MaxPlayerPositions = 50;
	private const float MinXZOffset = 2f;
	private const float MinPOIDistanceFromPlayer = 4f;
	private const float MinDistanceForNewPOI = 1f;
	private const float EllipsoidMajorAxisScale = 1.5f; // Scale along target path
	private const float EllipsoidMinorAxisScale = 1.0f; // Scale perpendicular to target path
	private const int MaxPositionSampleAttempts = 10; // Max attempts to find valid position

	// Internal state
	private static CameraState state = CameraState.Static;
	private static CameraState previousState = CameraState.Static;
	private static CameraData currentData;
	private static CameraData previousData;
	private static Vector3 playerPos;
	private static Camera mainCamera;
	private static float defaultFOV;
	private static float lastRefreshTime;
	private static bool allowAutoCinema = false;
	private static float cinemaSequenceTimer;
	private static float cinemaPauseTimer;
	private static List<Vector3> waypoints = new List<Vector3>();
	private static List<Vector3> playerPositions = new List<Vector3>();
	private static Vector3 cinemaStartOrigin;
	private static Vector3 cinemaStartTarget;
	private static Vector3 cinemaEndOrigin;
	private static Vector3 cinemaEndTarget;
	private static float currentCinemaFOVMax;

	public static void Initialize()
	{
		mainCamera = Camera.main;
		if (mainCamera == null) return;

		defaultFOV = mainCamera.fieldOfView;
		currentData = new CameraData
		{
			origin = Vector3.zero,
			target = Vector3.zero,
			originDst = Vector3.forward,
			targetDst = Vector3.forward,
			smoothingRate = DefaultSmoothingRate,
			fov = defaultFOV
		};
		previousData = currentData.Copy();
		playerPos = Vector3.zero;
		lastRefreshTime = Time.time;
		waypoints.Clear();
		playerPositions.Clear();
		cinemaSequenceTimer = 0f;
		cinemaPauseTimer = 0f;
		cinemaStartOrigin = Vector3.zero;
		cinemaStartTarget = Vector3.zero;
		cinemaEndOrigin = Vector3.zero;
		cinemaEndTarget = Vector3.zero;
		currentCinemaFOVMax = CinemaFOVMax;
		state = CameraState.Static;
		previousState = CameraState.Static;
		allowAutoCinema = false;
	}

	private static void UpdateMapExtents()
	{
		float minX = float.MaxValue;
		float maxX = float.MinValue;
		float minZ = float.MaxValue;
		float maxZ = float.MinValue;

		if (waypoints.Count > 0)
		{
			minX = waypoints.Min(p => p.x);
			maxX = waypoints.Max(p => p.x);
			minZ = waypoints.Min(p => p.z);
			maxZ = waypoints.Max(p => p.z);
		}

		if (playerPos != Vector3.zero)
		{
			minX = Mathf.Min(minX, playerPos.x);
			maxX = Mathf.Max(maxX, playerPos.x);
			minZ = Mathf.Min(minZ, playerPos.z);
			maxZ = Mathf.Max(maxZ, playerPos.z);
		}
	}

	private static void StartNewCinemaSequence()
	{
		cinemaSequenceTimer = 0f;
		cinemaPauseTimer = 0f;

		// Update map extents
		UpdateMapExtents();

		// Select random POI (waypoints only, never playerPos)
		List<Vector3> pois = waypoints.Where(p => p != Vector3.zero).ToList();
		Debug.Log($"Available POIs: [{string.Join(", ", pois.Select(p => p.ToString()))}]");

		Vector3 startPOI;
		if (pois.Count == 0)
		{
			startPOI = playerPos != Vector3.zero ? playerPos : Vector3.zero;
		}
		else
		{
			var validWaypoints = pois
				.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)) >= MinPOIDistanceFromPlayer)
				.ToList();

			if (validWaypoints.Count > 0)
			{
				startPOI = validWaypoints[Random.Range(0, validWaypoints.Count)];
			}
			else
			{
				var waypointDistances = pois
					.Select(p => (p, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z))))
					.OrderByDescending(d => d.Item2)
					.ToList();
				startPOI = waypointDistances.Count > 0 ? waypointDistances[0].p : playerPos;
			}
		}

		// Set start and end targets
		cinemaStartTarget = new Vector3(startPOI.x, Random.Range(0.5f, 1f), startPOI.z);
		Vector2 targetOffset = Random.insideUnitCircle * 0.5f;
		cinemaEndTarget = new Vector3(playerPos.x + targetOffset.x, 0.75f, playerPos.z + targetOffset.y);

		// Define the ellipsoid for position sampling
		Vector3 targetPath = cinemaEndTarget - cinemaStartTarget;
		float targetPathLength = targetPath.magnitude;
		Vector3 targetPathDir = targetPath.normalized;
		Vector3 midPoint = (cinemaStartTarget + cinemaEndTarget) / 2f;
		Vector3 perpendicular = new Vector3(-targetPathDir.z, 0f, targetPathDir.x).normalized;
		float majorAxis = targetPathLength * EllipsoidMajorAxisScale;
		float minorAxis = targetPathLength * EllipsoidMinorAxisScale;
		float heightAxis = MaxCameraHeight - MinCameraHeight;

		// Sample start position within the ellipsoid
		int attempts = 0;
		Vector3 startPos;
		float startHeight;
		do
		{
			startPos = SampleEllipsoidPosition(midPoint, targetPathDir, perpendicular, majorAxis, minorAxis, heightAxis);
			startHeight = startPos.y;
			startPos.y = 0f; // Temporarily set y to 0 for look direction calculation
			cinemaStartOrigin = startPos + Vector3.up * startHeight;
			attempts++;
		} while (!IsValidStartPosition() && attempts < MaxPositionSampleAttempts);

		if (attempts >= MaxPositionSampleAttempts)
		{
			// Fallback: Use a simple perpendicular offset
			Vector2 startPosXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * (targetPathLength * 0.5f);
			startHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
			cinemaStartOrigin = new Vector3(startPosXZ.x, startHeight, startPosXZ.y);
		}

		// Sample end position within the ellipsoid
		attempts = 0;
		Vector3 endPos;
		float endHeight;
		do
		{
			var v2d = Random.insideUnitCircle;
			v2d = (v2d + v2d.normalized * 0.25f) * 4f;
			endPos = cinemaEndTarget + new Vector3(v2d.x, Random.Range(1, 3), v2d.y);// SampleEllipsoidPosition(midPoint, targetPathDir, perpendicular, majorAxis, minorAxis, heightAxis);
			endHeight = endPos.y;
			endPos.y = 0f;
			cinemaEndOrigin = endPos + Vector3.up * endHeight;
			attempts++;
		} while (!IsValidCameraPath() && attempts < MaxPositionSampleAttempts);

		if (attempts >= MaxPositionSampleAttempts)
		{
			// Fallback: Use a simple perpendicular offset on the same side
			Vector2 startPosXZ = new Vector2(cinemaStartOrigin.x, cinemaStartOrigin.z);
			Vector2 targetPathDir2D = new Vector2(targetPathDir.x, targetPathDir.z);
			Vector2 perpendicular2D = new Vector2(perpendicular.x, perpendicular.z);
			Vector2 toStartPos = startPosXZ - new Vector2(midPoint.x, midPoint.z);
			float side = Vector2.Dot(toStartPos, perpendicular2D);
			Vector2 offsetDir = side >= 0 ? perpendicular2D : -perpendicular2D;
			Vector2 endPosXZ = new Vector2(playerPos.x, playerPos.z) + offsetDir * (targetPathLength * 0.5f);
			endHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
			cinemaEndOrigin = new Vector3(endPosXZ.x, endHeight, endPosXZ.y);
		}

		// Adjust heights to ensure pitch constraints
		AdjustHeight(ref cinemaStartOrigin, cinemaStartTarget);
		AdjustHeight(ref cinemaEndOrigin, cinemaEndTarget);

		// Ensure minimum XZ offset to prevent gimbal lock
		Vector2 startXZDelta = new Vector2(cinemaStartOrigin.x - startPOI.x, cinemaStartOrigin.z - startPOI.z);
		if (startXZDelta.magnitude < MinXZOffset)
		{
			startXZDelta = startXZDelta.normalized * MinXZOffset;
			cinemaStartOrigin.x = startPOI.x + startXZDelta.x;
			cinemaStartOrigin.z = startPOI.z + startXZDelta.y;
		}
		Vector2 endXZDelta = new Vector2(cinemaEndOrigin.x - playerPos.x, cinemaEndOrigin.z - playerPos.z);
		if (endXZDelta.magnitude < MinXZOffset)
		{
			endXZDelta = endXZDelta.normalized * MinXZOffset;
			cinemaEndOrigin.x = playerPos.x + endXZDelta.x;
			cinemaEndOrigin.z = playerPos.z + endXZDelta.y;
		}

		// FOV variation
		currentCinemaFOVMax = Random.value < 0.2f ? 60f : CinemaFOVMax;

		// Initialize camera
		currentData.origin = cinemaStartOrigin;
		currentData.target = cinemaStartTarget;
		currentData.fov = CinemaFOVMin;
		mainCamera.fieldOfView = CinemaFOVMin;

		// Validate pitch at start
		Vector3 direction = cinemaStartTarget - cinemaStartOrigin;
		float pitch = Vector3.Angle(direction, Vector3.down) - 90f;
		Vector3 startLookDir = (cinemaStartTarget - cinemaStartOrigin).normalized;
		Vector3 endLookDir = (cinemaEndTarget - cinemaEndOrigin).normalized;
		float lookDot = Vector3.Dot(startLookDir, endLookDir);
		Debug.Log($"New Cinema sequence: startPOI={startPOI}, playerPos={playerPos}, startOrigin={cinemaStartOrigin}, startTarget={cinemaStartTarget}, endOrigin={cinemaEndOrigin}, endTarget={cinemaEndTarget}, startPitch={pitch}, lookDot={lookDot}, fovMax={currentCinemaFOVMax}");
	}

	private static Vector3 SampleEllipsoidPosition(Vector3 center, Vector3 majorAxisDir, Vector3 minorAxisDir, float majorAxis, float minorAxis, float heightAxis)
	{
		// Sample a point within a unit sphere
		Vector3 unitSpherePoint = Random.insideUnitSphere;

		// Scale by ellipsoid axes
		float x = unitSpherePoint.x * majorAxis;
		float y = unitSpherePoint.y * heightAxis / 2f; // Divide by 2 to keep height range reasonable
		float z = unitSpherePoint.z * minorAxis;

		// Transform to ellipsoid space
		Vector3 position = center + majorAxisDir * x + Vector3.up * y + minorAxisDir * z;

		// Clamp height within bounds
		position.y = Mathf.Clamp(position.y, MinCameraHeight, MaxCameraHeight);

		return position;
	}

	private static bool IsValidStartPosition()
	{
		Vector2 startPosXZ = new Vector2(cinemaStartOrigin.x, cinemaStartOrigin.z);
		Vector2 startTargetXZ = new Vector2(cinemaStartTarget.x, cinemaStartTarget.z);
		Vector2 endTargetXZ = new Vector2(cinemaEndTarget.x, cinemaEndTarget.z);

		Vector2 toStartTarget = startTargetXZ - startPosXZ;
		Vector2 toEndTarget = endTargetXZ - startPosXZ;
		float dot = Vector2.Dot(toStartTarget.normalized, toEndTarget.normalized);
		return dot >= 0f;
	}

	private static bool IsValidCameraPath()
	{
		Vector3 startLookDir = (cinemaStartTarget - cinemaStartOrigin).normalized;
		Vector3 endLookDir = (cinemaEndTarget - cinemaEndOrigin).normalized;
		float dot = Vector3.Dot(startLookDir, endLookDir);
		return dot >= -0.2f;
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

	public static void SetMode(CameraState value)
	{
		if (value == CameraState.Cinema && state != CameraState.Cinema)
		{
			Debug.Log($"Switching to Cinema mode from {state}");
			previousState = state;
			previousData = currentData.Copy();
			StartNewCinemaSequence();
		}
		else if (value != CameraState.Cinema && state == CameraState.Cinema)
		{
			Debug.Log($"Exiting Cinema mode to {value}");
			currentData.fov = defaultFOV;
			mainCamera.fieldOfView = defaultFOV;
			currentData.Set(previousData);
		}
		state = value;
	}

	public static void SetOrigin(Vector3 value)
	{
		currentData.originDst = value;
		if (state == CameraState.Static) currentData.origin = value;
	}

	public static void SetTarget(Vector3 value)
	{
		currentData.targetDst = value;
		if (state == CameraState.Static) currentData.target = value;
	}

	public static void SetPlayer(Vector3 position)
	{
		playerPos = position;
		if (state == CameraState.Follow) currentData.targetDst = position;

		bool isFarEnough = true;
		foreach (var wp in waypoints)
		{
			if (Vector3.Distance(position, wp) < MinDistanceForNewPOI)
			{
				isFarEnough = false;
				break;
			}
		}
		if (isFarEnough)
		{
			foreach (var pp in playerPositions)
			{
				if (Vector3.Distance(position, pp) < MinDistanceForNewPOI)
				{
					isFarEnough = false;
					break;
				}
			}
		}

		if (isFarEnough)
		{
			playerPositions.Add(position);
			if (playerPositions.Count > MaxPlayerPositions)
				playerPositions.RemoveAt(0);
			waypoints.Add(position);
			Debug.Log($"Added playerPos {position} as new waypoint. Total waypoints: {waypoints.Count}");
		}

		UpdateMapExtents();
	}

	public static void SetWaypoints(List<Vector3> newWaypoints)
	{
		waypoints = newWaypoints?.Where(p => p != Vector3.zero && Vector3.Distance(p, Vector3.zero) > 0.1f).ToList() ?? new List<Vector3>();
		Debug.Log($"Waypoints set: Count={waypoints.Count}");
		UpdateMapExtents();
	}

	public static void Refresh()
	{
		lastRefreshTime = Time.time;
		if (state == CameraState.Cinema)
		{
			SetMode(previousState);
		}
	}

	public static void SetAutoCinema(bool allow = true)
	{
		allowAutoCinema = allow;
		Debug.Log($"Auto Cinema mode set to: {allowAutoCinema}");
	}

	public static bool CinemaEnabled => allowAutoCinema;

	public static void Update()
	{
		if (mainCamera == null) return;

		if (allowAutoCinema && state != CameraState.Cinema && Time.time - lastRefreshTime > CinemaTimeout)
		{
			Debug.Log("Auto-switching to Cinema mode");
			SetMode(CameraState.Cinema);
		}

		switch (state)
		{
			case CameraState.Static:
				break;

			case CameraState.Follow:
				currentData.smoothingRate = SmoothingUtils.Smooth(currentData.smoothingRate, FollowSmoothingNa, FollowSmoothingNb, Time.deltaTime, TargetFPS);
				var followLerp = SmoothingUtils.Smooth(0f, 1f, currentData.smoothingRate, Time.deltaTime, TargetFPS);
				currentData.target = Vector3.Lerp(currentData.target, currentData.targetDst, followLerp);
				var delta = currentData.target - currentData.origin;
				var deltaHorizontal = new Vector3(delta.x, 0, delta.z);
				if (deltaHorizontal.sqrMagnitude < 0.01f)
					deltaHorizontal = mainCamera.transform.forward;
				deltaHorizontal.Normalize();
				var idealPos = currentData.target - deltaHorizontal * (IdealDistance * IdealDistanceHorizontalScale);
				idealPos.y = currentData.target.y + IdealDistance;
				currentData.origin = Vector3.Lerp(currentData.origin, idealPos, followLerp);
				break;

			case CameraState.Preset:
				currentData.smoothingRate = SmoothingUtils.Smooth(currentData.smoothingRate, PresetSmoothingN, Time.deltaTime, TargetFPS);
				var presetLerp = SmoothingUtils.Smooth(0f, 1f, currentData.smoothingRate, Time.deltaTime, TargetFPS);
				currentData.origin = Vector3.Lerp(currentData.origin, currentData.originDst, presetLerp);
				currentData.target = Vector3.Lerp(currentData.target, currentData.targetDst, presetLerp);
				break;

			case CameraState.Cinema:
				UpdateCinemaMode();
				break;
		}

		UpdateCameraTransform();
	}

	private static void UpdateCinemaMode()
	{
		if (cinemaPauseTimer > 0)
		{
			cinemaPauseTimer -= Time.deltaTime;
			if (cinemaPauseTimer <= 0)
			{
				StartNewCinemaSequence();
			}
			return;
		}

		cinemaSequenceTimer += Time.deltaTime;
		if (cinemaSequenceTimer >= CinemaSequenceDuration)
		{
			cinemaSequenceTimer = 0f;
			cinemaPauseTimer = CinemaPauseDuration;
			Debug.Log($"Cinema sequence ended: finalTransform=[position={mainCamera.transform.position}, rotation={mainCamera.transform.rotation}]");
			return;
		}

		float t = cinemaSequenceTimer / CinemaSequenceDuration;
		float easedT = Sigmoid(t, 6f);
		currentData.origin = Vector3.Lerp(cinemaStartOrigin, cinemaEndOrigin, easedT);
		currentData.target = Vector3.Lerp(cinemaStartTarget, cinemaEndTarget, easedT);

		// Enforce pitch constraint
		Vector3 direction = currentData.target - currentData.origin;
		float angle = Vector3.Angle(direction, Vector3.down);
		if (angle < (90f - MaxLookAtAngle))
		{
			float maxY = currentData.origin.y - (direction.magnitude * Mathf.Tan(MaxLookAtAngle * Mathf.Deg2Rad));
			currentData.target.y = Mathf.Lerp(currentData.target.y, maxY, 0.5f);
		}

		// FOV dynamics
		float fovT = Mathf.PingPong(cinemaSequenceTimer, CinemaSequenceDuration / 2f) / (CinemaSequenceDuration / 2f);
		currentData.fov = Mathf.Lerp(CinemaFOVMin, currentCinemaFOVMax, fovT);
		mainCamera.fieldOfView = currentData.fov;
	}

	private static float Sigmoid(float t, float k = 6f)
	{
		return 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));
	}

	public static void UpdateCameraTransform()
	{
		mainCamera.transform.position = currentData.origin;
		var direction = currentData.target - currentData.origin;
		if (direction.sqrMagnitude < 0.01f)
		{
			Debug.LogWarning("Direction vector too small, skipping rotation update.");
			return;
		}
		mainCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
	}
}