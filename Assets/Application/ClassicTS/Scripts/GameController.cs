using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		private EggbotController eggbotController;
		public static MapManager mapManager;// public static for now - ToDo make private property and handle external requirements

		void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			CameraController.Start(Camera.main);
			LoadMap();
			CameraController.SetAutoCinema(PreviewSettings.LaunchInCinemaMode);
		}

		private void LoadMap(string map = null)
		{
			if (null != mapManager) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(transform, map ?? PreviewSettings.LoadMapName);
			(GestureController.instance ?? gameObject.AddComponent<GestureController>()).Reset();
			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(transform, mapManager.EggbotCostume);

			ResetCamera();

			void ResetCamera()
			{
				CameraController.Reset();

				var waypoints = mapManager.Waypoints.Select(w => mapManager.GetTilePosition(w.nTile)).ToList();
				var eggbotRoot = eggbotController.transform;

				if (null == mapManager || null == mapManager.Waypoints || 0 == mapManager.Waypoints.Length)
				{
					CameraController.SetMode(null != eggbotRoot ? CameraState.Follow : CameraState.Static);
					CameraController.SetOrigin(new Vector3(0f, 14f, -14f), true); // TS defaults
					CameraController.SetTarget(Vector3.zero, true);
					CameraController.SetPlayer(eggbotRoot);
					CameraController.SetFocusPoints(waypoints);
					return;
				}

				var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
				var srcPos = dstPos + new Vector3(0f, 14f, -14f); // TS defaults

				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.bCamera)
				{
					if (null != firstWaypoint.vSrc) srcPos = firstWaypoint.vSrc.ToVector3();
					if (null != firstWaypoint.vDst) dstPos = firstWaypoint.vDst.ToVector3();
				}

				CameraController.SetMode(CameraState.Follow);
				CameraController.SetOrigin(srcPos, true);
				CameraController.SetTarget(dstPos, true);
				CameraController.SetPlayer(eggbotRoot);
				CameraController.SetFocusPoints(waypoints);

				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}
		}

		void Update()
		{
			eggbotController.UpdateEggbot();
			UpdateCamera();

			void UpdateCamera()
			{
				CameraController.SetPlayer(null != eggbotController ? eggbotController.transform : null);
				CameraController.Update();
				CameraController.Project(Camera.main);
			}
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (CameraController.CinemaActive) return;
			if (null == eggbotController) return;// this can never happen because eggbot invokes this function - but leave the check just in case
			if (null == mapManager || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;//error!
			if (mapManager.Waypoints.Length - 1 == waypointIndex || 0 == waypointIndex) return;//just continue following

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (null == waypoint.vSrc || false == waypoint.vSrc.IsValidVector())
			{
				CameraController.SetMode(CameraState.Follow);
				CameraController.SetPlayer(eggbotController.transform);
				return;
			}

			CameraController.SetMode(CameraState.Preset);
			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f); // TS default
			CameraController.SetOrigin(origin);
			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.GetTilePosition(waypoint.nTile) + new Vector3(0f, 0.5f, 0f);
			CameraController.SetTarget(target);
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (true == CameraController.CinemaActive) return;
			if (null == eggbotController) return;// this can never happen because eggbot invokes this function - but leave the check just in case
			CameraController.SetMode(CameraState.Follow);
			CameraController.SetPlayer(eggbotController.transform);
		}

		private void OnLevelCompleted() { }

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload"))
			{
				LoadMap();
			}

			if (GUI.Button(new Rect(120, 10, 100, 30), "Solve"))
			{
				mapManager.Solve();
			}

			if (GUI.Button(new Rect(230, 10, 150, 30), "Previous Level"))
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (DatabaseLoader.Maps.Count + currentIndex - 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				LoadMap();
			}

			if (GUI.Button(new Rect(390, 10, 150, 30), "Next Level"))
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				LoadMap();
			}

			if (GUI.Button(new Rect(550, 10, 150, 30), CameraController.CinemaEnabled ? "Disable Cinematic" : "Enable Cinematic"))
			{
				CameraController.SetAutoCinema(!CameraController.CinemaEnabled);
				CameraController.Refresh(Time.time - (CameraController.CinemaEnabled ? 999 : 0));
			}
		}

		private void OnDestroy()
		{
			if (null == eggbotController) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
		}
	}
}