using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		private static GameController instance;

		private static MapManager mapManager => MapManager.instance ?? instance.gameObject.AddComponent<MapManager>();
		private static EggbotController eggbotController => EggbotController.instance ?? instance.gameObject.AddComponent<EggbotController>();
		private static GestureController gestureController => GestureController.instance ?? instance.gameObject.AddComponent<GestureController>();

		void Start()
		{
			instance = this;
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			CameraController.Start(Camera.main);
			LoadMap();
			CameraController.SetAutoCinema(PreviewSettings.LaunchInCinemaMode);
		}

		private void Reset()
		{
			mapManager.Reset();
			mapManager.Load(PreviewSettings.LoadMapName);
			eggbotController.Reset();
			gestureController.Reset();
			ResetCamera();

			void ResetCamera()
			{
				CameraController.Reset();

				var waypoints = mapManager.Waypoints.Select(w => mapManager.GetTilePosition(w.nTile)).ToList();
				var eggbotRoot = eggbotController?.eggbotRoot;

				if (null == mapManager || null == mapManager.Waypoints || 0 == mapManager.Waypoints.Count)
				{
					if (null != eggbotRoot)
					{
						CameraController.SetMode(CameraState.Follow);
					}
					else
					{
						CameraController.SetMode(CameraState.Static);
						CameraController.SetOrigin(new Vector3(0f, 14f, -14f), true);
						CameraController.SetTarget(Vector3.zero, true);
					}
					CameraController.SetPlayer(eggbotRoot);
					CameraController.SetFocusPoints(waypoints);
					return;
				}

				var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
				var srcPos = dstPos + new Vector3(0f, 14f, -14f); // TS defaults

				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.bCamera)
				{
					if (DatabaseLoader.VectorData.IsValidVector(firstWaypoint.vSrc))
						srcPos = new Vector3(firstWaypoint.vSrc.fX, firstWaypoint.vSrc.fY, firstWaypoint.vSrc.fZ);
					if (DatabaseLoader.VectorData.IsValidVector(firstWaypoint.vDst))
						dstPos = new Vector3(firstWaypoint.vDst.fX, firstWaypoint.vDst.fY, firstWaypoint.vDst.fZ);
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

		private void LoadMap(string map = null) => Reset();

		void Update()
		{
			eggbotController.UpdateEggbot();
			UpdateCamera();
		}

		private void UpdateCamera()
		{
			if (null != eggbotController && null != eggbotController.eggbotRoot)
				CameraController.SetPlayer(eggbotController.eggbotRoot);
			CameraController.Update();
			CameraController.Project(Camera.main);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (CameraController.CinemaActive)
				return;

			if (mapManager == null || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Count)
			{
				if (null != eggbotController?.eggbotRoot)
				{
					CameraController.SetMode(CameraState.Follow);
					CameraController.SetPlayer(eggbotController.eggbotRoot);
				}
				return;
			}

			var waypoint = mapManager.Waypoints[waypointIndex];
			var srcPos = new Vector3(waypoint.vSrc.fX, waypoint.vSrc.fY, waypoint.vSrc.fZ);
			if (srcPos == Vector3.zero)
				srcPos = new Vector3(0f, 14f, -14f); // TS default

			var lookAtPos = waypoint.vDst != null && DatabaseLoader.VectorData.IsValidVector(waypoint.vDst)
				? new Vector3(waypoint.vDst.fX, waypoint.vDst.fY, waypoint.vDst.fZ)
				: mapManager.GetTilePosition(waypoint.nTile) + new Vector3(0f, 0.5f, 0f);

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

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (CameraController.CinemaActive)
				return;
			CameraController.SetMode(CameraState.Follow);
			CameraController.SetPlayer(eggbotController?.eggbotRoot);
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
			instance = null;
			if (null != eggbotController)
			{
				eggbotController.OnWaypointReached -= OnWaypointReached;
				eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
				eggbotController.OnLevelCompleted -= OnLevelCompleted;
			}
		}
	}
}