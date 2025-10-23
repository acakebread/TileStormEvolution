using UnityEngine;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
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

		private void Update()
		{
			if (eggbotController != null) eggbotController.UpdateEggbot(mapManager);
		}

		private string GameModeToCameraMode(PreviewMode mode)
		{
			return mode switch
			{
				PreviewMode.Direct => CameraModeRegistry.Direct,
				PreviewMode.Editor => CameraModeRegistry.Editor,
				PreviewMode.Player => CameraModeRegistry.Follow,
				PreviewMode.Cinema => Random.Range(0, 7) switch { 0 or 1 or 2 => CameraModeRegistry.Orbit, _ => CameraModeRegistry.Path },
				_ => CameraModeRegistry.Absent
			};
		}

		public void SetPreviewMode(PreviewMode mode, bool forceCinema = false)
		{
			if (null == cameraController) return;

			cameraController.SetCameraMode(cameraController.GetCurrentGroupMode(GameModeToCameraMode(mode)));
			if (mode == PreviewMode.Cinema)
				cameraController.ResetCinemaTimer(forceCinema);
		}

		public void LoadMap(string mapName = null)
		{
			mapName = mapName ?? PreviewSettings.LoadMapName;
			if (mapName == null) return;

			var currentMap = DatabaseSerializer.Maps.FirstOrDefault(m => m.name == mapName) ?? DatabaseSerializer.Maps.FirstOrDefault();
			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseSerializer.Maps.Select(m => m.name))}");
				return;
			}

			SkyboxUtility.SetSkybox(PreviewSettings.SkycubesPath, currentMap.szMusic);

			if (mapManager != null) Destroy(mapManager.gameObject);
			mapManager = MapManager.Instantiate(currentMap, transform);

			if (eggbotController != null) Destroy(eggbotController.gameObject);
			eggbotController = EggbotController.Instantiate(currentMap.szEggbotCostume, transform);
			if (eggbotController != null)
				eggbotController.Initialise(mapManager);

			if (cameraController != null)
				cameraController.Initialise(mapManager, eggbotController, GameModeToCameraMode(PreviewSettings.CurrentMode));
		}

		public void Scramble() => mapManager?.Scramble();

		public void Solve()
		{
			if (mapManager != null) mapManager.Solve();
			if (cameraController != null) cameraController.OnMapSolved();
		}

		private void OnDestroy() { }
	}
}