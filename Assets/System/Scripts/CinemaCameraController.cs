using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CinemaCameraController
{
	// Cinema-specific constants
	public const float CinemaTimeout = 5f;
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
	private const float EllipsoidMajorAxisScale = 1.5f;
	private const float EllipsoidMinorAxisScale = 1.0f;
	private const int MaxPositionSampleAttempts = 10;

	// Public access to timeout
	public float CinemaTimeoutDuration => CinemaTimeout;

	// Cinema-specific state
	private float cinemaSequenceTimer;
	private float cinemaPauseTimer;
	private List<Vector3> playerPositions = new List<Vector3>();
	private Vector3 currentPlayerPos;
	private List<Vector3> currentWaypoints = new List<Vector3>();
	private Vector3 cinemaStartOrigin;
	private Vector3 cinemaStartTarget;
	private Vector3 cinemaEndOrigin;
	private Vector3 cinemaEndTarget;
	private float currentCinemaFOVMax;

	public void Reset()
	{
		playerPositions.Clear();
		currentPlayerPos = Vector3.zero;
		currentWaypoints.Clear();
		cinemaSequenceTimer = 0f;
		cinemaPauseTimer = 0f;
		cinemaStartOrigin = Vector3.zero;
		cinemaStartTarget = Vector3.zero;
		cinemaEndOrigin = Vector3.zero;
		cinemaEndTarget = Vector3.zero;
		currentCinemaFOVMax = CinemaFOVMax;
	}

	public void UpdatePlayerPosition(Vector3 position, List<Vector3> waypoints)
	{
		currentPlayerPos = position;
		currentWaypoints = new List<Vector3>(waypoints); // Store a copy

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
			currentWaypoints.Add(position); // Update local copy
		}
	}

	public void UpdateMapExtents(Vector3 playerPos, List<Vector3> waypoints)
	{
		currentPlayerPos = playerPos;
		currentWaypoints = new List<Vector3>(waypoints); // Update local copy

		float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

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

	public CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera)
	{
		if (cinemaPauseTimer > 0)
		{
			cinemaPauseTimer -= Time.deltaTime;
			if (cinemaPauseTimer <= 0) StartNewCinemaSequence(currentPlayerPos, currentWaypoints);
			return data;
		}

		cinemaSequenceTimer += Time.deltaTime;
		if (cinemaSequenceTimer >= CinemaSequenceDuration)
		{
			cinemaSequenceTimer = 0f;
			cinemaPauseTimer = CinemaPauseDuration;
			return data;
		}

		float t = cinemaSequenceTimer / CinemaSequenceDuration;
		float easedT = Sigmoid(t, 6f);
		data.origin = Vector3.Lerp(cinemaStartOrigin, cinemaEndOrigin, easedT);
		data.target = Vector3.Lerp(cinemaStartTarget, cinemaEndTarget, easedT);

		// Enforce pitch constraint
		Vector3 direction = data.target - data.origin;
		float angle = Vector3.Angle(direction, Vector3.down);
		if (angle < (90f - MaxLookAtAngle))
		{
			float maxY = data.origin.y - (direction.magnitude * Mathf.Tan(MaxLookAtAngle * Mathf.Deg2Rad));
			data.target.y = Mathf.Lerp(data.target.y, maxY, 0.5f);
		}

		// FOV dynamics
		float fovT = Mathf.PingPong(cinemaSequenceTimer, CinemaSequenceDuration / 2f) / (CinemaSequenceDuration / 2f);
		data.fov = Mathf.Lerp(CinemaFOVMin, currentCinemaFOVMax, fovT);
		camera.fieldOfView = data.fov;

		return data;

		static float Sigmoid(float t, float k = 6f) => 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));
	}

	public void StartNewCinemaSequence(Vector3 playerPos, List<Vector3> waypoints)
	{
		cinemaSequenceTimer = 0f;
		cinemaPauseTimer = 0f;

		UpdateMapExtents(playerPos, waypoints);

		List<Vector3> pois = waypoints.Where(p => p != Vector3.zero).ToList();
		Vector3 startPOI = SelectStartPOI(playerPos, pois);

		cinemaStartTarget = new Vector3(startPOI.x, Random.Range(0.5f, 1f), startPOI.z);
		Vector2 targetOffset = Random.insideUnitCircle * 0.5f;
		cinemaEndTarget = new Vector3(playerPos.x + targetOffset.x, 0.75f, playerPos.z + targetOffset.y);

		Vector3 targetPath = cinemaEndTarget - cinemaStartTarget;
		float targetPathLength = targetPath.magnitude;
		Vector3 targetPathDir = targetPath.normalized;
		Vector3 midPoint = (cinemaStartTarget + cinemaEndTarget) / 2f;
		Vector3 perpendicular = new Vector3(-targetPathDir.z, 0f, targetPathDir.x).normalized;
		float majorAxis = targetPathLength * EllipsoidMajorAxisScale;
		float minorAxis = targetPathLength * EllipsoidMinorAxisScale;
		float heightAxis = MaxCameraHeight - MinCameraHeight;

		// Sample start position
		int attempts = 0;
		float startHeight;
		do
		{
			Vector3 startPos = SampleEllipsoidPosition(midPoint, targetPathDir, perpendicular, majorAxis, minorAxis, heightAxis);
			startHeight = startPos.y;
			startPos.y = 0f;
			cinemaStartOrigin = startPos + Vector3.up * startHeight;
			attempts++;
		} while (!IsValidStartPosition() && attempts < MaxPositionSampleAttempts);

		if (attempts >= MaxPositionSampleAttempts)
		{
			Vector2 startPosXZ = new Vector2(midPoint.x, midPoint.z) + new Vector2(perpendicular.x, perpendicular.z) * (targetPathLength * 0.5f);
			startHeight = Random.Range(MinCameraHeight, MaxCameraHeight);
			cinemaStartOrigin = new Vector3(startPosXZ.x, startHeight, startPosXZ.y);
		}

		// Sample end position
		attempts = 0;
		float endHeight;
		do
		{
			Vector2 v2d = Random.insideUnitCircle;
			v2d = (v2d + v2d.normalized * 0.25f) * 4f;
			Vector3 endPos = cinemaEndTarget + new Vector3(v2d.x, Random.Range(1, 3), v2d.y);
			endHeight = endPos.y;
			endPos.y = 0f;
			cinemaEndOrigin = endPos + Vector3.up * endHeight;
			attempts++;
		} while (!IsValidCameraPath() && attempts < MaxPositionSampleAttempts);

		if (attempts >= MaxPositionSampleAttempts)
		{
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

		AdjustHeight(ref cinemaStartOrigin, cinemaStartTarget);
		AdjustHeight(ref cinemaEndOrigin, cinemaEndTarget);

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

		currentCinemaFOVMax = Random.value < 0.2f ? 60f : CinemaFOVMax;
	}

	private Vector3 SelectStartPOI(Vector3 playerPos, List<Vector3> pois)
	{
		if (pois.Count == 0)
			return playerPos != Vector3.zero ? playerPos : Vector3.zero;

		var validWaypoints = pois.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)) >= MinPOIDistanceFromPlayer).ToList();
		if (validWaypoints.Count > 0)
			return validWaypoints[Random.Range(0, validWaypoints.Count)];

		var waypointDistances = pois.Select(p => (p, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerPos.x, playerPos.z)))).OrderByDescending(d => d.Item2).ToList();
		return waypointDistances.Count > 0 ? waypointDistances[0].p : playerPos;
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

	private bool IsValidStartPosition()
	{
		Vector2 startPosXZ = new Vector2(cinemaStartOrigin.x, cinemaStartOrigin.z);
		Vector2 startTargetXZ = new Vector2(cinemaStartTarget.x, cinemaStartTarget.z);
		Vector2 endTargetXZ = new Vector2(cinemaEndTarget.x, cinemaEndTarget.z); // Fixed line
		Vector2 toStartTarget = startTargetXZ - startPosXZ;
		Vector2 toEndTarget = endTargetXZ - startPosXZ;
		float dot = Vector2.Dot(toStartTarget.normalized, toEndTarget.normalized);
		return dot >= 0f;
	}

	private bool IsValidCameraPath()
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
}