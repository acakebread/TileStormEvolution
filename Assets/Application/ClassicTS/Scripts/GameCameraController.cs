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
		
		protected override void SetupCameras()
		{
			if (null == GetComponent<Camera>())
			{
				Debug.LogWarning("Cannot setup camera configs: Camera is null");
				return;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();
			var editorConfig = new CameraConfig
			{
				data = new CameraData(GetComponent<Camera>()) { iorigin = srcPos, itarget = dstPos },
			};

			var followConfig = new CameraConfig
			{
				data = new CameraData(GetComponent<Camera>()) { iorigin = srcPos, itarget = dstPos },
				target = GetTargetPosition()
			};

			var presetConfig = new CameraConfig
			{
				data = new CameraData(GetComponent<Camera>()) { iorigin = srcPos, itarget = dstPos },
				origin = () => srcPos,
				target = GetTargetPosition()
			};

			var orbitConfig = new CameraConfig
			{
				data = new CameraData(GetComponent<Camera>()) { iorigin = srcPos, itarget = dstPos },
				target = GetTargetPosition(),
			};

			var pathConfig = new CameraConfig
			{
				data = new CameraData(GetComponent<Camera>()) { iorigin = srcPos, itarget = dstPos },
				target = GetTargetPosition(),
				//points = GetFocusPoints()
			};

			RegisterCamera(new CameraEditor(editorConfig), CameraMode.Editor);
			RegisterCamera(new CameraFollow(followConfig), CameraMode.Follow);
			RegisterCamera(new CameraPreset(presetConfig), CameraMode.Preset);
			RegisterCamera(new CameraOrbit(orbitConfig), CameraMode.Orbit);
			RegisterCamera(new CameraPath(pathConfig) { pointsFn = GetFocusPoints() }, CameraMode.Path);

			RegisterGroup("EDITOR", new[] { CameraMode.Editor });
			RegisterGroup("PLAYER", new[] { CameraMode.Follow, CameraMode.Preset });
			RegisterGroup("CINEMA", new[] { CameraMode.Path, CameraMode.Orbit });
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

			((CameraPreset)CameraSystems[CameraMode.Preset]).originFn = () => waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			((CameraPreset)CameraSystems[CameraMode.Preset]).targetFn = () => waypoint.vDst != null && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);

			SetCameraMode(CameraMode.Preset, true);
			OnWaypointReachedForGestures?.Invoke(true);
		}

		private void HandlePuzzleSolved(int waypointIndex)
		{
			if (eggbotController == null) return;
			SetCameraMode(CameraMode.Follow, true);
		}

		private void OnDestroy()
		{
			if (null == eggbotController) return;
			eggbotController.OnWaypointReached -= HandleWaypointReached;
			eggbotController.OnPuzzleSolved -= HandlePuzzleSolved;
		}
	}
}