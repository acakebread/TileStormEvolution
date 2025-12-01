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
			if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			ResourceSerializer.Initialise(PreviewSettings.DatabaseJsonFile);
			cameraController = gameObject.AddComponent<MainCameraController>();
			gameController = gameObject.AddComponent<GameController>();
			editorController = gameObject.AddComponent<EditorController>();
			LoadMap(PreviewSettings.LoadMapName);
			SetPreviewMode(PreviewSettings.CurrentMode);//invoke to enable and disable game and editor controllers - ToDo improve this
		}

		private void Update() { if (null != eggbotController) eggbotController.UpdateEggbot(mapManager); }

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

			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.music);

			if (null != mapManager) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);

			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.character, transform);
			if (null != eggbotController) eggbotController.Initialise(mapManager);

			if (null != cameraController) cameraController.Initialise(mapManager, eggbotController);
			if (null != gameController) gameController.Initialise();
			if (null != editorController) editorController.Initialise(mapManager);
		}

		public void ReloadCurrentMap()
		{
			if (mapManager?.CurrentMap != null)
				LoadMap(mapManager.CurrentMap.name);
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