using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	// Legacy Compatibility (remove after GameController integration)
	public class LegacyController : MonoBehaviour
	{
		#region Legacy Compatibility
		public static LegacyController instance;
		private void Awake() { instance = this; CameraController.Awake(Camera.main); }

		public void Reset()
		{
			CameraController.Reset();

			var mapManager = GamePreview.mapManager;
			var waypoints = mapManager.Waypoints.Select(w => mapManager.GetTilePosition(w.nTile)).ToList();

			var eggbotController = GamePreview.eggbotController;
			var eggbotRoot = null != eggbotController ? GamePreview.eggbotController.eggbotRoot : null;
			if (mapManager == null || mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
			{
				if (eggbotRoot != null)
					CameraController.SetMode(CameraState.Follow);
				else
				{
					CameraController.SetMode(CameraState.Static);
					CameraController.SetOrigin(new Vector3(0f, 14f, -14f), true);
					CameraController.SetTarget(Vector3.zero, true);
				}
				CameraController.SetPlayer(eggbotRoot);
				CameraController.SetFocusPoints(waypoints);// Pass waypoints to CameraController
				return;
			}

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f); // TS defaults

			var firstWaypoint = mapManager.Waypoints[0];
			if (firstWaypoint.bCamera)
			{
				if (DatabaseLoader.VectorData.IsValidVector(firstWaypoint.vSrc)) srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
				if (DatabaseLoader.VectorData.IsValidVector(firstWaypoint.vDst)) dstPos = new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ);
			}

			CameraController.SetMode(CameraState.Follow);
			CameraController.SetOrigin(srcPos, true);
			CameraController.SetTarget(dstPos, true);
			CameraController.SetPlayer(eggbotRoot);
			CameraController.SetFocusPoints(waypoints);// Pass waypoints to CameraController

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
			if (null != eggbotController && null != eggbotController.eggbotRoot) CameraController.SetPlayer(eggbotController.eggbotRoot);
			CameraController.Update();
			CameraController.Project(Camera.main);
		}

		public void OnWaypointReached(int waypointIndex)
		{
			if (true == CameraController.CinemaActive) return;

			var mapManager = GamePreview.mapManager;
			var eggbotController = GamePreview.eggbotController;
			if (mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
			{
				if (eggbotController?.eggbotRoot != null)
				{
					CameraController.SetMode(CameraState.Follow);
					CameraController.SetPlayer(eggbotController.eggbotRoot);
				}
				return;
			}

			var waypoint = mapManager.Waypoints[waypointIndex];
			var srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);
			if (srcPos == Vector3.zero) srcPos = new Vector3(0f, 14f, -14f);// TS default

			var lookAtPos = waypoint.vDst != null && DatabaseLoader.VectorData.IsValidVector(waypoint.vDst) ? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ) : mapManager.GetTilePosition(waypoint.nTile) + new Vector3(0f, 0.5f, 0f);

			if (waypointIndex == mapManager.Waypoints.Count - 1)
			{
				CameraController.SetMode(CameraState.Follow);
				CameraController.SetTarget(lookAtPos);
				return;
			}

			if (!DatabaseLoader.VectorData.IsValidVector(waypoint.vSrc))
			{
				CameraController.SetMode(CameraState.Follow);
				CameraController.SetPlayer(eggbotController?.eggbotRoot);
				return;
			}

			CameraController.SetMode(CameraState.Preset);
			CameraController.SetOrigin(srcPos);
			CameraController.SetTarget(lookAtPos);
		}

		public void OnPuzzleSolved(int waypointIndex)
		{
			if (true == CameraController.CinemaActive) return;
			CameraController.SetMode(CameraState.Follow);
			CameraController.SetPlayer(null != GamePreview.eggbotController ? GamePreview.eggbotController.eggbotRoot : null);
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
		#endregion
	}
}