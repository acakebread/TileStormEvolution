using UnityEngine;
using GameDatabase;
using System.Linq;

namespace GamePreviewNamespace
{
	public class GamePreview : MonoBehaviour
	{
		[SerializeField] private string mapName = "Industrial 01";

		private MapManager mapManager;
		private EggbotController eggbotController;
		private TileInteractionController tileInteractionController;
		private CameraController cameraController; // New
		private bool isInitialized;

		void Awake()
		{
			mapManager = GetComponent<MapManager>();
			eggbotController = GetComponent<EggbotController>();
			tileInteractionController = GetComponent<TileInteractionController>();
			cameraController = GetComponent<CameraController>(); // New

			if (mapManager == null) mapManager = gameObject.AddComponent<MapManager>();
			if (eggbotController == null) eggbotController = gameObject.AddComponent<EggbotController>();
			if (tileInteractionController == null) tileInteractionController = gameObject.AddComponent<TileInteractionController>();
			if (cameraController == null) cameraController = gameObject.AddComponent<CameraController>(); // New
		}

		void Start()
		{
			if (DatabaseLoader.instance == null)
			{
				Debug.LogError("GamePreview requires a DatabaseLoader!");
				return;
			}

			Debug.Log($"GamePreview Start: databaseLoader found, Maps.Count={DatabaseLoader.instance.Maps.Count}");
			DatabaseLoader.instance.OnDatabaseLoaded += Initialize;

			if (DatabaseLoader.instance.Maps.Count > 0)
			{
				Initialize();
			}
		}

		void OnDestroy()
		{
			if (DatabaseLoader.instance != null)
			{
				DatabaseLoader.instance.OnDatabaseLoaded -= Initialize;
			}
			if (cameraController == null) cameraController = gameObject.AddComponent<CameraController>(); // New
		}

		void Initialize()
		{
			if (isInitialized)
				return;

			isInitialized = true;
			Debug.Log($"GamePreview Initialize: Maps.Count={DatabaseLoader.instance.Maps.Count}, mapName={mapName}");

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
				isInitialized = false;
				Initialize();
			}

			if (eggbotController.IsLevelComplete && GUI.Button(new Rect(120, 10, 150, 30), "Next Level"))
			{
				int currentIndex = DatabaseLoader.instance.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % DatabaseLoader.instance.Maps.Count;
				mapName = DatabaseLoader.instance.Maps[currentIndex].name;
				isInitialized = false;
				Initialize();
			}
		}
	}
}
