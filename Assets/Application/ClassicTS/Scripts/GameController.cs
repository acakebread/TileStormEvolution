using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		private MapManager mapManager;
		private GestureController gestureController;
		private EggbotController eggbotController;

		private void Awake() => gestureController = gameObject.AddComponent<GestureController>();

		void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			CameraController.SetAutoCinema(PreviewSettings.LaunchInCinemaMode);
			CameraController.Start(Camera.main);
			LoadMap();
		}

		private void LoadMap(string mapName = null)
		{
			mapName ??= PreviewSettings.LoadMapName;
			if (null == mapName) return;

			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			if (null != mapManager) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);
			gestureController.Initialise(mapManager);

			Navigation.SetupWaypoints(currentMap, mapManager);
			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (null != eggbotController)
			{
				eggbotController.Initialise(mapManager);
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}

			CameraController.Reset();//Reset Camera
			CameraController.SetMode(CameraState.Follow);
			CameraController.SetPlayer(eggbotController.transform);
			CameraController.SetFocusPoints(Navigation.Waypoints.Select(w => MapManager.TileWorldPosition(mapManager, w.nTile)).ToList());

			var srcPos = new Vector3(0f, 14f, -14f); // Classic TS default
			var dstPos = Vector3.zero;

			if (null != Navigation.Waypoints && 0 != Navigation.Waypoints.Length)
			{
				dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);// Classic TS default
				srcPos += dstPos;

				var firstWaypoint = Navigation.Waypoints[0];
				if (firstWaypoint.bCamera)
				{
					if (null != firstWaypoint.vSrc) srcPos = firstWaypoint.vSrc.ToVector3();
					if (null != firstWaypoint.vDst) dstPos = firstWaypoint.vDst.ToVector3();
				}
			}

			CameraController.SetOrigin(srcPos, true);
			CameraController.SetTarget(dstPos, true);
		}

		void Update()
		{
			if (null != eggbotController)
			{
				eggbotController.UpdateEggbot(mapManager);
				CameraController.SetPlayer(eggbotController.transform);
			}
			CameraController.Update();
			CameraController.Project(Camera.main);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (CameraController.CinemaActive) return;
			if (null == eggbotController) return;// this can never happen because eggbot invokes this function - but leave the check just in case
			if (null == mapManager || waypointIndex < 0 || waypointIndex >= Navigation.Waypoints.Length) return;//error!
			if (Navigation.Waypoints.Length - 1 == waypointIndex || 0 == waypointIndex) return;//just continue following

			var waypoint = Navigation.Waypoints[waypointIndex];
			if (null == waypoint.vSrc || false == waypoint.vSrc.IsValidVector())
			{
				CameraController.SetMode(CameraState.Follow);
				CameraController.SetPlayer(eggbotController.transform);
				return;
			}

			CameraController.SetMode(CameraState.Preset);
			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f); // TS default
			CameraController.SetOrigin(origin);
			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : MapManager.TileWorldPosition(mapManager, waypoint.nTile);
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

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload")) LoadMap();

			if (GUI.Button(new Rect(120, 10, 100, 30), "Scramble")) mapManager.Scramble();

			if (GUI.Button(new Rect(230, 10, 100, 30), "Solve")) mapManager.Solve();

			if (GUI.Button(new Rect(340, 10, 150, 30), "Previous Level"))
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == PreviewSettings.LoadMapName);
				currentIndex = (DatabaseLoader.Maps.Count + currentIndex - 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				LoadMap();
			}

			if (GUI.Button(new Rect(500, 10, 150, 30), "Next Level"))
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == PreviewSettings.LoadMapName);
				currentIndex = (currentIndex + 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				LoadMap();
			}

			if (GUI.Button(new Rect(660, 10, 150, 30), CameraController.CinemaEnabled ? "Disable Cinematic" : "Enable Cinematic"))
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