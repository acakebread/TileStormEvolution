using UnityEngine;
using static GameDatabase.DatabaseLoader;

namespace GamePreviewNamespace
{
	public class CameraController : MonoBehaviour
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private Camera mainCamera;

		private enum CameraState
		{
			Idle,
			TrackingEggbot,
			LerpingToWaypoint,
			AtWaypoint
		}

		private CameraState currentState;
		private int currentWaypointIndex;
		private Vector3 lerpTargetPos;
		private Vector3 lerpTargetLookAt;
		private bool isAtWaypointSet;
		private float idleTimer;
		private bool isLerping;
		private Vector3 currentLookAt;
		private bool isFinalWaypoint;
		private float m_fRate; // Smoothing rate
		private bool debugLogging = false; // Toggle for testing

		public void Initialize(MapManager manager, EggbotController eggbot)
		{
			mapManager = manager;
			eggbotController = eggbot;
			mainCamera = Camera.main;

			if (mainCamera == null)
			{
				Debug.LogError("CameraController: No main camera found!");
				return;
			}

			ResetCamera();
		}

		public void ResetCamera()
		{
			if (mainCamera == null || mapManager == null)
			{
				if (debugLogging)
					Debug.LogWarning("CameraController: Cannot reset camera, dependencies not initialized.");
				return;
			}

			currentState = CameraState.Idle;
			currentWaypointIndex = 0;
			isAtWaypointSet = false;
			idleTimer = 0.1f;
			isLerping = false;
			isFinalWaypoint = false;
			currentLookAt = Vector3.zero;
			m_fRate = 0.00390625f; // 1/256

			if (mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
			{
				if (debugLogging)
					Debug.LogWarning("CameraController: No waypoints, defaulting to safe position.");
				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
				mainCamera.transform.position = defaultPos;
				currentLookAt = defaultPos + Vector3.forward;
				LookAtTarget(mainCamera.transform, currentLookAt);
				if (debugLogging)
					Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
				return;
			}

			var firstWaypoint = mapManager.Waypoints[0];
			Vector3 srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
			if (!IsValidVector(firstWaypoint.vSrc))
			{
				if (debugLogging)
					Debug.LogWarning("CameraController: Invalid vSrc in first waypoint, defaulting.");
				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
				mainCamera.transform.position = defaultPos;
				currentLookAt = defaultPos + Vector3.forward;
				LookAtTarget(mainCamera.transform, currentLookAt);
				if (debugLogging)
					Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
				return;
			}

			Vector3 lookAtPos = firstWaypoint.vDst != null && IsValidVector(firstWaypoint.vDst)
				? new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ)
				: mapManager.GetTilePosition(firstWaypoint.nTile) + new Vector3(0, 0.5f, 0);

			mainCamera.transform.position = srcPos;
			currentLookAt = lookAtPos;
			LookAtTarget(mainCamera.transform, currentLookAt);
			if (debugLogging)
				Debug.Log($"Camera initialized at waypoint 0: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}, vSrc=({firstWaypoint.vSrc.fX}, {firstWaypoint.vSrc.fY}, {firstWaypoint.vSrc.fZ}), vDst={(firstWaypoint.vDst != null ? $"({firstWaypoint.vDst.fX}, {firstWaypoint.vDst.fY}, {firstWaypoint.vDst.fZ})" : "null")}, nTile={firstWaypoint.nTile}");
		}

		public void UpdateCamera()
		{
			if (mainCamera == null || mapManager == null || eggbotController == null || eggbotController.eggbot == null)
			{
				Debug.LogWarning("CameraController: Cannot update, dependencies missing.");
				return;
			}

			float deltaTime = Time.deltaTime;
			float targetFPS = 60f;
			float timeScale = deltaTime * targetFPS; // ~0.12 at 250 FPS

			// Update m_fRate with default 1/256 smoothing
			float fRate = 0.015625f; // 1/64
			m_fRate = (m_fRate * 255f + fRate * timeScale) / 256f;

			switch (currentState)
			{
				case CameraState.Idle:
					idleTimer -= deltaTime;
					if (idleTimer <= 0 && eggbotController.eggbot != null)
					{
						currentState = CameraState.TrackingEggbot;
						currentLookAt = eggbotController.eggbot.transform.position;
						if (debugLogging)
							Debug.Log("Camera transitioning to TrackingEggbot");
					}
					break;

				case CameraState.TrackingEggbot:
					// Target 1/8 smoothing
					fRate = 0.125f; // 1/8
					m_fRate = (m_fRate * 63f + fRate * timeScale) / 64f;

					// Compute target position with ScreenToPlaneXZ
					Vector3 vOld = ScreenToPlaneXZ();
					Vector3 targetPos = eggbotController.eggbot.transform.position;
					currentLookAt = Vector3.Lerp(vOld, targetPos, m_fRate);

					// Compute ideal position
					Vector3 vDelta = currentLookAt - mainCamera.transform.position;
					float fDist = vDelta.magnitude;
					float fHeight = Mathf.Abs(vDelta.y);
					Vector3 vDeltaHorizontal = new Vector3(vDelta.x, 0, vDelta.z);
					if (vDeltaHorizontal.sqrMagnitude > 0.01f)
						vDeltaHorizontal.Normalize();
					float fIdeal = 14f;
					Vector3 idealPos = currentLookAt - vDeltaHorizontal * (fIdeal * 1.4f);
					idealPos.y = currentLookAt.y + fIdeal;

					// Lerp position to ideal
					mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, idealPos, m_fRate);

					LookAtTarget(mainCamera.transform, currentLookAt);
					if (debugLogging)
						Debug.Log($"Camera tracking Eggbot: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}, m_fRate={m_fRate}");
					break;

				case CameraState.LerpingToWaypoint:
					// Target 1/32 smoothing
					fRate = 0.03125f; // 1/32
					m_fRate = (m_fRate * 31f + fRate * timeScale) / 32f;

					mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, lerpTargetPos, m_fRate);
					currentLookAt = Vector3.Lerp(currentLookAt, lerpTargetLookAt, m_fRate);
					LookAtTarget(mainCamera.transform, currentLookAt);

