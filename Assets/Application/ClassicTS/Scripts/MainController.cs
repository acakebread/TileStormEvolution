using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using MassiveHadronLtd.FileBrowserUtil;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public class MainController : MonoBehaviour
	{
		private GameController gameController;
		private EditorController editorController;
		private EggbotController eggbotController;
		private MainCameraController cameraController;

		public static Map CurrentMap { get; set; }
		public static Transform MapRoot { get; set; }

		public event System.Action<int> OnChangeMapRequested; // delta or 0 for reload

		private void Awake()
		{
			// === ADD AUDIO MANAGER AUTOMATICALLY ===
			gameObject.AddComponent<AudioManager>();
			AssetConfiguration.Initialize();
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

		private void Update() => eggbotController?.UpdateEggbot(CurrentMap);

		public void OnApplicationFocus(bool hasFocus) => EditorCameraMovement.OnApplicationFocus(hasFocus);

		public void SetPreviewMode(ApplicationMode mode)
		{
			if (null == cameraController) return;

			cameraController.SetCameraMode(GameModes.GetModeString(mode));
			editorController.enabled = mode == ApplicationMode.Editor;
			gameController.enabled = mode != ApplicationMode.Editor;
			eggbotController.gameObject.SetActive(mode != ApplicationMode.Editor);
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName ??= ApplicationSettings.LoadMapName))
				return;

			var newMap = ResourceManager.Maps.FirstOrDefault(m => m.name == mapName)
						  ?? ResourceManager.Maps.FirstOrDefault();

			if (newMap == null)
			{
				Debug.LogError($"No map found for '{mapName}'! Available: {string.Join(", ", ResourceManager.Maps.Select(m => m.name))}");
				return;
			}

			var mainReflection = Camera.main?.GetComponent<ReflectionEffectCamera>();

			Resources.UnloadUnusedAssets();

			//GeometryFactory.ResetCounters();
			//LogTextureLeak("BEFORE loading new map");

			LeakDetector.ResetCounters("MapLoad");
			LeakDetector.LogSnapshot("BEFORE loading new map");

			// ─── Cleanup previous map ─────────────────────────────────────
			if (CurrentMap != null)
			{
				if (null != mainReflection)
				{
					CurrentMap.OnRenderSettingsChanged -= mainReflection.OnRenderSettingsChanged;
					CurrentMap.OnEffectChanged -= mainReflection.OnEffectChanged;
				}

				editorController?.Reset();
				cameraController?.Reset();
				gameController?.Reset();
				CurrentMap.Destroy();
			}

			if (MapRoot != null)
				DestroyImmediate(MapRoot.gameObject);

			// ─── Create new container GameObject ──────────────────────────
			var container = new GameObject($"Map: {newMap.name}");
			container.transform.SetParent(transform, false);
			MapRoot = container.transform;

			// ─── Load & initialise ────────────────────────────────────────

			CurrentMap = newMap;

			if (null != mainReflection)
			{
				mainReflection.SetEffectMode(CurrentMap.Effect);
				mainReflection.SetOffset(-0.2f);
				CurrentMap.OnRenderSettingsChanged += mainReflection.OnRenderSettingsChanged;
				CurrentMap.OnEffectChanged += mainReflection.OnEffectChanged;
			}

			// Skybox - auto fixup legacy map data - will be removed soon
			if (string.IsNullOrEmpty(CurrentMap.skybox))
				CurrentMap.skybox = $"{CurrentMap.music}Skybox";

			if (null == AssetRegistry<Material>.FindSkybox(CurrentMap.skybox))
				CurrentMap.skybox = null;
			// Skybox - auto fixup legacy map data - ends here

			CurrentMap.Initialise(MapRoot, !ApplicationSettings.Scrambled);

			//LogTextureLeak("AFTER loading new map");
			LeakDetector.LogSnapshot("AFTER loading new map");

			if (null != mainReflection)
				mainReflection.OnRenderSettingsChanged(CurrentMap.RenderSettings);

			// Eggbot
			if (eggbotController != null)
				DestroyImmediate(eggbotController.gameObject);

			eggbotController = EggbotController.Instantiate(CurrentMap.character, transform);
			eggbotController?.Initialise(CurrentMap);
			eggbotController.gameObject.SetActive(ApplicationSettings.CurrentMode != ApplicationMode.Editor);

			// Controllers
			cameraController?.Initialise(CurrentMap, eggbotController);
			gameController?.Initialise(CurrentMap);
			editorController?.Initialise(CurrentMap);
		}

		public void HandleChangeMap(int delta)
		{
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
			ResourceSerializer.SaveDatabase(ResourceManager.database, fullPath, verbose: true);
#else
			Debug.Log("Save Database only works in Editor");
#endif
		}

		public void ImportMapAsAtomic()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			string importRoot = RuntimeFileBrowser.GetDefaultRootFolder();
