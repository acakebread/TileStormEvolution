using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static CinemaCameraController;

public class CinemaMultiMode : CinemaCameraBase
{
	private const float PauseDuration = 1f; // Pause duration after sequence
	private float pauseTimer; // Tracks pause time

	public override void StartNewCinemaSequence(Vector3 playerPos, List<Vector3> waypoints)
	{
		if (playerTransform == null)
			return;

		sequenceTimer = 0f;
		pauseTimer = 0f;
		this.waypoints = new List<Vector3>(waypoints);
		lastPlayerPos = playerTransform.position;
		smoothedProjectedOffset = Vector3.zero;
		orbitCenter = Vector3.zero;
		controlPoint = Vector3.zero;

		// Set mode (ChiefBrody handled by CinemaDollyZoom)
		float rand = Random.value;
		if (rand < 0.25f)
			currentMode = CinemaMode.Orbit;
		else if (rand < 0.5f)
			currentMode = CinemaMode.PoiStandard;
		else if (rand < 0.75f)
			currentMode = CinemaMode.PoiVariation1;
		else
			currentMode = CinemaMode.PoiVariation2;
		currentSequenceDuration = DefaultSequenceDuration;

		// Select start POI (66% chance for POI, 33% for player)
		Vector3 startPoi;
		if (Random.value < 0.66f && waypoints.Count > 0)
		{
			var validPois = waypoints.Where(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(playerTransform.position.x, playerTransform.position.z)) >= MinPoiDistanceFromPlayer).ToList();
			startPoi = validPois.Count > 0 ? validPois[Random.Range(0, validPois.Count)] : playerTransform.position;
		}
		else
		{
			startPoi = playerTransform.position;
		}

		// Set targets
		targetSrc = new Vector3(startPoi.x, verticalOffset, startPoi.z);
		endTargetOffset = Vector3.zero;// Random.insideUnitCircle * 0.5f;
		targetDst = new Vector3(playerTransform.position.x + endTargetOffset.x, verticalOffset, playerTransform.position.z + endTargetOffset.y);

		// Check if targets are effectively the same (for orbit behavior)
		isOrbit = Vector2.Distance(new Vector2(targetSrc.x, targetSrc.z), new Vector2(targetDst.x, targetDst.z)) <= OrbitTargetDistanceThreshold;