					if (Vector3.Distance(mainCamera.transform.position, lerpTargetPos) < 0.01f)
					{
						currentState = CameraState.AtWaypoint;
						isAtWaypointSet = false;
						isLerping = false;
						m_fRate = 0.015625f; // Reset to 1/64
						if (debugLogging)
							Debug.Log($"Camera reached waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
					}
					break;

				case CameraState.AtWaypoint:
					if (!isAtWaypointSet)
					{
						mainCamera.transform.position = lerpTargetPos;
						currentLookAt = lerpTargetLookAt;
						LookAtTarget(mainCamera.transform, currentLookAt);
						isAtWaypointSet = true;
						m_fRate = 0.015625f; // Reset to 1/64
						if (debugLogging)
							Debug.Log($"Camera set at waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
					}
					break;
			}
		}

		public void OnWaypointReached(int waypointIndex)
		{
			if (isLerping || isFinalWaypoint || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
			{
				if (debugLogging)
					Debug.LogWarning($"CameraController: Ignoring waypoint {waypointIndex}: isLerping={isLerping}, isFinalWaypoint={isFinalWaypoint}, invalid index={waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count}");
				return;
			}

			var waypoint = mapManager.Waypoints[waypointIndex];
			Vector3 srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);
			Vector3 lookAtPos = waypoint.vDst != null && IsValidVector(waypoint.vDst)
				? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
				: eggbotController.eggbot.transform.position;

			if (debugLogging)
				Debug.Log($"Waypoint {waypointIndex} data: vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}, validSrc={IsValidVector(waypoint.vSrc)}, validDst={(waypoint.vDst != null ? IsValidVector(waypoint.vDst) : false)}");

			if (waypointIndex == mapManager.Waypoints.Count - 1)
			{
				isFinalWaypoint = true;
				currentState = CameraState.TrackingEggbot;
				currentWaypointIndex = waypointIndex;
				lerpTargetPos = mainCamera.transform.position;
				lerpTargetLookAt = eggbotController.eggbot.transform.position;
				isAtWaypointSet = false;
				isLerping = false;
				currentLookAt = lerpTargetLookAt;
				m_fRate = 0.015625f; // Reset to 1/64
				if (debugLogging)
					Debug.Log($"Camera at final waypoint {waypointIndex}: staying at position={mainCamera.transform.position}, looking at={currentLookAt}");
				return;
			}

			if (Vector3.Distance(mainCamera.transform.position, srcPos) < 0.1f)
			{
				currentState = CameraState.AtWaypoint;
				currentWaypointIndex = waypointIndex;
				lerpTargetPos = srcPos;
				lerpTargetLookAt = lookAtPos;
				isAtWaypointSet = false;
				isLerping = false;
				currentLookAt = lerpTargetLookAt;
				m_fRate = 0.015625f; // Reset to 1/64
				if (debugLogging)
					Debug.Log($"Camera already at waypoint {waypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}");
				return;
			}

			currentWaypointIndex = waypointIndex;
			lerpTargetPos = srcPos;
			lerpTargetLookAt = lookAtPos;
			isLerping = true;

			if (!IsValidVector(waypoint.vSrc))
			{
				if (debugLogging)
					Debug.LogWarning($"CameraController: Invalid vSrc at waypoint {waypointIndex}, staying at current position.");
				lerpTargetPos = mainCamera.transform.position;
				isLerping = false;
				currentState = CameraState.AtWaypoint;
				currentLookAt = eggbotController.eggbot.transform.position;
				isAtWaypointSet = false;
				m_fRate = 0.015625f; // Reset to 1/64
				if (debugLogging)
					Debug.Log($"Camera at waypoint {waypointIndex} with invalid vSrc: staying at position={mainCamera.transform.position}, looking at={currentLookAt}");
				return;
			}

			currentState = CameraState.LerpingToWaypoint;
			isAtWaypointSet = false;
			if (debugLogging)
				Debug.Log($"Camera lerping to waypoint {waypointIndex}: targetPos={lerpTargetPos}, targetLookAt={lerpTargetLookAt}");
		}

		public void OnPuzzleSolved(int waypointIndex)
		{
			if (debugLogging)
				Debug.Log($"CameraController: OnPuzzleSolved called for waypoint {waypointIndex}, currentIndex={currentWaypointIndex}, state={currentState}");

			currentState = CameraState.TrackingEggbot;
			isAtWaypointSet = false;
			isLerping = false;
			currentLookAt = eggbotController.eggbot.transform.position;
			m_fRate = 0.015625f; // Reset to 1/64
			if (debugLogging)
				Debug.Log($"Camera resuming tracking Eggbot from waypoint {waypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}");
		}

		private bool IsValidVector(VectorData vector)
		{
			const float maxValue = 100f;
			bool valid = vector != null &&
						 !float.IsNaN(vector.fX) && !float.IsInfinity(vector.fX) && Mathf.Abs(vector.fX) < maxValue &&
						 !float.IsNaN(vector.fY) && !float.IsInfinity(vector.fY) && Mathf.Abs(vector.fY) < maxValue &&
						 !float.IsNaN(vector.fZ) && !float.IsInfinity(vector.fZ) && Mathf.Abs(vector.fZ) < maxValue;
			if (!valid && debugLogging)
				Debug.LogWarning($"Invalid vector: fX={vector?.fX}, fY={vector?.fY}, fZ={vector?.fZ}");
			return valid;
		}

