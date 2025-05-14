using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class GamePreview : MonoBehaviour
	{
		private static GamePreview instance;

		public static MapManager mapManager => null == instance._mapManager ? instance._mapManager = instance.gameObject.AddComponent<MapManager>() : instance._mapManager;
		public static EggbotController eggbotController => null == instance._eggbotController ? instance._eggbotController = instance.gameObject.AddComponent<EggbotController>() : instance._eggbotController;
		public static GestureController gestureController => null == instance._gestureController ? instance._gestureController = instance.gameObject.AddComponent<GestureController>() : instance._gestureController;
		public static LegacyController cameraController => null == instance._cameraController ? instance._cameraController = instance.gameObject.AddComponent<LegacyController>() : instance._cameraController;

		private MapManager _mapManager;
		private EggbotController _eggbotController;
		private GestureController _gestureController;
		private LegacyController _cameraController;

		void Awake()
		{
			instance = this;
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			Initialize();
		}

		private void Initialize()
		{
			Debug.Log($"GamePreview Initialize: mapName={PreviewSettings.LoadMapName}");

			// Initialize in order
			mapManager.Initialize();
			eggbotController.Initialize();
			cameraController.Initialize();
			gestureController.Initialize();
		}

		void Update()
		{
			eggbotController?.UpdateEggbot();
			cameraController?.UpdateCamera();
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload")) { Initialize(); }

			if (GUI.Button(new Rect(120, 10, 100, 30), "Solve")) { _mapManager.Solve(); }

			if (GUI.Button(new Rect(230, 10, 150, 30), "Previous Level"))//eggbotController.IsLevelComplete && 
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (DatabaseLoader.Maps.Count + currentIndex - 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				Initialize();
			}

			if (GUI.Button(new Rect(390, 10, 150, 30), "Next Level"))//eggbotController.IsLevelComplete && 
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				Initialize();
			}

			if (GUI.Button(new Rect(550, 10, 150, 30), CameraController.CinemaEnabled ? "Disable Cinematic" : "Enable Cinematic")) { CameraController.SetAutoCinema(!CameraController.CinemaEnabled); CameraController.Refresh(Time.time - (CameraController.CinemaEnabled ? 999 : 0)); }
		}
	}
}
