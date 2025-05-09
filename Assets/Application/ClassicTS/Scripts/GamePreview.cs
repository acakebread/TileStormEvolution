using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public class GamePreview : MonoBehaviour
	{
		private static GamePreview instance;

		public static MapManager mapManager => instance?._mapManager;
		public static EggbotController eggbotController => instance?._eggbotController;
		public static TileInteractionController tileInteractionController => instance?._tileInteractionController;
		public static CameraController cameraController => instance?._cameraController;

		private MapManager _mapManager;
		private EggbotController _eggbotController;
		private TileInteractionController _tileInteractionController;
		private CameraController _cameraController;

		void Awake()
		{
			instance = this;

			DatabaseLoader.Init(PreviewSettings.DatabaseJsonFile);
			_mapManager = GetComponent<MapManager>();
			_eggbotController = GetComponent<EggbotController>();
			_cameraController = GetComponent<CameraController>();
			_tileInteractionController = GetComponent<TileInteractionController>();

			if (null == _mapManager) _mapManager = gameObject.AddComponent<MapManager>();
			if (null == _eggbotController) _eggbotController = gameObject.AddComponent<EggbotController>();
			if (null == _cameraController) _cameraController = gameObject.AddComponent<CameraController>();
			if (null == _tileInteractionController) _tileInteractionController = gameObject.AddComponent<TileInteractionController>();

			Initialize();
		}

		private void Initialize()
		{
			Debug.Log($"GamePreview Initialize: mapName={PreviewSettings.LoadMapName}");

			// Reset all components
			_mapManager.Reset();
			_eggbotController.Reset();

			// Initialize in order
			_mapManager.Initialize(PreviewSettings.LoadMapName);
			_eggbotController.Initialize();
			_cameraController.Initialize();
			_tileInteractionController.Initialize();
		}

		void Update()
		{
			_eggbotController?.UpdateEggbot();
			_cameraController?.UpdateCamera();
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			if (GUI.Button(new Rect(10, 10, 100, 30), "Reload")) { Initialize(); }

			if (GUI.Button(new Rect(120, 10, 100, 30), "Solve")) { _mapManager.Solve(); }

			if (GUI.Button(new Rect(230, 10, 150, 30), "Previous Level"))//eggbotController.IsLevelComplete && 
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == _mapManager.CurrentMapName);
				currentIndex = (DatabaseLoader.Maps.Count + currentIndex - 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				Initialize();
			}

			if (GUI.Button(new Rect(390, 10, 150, 30), "Next Level"))//eggbotController.IsLevelComplete && 
			{
				var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == _mapManager.CurrentMapName);
				currentIndex = (currentIndex + 1) % DatabaseLoader.Maps.Count;
				PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
				Initialize();
			}
		}
	}
}