		private void LookAtTarget(Transform cameraTransform, Vector3 target)
		{
			Vector3 direction = target - cameraTransform.position;
			if (direction.sqrMagnitude < 0.01f)
			{
				Debug.LogWarning("CameraController: Target too close to camera, skipping orientation.");
				return;
			}

			if (target.y > cameraTransform.position.y - 0.5f)
			{
				target.y = Mathf.Min(target.y, cameraTransform.position.y - 1f);
				direction = target - cameraTransform.position;
				if (debugLogging)
					Debug.Log($"CameraController: Adjusted target to ensure downward tilt: new target={target}");
			}

			cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}

		private Vector3 ScreenToPlaneXZ()
		{
			// Project screen center (0.5, 0.5) to XZ plane (y=0)
			Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
			Plane xzPlane = new Plane(Vector3.up, Vector3.zero); // y=0 plane
			if (xzPlane.Raycast(ray, out float distance))
			{
				Vector3 point = ray.GetPoint(distance);
				return new Vector3(point.x, 0, point.z);
			}
			// Fallback: Use Eggbot position projected to y=0
			Vector3 fallback = eggbotController.eggbot.transform.position;
			if (debugLogging)
				Debug.LogWarning($"ScreenToPlaneXZ failed, using fallback: {fallback}");
			return new Vector3(fallback.x, 0, fallback.z);
		}
	}
}


//using UnityEngine;
//using GameDatabase;
//using GamePreviewNamespace;
//using static GameDatabase.DatabaseLoader;

//namespace GamePreviewNamespace
//{
//	public class CameraController : MonoBehaviour
//	{
//		private MapManager mapManager;
//		private EggbotController eggbotController;
//		private Camera mainCamera;

//		private enum CameraState
//		{
//			Idle,
//			TrackingEggbot,
//			LerpingToWaypoint,
//			AtWaypoint
//		}

//		private CameraState currentState;
//		private int currentWaypointIndex;
//		private float lerpTimer;
//		private float lerpDuration = 2f;
//		private Vector3 lerpStartPos;
//		private Quaternion lerpStartRot;
//		private Vector3 lerpTargetPos;
//		private Vector3 lerpTargetLookAt;
//		private Vector3 lerpStartLookAt;
//		private bool isAtWaypointSet;
//		private float idleTimer;
//		private bool isLerping;
//		private float m_fRate; // Smoothed rate for nFollow
//		private Vector3 currentLookAt; // Current blended look-at target

//		public void Initialize(MapManager manager, EggbotController eggbot)
//		{
//			mapManager = manager;
//			eggbotController = eggbot;
//			mainCamera = Camera.main;

//			if (mainCamera == null)
//			{
//				Debug.LogError("CameraController: No main camera found!");
//				return;
//			}

//			ResetCamera();
//		}

//		public void ResetCamera()
//		{
//			if (mainCamera == null || mapManager == null)
//			{
//				Debug.LogWarning("CameraController: Cannot reset camera, dependencies not initialized.");
//				return;
//			}

//			currentState = CameraState.Idle;
//			currentWaypointIndex = 0;
//			lerpTimer = 0f;
//			isAtWaypointSet = false;
//			idleTimer = 0.1f;
//			isLerping = false;
//			m_fRate = 1f / 8f; // Initial rate
//			currentLookAt = Vector3.zero; // Initialize later

//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
//			{
//				Debug.LogWarning("CameraController: No waypoints available, defaulting to safe position.");
//				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
//				mainCamera.transform.position = defaultPos;
//				LookAtTarget(mainCamera.transform, defaultPos + Vector3.forward);
//				currentLookAt = defaultPos + Vector3.forward;
//				Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
//				return;
//			}

//			var firstWaypoint = mapManager.Waypoints[0];
//			Vector3 srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
//			if (!IsValidVector(firstWaypoint.vSrc))
//			{
//				Debug.LogWarning("CameraController: Invalid vSrc in first waypoint, defaulting to safe position.");
//				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
//				mainCamera.transform.position = defaultPos;
//				LookAtTarget(mainCamera.transform, defaultPos + Vector3.forward);
//				currentLookAt = defaultPos + Vector3.forward;
//				Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
//				return;
//			}

//			Vector3 lookAtPos = firstWaypoint.vDst != null && IsValidVector(firstWaypoint.vDst)
//				? new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ)
//				: mapManager.GetTilePosition(firstWaypoint.nTile) + new Vector3(0, 0.5f, 0);

//			mainCamera.transform.position = srcPos;
//			LookAtTarget(mainCamera.transform, lookAtPos);
//			currentLookAt = lookAtPos;
//			Debug.Log($"Camera initialized at waypoint 0: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}, vSrc=({firstWaypoint.vSrc.fX}, {firstWaypoint.vSrc.fY}, {firstWaypoint.vSrc.fZ}), vDst={(firstWaypoint.vDst != null ? $"({firstWaypoint.vDst.fX}, {firstWaypoint.vDst.fY}, {firstWaypoint.vDst.fZ})" : "null")}, nTile={firstWaypoint.nTile}");
//		}

//		public void UpdateCamera()
//		{
//			if (mainCamera == null || mapManager == null || eggbotController == null || eggbotController.eggbot == null)
//			{
//				Debug.LogWarning("CameraController: Cannot update, dependencies missing.");
//				return;
//			}

//			// Update smoothed rate
//			float fRate = 1f / 8f;
//			m_fRate = (m_fRate * 63f + fRate) / 64f;

//			switch (currentState)
//			{
//				case CameraState.Idle:
//					idleTimer -= Time.deltaTime;
//					if (idleTimer <= 0 && eggbotController.eggbot != null)
//					{
//						currentState = CameraState.TrackingEggbot;
//						Debug.Log("Camera transitioning to TrackingEggbot");
//					}
//					break;

