using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using UnityEngine.EventSystems;
using UnityEditor;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public class MainController : MonoBehaviour
	{
		private GameController gameController;
		private EditorController editorController;
		private Transform mapRoot;
		public static Map CurrentMap { get; set; }
		private EggbotController eggbotController;
		private MainCameraController cameraController;

		public static Transform MapRoot;

		public event System.Action<int> OnChangeMapRequested; // delta or 0 for reload

		private void Awake()
		{
			// === ADD AUDIO MANAGER AUTOMATICALLY ===
			gameObject.AddComponent<AudioManager>(); //audioManager = gameObject.AddComponent<AudioManager>();
			AssetConfiguration.Initialize(); // Sets initial remapper + roots
			AssetRegistry<GameObject>.NameRemapper = ApplicationSettings.RemapGeometry ? ClassicTileStormAssetRemapHelper.RemapName : null;
			ApplicationSettings.OnRemapGeometryChanged += (value) =>
			{
				AssetRegistry<GameObject>.NameRemapper = value ? ClassicTileStormAssetRemapHelper.RemapName : null;
				ModelAssets.ClearCache();
				CurrentMap?.RefreshGeometry();
			};

			if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
			ResourceSerializer.Initialise(ApplicationSettings.DatabaseJsonFile);
			gameController = gameObject.AddComponent<GameController>();
			editorController = gameObject.AddComponent<EditorController>();
			cameraController = gameObject.AddComponent<MainCameraController>();
			OnChangeMapRequested += HandleChangeMap;
			LoadMap(ApplicationSettings.LoadMapName);
			SetPreviewMode(ApplicationSettings.CurrentMode);//invoke to enable and disable game and editor controllers - ToDo improve this
		}

		private int guard = 0;//temporary workaround for double events from ongui (due to camera stack) - hopefully this will go away when full ui is implemented
		private void Update() { guard = 0; if (null != eggbotController) eggbotController.UpdateEggbot(CurrentMap); }

		//public void SetGeometryMode(bool value)
		//{
		//	AssetRegistry<GameObject>.NameRemapper = value ? ClassicTileStormAssetRemapHelper.RemapName : null;
		//	ModelAssets.ClearCache();
		//	mapManager?.RefreshGeometry();
		//}

		public void SetPreviewMode(ApplicationMode mode)
		{
			if (null == cameraController) return;

			cameraController.SetCameraMode(GameModes.GetModeString(mode));
			editorController.enabled = mode == ApplicationMode.Editor;
			gameController.enabled = mode != ApplicationMode.Editor;
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName ??= ApplicationSettings.LoadMapName))
				return;

			var currentMap = ResourceManager.Maps.FirstOrDefault(m => m.name == mapName)
						  ?? ResourceManager.Maps.FirstOrDefault();

			if (currentMap == null)
			{
				Debug.LogError($"No map found for '{mapName}'! Available: {string.Join(", ", ResourceManager.Maps.Select(m => m.name))}");
				return;
			}

			// ─── Cleanup previous map ─────────────────────────────────────
			if (CurrentMap != null)
				CurrentMap.Destroy();

			if (mapRoot != null)
				DestroyImmediate(mapRoot.gameObject);

			// ─── Create new container GameObject ──────────────────────────
			var container = new GameObject($"Map: {currentMap.name}");
			container.transform.SetParent(transform, false);
			MapRoot = mapRoot = container.transform;

			// ─── Load & initialise ────────────────────────────────────────

			//// Sound effect
			//AudioManager.PlaySound(SoundAssets.Find("jump"));

			// Music
			//AudioManager.PlayMusic(currentMap.music, loop: true);
			//AudioManager.PlayMusic(MusicAssets.Find(currentMap.music));

			//SkyboxUtility.SetSkybox(AssetPath.SkycubesPath, $"{(string.IsNullOrEmpty(currentMap.skybox) ? currentMap.music : currentMap.skybox)}Skybox");//fall back to music for now, but will be 'DefaultSkybox'

			CurrentMap = currentMap;
			currentMap.Initialise(mapRoot);

			// Eggbot
			if (eggbotController != null)
				DestroyImmediate(eggbotController.gameObject);

			eggbotController = EggbotController.Instantiate(currentMap.character, transform);
			eggbotController?.Initialise(CurrentMap);

			// Controllers
			cameraController?.Initialise(CurrentMap, eggbotController);
			gameController?.Initialise(CurrentMap);
			editorController?.Initialise(CurrentMap);

			// Skybox
			var skyName = string.IsNullOrEmpty(currentMap.skybox)
				? $"{currentMap.music}Skybox"
				: currentMap.skybox;
			SkyboxUtility.SetSkybox(skyName);
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
				if (maps[i]?.name == ApplicationSettings.LoadMapName)
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
				ApplicationSettings.LoadMapName = maps[currentIndex].name;
			}

			LoadMap();
		}

		public void Preset() => CurrentMap?.Preset();

		public void Scramble() => CurrentMap?.Scramble();

		public void Solve()
		{
			CurrentMap?.Solve();
			if (null != cameraController) cameraController.OnMapSolved();
		}

		public void LoadDatabase()
		{
			var dbAsset = ApplicationSettings.DatabaseJsonFile;
			if (dbAsset == null)
			{
				Debug.LogError("PreviewSettings.DatabaseJsonFile is not assigned in PreviewSettings!");
				return;
			}

			ResourceSerializer.Initialise(dbAsset);

			if (ResourceManager.database == null)
			{
				Debug.LogError("Failed to load database from DatabaseJsonFile!");
				return;
			}

			OnChangeMapRequested?.Invoke(0);
		}

		public void SaveDatabase()
		{
#if UNITY_EDITOR
			if (ResourceManager.database == null)
			{
				Debug.LogError("Cannot save: database not loaded");
				return;
			}

			var database = ApplicationSettings.DatabaseJsonFile;
			if (database == null)
			{
				Debug.LogError("PreviewSettings.DatabaseJsonFile is not assigned!");
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(database);
			if (string.IsNullOrEmpty(assetPath) || assetPath.Contains("Resources/unity_builtin_extra"))
			{
				Debug.LogError("Cannot save to project: not a real project asset.");
				return;
			}

			string fullPath = System.IO.Path.GetFullPath(assetPath);
			ResourceSerializer.SaveDatabase(ResourceManager.database, fullPath, verbose:true);
#else
			Debug.Log("Save Database only works in Editor");
#endif
		}

		public void ImportMapAsAtomic()
		{
#if UNITY_EDITOR
			string path = EditorUtility.OpenFilePanel("Import Atomic Map", ApplicationSettings.ExportFolder, "json");
			if (!string.IsNullOrEmpty(path))
			{
				ResourceSerializer.ImportAtomicMap(path);
				string importedName = System.IO.Path.GetFileNameWithoutExtension(path);

				if (CurrentMap != null && CurrentMap.name == importedName)
					OnChangeMapRequested?.Invoke(0);
			}
#else
			Debug.Log("Import currently only available in Unity Editor");
#endif
		}

		public void ExportMapAsAtomic()
		{
#if UNITY_EDITOR
			if (CurrentMap == null)
			{
				EditorUtility.DisplayDialog("Export Error", "No map is currently loaded.", "OK");
				return;
			}

			var map = CurrentMap;
			string originalName = map.name;
			string lastFolder = PlayerPrefs.GetString("ClassicTilestorm_LastExportFolder", PreviewSettingsStatic.ExportFolder);
			System.IO.Directory.CreateDirectory(lastFolder);

			string initialPath = System.IO.Path.Combine(lastFolder, originalName + ".json");
			string path = EditorUtility.SaveFilePanel("Export Map As Atomic JSON", lastFolder, originalName + ".json", "json");

			if (string.IsNullOrEmpty(path))
			{
				Debug.Log("Export cancelled by user.");
				return;
			}

			string chosenFolder = System.IO.Path.GetDirectoryName(path);
			string chosenName = System.IO.Path.GetFileNameWithoutExtension(path);
			PlayerPrefs.SetString("ClassicTilestorm_LastExportFolder", chosenFolder);
			PlayerPrefs.Save();

			bool nameChanged = !string.Equals(originalName, chosenName, System.StringComparison.Ordinal);

			try
			{
				if (nameChanged) map.name = chosenName;
				ResourceSerializer.ExportAtomicMap(map, chosenFolder, true);
				EditorUtility.DisplayDialog("Export Successful", $"Map exported successfully!\n\nPath: {path}", "OK");
				Debug.Log($"Map exported: {path}");
			}
			catch (System.Exception ex)
			{
				EditorUtility.DisplayDialog("Export Failed", $"Error during export:\n{ex.Message}", "OK");
				Debug.LogError($"Export failed: {ex}");
			}
			finally
			{
				if (nameChanged) map.name = originalName;
			}
#else
			Debug.Log("Export currently only available in Unity Editor");
#endif
		}
	}
}