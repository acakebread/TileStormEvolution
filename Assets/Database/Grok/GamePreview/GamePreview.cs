using UnityEngine;
using GameDatabase;
using System.Linq;

namespace GamePreviewNamespace
{
	public class GamePreview : MonoBehaviour
	{
		[Header("Workaround for inverted .obj meshes")]
		public bool flip = true;
		[Header("load map scrambled or solved")]
		public bool scramble = true;

		[SerializeField] private DatabaseLoader databaseLoader;
		[SerializeField] private GestureSystem gestureSystem;
		[SerializeField] private string mapName = "Industrial 01";
		[SerializeField] private string geometryPath = "Geometry/fbx/";
		[SerializeField] private string texturePath = "Textures/";
		[SerializeField] private float tileMoveSpeed = 2f;
		[SerializeField] private float pauseDuration = 1f;
		[SerializeField] private float dragThreshold = 0.5f;

		private MapManager mapManager;
		private EggbotController eggbotController;
		private TileInteractionController tileInteractionController;
		private bool isInitialized;

		void Awake()
		{
			mapManager = GetComponent<MapManager>();
			eggbotController = GetComponent<EggbotController>();
			tileInteractionController = GetComponent<TileInteractionController>();

			if (mapManager == null) mapManager = gameObject.AddComponent<MapManager>();
			if (eggbotController == null) eggbotController = gameObject.AddComponent<EggbotController>();
			if (tileInteractionController == null) tileInteractionController = gameObject.AddComponent<TileInteractionController>();
		}

		void Start()
		{
			if (databaseLoader == null)
			{
				Debug.LogError("GamePreview requires a DatabaseLoader!");
				return;
			}

			Debug.Log($"GamePreview Start: databaseLoader found, Maps.Count={databaseLoader.Maps.Count}");
			databaseLoader.OnDatabaseLoaded += Initialize;

			if (databaseLoader.Maps.Count > 0)
			{
				Initialize();
			}
		}

		void OnDestroy()
		{
			if (databaseLoader != null)
			{
				databaseLoader.OnDatabaseLoaded -= Initialize;
			}
		}

		void Initialize()
		{
			if (isInitialized)
				return;

			isInitialized = true;
			Debug.Log($"GamePreview Initialize: Maps.Count={databaseLoader.Maps.Count}, mapName={mapName}");

			// Reset all components
			mapManager.Reset();
			eggbotController.Reset();
			tileInteractionController.Initialize(mapManager, gestureSystem);//);//, gestureSystem);//, tileMoveSpeed, dragThreshold

			// Initialize in order
			mapManager.Initialize(databaseLoader, mapName, geometryPath, texturePath, flip, scramble);
			eggbotController.Initialize(mapManager, pauseDuration);
		}

		void Update()
		{
			eggbotController.UpdateEggbot();
			//tileInteractionController.UpdateInteractions();
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
				int currentIndex = databaseLoader.Maps.ToList().FindIndex(m => m.name == mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % databaseLoader.Maps.Count;
				mapName = databaseLoader.Maps[currentIndex].name;
				isInitialized = false;
				Initialize();
			}
		}
	}
}