//				case CameraState.TrackingEggbot:
//					Vector3 targetPos = eggbotController.eggbot.transform.position;
//					currentLookAt = Vector3.Lerp(currentLookAt, targetPos, m_fRate); // Blend target

//					// Position adjustment
//					Vector3 vDelta = currentLookAt - mainCamera.transform.position;
//					float fDist = vDelta.magnitude;
//					float fHeight = Mathf.Abs(vDelta.y);
//					Vector3 vDeltaHorizontal = new Vector3(vDelta.x, 0, vDelta.z).normalized;
//					float fIdeal = 14f;
//					Vector3 vDistAdjust = vDeltaHorizontal * (fDist - fIdeal * 1.4f) * m_fRate;
//					Vector3 vHeightAdjust = new Vector3(0, (fIdeal - fHeight) * m_fRate, 0);
//					mainCamera.transform.position += vDistAdjust + vHeightAdjust;

//					LookAtTarget(mainCamera.transform, currentLookAt);
//					Debug.Log($"Camera tracking Eggbot: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}, m_fRate={m_fRate}");
//					break;

//				case CameraState.LerpingToWaypoint:
//					lerpTimer += Time.deltaTime;
//					float t = Mathf.Clamp01(lerpTimer / lerpDuration);
//					float easedT = SigmoidEase(t);

//					mainCamera.transform.position = Vector3.Lerp(lerpStartPos, lerpTargetPos, easedT);
//					currentLookAt = Vector3.Lerp(lerpStartLookAt, lerpTargetLookAt, easedT);
//					LookAtTarget(mainCamera.transform, currentLookAt);

//					if (t >= 1f)
//					{
//						currentState = CameraState.AtWaypoint;
//						lerpTimer = 0f;
//						isAtWaypointSet = false;
//						isLerping = false;
//						Debug.Log($"Camera reached waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
//					}
//					break;

//				case CameraState.AtWaypoint:
//					if (!isAtWaypointSet)
//					{
//						mainCamera.transform.position = lerpTargetPos;
//						currentLookAt = lerpTargetLookAt;
//						LookAtTarget(mainCamera.transform, currentLookAt);
//						isAtWaypointSet = true;
//						Debug.Log($"Camera set at waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}, up={mainCamera.transform.up}");
//					}
//					break;
//			}
//		}

//		public void OnWaypointReached(int waypointIndex)
//		{
//			if (isLerping || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
//			{
//				Debug.LogWarning($"CameraController: Ignoring waypoint {waypointIndex}: isLerping={isLerping}, invalid index={waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count}");
//				return;
//			}

//			var waypoint = mapManager.Waypoints[waypointIndex];
//			Vector3 srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);

//			// Handle final waypoint: stay at current position, track Eggbot
//			if (waypointIndex == mapManager.Waypoints.Count - 1)
//			{
//				currentState = CameraState.TrackingEggbot; // Use smooth tracking
//				currentWaypointIndex = waypointIndex;
//				lerpTargetPos = mainCamera.transform.position;
//				lerpTargetLookAt = eggbotController.eggbot.transform.position;
//				isAtWaypointSet = false;
//				isLerping = false;
//				currentLookAt = lerpTargetLookAt; // Start tracking Eggbot
//				Debug.Log($"Camera at final waypoint {waypointIndex}: staying at position={mainCamera.transform.position}, looking at={currentLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//				return;
//			}

//			// Skip lerp if already at or very close to target
//			if (Vector3.Distance(mainCamera.transform.position, srcPos) < 0.1f)
//			{
//				currentState = CameraState.AtWaypoint;
//				currentWaypointIndex = waypointIndex;
//				lerpTargetPos = srcPos;
//				lerpTargetLookAt = waypoint.vDst != null && IsValidVector(waypoint.vDst)
//					? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
//					: eggbotController.eggbot.transform.position;
//				isAtWaypointSet = false;
//				isLerping = false;
//				currentLookAt = lerpTargetLookAt;
//				Debug.Log($"Camera already at waypoint {waypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//				return;
//			}

//			currentWaypointIndex = waypointIndex;
//			lerpStartPos = mainCamera.transform.position;
//			lerpStartRot = mainCamera.transform.rotation;
//			lerpTargetPos = srcPos;
//			lerpStartLookAt = currentLookAt; // Use current look-at
//			isLerping = true;

//			if (!IsValidVector(waypoint.vSrc))
//			{
//				Debug.LogWarning($"CameraController: Invalid vSrc at waypoint {waypointIndex}, staying at current position.");
//				lerpTargetPos = mainCamera.transform.position;
//				isLerping = false;
//				currentState = CameraState.AtWaypoint;
//				lerpTargetLookAt = eggbotController.eggbot.transform.position;
//				isAtWaypointSet = false;
//				currentLookAt = lerpTargetLookAt;
//				Debug.Log($"Camera at waypoint {waypointIndex} with invalid vSrc: staying at position={mainCamera.transform.position}, looking at={currentLookAt}");
//				return;
//			}

//			lerpTargetLookAt = waypoint.vDst != null && IsValidVector(waypoint.vDst)
//				? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
//				: eggbotController.eggbot.transform.position;

//			currentState = CameraState.LerpingToWaypoint;
//			lerpTimer = 0f;
//			isAtWaypointSet = false;
//			Debug.Log($"Camera lerping to waypoint {waypointIndex}: startPos={lerpStartPos}, targetPos={lerpTargetPos}, startLookAt={lerpStartLookAt}, targetLookAt={lerpTargetLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//		}

//		public void OnPuzzleSolved(int waypointIndex)
//		{
//			Debug.Log($"CameraController: OnPuzzleSolved called for waypoint {waypointIndex}, currentIndex={currentWaypointIndex}, state={currentState}");