		if (currentMode == CinemaMode.Orbit || isOrbit)
		{
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
		}
		else
		{
			Vector3 targetPath = targetDst - targetSrc;
			float pathLength = Mathf.Max(targetPath.magnitude, MinPathLength);
			Vector3 pathDir = targetPath.magnitude > 0.1f ? targetPath.normalized : Random.onUnitSphere;
			Vector3 midPoint = (targetSrc + targetDst) / 2f;
			Vector3 perpendicular = new Vector3(-pathDir.z, 0f, pathDir.x).normalized;

			float lozengeMajor = (pathLength + 2f * MinOrbitRadius) / 2f;
			float lozengeMinor = MinOrbitRadius;

			if (useSplines)
			{
				var (src, dst, ctrl) = SampleSplineCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor, targetSrc, targetDst);
				originSrc = src;
				originDst = dst;
				controlPoint = ctrl;

				AdjustHeight(ref originSrc, targetSrc);
				AdjustHeight(ref originDst, targetDst);
				AdjustHeight(ref controlPoint, (targetSrc + targetDst) / 2f);
			}
			else
			{
				var (src, dst) = SampleTangentCameraPosition(midPoint, pathDir, perpendicular, lozengeMajor, lozengeMinor, targetSrc, targetDst);
				originSrc = src;
				originDst = dst;

				AdjustHeight(ref originSrc, targetSrc);
				AdjustHeight(ref originDst, targetDst);
			}
		}

		UpdateMapExtents();
		currentFovMax = Random.value < 0.2f ? 60f : FovMax;

		Debug.Log($"MultiMode: Starting new sequence, mode={currentMode}, targetSrc={targetSrc}, targetDst={targetDst}");
	}

	public override CameraController.CameraData UpdateCinemaMode(CameraController.CameraData data, Camera camera)
	{
		if (playerTransform == null)
			return data;

		var delta = playerTransform.position - lastPlayerPos;
		lastPlayerPos = playerTransform.position;

		// Handle pause state (matches CinemaCameraController)
		if (pauseTimer > 0f)
		{
			pauseTimer -= Time.deltaTime;
			UpdateDataValues(originDst, targetDst);
			if (pauseTimer <= 0f)
			{
				StartNewCinemaSequence(playerTransform.position, waypoints);
				data = GetCinemaData(data);
			}
			return data;
		}

		sequenceTimer += Time.deltaTime;
		if (sequenceTimer >= currentSequenceDuration)
		{
			sequenceTimer = 0f;
			pauseTimer = PauseDuration;
			return data;
		}

		var t = currentSequenceDuration > 0 ? Mathf.Clamp01(sequenceTimer / currentSequenceDuration) : 1f;
		var easedT = SmoothingUtils.Ease(t);

		Vector3 targetProjectionOffset = delta * 2f;
		smoothedProjectedOffset.x = SmoothingUtils.Smooth(smoothedProjectedOffset.x, targetProjectionOffset.x, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.y = SmoothingUtils.Smooth(smoothedProjectedOffset.y, targetProjectionOffset.y, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);
		smoothedProjectedOffset.z = SmoothingUtils.Smooth(smoothedProjectedOffset.z, targetProjectionOffset.z, ProjectionSmoothingRate, Time.deltaTime, TargetFPS);

		Vector3 transOrigin;
		Vector3 transTarget;

		if (currentMode == CinemaMode.Orbit || isOrbit)
		{
			orbitCenter += delta;
			targetSrc = new Vector3(playerTransform.position.x + endTargetOffset.x, verticalOffset, playerTransform.position.z + endTargetOffset.y);
			targetDst = targetSrc;
			transOrigin = SampleOrbitPosition(orbitCenter, Mathf.Lerp(orbitStartAngle, orbitEndAngle, easedT), easedT);
			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);

			var fovT = SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration);
			data.fov = Mathf.Lerp(FovMin, currentFovMax, fovT);
			camera.fieldOfView = data.fov;
		}
		else
		{
			orbitCenter += delta;
			originDst += delta;
			targetDst += delta;

			if (useSplines)
			{
				controlPoint += delta;
				transOrigin = EvaluateQuadraticBezier(easedT, originSrc, controlPoint, originDst);
				if (debugVisualizeBezier)
				{
					VisualizeBezierPath(originSrc, controlPoint, originDst);
					Debug.DrawLine(originSrc, controlPoint, Color.blue, 0.1f);
					Debug.DrawLine(controlPoint, originDst, Color.blue, 0.1f);
				}
			}
			else
			{
				transOrigin = Vector3.Lerp(originSrc, originDst, easedT);
				if (debugVisualizeBezier)
				{
					Debug.DrawLine(originSrc, originDst, Color.blue, 0.1f);
				}
			}
			transTarget = Vector3.Lerp(targetSrc, targetDst + smoothedProjectedOffset, easedT);

			var fovT = SmoothingUtils.EasePingPong(sequenceTimer / currentSequenceDuration);
			data.fov = Mathf.Lerp(FovMin, currentFovMax, fovT);
			camera.fieldOfView = data.fov;
		}

		UpdateDataValues(transOrigin, transTarget);
		data.smoothingRate = SmoothingUtils.Smooth(data.smoothingRate, 16f, currentSequenceDuration, Time.deltaTime, TargetFPS);
		return data;

		void UpdateDataValues(Vector3 originNew, Vector3 transTarget)
		{
			var followLerp = SmoothingUtils.Smooth(0f, 1f, data.smoothingRate, Time.deltaTime, TargetFPS);
			data.originSrc = Vector3.Lerp(data.originSrc, originNew, followLerp);
			data.targetSrc = Vector3.Lerp(data.targetSrc, transTarget, followLerp);
		}
	}
}