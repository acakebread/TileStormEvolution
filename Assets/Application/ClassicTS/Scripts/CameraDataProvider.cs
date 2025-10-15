using System;
using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class CameraDataProvider : MonoBehaviour
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private SpatialBucketSystem spatialSystem = new SpatialBucketSystem(3f, 50);

		public void Initialize(MapManager map, EggbotController eggbot)
		{
			mapManager = map ?? throw new ArgumentNullException(nameof(map));
			eggbotController = eggbot;
		}

		public (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions()
		{
			if (mapManager == null)
			{
				return (new Vector3(0f, 14f, -14f), Vector3.zero);
			}

			var defaultPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = defaultPos + new Vector3(0f, 14f, -14f);
			var dstPos = defaultPos;

			if (mapManager.Waypoints != null && mapManager.Waypoints.Length > 0 && mapManager.Waypoints[0].bCamera)
			{
				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.vSrc != null) srcPos = firstWaypoint.vSrc.ToVector3();
				if (firstWaypoint.vDst != null) dstPos = firstWaypoint.vDst.ToVector3();
			}

			return (srcPos, dstPos);
		}

		public Func<Vector3> GetTargetPosition()
		{
			return () => eggbotController != null ? eggbotController.transform.position : Vector3.zero;
		}

		public Func<IReadOnlyList<Vector3>> GetFocusPoints()
		{
			return () =>
			{
				if (mapManager == null) return Array.Empty<Vector3>();

				var waypoints = mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList();
				spatialSystem.SetPoints(waypoints);

				if (eggbotController != null)
				{
					spatialSystem.TryAddPoint(eggbotController.transform.position);
				}

				return spatialSystem.Points;
			};
		}

		public void HandleWaypointReached(int waypointIndex, Action<CameraMode, Vector3, Vector3> updateCamera)
		{
			if (mapManager == null || eggbotController == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (waypointIndex == 0 || waypointIndex == mapManager.Waypoints.Length - 1) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
			{
				updateCamera(CameraMode.Follow, Vector3.zero, eggbotController.transform.position);
				return;
			}

			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			var target = waypoint.vDst != null && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			updateCamera(CameraMode.Preset, origin, target);
		}

		public void HandlePuzzleSolved(int waypointIndex, Action<CameraMode> updateCamera)
		{
			if (eggbotController == null) return;
			updateCamera(CameraMode.Follow);
		}
	}
}