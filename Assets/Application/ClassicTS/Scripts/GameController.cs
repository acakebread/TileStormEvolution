using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class GameController : MonoBehaviour
	{
		public MapManager mapManager { get; private set; }
		private GestureController gestureController;
		private EggbotController eggbotController;
		private CameraController cameraController; private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();// Add PlaceholderUI component to this GameObject
			gestureController = gameObject.GetComponent<GestureController>();
			gestureController.OnMapUpdated += CheckDisableDrag;

			cameraController = Camera.main?.GetComponent<CameraController>();
			if (cameraController == null && Camera.main != null)
			{
				cameraController = Camera.main.gameObject.AddComponent<CameraController>();
			}
		}

		private void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			cameraController.SetAutoCinema(PreviewSettings.LaunchInCinemaMode);

			// Load the last map from PlayerPrefs if it exists, otherwise use PreviewSettings
			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
		}

		public void LoadMap(string mapName = null)
		{
			// If no mapName provided, use PlayerPrefs or PreviewSettings for first load
			if (string.IsNullOrEmpty(mapName))
				mapName = null != mapManager ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);

			if (null == mapName) return;

			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			// Update PreviewSettings to reflect the loaded map
			PreviewSettings.LoadMapName = currentMap.name;
			// Save the loaded map name to PlayerPrefs
			PlayerPrefs.SetString("LastLoadedMap", currentMap.name);
			PlayerPrefs.Save();

			// Load skybox using the music name (ToDo: implement custom skybox resource ID in database)
			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.szMusic);

			if (null != mapManager) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);
			mapManager.SetupWaypoints(currentMap);

			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (null != eggbotController)
			{
				eggbotController.Initialise(mapManager);
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}

			cameraController.Reset(); // Reset Camera
			cameraController.SetMode(CameraState.Follow);
			cameraController.SetPlayer(eggbotController.transform);
			cameraController.SetFocusPoints(mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList());

			var srcPos = new Vector3(0f, 14f, -14f); // Classic TS default
			var dstPos = Vector3.zero;

			if (null != mapManager.Waypoints && 0 != mapManager.Waypoints.Length)
			{
				dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f); // Classic TS default
				srcPos += dstPos;

				var firstWaypoint = mapManager.Waypoints[0];
				if (firstWaypoint.bCamera)
				{
					if (null != firstWaypoint.vSrc) srcPos = firstWaypoint.vSrc.ToVector3();
					if (null != firstWaypoint.vDst) dstPos = firstWaypoint.vDst.ToVector3();
				}
			}

			cameraController.SetOrigin(srcPos, true);
			cameraController.SetTarget(dstPos, true);
			cameraController._Update();
			cameraController.Project();

			if (null != eggbotController)
			{
				var postProcessingCameraController = FindFirstObjectByType<PostProcessingCameraController>(FindObjectsInactive.Include);
				if (null != postProcessingCameraController) postProcessingCameraController.target = eggbotController.transform;
			}

			gestureController.Initialise(mapManager);
			if (true == PreviewSettings.DebugMode) Camera.main.fieldOfView = 45;
		}

		void Update()
		{
			if (null != eggbotController) eggbotController.UpdateEggbot(mapManager);
			if (true == PreviewSettings.DebugMode) return;

			if (null != eggbotController) cameraController.SetPlayer(eggbotController.transform);//need this for now
			cameraController._Update();
			cameraController.Project();
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (cameraController.CinemaActive) return;
			if (null == eggbotController) return; // this can never happen because eggbot invokes this function - but leave the check just in case
			if (null == mapManager || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return; // error!
			if (mapManager.Waypoints.Length - 1 == waypointIndex || 0 == waypointIndex) return; // just continue following

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (null == waypoint.vSrc || false == waypoint.vSrc.IsValidVector())
			{
				cameraController.SetMode(CameraState.Follow);
				cameraController.SetPlayer(eggbotController.transform);
				return;
			}

			cameraController.SetMode(CameraState.Preset);
			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f); // Classic TS default
			cameraController.SetOrigin(origin);
			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);
			cameraController.SetTarget(target);
			gestureController.enabled = true;
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (true == cameraController.CinemaActive) return;
			if (null == eggbotController) return; // this can never happen because eggbot invokes this function - but leave the check just in case
			cameraController.SetMode(CameraState.Follow);
			cameraController.SetPlayer(eggbotController.transform);
		}

		private void OnLevelCompleted() { } // => gestureController.enabled = false; ToDo prevent gesture system from re-enabling after level complete - only re-enable after map load/reload

		private void CheckDisableDrag(IMapManager imap) { if (null != eggbotController && 0 != eggbotController.NavDirection(imap)) gestureController.enabled = false; }//check if solved

		private void OnDestroy()
		{
			if (null == eggbotController) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
			gestureController.OnMapUpdated -= CheckDisableDrag;
		}
	}
}



//using UnityEngine;
//using System.Linq;
//using MassiveHadronLtd;

//namespace ClassicTilestorm
//{
//	[RequireComponent(typeof(GestureController))]
//	public class GameController : MonoBehaviour
//	{
//		public MapManager mapManager { get; private set; }
//		private GestureController gestureController;
//		private EggbotController eggbotController;

