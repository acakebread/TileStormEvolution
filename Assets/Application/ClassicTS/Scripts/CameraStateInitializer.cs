using System;
using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public class CameraStateInitializer : MonoBehaviour
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private SpatialBucketSystem spatialSystem;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		public void Initialize(MapManager map, EggbotController eggbot, CameraController cameraController)
		{
			mapManager = map ?? throw new ArgumentNullException(nameof(map));
			eggbotController = eggbot;
			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);

			var (srcPos, dstPos) = GetInitialCameraPositions();
			SetupCameraStates(cameraController, srcPos, dstPos);
		}

		public (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions()
		{
			if (mapManager == null)
			{
				return (new Vector3(0f, 14f, -14f), Vector3.zero);
			}

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);

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
			return () => eggbotController != null && eggbotController.transform != null
				? eggbotController.transform.position
				: Vector3.zero;
		}

		public Func<IReadOnlyList<Vector3>> GetFocusPoints()
		{
			Func<IReadOnlyList<Vector3>> focusFunc = () =>
			{
				var waypoints = mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList();
				spatialSystem.SetPoints(waypoints);

				focusFunc = () =>
				{
					if (eggbotController != null && eggbotController.transform != null)
						spatialSystem.TryAddPoint(eggbotController.transform.position);
					return spatialSystem.Points;
				};

				return spatialSystem.Points;
			};

			return focusFunc;
		}

		private void SetupCameraStates(CameraController cameraController, Vector3 srcPos, Vector3 dstPos)
		{
			var editorState = new CameraState
			{
				mode = CameraMode.Editor,
				data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos }
			};

			var playerState = new CameraState
			{
				mode = CameraMode.Follow,
				data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = GetTargetPosition(),
				origin = () => srcPos
			};

			var cinemaState = new CameraState
			{
				mode = CameraMode.Cinema,
				data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = GetTargetPosition(),
				points = GetFocusPoints()
			};

			cameraController.RegisterState(editorState, new[] { CameraMode.Editor });
			cameraController.RegisterState(playerState, new[] { CameraMode.Follow, CameraMode.Preset });
			cameraController.RegisterState(cinemaState, new[] { CameraMode.Cinema });
		}
	}
}