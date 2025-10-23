using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class MainController : MonoBehaviour
	{
		private MapManager mapManager;
		private EggbotController eggbotController;
		private GameCameraController cameraController;

		private void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
			cameraController = gameObject.AddComponent<GameCameraController>();
		}

		private void Start()
		{
			DatabaseSerializer.Init(PreviewSettings.DatabaseJsonFile, (asset) => { PreviewSettings.DatabaseJsonFile = asset; });
			LoadMap(PreviewSettings.LoadMapName);
		}

		private void Update() { if (eggbotController != null) eggbotController.UpdateEggbot(mapManager); }

		private string PreviewModeToCameraMode(PreviewMode mode) => mode switch
		{
			PreviewMode.Direct => CameraModeRegistry.Direct,
			PreviewMode.Editor => CameraModeRegistry.Editor,
			PreviewMode.Player => CameraModeRegistry.Follow,
			PreviewMode.Cinema => Random.Range(0, 7) switch { 0 or 1 or 2 => CameraModeRegistry.Orbit, _ => CameraModeRegistry.Path },
			_ => CameraModeRegistry.Absent
		};

		public void SetPreviewMode(PreviewMode mode, bool forceCinema = false)
		{
			if (null == cameraController) return;
			cameraController.SetCameraMode(cameraController.GetCurrentGroupMode(PreviewModeToCameraMode(mode)));
			if (mode == PreviewMode.Cinema) cameraController.ResetCinemaTimer(forceCinema);
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
			if (null != cameraController) cameraController.Initialise(mapManager, eggbotController, PreviewModeToCameraMode(PreviewSettings.CurrentMode));
		}

		public void Scramble() { if (null != mapManager) mapManager.Scramble(); }

		public void Solve()
		{
			if (null != mapManager) mapManager.Solve();
			if (null != cameraController) cameraController.OnMapSolved();
		}
	}
}