//			currentState = CameraState.TrackingEggbot;
//			isAtWaypointSet = false;
//			isLerping = false;
//			lerpTimer = 0f;
//			currentLookAt = eggbotController.eggbot.transform.position; // Start tracking Eggbot
//			Debug.Log($"Camera resuming tracking Eggbot from waypoint {waypointIndex}: position={mainCamera.transform.position}, looking at={currentLookAt}");
//		}

//		private bool IsValidVector(VectorData vector)
//		{
//			const float maxValue = 100f; // Tighter bound
//			return vector != null &&
//				   !float.IsNaN(vector.fX) && !float.IsInfinity(vector.fX) && Mathf.Abs(vector.fX) < maxValue &&
//				   !float.IsNaN(vector.fY) && !float.IsInfinity(vector.fY) && Mathf.Abs(vector.fY) < maxValue &&
//				   !float.IsNaN(vector.fZ) && !float.IsInfinity(vector.fZ) && Mathf.Abs(vector.fZ) < maxValue;
//		}

//		private void LookAtTarget(Transform cameraTransform, Vector3 target)
//		{
//			Vector3 direction = target - cameraTransform.position;
//			if (direction.sqrMagnitude < 0.01f)
//			{
//				Debug.LogWarning("CameraController: Target too close to camera, skipping orientation.");
//				return;
//			}

//			if (target.y > cameraTransform.position.y - 0.5f)
//			{
//				target.y = Mathf.Min(target.y, cameraTransform.position.y - 1f);
//				direction = target - cameraTransform.position;
//				Debug.Log($"CameraController: Adjusted target to ensure downward tilt: new target={target}");
//			}

//			cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
//		}

//		private float SigmoidEase(float t)
//		{
//			float k = 10f;
//			return 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));
//		}
//	}
//}


//using UnityEngine;
//using GameDatabase;
//using GamePreviewNamespace;
//using static GameDatabase.DatabaseLoader;

//namespace GamePreviewNamespace
//{
//	public class CameraController : MonoBehaviour
//	{
//		private MapManager mapManager;
//		private EggbotController eggbotController;
//		private Camera mainCamera;

//		private enum CameraState
//		{
//			Idle,
//			TrackingEggbot,
//			LerpingToWaypoint,
//			AtWaypoint
//		}

//		private CameraState currentState;
//		private int currentWaypointIndex;
//		private float lerpTimer;
//		private float lerpDuration = 2f;
//		private Vector3 lerpStartPos;
//		private Quaternion lerpStartRot;
//		private Vector3 lerpTargetPos;
//		private Vector3 lerpTargetLookAt;
//		private Vector3 lerpStartLookAt;
//		private bool isAtWaypointSet;
//		private float idleTimer;
//		private bool isLerping;

//		public void Initialize(MapManager manager, EggbotController eggbot)
//		{
//			mapManager = manager;
//			eggbotController = eggbot;
//			mainCamera = Camera.main;

//			if (mainCamera == null)
//			{
//				Debug.LogError("CameraController: No main camera found!");
//				return;
//			}

//			ResetCamera();
//		}

//		public void ResetCamera()
//		{
//			if (mainCamera == null || mapManager == null)
//			{
//				Debug.LogWarning("CameraController: Cannot reset camera, dependencies not initialized.");
//				return;
//			}

//			currentState = CameraState.Idle;
//			currentWaypointIndex = 0;
//			lerpTimer = 0f;
//			isAtWaypointSet = false;
//			idleTimer = 0.1f;
//			isLerping = false;

//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
//			{
//				Debug.LogWarning("CameraController: No waypoints available, defaulting to safe position.");
//				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
//				mainCamera.transform.position = defaultPos;
//				LookAtTarget(mainCamera.transform, defaultPos + Vector3.forward);
//				Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={defaultPos + Vector3.forward}, up={mainCamera.transform.up}");
//				return;
//			}

//			var firstWaypoint = mapManager.Waypoints[0];
//			Vector3 srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
//			if (!IsValidVector(firstWaypoint.vSrc))
//			{
//				Debug.LogWarning("CameraController: Invalid vSrc in first waypoint, defaulting to safe position.");
//				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
//				mainCamera.transform.position = defaultPos;
//				LookAtTarget(mainCamera.transform, defaultPos + Vector3.forward);
//				Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={defaultPos + Vector3.forward}, up={mainCamera.transform.up}");
//				return;
//			}

//			Vector3 lookAtPos = firstWaypoint.vDst != null && IsValidVector(firstWaypoint.vDst)
//				? new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ)
//				: mapManager.GetTilePosition(firstWaypoint.nTile) + new Vector3(0, 0.5f, 0);

//			mainCamera.transform.position = srcPos;
//			LookAtTarget(mainCamera.transform, lookAtPos);
//			Debug.Log($"Camera initialized at waypoint 0: position={mainCamera.transform.position}, looking at={lookAtPos}, up={mainCamera.transform.up}, vSrc=({firstWaypoint.vSrc.fX}, {firstWaypoint.vSrc.fY}, {firstWaypoint.vSrc.fZ}), vDst={(firstWaypoint.vDst != null ? $"({firstWaypoint.vDst.fX}, {firstWaypoint.vDst.fY}, {firstWaypoint.vDst.fZ})" : "null")}, nTile={firstWaypoint.nTile}");
//		}

//		public void UpdateCamera()
//		{
//			if (mainCamera == null || mapManager == null || eggbotController == null || eggbotController.eggbot == null)
//			{
//				Debug.LogWarning("CameraController: Cannot update, dependencies missing.");
//				return;
//			}

