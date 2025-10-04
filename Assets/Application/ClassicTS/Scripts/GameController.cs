using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class GameController : MonoBehaviour
	{
		private MapManager mapManager;
		private GestureController gestureController;
		private EggbotController eggbotController;
		private bool locked = false; // true while player is dragging tiles
		private bool isFirstLoad = true; // Flag to track first map load after launch
		private Material defaultSkycubeMaterial;

		private void Awake()
		{
			defaultSkycubeMaterial = RenderSettings.skybox;
			gestureController = gameObject.GetComponent<GestureController>();

			// Add PlaceholderUI component to this GameObject
			gameObject.AddComponent<PlaceholderUI>();
		}

		private void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			CameraController.SetAutoCinema(PreviewSettings.LaunchInCinemaMode);
			CameraController.Start(Camera.main);

			var gestureSystem = gameObject.GetComponent<GestureSystem>();
			gestureSystem.OnBeginDrag += (screenPos) => locked = true;
			gestureSystem.OnEndDrag += (screenPos) => locked = false;

			// Load the last map from PlayerPrefs if it exists, otherwise use PreviewSettings
			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
		}

		public void LoadMap(string mapName = null)
		{
			// If no mapName provided, use PlayerPrefs or PreviewSettings for first load
			if (string.IsNullOrEmpty(mapName))
			{
				mapName = isFirstLoad ? PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName) : PreviewSettings.LoadMapName;
			}

			if (null == mapName) return;

			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			LoadSkybox(currentMap.szMusic); // temporarily use the music name ToDo implement custom skybox resource ID in database

			// Update PreviewSettings to reflect the loaded map
			PreviewSettings.LoadMapName = currentMap.name;

			// Save the loaded map name to PlayerPrefs
			PlayerPrefs.SetString("LastLoadedMap", currentMap.name);
			PlayerPrefs.Save();

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

			CameraController.Reset(); // Reset Camera
			CameraController.SetMode(CameraState.Follow);
			CameraController.SetPlayer(eggbotController.transform);
			CameraController.SetFocusPoints(Navigation.Waypoints.Select(w => MapManager.TileWorldPosition(mapManager, w.nTile)).ToList());

			var srcPos = new Vector3(0f, 14f, -14f); // Classic TS default
			var dstPos = Vector3.zero;

			if (null != Navigation.Waypoints && 0 != Navigation.Waypoints.Length)
			{
				dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f); // Classic TS default
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
			CameraController.Update();
			CameraController.Project(Camera.main);
			gestureController.enabled = false;
			locked = false;

			// Mark first load as complete
			isFirstLoad = false;

			if (null != eggbotController)
			{
				var postProcessingCameraController = FindFirstObjectByType<PostProcessingCameraController>(FindObjectsInactive.Include);
				if (null != postProcessingCameraController)
					postProcessingCameraController.target = eggbotController.transform;
			}
			if (true == PreviewSettings.DebugMode) Camera.main.fieldOfView = 45;

			//local function
			void LoadSkybox(string skycube)
			{
				var skybox = $"{PreviewSettings.SkycubesPath}{skycube}Skybox".Replace(".mat", "");
				var material = Resources.Load<Material>(skybox);
				RenderSettings.skybox = material ? material : defaultSkycubeMaterial;
			}
		}

		void Update()
		{
			if (null != eggbotController)
			{
				eggbotController.UpdateEggbot(locked ? null : mapManager);
				CameraController.SetPlayer(eggbotController.transform);
			}
			if (true == PreviewSettings.DebugMode) return;

			CameraController.Update();
			CameraController.Project(Camera.main);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (CameraController.CinemaActive) return;
			if (null == eggbotController) return; // this can never happen because eggbot invokes this function - but leave the check just in case
			if (null == mapManager || waypointIndex < 0 || waypointIndex >= Navigation.Waypoints.Length) return; // error!
			if (Navigation.Waypoints.Length - 1 == waypointIndex || 0 == waypointIndex) return; // just continue following

			var waypoint = Navigation.Waypoints[waypointIndex];
			if (null == waypoint.vSrc || false == waypoint.vSrc.IsValidVector())
			{
				CameraController.SetMode(CameraState.Follow);
				CameraController.SetPlayer(eggbotController.transform);
				return;
			}

			gestureController.enabled = true;
			CameraController.SetMode(CameraState.Preset);
			var origin = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f); // TS default
			CameraController.SetOrigin(origin);
			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : MapManager.TileWorldPosition(mapManager, waypoint.nTile);
			CameraController.SetTarget(target);
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (true == CameraController.CinemaActive) return;
			if (null == eggbotController) return; // this can never happen because eggbot invokes this function - but leave the check just in case
			CameraController.SetMode(CameraState.Follow);
			CameraController.SetPlayer(eggbotController.transform);
			gestureController.enabled = false;
		}

		private void OnLevelCompleted() { } // => gestureController.enabled = false; ToDo prevent gesture system from re-enabling after level complete - only re-enable after map load/reload

		private void OnDestroy()
		{
			if (null == eggbotController) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
		}
	}
}