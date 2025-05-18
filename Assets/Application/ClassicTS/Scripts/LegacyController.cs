using System.Linq;
using UnityEngine;
using static CameraController;

namespace ClassicTilestorm
{
	// Legacy Compatibility (remove after GameController integration)
	public class LegacyController : MonoBehaviour
	{
		#region Legacy Compatibility
		public static LegacyController instance;
		private void Awake() { instance = this; Reset(); }

		public void Reset()
		{
			CameraController.Reset();
			Initialize();
			SetMode(CameraState.Static);

			var mapManager = GamePreview.mapManager;
			var eggbotController = GamePreview.eggbotController;

			if (eggbotController?.eggbotRoot != null)
				SetPlayer(eggbotController.eggbotRoot);

			if (mapManager == null || mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
			{
				if (eggbotController?.eggbotRoot != null)
				{
					SetMode(CameraState.Follow);
				}
				else
				{
					SetOrigin(new Vector3(0f, 14f, -14f));
					SetTarget(Vector3.zero);
				}
				UpdateCameraTransform();
				return;
			}

			// Pass waypoints to CameraController
			var waypoints = mapManager.Waypoints.Select(w => mapManager.GetTilePosition(w.nTile)).ToList();
			SetFocusPoints(waypoints);

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f); // TS defaults

			var firstWaypoint = mapManager.Waypoints[0];
			if (firstWaypoint.bCamera)
			{
				if (IsValidVector(firstWaypoint.vSrc)) srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
				if (IsValidVector(firstWaypoint.vDst)) dstPos = new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ);
			}

			SetOrigin(srcPos);
			SetTarget(dstPos);
			SetMode(CameraState.Follow);

			UpdateCameraTransform();

			if (eggbotController != null)
			{
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}
		}

		public void UpdateCamera()
		{
			var eggbotController = GamePreview.eggbotController;
			if (eggbotController != null && eggbotController.eggbotRoot != null)
				SetPlayer(eggbotController.eggbotRoot);
			Update();
		}

		public void OnWaypointReached(int waypointIndex)
		{
			if (true == CinemaActive) return;

			var mapManager = GamePreview.mapManager;
			var eggbotController = GamePreview.eggbotController;
			if (mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
			{
				if (eggbotController?.eggbotRoot != null)
				{
					SetMode(CameraState.Follow);
					SetPlayer(eggbotController.eggbotRoot);
				}
				return;
			}

			var waypoint = mapManager.Waypoints[waypointIndex];
			Vector3 srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);
			if (srcPos == Vector3.zero) srcPos = new Vector3(0f, 14f, -14f);

			Vector3 lookAtPos = waypoint.vDst != null && IsValidVector(waypoint.vDst) ? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ) : mapManager.GetTilePosition(waypoint.nTile) + new Vector3(0f, 0.5f, 0f);

			if (waypointIndex == mapManager.Waypoints.Count - 1)
			{
				SetMode(CameraState.Follow);
				SetTarget(lookAtPos);
				return;
			}

			if (!IsValidVector(waypoint.vSrc))
			{
				SetMode(CameraState.Follow);
				SetPlayer(eggbotController?.eggbotRoot);
				return;
			}

			SetMode(CameraState.Preset);
			SetOrigin(srcPos);
			SetTarget(lookAtPos);
		}

		public void OnPuzzleSolved(int waypointIndex)
		{
			if (true == CinemaActive) return;
			SetMode(CameraState.Follow);
			SetPlayer(GamePreview.eggbotController?.eggbotRoot);
		}

		public void OnLevelCompleted() { }

		private void OnDestroy()
		{
			instance = null; 
			var eggbotController = EggbotController.instance;
			if (null != eggbotController)
			{
				eggbotController.OnWaypointReached -= OnWaypointReached;
				eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
				eggbotController.OnLevelCompleted -= OnLevelCompleted;
			}
		}

		private const float MaxVectorValue = 100f;
		private static bool IsValidVector(DatabaseLoader.VectorData vector)
		{
			if (vector == null)
				return false;

			bool valid = !float.IsNaN(vector.fX) && !float.IsInfinity(vector.fX) && Mathf.Abs(vector.fX) < MaxVectorValue &&
						 !float.IsNaN(vector.fY) && !float.IsInfinity(vector.fY) && Mathf.Abs(vector.fY) < MaxVectorValue &&
						 !float.IsNaN(vector.fZ) && !float.IsInfinity(vector.fZ) && Mathf.Abs(vector.fZ) < MaxVectorValue;

			if (!valid)
				Debug.LogWarning($"Invalid vector: fX={vector?.fX}, fY={vector?.fY}, fZ={vector?.fZ}");

			return valid;
		}
		#endregion
	}
}