//		private void Awake()
//		{
//			gameObject.AddComponent<PlaceholderUI>();// Add PlaceholderUI component to this GameObject
//			gestureController = gameObject.GetComponent<GestureController>();
//			gestureController.OnMapUpdated += CheckDisableDrag;
//		}

//		private void Start()
//		{
//			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
//			CameraController.SetAutoCinema(PreviewSettings.LaunchInCinemaMode);
//			CameraController.Start(Camera.main);

//			// Load the last map from PlayerPrefs if it exists, otherwise use PreviewSettings
//			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
//		}

//		public void LoadMap(string mapName = null)
//		{
//			// If no mapName provided, use PlayerPrefs or PreviewSettings for first load
//			if (string.IsNullOrEmpty(mapName))
//				mapName = null != mapManager ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);

//			if (null == mapName) return;

//			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
//			if (null == currentMap)
//			{
//				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
//				return;
//			}

//			// Update PreviewSettings to reflect the loaded map
//			PreviewSettings.LoadMapName = currentMap.name;
//			// Save the loaded map name to PlayerPrefs
//			PlayerPrefs.SetString("LastLoadedMap", currentMap.name);
//			PlayerPrefs.Save();

//			// Load skybox using the music name (ToDo: implement custom skybox resource ID in database)
//			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.szMusic);

//			if (null != mapManager) Destroy(mapManager.gameObject);
//			mapManager = MapManager.Instantiate(currentMap, transform);
//			mapManager.SetupWaypoints(currentMap);

//			if (null != eggbotController) Destroy(eggbotController.gameObject);
//			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
//			if (null != eggbotController)
//			{
//				eggbotController.Initialise(mapManager);
//				eggbotController.OnWaypointReached += OnWaypointReached;
//				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
//				eggbotController.OnLevelCompleted += OnLevelCompleted;
//			}

//			CameraController.Reset(); // Reset Camera
//			CameraController.SetMode(CameraState.Follow);
//			CameraController.SetPlayer(eggbotController.transform);
//			CameraController.SetFocusPoints(mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList());

//			var srcPos = new Vector3(0f, 14f, -14f); // Classic TS default
//			var dstPos = Vector3.zero;

//			if (null != mapManager.Waypoints && 0 != mapManager.Waypoints.Length)
//			{
//				dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f); // Classic TS default
//				srcPos += dstPos;

//				var firstWaypoint = mapManager.Waypoints[0];
//				if (firstWaypoint.bCamera)
//				{
//					if (null != firstWaypoint.vSrc) srcPos = firstWaypoint.vSrc.ToVector3();
//					if (null != firstWaypoint.vDst) dstPos = firstWaypoint.vDst.ToVector3();
//				}
//			}

//			CameraController.SetOrigin(srcPos, true);
//			CameraController.SetTarget(dstPos, true);
//			CameraController.Update();
//			CameraController.Project(Camera.main);

//			if (null != eggbotController)
//			{
//				var postProcessingCameraController = FindFirstObjectByType<PostProcessingCameraController>(FindObjectsInactive.Include);
//				if (null != postProcessingCameraController) postProcessingCameraController.target = eggbotController.transform;
//			}

//			gestureController.Initialise(mapManager);
//			if (true == PreviewSettings.DebugMode) Camera.main.fieldOfView = 45;
//		}

//		void Update()
//		{
//			if (null != eggbotController) eggbotController.UpdateEggbot(mapManager);
//			if (true == PreviewSettings.DebugMode) return;

//			CameraController.Update();
//			CameraController.Project(Camera.main);
//		}

//		private void OnWaypointReached(int waypointIndex)
//		{
//			if (CameraController.CinemaActive) return;
//			if (null == eggbotController) return; // this can never happen because eggbot invokes this function - but leave the check just in case
//			if (null == mapManager || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return; // error!
//			if (mapManager.Waypoints.Length - 1 == waypointIndex || 0 == waypointIndex) return; // just continue following

//			var waypoint = mapManager.Waypoints[waypointIndex];
//			if (null == waypoint.vSrc || false == waypoint.vSrc.IsValidVector())
//			{
//				CameraController.SetMode(CameraState.Follow);
//				CameraController.SetPlayer(eggbotController.transform);
//				return;
//			}

//			CameraController.SetMode(CameraState.Preset);
//			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f); // Classic TS default
//			CameraController.SetOrigin(origin);
//			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);
//			CameraController.SetTarget(target);
//			gestureController.enabled = true;
//		}

//		private void OnPuzzleSolved(int waypointIndex)
//		{
//			if (true == CameraController.CinemaActive) return;
//			if (null == eggbotController) return; // this can never happen because eggbot invokes this function - but leave the check just in case
//			CameraController.SetMode(CameraState.Follow);
//			CameraController.SetPlayer(eggbotController.transform);
//		}

//		private void OnLevelCompleted() { } // => gestureController.enabled = false; ToDo prevent gesture system from re-enabling after level complete - only re-enable after map load/reload

//		private void CheckDisableDrag(IMapManager imap) { if (null != eggbotController && 0 != eggbotController.NavDirection(imap)) gestureController.enabled = false; }//check if solved

//		private void OnDestroy()
//		{
//			if (null == eggbotController) return;
//			eggbotController.OnWaypointReached -= OnWaypointReached;
//			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
//			eggbotController.OnLevelCompleted -= OnLevelCompleted;
//			gestureController.OnMapUpdated -= CheckDisableDrag;
//		}
//	}
//}