//			switch (currentState)
//			{
//				case CameraState.Idle:
//					idleTimer -= Time.deltaTime;
//					if (idleTimer <= 0 && eggbotController.eggbot != null)
//					{
//						currentState = CameraState.TrackingEggbot;
//						Debug.Log("Camera transitioning to TrackingEggbot");
//					}
//					break;

//				case CameraState.TrackingEggbot:
//					Vector3 eggbotPos = eggbotController.eggbot.transform.position;
//					LookAtTarget(mainCamera.transform, eggbotPos);
//					// Debug.Log($"Camera tracking Eggbot: position={mainCamera.transform.position}, looking at={eggbotPos}, up={mainCamera.transform.up}");
//					break;

//				case CameraState.LerpingToWaypoint:
//					lerpTimer += Time.deltaTime;
//					float t = Mathf.Clamp01(lerpTimer / lerpDuration);
//					float easedT = SigmoidEase(t);

//					mainCamera.transform.position = Vector3.Lerp(lerpStartPos, lerpTargetPos, easedT);
//					Vector3 lookAt = Vector3.Lerp(lerpStartLookAt, lerpTargetLookAt, easedT);
//					LookAtTarget(mainCamera.transform, lookAt);

//					if (t >= 1f)
//					{
//						currentState = CameraState.AtWaypoint;
//						lerpTimer = 0f;
//						isAtWaypointSet = false;
//						isLerping = false;
//						Debug.Log($"Camera reached waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={lerpTargetLookAt}, up={mainCamera.transform.up}");
//					}
//					break;

//				case CameraState.AtWaypoint:
//					if (!isAtWaypointSet)
//					{
//						mainCamera.transform.position = lerpTargetPos;
//						LookAtTarget(mainCamera.transform, lerpTargetLookAt);
//						isAtWaypointSet = true;
//						Debug.Log($"Camera set at waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={lerpTargetLookAt}, up={mainCamera.transform.up}");
//					}
//					break;
//			}
//		}

//		public void OnWaypointReached(int waypointIndex)
//		{
//			if (isLerping || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
//			{
//				Debug.LogWarning($"CameraController: Ignoring waypoint {waypointIndex}: isLerping={isLerping}, invalid index={waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count}");
//				return;
//			}

//			var waypoint = mapManager.Waypoints[waypointIndex];
//			Vector3 srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);

//			// Handle final waypoint: stay at current position, track Eggbot
//			if (waypointIndex == mapManager.Waypoints.Count - 1)
//			{
//				currentState = CameraState.AtWaypoint;
//				currentWaypointIndex = waypointIndex;
//				lerpTargetPos = mainCamera.transform.position;
//				lerpTargetLookAt = eggbotController.eggbot.transform.position;
//				isAtWaypointSet = false;
//				isLerping = false;
//				Debug.Log($"Camera at final waypoint {waypointIndex}: staying at position={mainCamera.transform.position}, looking at={lerpTargetLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//				return;
//			}

//			// Skip lerp if already at or very close to target
//			if (Vector3.Distance(mainCamera.transform.position, srcPos) < 0.1f)
//			{
//				currentState = CameraState.AtWaypoint;
//				currentWaypointIndex = waypointIndex;
//				lerpTargetPos = srcPos;
//				lerpTargetLookAt = waypoint.vDst != null && IsValidVector(waypoint.vDst)
//					? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
//					: eggbotController.eggbot.transform.position;
//				isAtWaypointSet = false;
//				isLerping = false;
//				Debug.Log($"Camera already at waypoint {waypointIndex}: position={mainCamera.transform.position}, looking at={lerpTargetLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//				return;
//			}

//			currentWaypointIndex = waypointIndex;
//			lerpStartPos = mainCamera.transform.position;
//			lerpStartRot = mainCamera.transform.rotation;
//			lerpTargetPos = srcPos;
//			lerpStartLookAt = eggbotController.eggbot.transform.position;
//			isLerping = true;

//			if (!IsValidVector(waypoint.vSrc))
//			{
//				Debug.LogWarning($"CameraController: Invalid vSrc at waypoint {waypointIndex}, staying at current position.");
//				lerpTargetPos = mainCamera.transform.position;
//				isLerping = false;
//				currentState = CameraState.AtWaypoint;
//				lerpTargetLookAt = eggbotController.eggbot.transform.position;
//				isAtWaypointSet = false;
//				Debug.Log($"Camera at waypoint {waypointIndex} with invalid vSrc: staying at position={mainCamera.transform.position}, looking at={lerpTargetLookAt}");
//				return;
//			}

//			lerpTargetLookAt = waypoint.vDst != null && IsValidVector(waypoint.vDst)
//				? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
//				: eggbotController.eggbot.transform.position;

//			currentState = CameraState.LerpingToWaypoint;
//			lerpTimer = 0f;
//			isAtWaypointSet = false;
//			Debug.Log($"Camera lerping to waypoint {waypointIndex}: startPos={lerpStartPos}, targetPos={lerpTargetPos}, startLookAt={lerpStartLookAt}, targetLookAt={lerpTargetLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//		}

//		public void OnPuzzleSolved(int waypointIndex)
//		{
//			Debug.Log($"CameraController: OnPuzzleSolved called for waypoint {waypointIndex}, currentIndex={currentWaypointIndex}, state={currentState}");

//			if (currentState != CameraState.AtWaypoint)
//			{
//				Debug.LogWarning($"CameraController: Puzzle solved ignored, not in AtWaypoint state (state={currentState})");
//				return;
//			}

//			currentState = CameraState.TrackingEggbot;
//			isAtWaypointSet = false;
//			isLerping = false;
//			Debug.Log($"Camera resuming tracking Eggbot from waypoint {waypointIndex}");
//		}

