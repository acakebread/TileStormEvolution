using UnityEngine;
using GameDatabase;
using System.Linq;

namespace GamePreviewNamespace
{
	public class GamePreview : MonoBehaviour
	{
		private string mapName = "Industrial 01";

		private MapManager mapManager;
		private EggbotController eggbotController;
		private TileInteractionController tileInteractionController;
		private CameraController cameraController; // New

		void Awake()
		{
			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			mapName = PreviewSettings.LoadMapName;
			mapManager = GetComponent<MapManager>();
			eggbotController = GetComponent<EggbotController>();
			tileInteractionController = GetComponent<TileInteractionController>();
			cameraController = GetComponent<CameraController>(); // New

			if (mapManager == null) mapManager = gameObject.AddComponent<MapManager>();
			if (eggbotController == null) eggbotController = gameObject.AddComponent<EggbotController>();
			if (tileInteractionController == null) tileInteractionController = gameObject.AddComponent<TileInteractionController>();
			if (cameraController == null) cameraController = gameObject.AddComponent<CameraController>(); // New

			Initialize();
		}

		void OnDestroy()
		{
			if (cameraController == null) cameraController = gameObject.AddComponent<CameraController>(); // New
		}

		void Initialize()
		{
			Debug.Log($"GamePreview Initialize: Maps.Count={DatabaseLoader.Maps.Count}, mapName={mapName}");

			// Reset all components
			mapManager.Reset();
			eggbotController.Reset();

			// Initialize in order
			tileInteractionController.Initialize(mapManager);
			mapManager.Initialize(mapName);
			eggbotController.Initialize(mapManager);
			cameraController.Initialize(mapManager, eggbotController); // Initialize and reset together
			cameraController.ResetCamera(); // Moved after Initialize
		}

		void Update()
		{
			//mapManager?.UpdateMap();
			eggbotController?.UpdateEggbot();
			cameraController?.UpdateCamera();
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload"))
			{
				Initialize();
			}

			if (GUI.Button(new Rect(120, 10, 100, 30), "Solve"))
			{
				mapManager.Solve();
			}

			if (GUI.Button(new Rect(230, 10, 150, 30), "Previous Level"))//eggbotController.IsLevelComplete && 
			{
				int currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (DatabaseLoader.Maps.Count + currentIndex - 1) % DatabaseLoader.Maps.Count;
				mapName = DatabaseLoader.Maps[currentIndex].name;
				Initialize();
			}

			if (GUI.Button(new Rect(390, 10, 150, 30), "Next Level"))//eggbotController.IsLevelComplete && 
			{
				int currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % DatabaseLoader.Maps.Count;
				mapName = DatabaseLoader.Maps[currentIndex].name;
				Initialize();
			}
		}
	}
}
