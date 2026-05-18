using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using MassiveHadronLtd;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public enum ApplicationMode
	{
		Editor,
		Player,
		Cinema
	}

	public class ApplicationSettings : MonoBehaviour
	{
		public const string JsonDataSubfolder = "Config";

		[Header("map to load")]
		[SerializeField] private string loadMapName = "Industrial 01";
		public static string LoadMapName
		{
			get => PlayerPrefsX.GetString("LastLoadedMap", instance.loadMapName);
			set
			{
				instance.loadMapName = value;
				PlayerPrefsX.SetString("LastLoadedMap", value, true);
			}
		}

		[Header("geometry loading")]
		[SerializeField]
		private bool remapGeometry = true;

		public static bool RemapGeometry
		{
			get => PlayerPrefsX.GetBool("RemapGeometry", instance?.remapGeometry ?? true);
			set
			{
				if (instance == null) return;
				if (instance.remapGeometry == value) return;
				instance.remapGeometry = value;
				PlayerPrefsX.SetBool("RemapGeometry", value, true);
				OnRemapGeometryChanged?.Invoke(instance.remapGeometry);
			}
		}

		public static event System.Action<bool> OnRemapGeometryChanged;

		[Header("load map scrambled or solved")]
		[SerializeField] private bool scrambled = true;
		public static bool Scrambled => instance.scrambled;

		[Header("enable or disable easy mode")]
		[SerializeField] private bool difficulty = true;
		public static bool Difficulty
		{
			get => PlayerPrefsX.GetBool("Difficulty", instance.difficulty);
			set
			{
				if (instance != null)
				{
					instance.difficulty = value;
					PlayerPrefsX.SetBool("Difficulty", value, true);
				}
			}
		}

		[Header("enable or disable music")]
		[SerializeField] private bool music = true;
		public static bool Music
		{
			get => PlayerPrefsX.GetBool("Music", instance.music);
			set
			{
				if (instance != null)
				{
					instance.music = value;
					PlayerPrefsX.SetBool("Music", value, true);
				}
			}
		}

		[Header("editor grid enable")]
		[SerializeField] private bool showEditorGrid = true;
		public static bool ShowEditorGrid
		{
			get => PlayerPrefsX.GetBool("ShowEditorGrid", instance.showEditorGrid);
			set
			{
				if (instance != null)
				{
					instance.showEditorGrid = value;
					PlayerPrefsX.SetBool("ShowEditorGrid", value, true);
				}
			}
		}

		[Header("detail level")]
		[SerializeField] private int detailLevel = 1;
		public static int DetailLevel
		{
			get => PlayerPrefsX.GetInt("DetailLevel", instance.detailLevel);
			set
			{
				if (instance != null)
				{
					instance.detailLevel = value;
					PlayerPrefsX.SetInt("DetailLevel", value, true);
				}
			}
		}

		[Header("hidden tiles")]
		[SerializeField] private bool showHiddenTiles = false;
		public static bool ShowHiddenTiles => instance.showHiddenTiles;

		[Header("tile selection")]
		[SerializeField] private bool showTileSelection = false;
		public static bool ShowTileSelection => instance.showTileSelection;

		[Header("json data root")]
		[SerializeField, ResourcePath] private string jsonDataPath = "ClassicTS/Config";
		public static string JsonDataPath => string.IsNullOrWhiteSpace(instance?.jsonDataPath) ? "ClassicTS/Config" : instance.jsonDataPath.Trim('/').Trim();
		public static string JsonDataProjectPath => Path.Combine(Application.dataPath, "Application", "ClassicTS", "Resources", JsonDataPath.Replace('/', Path.DirectorySeparatorChar));
		public static string JsonDataResourcePath => JsonDataPath;

		[Header("content roots")]
		[SerializeField] private string[] contentRoots = new[] { "ClassicTS", "Evolution" };
		private static readonly string[] DefaultContentRoots = new[] { "ClassicTS", "Evolution" };
		public static IReadOnlyList<string> ContentRoots => NormalizeContentRoots(instance?.contentRoots);

		public static IEnumerable<string> GetContentPaths(string subfolder)
			=> AssetPath.BuildPaths(ContentRoots, subfolder);

		public static IEnumerable<string> GetGeometryPaths() => GetContentPaths(AssetPath.GeometryFolder);
		public static IEnumerable<string> GetTexturePaths() => GetContentPaths(AssetPath.TextureFolder);
		public static IEnumerable<string> GetMaterialPaths() => GetContentPaths(AssetPath.MaterialFolder);
		public static IEnumerable<string> GetPrefabPaths() => GetContentPaths(AssetPath.PrefabFolder);
		public static IEnumerable<string> GetSkyCubePaths() => GetContentPaths(AssetPath.SkyCubesFolder);
		public static IEnumerable<string> GetSoundPaths() => GetContentPaths(AssetPath.SoundFolder);
		public static IEnumerable<string> GetMusicPaths() => GetContentPaths(AssetPath.MusicFolder);
		public static IEnumerable<string> GetGeometryMaterialPaths() => GetContentPaths($"{AssetPath.GeometryFolder}/{AssetPath.MaterialFolder}");

		[Header("Game Mode")]
		[SerializeField] private ApplicationMode previewMode = ApplicationMode.Player;
		public static ApplicationMode CurrentMode
		{
			get => (ApplicationMode)PlayerPrefsX.GetInt("PreviousMode", (int)instance.previewMode);
			set
			{
				if (instance != null)
				{
					instance.previewMode = value;
					PlayerPrefsX.SetInt("PreviousMode", (int)value, true);
				}
			}
		}

		// ─────── Editor button helper ───────
#if UNITY_EDITOR
		[CustomEditor(typeof(ApplicationSettings))]
		private class PreviewSettingsEditor : Editor
		{
			public override void OnInspectorGUI()
			{
				DrawDefaultInspector();
				EditorGUILayout.Space(10);

				if (GUILayout.Button("Locate Export Folder", GUILayout.Height(30)))
				{
					string folder = ExportFolder;
					System.IO.Directory.CreateDirectory(folder);
					EditorUtility.RevealInFinder(folder);
					Debug.Log($"Opened export folder: {folder}");
				}
			}
		}
#endif

		private static ApplicationSettings instance;

		private void Awake()
		{
			instance = this;
			remapGeometry = RemapGeometry;
		}

		private static IReadOnlyList<string> NormalizeContentRoots(IEnumerable<string> roots)
		{
			var cleaned = (roots ?? DefaultContentRoots)
				.Select(AssetPath.NormalizePath)
				.Where(root => !string.IsNullOrWhiteSpace(root))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			return cleaned.Length > 0 ? cleaned : DefaultContentRoots;
		}

		private void OnValidate()
		{
			if (!Application.isPlaying) return;
			if (instance != this) return;
		}

		public static string DatabaseFolder => Path.Combine(Application.persistentDataPath, AssetPath.DataRootFolder);
		public static string ExportFolder => UserFolder;
		public static string UserFolder => Path.Combine(Application.persistentDataPath, AssetPath.UserRootFolder);
		public static string SystemFolder => Path.Combine(Application.persistentDataPath, AssetPath.SystemRootFolder);
		public static string SystemMapsFolder => Path.Combine(SystemFolder, AssetPath.MapsFolder);
		public static string SystemDefinitionsFolder => Path.Combine(SystemFolder, AssetPath.DefinitionsFolder);
		public static string SystemModelsFolder => Path.Combine(SystemFolder, AssetPath.ModelsFolder);
		public static string SystemMaterialsFolder => Path.Combine(SystemFolder, AssetPath.MaterialFolder);
		public static string SystemTexturesFolder => Path.Combine(SystemFolder, AssetPath.TextureFolder);
		public static string SystemSkyCubesFolder => Path.Combine(SystemFolder, AssetPath.SkyCubesFolder);
		public static string SystemMusicFolder => Path.Combine(SystemFolder, AssetPath.MusicFolder);
		public static string SystemSoundsFolder => Path.Combine(SystemFolder, AssetPath.SoundFolder);

		// ====================== EDITOR HELPER (for manifest generation) ======================
#if UNITY_EDITOR
		/// <summary>
		/// Forces loading of the ApplicationSettings instance so content roots are available
		/// in MenuItems and PreprocessBuild, even without entering Play Mode.
		/// </summary>
		public static void Editor_ForceLoadInstance()
		{
			if (instance != null)
				return;

			// Use FindAnyObjectByType instead of the obsolete FindFirstObjectByType
			instance = UnityEngine.Object.FindAnyObjectByType<ApplicationSettings>(FindObjectsInactive.Include);

			if (instance == null)
			{
				Debug.LogWarning("ApplicationSettings instance not found in any loaded scene.\n" +
								 "Asset manifests will only use hardcoded fallback roots (e.g. Levels).");
			}
			else
			{
				Debug.Log("<color=cyan>ApplicationSettings instance loaded for editor manifest generation.</color>");
			}
		}
#endif
	}

}