//		private bool IsValidVector(VectorData vector)
//		{
//			const float maxValue = 1000f; // Arbitrary limit for sane coordinates
//			return vector != null &&
//				   !float.IsNaN(vector.fX) && !float.IsInfinity(vector.fX) && Mathf.Abs(vector.fX) < maxValue &&
//				   !float.IsNaN(vector.fY) && !float.IsInfinity(vector.fY) && Mathf.Abs(vector.fY) < maxValue &&
//				   !float.IsNaN(vector.fZ) && !float.IsInfinity(vector.fZ) && Mathf.Abs(vector.fZ) < maxValue;
//		}

//		private void LookAtTarget(Transform cameraTransform, Vector3 target)
//		{
//			Vector3 direction = target - cameraTransform.position;
//			if (direction.sqrMagnitude < 0.01f)
//			{
//				Debug.LogWarning("CameraController: Target too close to camera, skipping orientation.");
//				return;
//			}

//			if (target.y > cameraTransform.position.y - 0.5f)
//			{
//				target.y = Mathf.Min(target.y, cameraTransform.position.y - 1f);
//				direction = target - cameraTransform.position;
//				Debug.Log($"CameraController: Adjusted target to ensure downward tilt: new target={target}");
//			}

//			cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
//		}

//		private float SigmoidEase(float t)
//		{
//			float k = 10f;
//			return 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));
//		}
//	}
//}


//using UnityEngine;
//using GameDatabase;
//using GamePreviewNamespace;
//using static GameDatabase.DatabaseLoader;

//namespace GamePreviewNamespace
//{
//	public class CameraController : MonoBehaviour
//	{
//		private MapManager mapManager;
//		private EggbotController eggbotController;
//		private Camera mainCamera;

//		private enum CameraState
//		{
//			Idle,
//			TrackingEggbot,
//			LerpingToWaypoint,
//			AtWaypoint
//		}

//		private CameraState currentState;
//		private int currentWaypointIndex;
//		private float lerpTimer;
//		private float lerpDuration = 2f;
//		private Vector3 lerpStartPos;
//		private Quaternion lerpStartRot;
//		private Vector3 lerpTargetPos;
//		private Vector3 lerpTargetLookAt;
//		private Vector3 lerpStartLookAt; // New: Start look-at for lerp
//		private bool isAtWaypointSet;
//		private float idleTimer;
//		private bool isLerping; // New: Prevent re-entering lerp

//		public void Initialize(MapManager manager, EggbotController eggbot)
//		{
//			mapManager = manager;
//			eggbotController = eggbot;
//			mainCamera = Camera.main;

//			if (mainCamera == null)
//			{
//				Debug.LogError("CameraController: No main camera found!");
//				return;
//			}

//			ResetCamera();
//		}

//		public void ResetCamera()
//		{
//			if (mainCamera == null || mapManager == null)
//			{
//				Debug.LogWarning("CameraController: Cannot reset camera, dependencies not initialized.");
//				return;
//			}

//			currentState = CameraState.Idle;
//			currentWaypointIndex = 0;
//			lerpTimer = 0f;
//			isAtWaypointSet = false;
//			idleTimer = 0.1f;
//			isLerping = false;

//			if (mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
//			{
//				Debug.LogWarning("CameraController: No waypoints available, defaulting to safe position.");
//				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
//				mainCamera.transform.position = defaultPos;
//				LookAtTarget(mainCamera.transform, defaultPos + Vector3.forward);
//				Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={defaultPos + Vector3.forward}, up={mainCamera.transform.up}");
//				return;
//			}

//			var firstWaypoint = mapManager.Waypoints[0];
//			Vector3 srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
//			if (!IsValidVector(firstWaypoint.vSrc))
//			{
//				Debug.LogWarning("CameraController: Invalid vSrc in first waypoint, defaulting to safe position.");
//				Vector3 defaultPos = new Vector3(5f, 8f, -3f);
//				mainCamera.transform.position = defaultPos;
//				LookAtTarget(mainCamera.transform, defaultPos + Vector3.forward);
//				Debug.Log($"Camera defaulted: position={mainCamera.transform.position}, looking at={defaultPos + Vector3.forward}, up={mainCamera.transform.up}");
//				return;
//			}

//			Vector3 lookAtPos = firstWaypoint.vDst != null && IsValidVector(firstWaypoint.vDst)
//				? new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ)
//				: mapManager.GetTilePosition(firstWaypoint.nTile) + new Vector3(0, 0.5f, 0);

//			mainCamera.transform.position = srcPos;
//			LookAtTarget(mainCamera.transform, lookAtPos);
//			Debug.Log($"Camera initialized at waypoint 0: position={mainCamera.transform.position}, looking at={lookAtPos}, up={mainCamera.transform.up}, vSrc=({firstWaypoint.vSrc.fX}, {firstWaypoint.vSrc.fY}, {firstWaypoint.vSrc.fZ}), vDst={(firstWaypoint.vDst != null ? $"({firstWaypoint.vDst.fX}, {firstWaypoint.vDst.fY}, {firstWaypoint.vDst.fZ})" : "null")}, nTile={firstWaypoint.nTile}");
//		}

//		public void UpdateCamera()
//		{
//			if (mainCamera == null || mapManager == null || eggbotController == null || eggbotController.eggbot == null)
//			{
//				Debug.LogWarning("CameraController: Cannot update, dependencies missing.");
//				return;
//			}

//			switch (currentState)
//			{
//				case CameraState.Idle:
//					idleTimer -= Time.deltaTime;
//					if (idleTimer <= 0 && eggbotController.eggbot != null)
//					{
//						currentState = CameraState.TrackingEggbot;
//						Debug.Log("Camera transitioning to TrackingEggbot");
//					}
//					break;