#else
			string importRoot = ApplicationSettings.ExportFolder;
#endif
			RuntimeFileBrowser.OpenFile(
				"Import Atomic Map",
				".json",
				path =>
				{
					ResourceSerializer.ImportAtomicMap(path);
					string importedName = System.IO.Path.GetFileNameWithoutExtension(path);

					if (CurrentMap != null && CurrentMap.name == importedName)
						OnChangeMapRequested?.Invoke(0);
				},
				importRoot,
				importRoot);
		}

		public void ExportMapAsAtomic()
		{
			if (CurrentMap == null)
			{
#if UNITY_EDITOR
				EditorUtility.DisplayDialog("Export Error", "No map is currently loaded.", "OK");
#else
				Debug.LogError("No map is currently loaded.");
#endif
				return;
			}

			var map = CurrentMap;
			string fileName = $"{map.name}.json";
			string json = ResourceSerializer.BuildAtomicMapJson(map, verbose: true, crop: true);

			if (string.IsNullOrEmpty(json))
			{
				Debug.LogError("Failed to build export JSON.");
				return;
			}

#if UNITY_WEBGL && !UNITY_EDITOR
			WebGLDownloadUtility.DownloadText(fileName, json, "application/json;charset=utf-8");
			Debug.Log($"Map export prepared for browser download: {fileName}");
#elif UNITY_EDITOR
			string defaultFolder = ApplicationSettings.ExportFolder;
			System.IO.Directory.CreateDirectory(defaultFolder);
			string path = EditorUtility.SaveFilePanel("Export Map As Atomic JSON", defaultFolder, fileName, "json");
			if (string.IsNullOrEmpty(path))
			{
				Debug.Log("Export cancelled by user.");
				return;
			}

			try
			{
				System.IO.File.WriteAllText(path, json);
				EditorUtility.DisplayDialog("Export Successful", $"Map exported successfully!\n\nPath: {path}", "OK");
				Debug.Log($"Map exported: {path}");
			}
			catch (System.Exception ex)
			{
				EditorUtility.DisplayDialog("Export Failed", $"Error during export:\n{ex.Message}", "OK");
				Debug.LogError($"Export failed: {ex}");
			}
#else
			string defaultFolder = ResourceSerializer.GetDefaultMapExportFolder();
			System.IO.Directory.CreateDirectory(defaultFolder);
			string path = System.IO.Path.Combine(defaultFolder, fileName);
			try
			{
				System.IO.File.WriteAllText(path, json);
				Debug.Log($"Map exported: {path}");
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Export failed: {ex}");
			}
#endif
		}

		//private void LogTextureLeak(string label)
		//{
		//	var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
		//	var cubemaps = Resources.FindObjectsOfTypeAll<Cubemap>();

		//	long texBytes = 0;
		//	foreach (var t in textures)
		//		texBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);

		//	long cubeBytes = 0;
		//	foreach (var c in cubemaps)
		//		cubeBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(c);

		//	Debug.Log($"[{label}] Textures: {textures.Length} | {texBytes / (1024f * 1024f):F2} MB");
		//	Debug.Log($"[{label}] Cubemaps: {cubemaps.Length} | {cubeBytes / (1024f * 1024f):F2} MB");

		//	// Show the biggest ones
		//	var biggest = textures.OrderByDescending(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t)).Take(8);
		//	foreach (var t in biggest)
		//	{
		//		long size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
		//		Debug.Log($"   → {t.name} ({t.width}x{t.height}) - {size / (1024f * 1024f):F2} MB");
		//	}
		//}
	}
}
