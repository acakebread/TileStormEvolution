using System.Linq;
using UnityEngine;
using static CameraController;

namespace ClassicTilestorm
{
	// Legacy Compatibility (remove after GameController integration)
	public class LegacyController : MonoBehaviour
	{
		#region Legacy Compatibility
		public void Initialize()
		{
			CameraController.Initialize();
			//SetAutoCinema();

			SetMode(CameraState.Static);

			var mapManager = GamePreview.mapManager;
			var eggbotController = GamePreview.eggbotController;

			if (mapManager == null || mapManager.Waypoints == null || mapManager.Waypoints.Count == 0)
			{
				if (eggbotController?.eggbotRoot != null)
				{
					SetMode(CameraState.Follow);
					SetPlayer(eggbotController.eggbotRoot.position);
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
			SetWaypoints(waypoints);

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f); // TS defaults

			var firstWaypoint = mapManager.Waypoints[0];
			if (firstWaypoint.bCamera)
			{
				if (CameraUtils.IsValidVector(firstWaypoint.vSrc)) srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
				if (CameraUtils.IsValidVector(firstWaypoint.vDst)) dstPos = new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ);
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
			if (eggbotController == null || eggbotController.eggbotRoot == null)
			{
				Update();
				return;
			}

			//if (State == CameraState.Follow)
				SetPlayer(eggbotController.eggbotRoot.position);

			Update();
		}

		public void OnWaypointReached(int waypointIndex)
		{
			var mapManager = GamePreview.mapManager;
			var eggbotController = GamePreview.eggbotController;
			if (mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
			{
				if (eggbotController?.eggbotRoot != null)
				{
					SetMode(CameraState.Follow);
					SetPlayer(eggbotController.eggbotRoot.position);
				}
				return;
			}

			var waypoint = mapManager.Waypoints[waypointIndex];
			Vector3 srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);
			if (srcPos == Vector3.zero) srcPos = new Vector3(0f, 14f, -14f);

			Vector3 lookAtPos = waypoint.vDst != null && CameraUtils.IsValidVector(waypoint.vDst)
				? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
				: mapManager.GetTilePosition(waypoint.nTile) + new Vector3(0f, 0.5f, 0f);

			if (waypointIndex == mapManager.Waypoints.Count - 1)
			{
				SetMode(CameraState.Follow);
				SetTarget(lookAtPos);
				return;
			}

			if (!CameraUtils.IsValidVector(waypoint.vSrc))
			{
				SetMode(CameraState.Follow);
				SetPlayer(eggbotController?.eggbotRoot.position ?? Vector3.zero);
				return;
			}

			SetMode(CameraState.Preset);
			SetOrigin(srcPos);
			SetTarget(lookAtPos);
		}

		public void OnPuzzleSolved(int waypointIndex)
		{
			var eggbotController = GamePreview.eggbotController;
			SetMode(CameraState.Follow);
			SetPlayer(eggbotController?.eggbotRoot.position ?? Vector3.zero);
		}

		public void OnLevelCompleted() { }

		private void OnDestroy()
		{
			var eggbotController = GamePreview.eggbotController;
			if (eggbotController != null)
			{
				eggbotController.OnWaypointReached -= OnWaypointReached;
				eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
				eggbotController.OnLevelCompleted -= OnLevelCompleted;
			}
		}
		#endregion
	}
}