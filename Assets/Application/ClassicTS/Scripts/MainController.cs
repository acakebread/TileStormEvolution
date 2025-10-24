using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

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
			gameObject.AddComponent<PlaceholderUI>();
			gameController = gameObject.AddComponent<GameController>();
			editorController = gameObject.AddComponent<EditorController>();
			cameraController = gameObject.AddComponent<MainCameraController>();
			DatabaseSerializer.Init(PreviewSettings.DatabaseJsonFile, (asset) => { PreviewSettings.DatabaseJsonFile = asset; });
			LoadMap(PreviewSettings.LoadMapName);
			SetPreviewMode(PreviewSettings.CurrentMode);//invoke to enable and disable game and editor controllers - ToDo improve this
		}

		private void Update() { if (eggbotController != null) eggbotController.UpdateEggbot(mapManager); }

		public void SetPreviewMode(PreviewMode mode)
		{
			if (cameraController == null) return;

			cameraController.SetCameraMode(GameModes.GetModeString(mode));
			editorController.enabled = mode == PreviewMode.Editor;
			gameController.enabled = mode != PreviewMode.Editor;
		}

		public void LoadMap(string mapName = null)
		{
			if ((mapName = mapName ?? PreviewSettings.LoadMapName) == null) return;

			var currentMap = DatabaseSerializer.Maps.FirstOrDefault(m => m.name == mapName) ?? DatabaseSerializer.Maps.FirstOrDefault();
			if (null == currentMap)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseSerializer.Maps.Select(m => m.name))}");
				return;
			}

			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.szMusic);

			if (null != mapManager) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);

			if (null != eggbotController) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (null != eggbotController) eggbotController.Initialise(mapManager);
			if (null != cameraController) cameraController.Initialise(mapManager, eggbotController);

			if (null != gameController) gameController.Initialise();
			if (null != editorController) editorController.Initialise(mapManager, eggbotController);
		}

		public void Scramble() { if (null != mapManager) mapManager.Scramble(); }

		public void Solve()
		{
			if (null != mapManager) mapManager.Solve();
			if (null != cameraController) cameraController.OnMapSolved();
		}
	}
}