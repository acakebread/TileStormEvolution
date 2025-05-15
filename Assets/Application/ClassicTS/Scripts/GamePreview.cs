using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class GamePreview : MonoBehaviour
	{
		private static GamePreview instance;

		public static MapManager mapManager => MapManager.instance ?? instance.gameObject.AddComponent<MapManager>();
		public static EggbotController eggbotController => EggbotController.instance ?? instance.gameObject.AddComponent<EggbotController>();
		public static GestureController gestureController => GestureController.instance ?? instance.gameObject.AddComponent<GestureController>();
		public static LegacyController cameraController => LegacyController.instance ?? instance.gameObject.AddComponent<LegacyController>();

		void Awake()
		{
			instance = this;
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			LoadMap();
		}

		private void Reset()
		{
			mapManager.Reset();
			mapManager.Load(PreviewSettings.LoadMapName);
			eggbotController.Reset();
			cameraController.Reset();
			gestureController.Reset();
		}

		private void LoadMap(string map = null) { Reset(); }//{ Reset(); mapManager.Load(map ?? PreviewSettings.LoadMapName); }

		void Update()
		{
			eggbotController.UpdateEggbot();
			cameraController.UpdateCamera();
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload")) { LoadMap(); }

			if (GUI.Button(new Rect(120, 10, 100, 30), "Solve")) { mapManager.Solve(); }

			if (GUI.Button(new Rect(230, 10, 150, 30), "Previous Level"))//eggbotController.IsLevelComplete && 
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (DatabaseLoader.Maps.Count + currentIndex - 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				LoadMap();
			}

			if (GUI.Button(new Rect(390, 10, 150, 30), "Next Level"))//eggbotController.IsLevelComplete && 
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				LoadMap();
			}

			if (GUI.Button(new Rect(550, 10, 150, 30), CameraController.CinemaEnabled ? "Disable Cinematic" : "Enable Cinematic")) { CameraController.SetAutoCinema(!CameraController.CinemaEnabled); CameraController.Refresh(Time.time - (CameraController.CinemaEnabled ? 999 : 0)); }
		}
	}
}