//				case CameraState.TrackingEggbot:
//					Vector3 eggbotPos = eggbotController.eggbot.transform.position;
//					LookAtTarget(mainCamera.transform, eggbotPos);
//					Debug.Log($"Camera tracking Eggbot: position={mainCamera.transform.position}, looking at={eggbotPos}, up={mainCamera.transform.up}");
//					break;

//				case CameraState.LerpingToWaypoint:
//					lerpTimer += Time.deltaTime;
//					float t = Mathf.Clamp01(lerpTimer / lerpDuration);
//					float easedT = SigmoidEase(t);

//					mainCamera.transform.position = Vector3.Lerp(lerpStartPos, lerpTargetPos, easedT);
//					Vector3 lookAt = Vector3.Lerp(lerpStartLookAt, lerpTargetLookAt, easedT); // Fixed: Lerp look-at points
//					LookAtTarget(mainCamera.transform, lookAt);

//					if (t >= 1f)
//					{
//						currentState = CameraState.AtWaypoint;
//						lerpTimer = 0f;
//						isAtWaypointSet = false;
//						isLerping = false;
//						Debug.Log($"Camera reached waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={lerpTargetLookAt}, up={mainCamera.transform.up}");
//					}
//					break;

//				case CameraState.AtWaypoint:
//					if (!isAtWaypointSet)
//					{
//						mainCamera.transform.position = lerpTargetPos;
//						LookAtTarget(mainCamera.transform, lerpTargetLookAt);
//						isAtWaypointSet = true;
//						Debug.Log($"Camera set at waypoint {currentWaypointIndex}: position={mainCamera.transform.position}, looking at={lerpTargetLookAt}, up={mainCamera.transform.up}");
//					}
//					break;
//			}
//		}

//		public void OnWaypointReached(int waypointIndex)
//		{
//			if (isLerping || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
//			{
//				Debug.LogWarning($"CameraController: Ignoring waypoint {waypointIndex}: isLerping={isLerping}, invalid index={waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count}");
//				return;
//			}

//			var waypoint = mapManager.Waypoints[waypointIndex];
//			Vector3 srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);

//			// Skip lerp if already at or very close to target
//			if (Vector3.Distance(mainCamera.transform.position, srcPos) < 0.1f)
//			{
//				currentState = CameraState.AtWaypoint;
//				currentWaypointIndex = waypointIndex;
//				lerpTargetPos = srcPos;
//				lerpTargetLookAt = waypoint.vDst != null && IsValidVector(waypoint.vDst)
//					? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
//					: eggbotController.eggbot.transform.position;
//				isAtWaypointSet = false;
//				Debug.Log($"Camera already at waypoint {waypointIndex}: position={mainCamera.transform.position}, looking at={lerpTargetLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//				return;
//			}

//			currentWaypointIndex = waypointIndex;
//			lerpStartPos = mainCamera.transform.position;
//			lerpStartRot = mainCamera.transform.rotation;
//			lerpTargetPos = srcPos;
//			lerpStartLookAt = eggbotController.eggbot.transform.position; // Start looking at Eggbot
//			isLerping = true;

//			if (!IsValidVector(waypoint.vSrc))
//			{
//				Debug.LogWarning($"CameraController: Invalid vSrc at waypoint {waypointIndex}, using Eggbot position.");
//				lerpTargetPos = eggbotController.eggbot.transform.position + new Vector3(0, 8, -8);
//			}

//			lerpTargetLookAt = waypoint.vDst != null && IsValidVector(waypoint.vDst)
//				? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
//				: eggbotController.eggbot.transform.position;

//			currentState = CameraState.LerpingToWaypoint;
//			lerpTimer = 0f;
//			isAtWaypointSet = false;
//			Debug.Log($"Camera lerping to waypoint {waypointIndex}: startPos={lerpStartPos}, targetPos={lerpTargetPos}, startLookAt={lerpStartLookAt}, targetLookAt={lerpTargetLookAt}, vSrc=({waypoint.vSrc.fX}, {waypoint.vSrc.fY}, {waypoint.vSrc.fZ}), vDst={(waypoint.vDst != null ? $"({waypoint.vDst.fX}, {waypoint.vDst.fY}, {waypoint.vDst.fZ})" : "null")}, nTile={waypoint.nTile}");
//		}

//		public void OnPuzzleSolved(int waypointIndex)
//		{
//			if (waypointIndex != currentWaypointIndex || currentState != CameraState.AtWaypoint)
//			{
//				Debug.Log($"CameraController: Puzzle solved ignored (index={waypointIndex}, state={currentState})");
//				return;
//			}

//			currentState = CameraState.TrackingEggbot;
//			isAtWaypointSet = false;
//			isLerping = false;
//			Debug.Log($"Camera resuming tracking Eggbot from waypoint {waypointIndex}");
//		}

//		private bool IsValidVector(VectorData vector)
//		{
//			return vector != null && !float.IsNaN(vector.fX) && !float.IsNaN(vector.fY) && !float.IsNaN(vector.fZ);
//		}

//		private void LookAtTarget(Transform cameraTransform, Vector3 target)
//		{
//			Vector3 direction = target - cameraTransform.position;
//			if (direction.sqrMagnitude < 0.01f)
//			{
//				Debug.LogWarning("CameraController: Target too close to camera, skipping orientation.");
//				return;
//			}

//			if (target.y > cameraTransform.position.y - 0.5f)
//			{
//				target.y = Mathf.Min(target.y, cameraTransform.position.y - 1f);
//				direction = target - cameraTransform.position;
//				Debug.Log($"CameraController: Adjusted target to ensure downward tilt: new target={target}");
//			}

//			cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
//		}

//		private float SigmoidEase(float t)
//		{
//			float k = 10f;
//			return 1f / (1f + Mathf.Exp(-k * (t - 0.5f)));
//		}
//	}
//}