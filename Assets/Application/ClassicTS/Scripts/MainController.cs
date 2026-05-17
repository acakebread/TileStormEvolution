using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MassiveHadronLtd;
using MassiveHadronLtd.FileBrowserUtil;
using UnityEngine.EventSystems;
using ClassicTilestorm.Assets;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

		private static string GetMapHash(Map map) => map == null ? null : HTB50Settings.ToString(map.HashID);

		private static string DescribeMap(Map map)
		{
			if (map == null) return "<null>";
			var name = string.IsNullOrWhiteSpace(map.name) ? "Unnamed" : map.name;
			var hash = GetMapHash(map) ?? "000000";
			return $"{name} [{hash}]";
		}

		private static string BuildExportFileBase(Map map)
		{
			var name = string.IsNullOrWhiteSpace(map?.name) ? "Untitled" : map.name;
			var invalid = System.IO.Path.GetInvalidFileNameChars();
			var safeName = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
			return $"{safeName}__{GetMapHash(map) ?? "000000"}";
		}

		private static HashId? TryParseMapHash(string identifier)
		{
			if (string.IsNullOrWhiteSpace(identifier))
				return null;

			try
			{
				return HTB50.Decode(identifier);
			}
			catch
			{
				return null;
			}
		}

		private static int FindMapIndex(IList<Map> maps, string identifier)
		{
			if (maps == null || maps.Count == 0)
				return -1;

			var hash = TryParseMapHash(identifier);
			if (hash.HasValue)
			{
				for (int i = 0; i < maps.Count; i++)
					if (maps[i] != null && maps[i].HashID == hash.Value)
						return i;
			}

			if (!string.IsNullOrWhiteSpace(identifier))
			{
				for (int i = 0; i < maps.Count; i++)
					if (maps[i] != null && string.Equals(maps[i].name, identifier, StringComparison.OrdinalIgnoreCase))
						return i;
			}

			return -1;
		}

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
			ResourceSerializer.Initialise();
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

			cameraController.Suspend();
			try
			{
				cameraController.SetCameraMode(GameModes.GetModeString(mode));
				editorController.enabled = mode == ApplicationMode.Editor;
				gameController.enabled = mode != ApplicationMode.Editor;
				if (eggbotController != null)
					eggbotController.gameObject.SetActive(mode != ApplicationMode.Editor);
			}
			finally
			{
				cameraController.Resume();
			}
		}

		public void LoadMap(string mapName = null)
		{
			if (string.IsNullOrEmpty(mapName ??= ApplicationSettings.LoadMapName))
				return;

			var maps = ResourceManager.Maps;
			var mapHash = TryParseMapHash(mapName);
			var newMap = mapHash.HasValue
				? maps.FirstOrDefault(m => m != null && m.HashID == mapHash.Value)
				: null;
			newMap = newMap
						  ?? maps.FirstOrDefault(m => m != null && string.Equals(m.name, mapName, StringComparison.OrdinalIgnoreCase))
						  ?? maps.FirstOrDefault();

			if (newMap == null)
			{
				Debug.LogError($"No map found for '{mapName}'! Available: {string.Join(", ", ResourceManager.Maps.Select(DescribeMap))}");
				return;
			}

			var mainReflection = Camera.main?.GetComponent<ReflectionEffectCamera>();
			cameraController?.Suspend();
			try
			{

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
			var container = new GameObject($"Map: {DescribeMap(newMap)}");
			container.transform.SetParent(transform, false);
			MapRoot = container.transform;

			// ─── Load & initialise ────────────────────────────────────────

			CurrentMap = newMap;
			ApplicationSettings.LoadMapName = GetMapHash(CurrentMap);

			if (null != mainReflection)
			{
				mainReflection.SetEffectMode(CurrentMap.Effect);
				mainReflection.SetOffset(-0.2f);
				CurrentMap.OnRenderSettingsChanged += mainReflection.OnRenderSettingsChanged;
				CurrentMap.OnEffectChanged += mainReflection.OnEffectChanged;
			}

			// Skybox - auto fixup legacy map data - will be removed soon
			if (string.IsNullOrEmpty(CurrentMap.skybox))
			{
				var musicDisplayName = Assets.MusicResourceTable.GetDisplayName(CurrentMap.music) ?? CurrentMap.music;
				CurrentMap.skybox = Assets.SkycubeResourceTable.GetHashForDisplayName($"{musicDisplayName}Skybox");
			}

			if (null == SkyboxAssets.Find(CurrentMap.skybox))
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
			finally
			{
				cameraController?.Resume();
			}
		}

		public void HandleChangeMap(int delta)
		{
			var maps = ResourceManager.Maps;
			if (maps == null || maps.Count == 0) return;

			// Find current index the old-school way — works on ANY IList<T>
			int currentIndex = FindMapIndex(maps, ApplicationSettings.LoadMapName);
			if (currentIndex == -1) currentIndex = 0;

			if (delta != 0)
			{
				currentIndex = (currentIndex + delta + maps.Count) % maps.Count;
				ApplicationSettings.LoadMapName = GetMapHash(maps[currentIndex]);
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
			#if UNITY_EDITOR
			UnityEditor.AssetDatabase.Refresh();
			#endif

			ResourceSerializer.Initialise();

			if (ResourceManager.database == null)
			{
				Debug.LogError("Failed to reload content data from levels.json / definitions.json!");
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

			if (CurrentMap != null)
			{
				ResourceManager.ApplyMapChanges(CurrentMap);
			}

			ResourceSerializer.SaveDatabase(ResourceManager.database, verbose: true);
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
					var importedMap = ResourceSerializer.ImportAtomicMap(path);

					if (CurrentMap != null && importedMap != null && CurrentMap.HashID == importedMap.HashID)
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
			string fileName = $"{BuildExportFileBase(map)}.json";
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
