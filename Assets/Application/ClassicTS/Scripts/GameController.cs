using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using System;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureController))]
	public class GameController : MonoBehaviour
	{
		public MapManager mapManager { get; private set; }
		private GestureController gestureController;
		private EggbotController eggbotController;
		private CameraController cameraController; private SpatialBucketSystem spatialSystem;
		private const int MaxFocusPoints = 50;
		private const float MinDistanceForNewFocusPoint = 3f;

		private const float CinemaTimeoutDuration = 5f;
		private float timeStart;

		private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
			gestureController = gameObject.GetComponent<GestureController>();
			gestureController.OnMapUpdated += CheckDisableDrag;

			cameraController = null != Camera.main ? Camera.main.GetComponent<CameraController>() : null;
			if (null == cameraController && null != Camera.main) cameraController = Camera.main.gameObject.AddComponent<CameraController>();
		}

		private void Start()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
		}

		private void Update()
		{
			if (null != eggbotController) eggbotController.UpdateEggbot(mapManager);
			CinemaUpdate();
		}

		private void CinemaUpdate()
		{
			if (null == cameraController || null == cameraController.CameraSystem) return;

			bool startCinema = PreviewSettings.CinemaMode ? cameraController.CameraSystem.HasCompleted : PreviewSettings.CinemaMode && Time.time - timeStart > CinemaTimeoutDuration;
			if (!startCinema) return;

			timeStart = Time.time;
			cameraController.SetMode(CameraState.Cinema);
		}

		public void ToggleCinemma(bool force = false)
		{
			PreviewSettings.CinemaMode = !PreviewSettings.CinemaMode;
			if (PreviewSettings.CinemaMode) timeStart = force ? Time.time - CinemaTimeoutDuration : Time.time;
			cameraController.SetMode(PreviewSettings.CinemaMode ? CameraState.Cinema : cameraController.RestoreState);
		}

		public void ToggleEditor()
		{
			PreviewSettings.EditorMode = !PreviewSettings.EditorMode;
			cameraController.SetMode(PreviewSettings.EditorMode ? CameraState.Editor : cameraController.RestoreState);
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName)) mapName = mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);
			if (null == mapName) return;

			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			PreviewSettings.LoadMapName = currentMap.name;
			PlayerPrefs.SetString("LastLoadedMap", currentMap.name);
			PlayerPrefs.Save();

			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.szMusic);

			if (null != mapManager) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);
			mapManager.SetupWaypoints(currentMap);

			gestureController.Initialise(mapManager);
			timeStart = Time.time;

			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (null != eggbotController)
			{
				eggbotController.Initialise(mapManager);
				eggbotController.OnWaypointReached += OnWaypointReached;
				eggbotController.OnPuzzleSolved += OnPuzzleSolved;
				eggbotController.OnLevelCompleted += OnLevelCompleted;
			}

			cameraController.Reset();

			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);

			// Create a single CameraDelegates instance
			var delegates = new CameraDelegates { target = () => null != eggbotController && null != eggbotController.transform ? eggbotController.transform.position : Vector3.zero };

			// Define the "initial" version (this will replace itself after running once)
			Func<IReadOnlyList<Vector3>> focusFunc = () =>
			{
				// First-time setup: populate spatial points from waypoints
				var waypoints = mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList();
				spatialSystem.SetPoints(waypoints);

				// Now redefine focusFunc for all *future* calls
				focusFunc = () =>
				{
					if (null != eggbotController && null != eggbotController?.transform)
						spatialSystem.TryAddPoint(eggbotController.transform.position);
					return spatialSystem.Points;
				};

				// Return the initialized points for this first call
				return spatialSystem.Points;
			};

			// Assign the initial delegate
			delegates.focusPoints = () => focusFunc();

			// Register delegates once
			cameraController.delegates = () => delegates;

			var ppController = cameraController.GetComponentInChildren<PostProcessingCameraController>(true);
			if (null != eggbotController && null != ppController)
				ppController.dofTarget = eggbotController.transform;

			cameraController.SetMode(PreviewSettings.EditorMode ? CameraState.Editor : CameraState.Follow);

			var dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
			var srcPos = dstPos + new Vector3(0f, 14f, -14f);// Classic TS default for no waypoints

			if (null != mapManager.Waypoints && 0 != mapManager.Waypoints.Length && true == mapManager.Waypoints[0].bCamera)
			{
				var firstWaypoint = mapManager.Waypoints[0];
				if (null != firstWaypoint.vSrc) srcPos = firstWaypoint.vSrc.ToVector3();
				if (null != firstWaypoint.vDst) dstPos = firstWaypoint.vDst.ToVector3();
			}

			cameraController.SetOrigin(srcPos, true);
			cameraController.SetTarget(dstPos, true);
		}

		private void OnWaypointReached(int waypointIndex)
		{
			if (CameraState.Cinema == cameraController.CurrentState || CameraState.Editor == cameraController.CurrentState) return;
			if (null == eggbotController) return;
			if (null == mapManager || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
			if (mapManager.Waypoints.Length - 1 == waypointIndex || waypointIndex == 0) return;

			var waypoint = mapManager.Waypoints[waypointIndex];
			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
			{
				cameraController.SetMode(CameraState.Follow);
				return;
			}

			var position = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
			cameraController.SetOrigin(position);
			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);
			cameraController.SetTarget(target);
			cameraController.SetMode(CameraState.Preset);
			gestureController.enabled = true;
		}

		private void OnPuzzleSolved(int waypointIndex)
		{
			if (CameraState.Cinema == cameraController.CurrentState || CameraState.Editor == cameraController.CurrentState) return;
			if (null == eggbotController) return;
			cameraController.SetMode(CameraState.Follow);
		}

		private void OnLevelCompleted() { }

		private void CheckDisableDrag(IMapManager imap)
		{
			if (null != eggbotController && 0 != eggbotController.NavDirection(imap))
				gestureController.enabled = false;
		}

		private void OnDestroy()
		{
			gestureController.OnMapUpdated -= CheckDisableDrag;
			if (null == eggbotController) return;
			eggbotController.OnWaypointReached -= OnWaypointReached;
			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
			eggbotController.OnLevelCompleted -= OnLevelCompleted;
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
//		private CameraController cameraController;

//		private SpatialBucketSystem spatialSystem;
//		private const int MaxFocusPoints = 50;
//		private const float MinDistanceForNewFocusPoint = 3f;

//		private const float CinemaTimeoutDuration = 5f;
//		private float timeStart;

//		private void Awake()
//		{
//			gameObject.AddComponent<PlaceholderUI>();
//			gestureController = gameObject.GetComponent<GestureController>();
//			gestureController.OnMapUpdated += CheckDisableDrag;

//			cameraController = null != Camera.main ? Camera.main.GetComponent<CameraController>() : null;
//			if (null == cameraController && null != Camera.main) cameraController = Camera.main.gameObject.AddComponent<CameraController>();
//		}

//		private void Start()
//		{
//			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
//			LoadMap(PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName));
//		}

//		private void Update()
//		{
//			if (null != eggbotController) eggbotController.UpdateEggbot(mapManager);
//			CinemaUpdate();
//		}

//		private void CinemaUpdate()
//		{
//			if (null == cameraController || null == cameraController.CameraSystem) return;

//			bool startCinema = PreviewSettings.CinemaMode ? cameraController.CameraSystem.HasCompleted : PreviewSettings.CinemaMode && Time.time - timeStart > CinemaTimeoutDuration;
//			if (!startCinema) return;

//			timeStart = Time.time;
//			cameraController.SetMode(CameraState.Cinema);
//		}

//		public void ToggleCinemma(bool force = false)
//		{
//			PreviewSettings.CinemaMode = !PreviewSettings.CinemaMode;
//			if (PreviewSettings.CinemaMode) timeStart = force ? Time.time - CinemaTimeoutDuration : Time.time;
//			cameraController.SetMode(PreviewSettings.CinemaMode ? CameraState.Cinema : cameraController.RestoreState);
//		}

//		public void ToggleEditor()
//		{
//			PreviewSettings.EditorMode = !PreviewSettings.EditorMode;
//			cameraController.SetMode(PreviewSettings.EditorMode ? CameraState.Editor : cameraController.RestoreState);
//		}

//		public void LoadMap(string mapName = null)
//		{
//			if (string.IsNullOrEmpty(mapName)) mapName = mapManager != null ? PreviewSettings.LoadMapName : PlayerPrefs.GetString("LastLoadedMap", PreviewSettings.LoadMapName);
//			if (null == mapName) return;

//			var currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.Maps.FirstOrDefault() : DatabaseLoader.Maps.FirstOrDefault(m => m.name == mapName);
//			if (null == currentMap)
//			{
//				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.Maps.Select(m => m.name))}");
//				return;
//			}

//			PreviewSettings.LoadMapName = currentMap.name;
//			PlayerPrefs.SetString("LastLoadedMap", currentMap.name);
//			PlayerPrefs.Save();

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

//			cameraController.Reset();
//			spatialSystem = new SpatialBucketSystem(MinDistanceForNewFocusPoint, MaxFocusPoints);
//			cameraController.playerTransform = () => eggbotController.transform;

//			cameraController.focusPoints = () =>
//			{
//				spatialSystem.SetPoints(mapManager.Waypoints.Select(w => mapManager.TileWorldPosition(w.nTile)).ToList());
//				cameraController.focusPoints = () =>
//				{
//					if (null != eggbotController.transform) spatialSystem.TryAddPoint(eggbotController.transform.position);
//					return spatialSystem.Points;
//				};
//				return spatialSystem.Points;
//			};

//			cameraController.SetMode(CameraState.Follow);

//			var srcPos = new Vector3(0f, 14f, -14f);// Classic TS default
//			var dstPos = Vector3.zero;

//			if (null != mapManager.Waypoints && 0 != mapManager.Waypoints.Length)
//			{
//				dstPos = new Vector3(mapManager.Width * 0.5f, 0f, mapManager.Height * 0.5f);
//				srcPos += dstPos;

//				var firstWaypoint = mapManager.Waypoints[0];
//				if (firstWaypoint.bCamera)
//				{
//					if (firstWaypoint.vSrc != null) srcPos = firstWaypoint.vSrc.ToVector3();
//					if (firstWaypoint.vDst != null) dstPos = firstWaypoint.vDst.ToVector3();
//				}
//			}

//			cameraController.SetOrigin(srcPos, true);
//			cameraController.SetTarget(dstPos, true);

//			if (null != eggbotController)
//			{
//				var postProcessingCameraController = cameraController.GetComponentInChildren<PostProcessingCameraController>(true);
//				if (postProcessingCameraController != null) postProcessingCameraController.dofTarget = eggbotController.transform;
//			}

//			gestureController.Initialise(mapManager);
//			if (PreviewSettings.EditorMode) cameraController.SetMode(CameraState.Editor);
//			timeStart = Time.time;
//		}

//		private void OnWaypointReached(int waypointIndex)
//		{
//			if (CameraState.Cinema == cameraController.CurrentState || CameraState.Editor == cameraController.CurrentState) return;
//			if (null == eggbotController) return;
//			if (null == mapManager || waypointIndex < 0 || waypointIndex >= mapManager.Waypoints.Length) return;
//			if (mapManager.Waypoints.Length - 1 == waypointIndex || waypointIndex == 0) return;

//			var waypoint = mapManager.Waypoints[waypointIndex];
//			if (waypoint.vSrc == null || !waypoint.vSrc.IsValidVector())
//			{
//				cameraController.SetMode(CameraState.Follow);
//				return;
//			}

//			var position = waypoint.vSrc.IsValidVector() ? waypoint.vSrc.ToVector3() : new Vector3(0f, 14f, -14f);
//			cameraController.SetOrigin(position);
//			var target = null != waypoint.vDst && waypoint.vDst.IsValidVector() ? waypoint.vDst.ToVector3() : mapManager.TileWorldPosition(waypoint.nTile);
//			cameraController.SetTarget(target);
//			cameraController.SetMode(CameraState.Preset);
//			gestureController.enabled = true;
//		}

//		private void OnPuzzleSolved(int waypointIndex)
//		{
//			if (CameraState.Cinema == cameraController.CurrentState || CameraState.Editor == cameraController.CurrentState) return;
//			if (null == eggbotController) return;
//			cameraController.SetMode(CameraState.Follow);
//		}

//		private void OnLevelCompleted() { }

//		private void CheckDisableDrag(IMapManager imap)
//		{
//			if (null != eggbotController && 0 != eggbotController.NavDirection(imap))
//				gestureController.enabled = false;
//		}

//		private void OnDestroy()
//		{
//			gestureController.OnMapUpdated -= CheckDisableDrag;
//			if (null == eggbotController) return;
//			eggbotController.OnWaypointReached -= OnWaypointReached;
//			eggbotController.OnPuzzleSolved -= OnPuzzleSolved;
//			eggbotController.OnLevelCompleted -= OnLevelCompleted;
//		}
//	}
//}