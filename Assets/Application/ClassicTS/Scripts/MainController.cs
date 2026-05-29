using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;
using MassiveHadronLtd;
using MassiveHadronLtd.FileBrowserUtil;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
		private static string launchMapQuery;

#if UNITY_WEBGL && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern string MassiveHadron_GetQueryParameter(string name);
#endif

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

			if (!FindAnyObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

#if UNITY_WEBGL && !UNITY_EDITOR
			WebGLPersistentStorage.EnsureLoaded(_ => CompleteStartupInitialization());
#else
			CompleteStartupInitialization();
#endif
		}

		private void CompleteStartupInitialization()
		{
			if (gameController != null)
				return;

			ResourceSerializer.Initialise();
			gameController = gameObject.AddComponent<GameController>();
			editorController = gameObject.AddComponent<EditorController>();
			cameraController = gameObject.AddComponent<MainCameraController>();
			OnChangeMapRequested += HandleChangeMap;
			launchMapQuery = TryGetLaunchMapQuery();
			if (!string.IsNullOrWhiteSpace(launchMapQuery))
			{
				Debug.Log($"Launch map query detected: {launchMapQuery}");
				StartCoroutine(LoadLaunchMapOrDefault());
			}
			else
			{
				Debug.Log("No launch map query detected. Loading configured default map.");
				LoadMap(ApplicationSettings.LoadMapName);
			}
			SetPreviewMode(ApplicationSettings.CurrentMode);//invoke to enable and disable game and editor controllers - ToDo improve this
		}

		private System.Collections.IEnumerator LoadLaunchMapOrDefault()
		{
			yield return SharedMapRepository.FetchManifest(
				manifest =>
				{
					Debug.Log($"Launch map manifest loaded: {manifest?.entries?.Length ?? 0} entrie(s).");
					var entry = FindLaunchManifestEntry(manifest?.entries, launchMapQuery);
					if (entry != null)
					{
						Debug.Log($"Launch map matched repository entry: {entry.DisplayName} ({entry.fileName})");
						StartCoroutine(ImportLaunchMap(entry));
						return;
					}

					Debug.LogWarning($"Launch map '{launchMapQuery}' was not found in the repository manifest. Loading default map instead.");
					LoadMap(ApplicationSettings.LoadMapName);
				},
				error =>
				{
					Debug.LogWarning($"Launch map lookup failed: {error}. Loading default map instead.");
					LoadMap(ApplicationSettings.LoadMapName);
				});
		}

		private System.Collections.IEnumerator ImportLaunchMap(SharedMapRepository.Entry entry)
		{
			Debug.Log($"Downloading launch map from repository: {entry?.DisplayName}");
			yield return SharedMapRepository.DownloadAndImport(
				entry,
				imported =>
				{
					if (imported == null)
					{
						LoadMap(ApplicationSettings.LoadMapName);
						return;
					}

					ApplicationSettings.LoadMapName = HTB50Settings.ToString(imported.HashID);
					Debug.Log($"Launch map imported: {imported.name} [{ApplicationSettings.LoadMapName}]");
					LoadMap(ApplicationSettings.LoadMapName);
				},
				error =>
				{
					Debug.LogWarning($"Launch map download failed: {error}. Loading default map instead.");
					LoadMap(ApplicationSettings.LoadMapName);
				});
		}

		private static SharedMapRepository.Entry FindLaunchManifestEntry(IEnumerable<SharedMapRepository.Entry> entries, string query)
		{
			if (string.IsNullOrWhiteSpace(query))
				return null;

			var normalizedQuery = NormalizeLaunchIdentifier(query);
			return entries?.FirstOrDefault(entry =>
				entry != null &&
				(string.Equals(NormalizeLaunchIdentifier(entry.fileName), normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(NormalizeLaunchIdentifier(entry.id), normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
				 string.Equals(NormalizeLaunchIdentifier(entry.mapHash), normalizedQuery, StringComparison.OrdinalIgnoreCase)));
		}

		private static string NormalizeLaunchIdentifier(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;

			var normalized = Uri.UnescapeDataString(value).Trim();
			if (normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
				normalized.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
			{
				normalized = Path.GetFileNameWithoutExtension(normalized);
			}

			return normalized;
		}

		private static string TryGetLaunchMapQuery()
		{
#if UNITY_WEBGL
			try
			{
#if !UNITY_EDITOR
				string bridgeValue = MassiveHadron_GetQueryParameter("map");
				if (!string.IsNullOrWhiteSpace(bridgeValue))
				{
					Debug.Log($"WebGL launch map bridge value: {bridgeValue}");
					return bridgeValue.Trim();
				}

				Debug.Log("WebGL launch map bridge returned no value.");
#endif

				var url = Application.absoluteURL;
				Debug.Log($"Application.absoluteURL: {url}");
				if (string.IsNullOrWhiteSpace(url))
				{
					Debug.Log("Application.absoluteURL was empty.");
					return null;
				}

				if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
				{
					Debug.LogWarning($"Could not parse Application.absoluteURL: {url}");
					return null;
				}

				var query = uri.Query;
				if (string.IsNullOrWhiteSpace(query))
				{
					Debug.Log("Application.absoluteURL had no query string.");
					return null;
				}

				foreach (var pair in query.TrimStart('?').Split('&'))
				{
					if (string.IsNullOrWhiteSpace(pair))
						continue;

					var parts = pair.Split(new[] { '=' }, 2);
					if (parts.Length != 2)
						continue;

					if (!string.Equals(Uri.UnescapeDataString(parts[0]), "map", StringComparison.OrdinalIgnoreCase))
						continue;

					return Uri.UnescapeDataString(parts[1]).Trim();
				}

				Debug.Log("Application.absoluteURL query string did not contain a map parameter.");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to read launch map query: {ex.Message}");
				return null;
			}
#endif
			return null;
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

			if (string.IsNullOrEmpty(CurrentMap.skybox))
			{
				var musicDisplayName = Assets.MusicResourceTable.GetDisplayName(CurrentMap.music) ?? CurrentMap.music;
				CurrentMap.skybox = Assets.SkycubeResourceTable.GetHashForDisplayName($"{musicDisplayName}Skybox");
			}

			if (null == SkyboxAssets.Find(CurrentMap.skybox))
				CurrentMap.skybox = null;
			CurrentMap.Initialise(MapRoot);

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
			if (CurrentMap == null)
				return;

			if (eggbotController == null)
			{
				Debug.LogWarning("Solve step skipped: no active eggbot controller.");
			}
			else
			{
				var destinationTile = eggbotController.DestinationTile(CurrentMap);
				if (destinationTile < 0)
				{
					Debug.LogWarning("Solve step skipped: no active destination waypoint.");
					return;
				}

				if (TileDebugVisualizer.Enabled)
				{
					Debug.Log(TileDebugVisualizer.Visualize(CurrentMap, eggbotController.CurrentTile, destinationTile));
				}

				if (eggbotController.NavDirection(CurrentMap) != 0)
				{
					Debug.Log("Solve step skipped: the current path is already complete.");
					return;
				}

				if (TileRouteAssembler.TryAssemble(CurrentMap, eggbotController.CurrentTile, destinationTile, out var assembled))
				{
					if (CurrentMap.ApplyState(assembled.State))
						Debug.Log($"Solve assembled candidate state: {assembled.Summary}");
					else
						Debug.LogWarning("Solve assembly found a state but failed to apply it.");
					return;
				}

				Debug.Log($"Solve assembly skipped: {assembled?.Summary ?? "no candidate route state found."}");
				return;
			}

		}

		public void LoadDatabase()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			WebGLPersistentStorage.EnsureLoaded(_ => ReloadDatabaseNow());
#else
			ReloadDatabaseNow();
#endif
		}

		private void ReloadDatabaseNow()
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
			if (ResourceManager.database == null)
			{
				Debug.LogError("Cannot save: database not loaded");
				return;
			}

			if (CurrentMap != null)
			{
				ResourceManager.ApplyMapChanges(CurrentMap);
			}

			bool externalOnly = !Application.isEditor;
			ResourceSerializer.SaveDatabase(ResourceManager.database, verbose: true, externalOnly: externalOnly);
			WebGLPersistentStorage.Flush();
		}

		public void ImportMapAsAtomic()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			string importRoot = RuntimeFileBrowser.GetDefaultRootFolder();
#else
			string importRoot = ApplicationSettings.UserFolder;
#endif
			RuntimeFileBrowser.OpenFile(
				"Import Atomic Map",
				new[] { ".json", ".zip" },
				path =>
				{
					var importedMap = ResourceSerializer.ImportAtomicMap(path);

					if (importedMap == null)
						return;

					ApplicationSettings.LoadMapName = GetMapHash(importedMap);
					LoadMap(ApplicationSettings.LoadMapName);
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
			const bool padded = false; // Release-style atomic exports are compact by default.
			const bool verbose = false; // Release-style atomic exports stay filtered by default.

			var export = ResourceSerializer.ExportAtomicMap(map, crop: true, padded: padded, verbose: verbose);
			if (export == null || !export.IsValid)
			{
				Debug.LogError("Failed to prepare atomic export.");
				return;
			}

			PlatformFileBrowser.ExportAtomicMap(export);
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
