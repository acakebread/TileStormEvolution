using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using UnityEngine.EventSystems;

namespace ClassicTilestorm
{
	public class MainController : MonoBehaviour
	{
		private GameController gameController;
		private EditorController editorController;
		private MapManager mapManager;
		private EggbotController eggbotController;
		private MainCameraController cameraController;

		private void Awake()
		{
			GeometrySearchProvider.Register();//important for all resource loading
			if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			ResourceSerializer.Initialise(PreviewSettings.DatabaseJsonFile);
			cameraController = gameObject.AddComponent<MainCameraController>();
			gameController = gameObject.AddComponent<GameController>();
			editorController = gameObject.AddComponent<EditorController>();
			LoadMap(PreviewSettings.LoadMapName);
			SetPreviewMode(PreviewSettings.CurrentMode);//invoke to enable and disable game and editor controllers - ToDo improve this
		}

		private int guard = 0;//temporary workaround for double events from ongui (due to camera stack) - hopefully this will go away when full ui is implemented
		private void Update() { guard = 0; if (null != eggbotController) eggbotController.UpdateEggbot(mapManager); }

		public void SetPreviewMode(PreviewMode mode)
		{
			if (null == cameraController) return;

			cameraController.SetCameraMode(GameModes.GetModeString(mode));
			editorController.enabled = mode == PreviewMode.Editor;
			gameController.enabled = mode != PreviewMode.Editor;
		}

		public void LoadMap(string mapName = null)
		{
			if (null == (mapName = mapName ?? PreviewSettings.LoadMapName)) return;

			var currentMap = ResourceManager.Maps.FirstOrDefault(m => m.name == mapName) ?? ResourceManager.Maps.FirstOrDefault();
			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", ResourceManager.Maps.Select(m => m.name))}");
				return;
			}

			SkyboxUtility.SetSkybox(AssetPath.SkycubesPath, $"{(string.IsNullOrEmpty(currentMap.skybox) ? currentMap.music : currentMap.skybox)}Skybox");//fall back to music for now, but will be 'DefaultSkybox'

			if (null != mapManager) DestroyImmediate(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);

			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.character, transform);
			if (null != eggbotController) eggbotController.Initialise(mapManager);

			if (null != cameraController) cameraController.Initialise(mapManager, eggbotController);
			if (null != gameController) gameController.Initialise();
			if (null != editorController) editorController.Initialise(mapManager);
			if (null != editorController && null != gameController) editorController.OnChangeMapRequested += HandleChangeMap;

			//static string SkycubesPath(string id) => string.IsNullOrEmpty(id) ? null : $"{AssetPath.SkycubesPath}{id}";
		}

		//public void ReloadCurrentMap() { if (null != mapManager && null != mapManager.CurrentMap) LoadMap(mapManager.CurrentMap.name); }
		public void HandleChangeMap(int delta)
		{
			if (++guard > 1) return;
			var maps = ResourceManager.Maps;
			if (maps == null || maps.Count == 0) return;

			// Find current index the old-school way — works on ANY IList<T>
			int currentIndex = -1;
			for (int i = 0; i < maps.Count; i++)
			{
				if (maps[i]?.name == PreviewSettings.LoadMapName)
				{
					currentIndex = i;
					break;
				}
			}

			// If not found, default to 0
			if (currentIndex == -1) currentIndex = 0;

			if (delta != 0)
			{
				currentIndex = (currentIndex + delta + maps.Count) % maps.Count;
				PreviewSettings.LoadMapName = maps[currentIndex].name;
			}

			LoadMap();
		}

		public void Preset() { if (null != mapManager) mapManager.Preset(); }

		public void Scramble() { if (null != mapManager) mapManager.Scramble(); }

		public void Solve()
		{
			if (null != mapManager) mapManager.Solve();
			if (null != cameraController) cameraController.OnMapSolved();
		}
	}
}