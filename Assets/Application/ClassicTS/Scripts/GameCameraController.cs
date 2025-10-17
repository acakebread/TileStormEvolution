using UnityEngine;
using MassiveHadronLtd;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public class GameCameraController : CameraController
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private SpatialBucketSystem spatialSystem;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		public event Action<bool> OnWaypointReachedForGestures;

		public void Initialise(MapManager map, EggbotController eggbot, CameraMode initialMode = CameraMode.Follow)
		{
			mapManager = map ?? throw new ArgumentNullException(nameof(map));
			eggbotController = eggbot;
			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);
			if (eggbotController != null)
			{
				eggbotController.OnWaypointReached += HandleWaypointReached;
				eggbotController.OnPuzzleSolved += HandlePuzzleSolved;
			}
			base.Initialise(initialMode);
		}

		protected override (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions()
		{
			if (mapManager == null)
				return (new Vector3(0f, 14f, -14f), Vector3.zero);

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);

			if (mapManager.Waypoints?.Length > 0 && mapManager.Waypoints[0].bCamera)
			{
				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.vSrc != null) srcPos = firstWaypoint.vSrc.ToVector3();
				if (firstWaypoint.vDst != null) dstPos = firstWaypoint.vDst.ToVector3();
			}

			return (srcPos, dstPos);
		}

		protected override Func<Vector3> GetTargetPosition()
		{
			return () => eggbotController != null && eggbotController.transform != null
				? eggbotController.transform.position
				: Vector3.zero;
		}

		protected override Func<IReadOnlyList<Vector3>> GetFocusPoints()
		{
			if (mapManager == null) return () => Array.Empty<Vector3>();

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

		protected override void SetupCameraStates()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("Cannot setup camera states: Camera is null");
				return;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();
			var editorState = new CameraState
			{
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				origin = () => srcPos,
				target = GetTargetPosition(),
				points = GetFocusPoints()
			};

			var playerState = new CameraState
			{
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = GetTargetPosition(),
				origin = () => srcPos
			};

			var directState = new CameraState
			{
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = () => dstPos,
				origin = () => srcPos
			};

			var cinemaState = new CameraState
			{
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = GetTargetPosition(),
				points = GetFocusPoints()
			};

			RegisterState(editorState, new[] { CameraMode.Editor });
			RegisterState(playerState, new[] { CameraMode.Follow, CameraMode.Preset });
			RegisterState(directState, new[] { CameraMode.Direct });
			RegisterState(cinemaState, new[] { CameraMode.Cinema });
		}

		private void HandleWaypointReached(int waypointIndex)
		{
			if (eggbotController == null || mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (waypointIndex == 0 || waypointIndex == mapManager.Waypoints.Length - 1) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
			{
				SetCameraMode(CameraMode.Follow, true);
				return;
			}

			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			var target = waypoint.vDst != null && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			var playerState = GetStateForMode(CameraMode.Preset);
			if (playerState != null)
			{
				playerState.origin = () => origin;
				playerState.target = () => target;
				SetCameraMode(CameraMode.Preset, true);
				OnWaypointReachedForGestures?.Invoke(true);
			}
		}

		private void HandlePuzzleSolved(int waypointIndex)
		{
			if (eggbotController == null) return;

			var playerState = GetStateForMode(CameraMode.Follow);
			if (playerState != null)
			{
				playerState.target = GetTargetPosition();
				SetCameraMode(CameraMode.Follow, true);
			}
		}

		private void OnDestroy()
		{
			if (eggbotController != null)
			{
				eggbotController.OnWaypointReached -= HandleWaypointReached;
				eggbotController.OnPuzzleSolved -= HandlePuzzleSolved;
			}
		}
	}
}