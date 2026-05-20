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
		public static string JsonDataProjectPath => Path.Combine(Application.dataPath, "Application", "Resources", JsonDataPath.Replace('/', Path.DirectorySeparatorChar));
		public static string JsonDataResourcePath => JsonDataPath;

		[Header("online map repository")]
		[SerializeField] private string mapRepositoryBaseUrl = "";
		[SerializeField] private string mapRepositoryUploadKey = "";
		[SerializeField] private string mapRepositoryGitHubRepository = "";
		[SerializeField] private string mapRepositoryGitHubBranch = "main";
		private const string MapRepositoryPrivateTokenFile = "Assets/Private/TileStormMapRepositoryToken.txt";
		private const string MapRepositoryPrivateTokenResourceFile = "Assets/Private/Resources/TileStormMapRepositoryToken.txt";
		private const string MapRepositoryPrivateTokenResourceName = "TileStormMapRepositoryToken";
		private const string MapRepositoryBaseUrlPrefKey = "MapRepositoryBaseUrl";
		private const string MapRepositoryUploadKeyPrefKey = "MapRepositoryUploadKey";
		private const string MapRepositoryGitHubRepositoryPrefKey = "MapRepositoryGitHubRepository";
		private const string MapRepositoryGitHubBranchPrefKey = "MapRepositoryGitHubBranch";

		public static string MapRepositoryBaseUrl
		{
			get
			{
				string fallback = instance != null ? instance.mapRepositoryBaseUrl : "";
				return PlayerPrefsX.GetString(MapRepositoryBaseUrlPrefKey, fallback ?? string.Empty);
			}
			set
			{
				if (instance != null)
					instance.mapRepositoryBaseUrl = value;

				PlayerPrefsX.SetString(MapRepositoryBaseUrlPrefKey, value ?? string.Empty, true);
			}
		}

		public static string MapRepositoryUploadKey
		{
			get
			{
				string privateValue = ReadPrivateMapRepositoryUploadKey();
				if (!string.IsNullOrWhiteSpace(privateValue))
					return privateValue;

				string fallback = instance != null ? instance.mapRepositoryUploadKey : "";
				string value = PlayerPrefsX.GetString(MapRepositoryUploadKeyPrefKey, fallback ?? string.Empty);
				return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
			}
			set
			{
				if (instance != null)
					instance.mapRepositoryUploadKey = value;

				PlayerPrefsX.SetString(MapRepositoryUploadKeyPrefKey, value ?? string.Empty, true);
				WritePrivateMapRepositoryUploadKey(value);
			}
		}

		public static bool HasPrivateMapRepositoryUploadKey => !string.IsNullOrWhiteSpace(ReadPrivateMapRepositoryUploadKey());

		public static string MapRepositoryGitHubRepository
		{
			get
			{
				string fallback = instance != null ? instance.mapRepositoryGitHubRepository : "";
				return PlayerPrefsX.GetString(MapRepositoryGitHubRepositoryPrefKey, fallback ?? string.Empty);
			}
			set
			{
				if (instance != null)
					instance.mapRepositoryGitHubRepository = value;

				PlayerPrefsX.SetString(MapRepositoryGitHubRepositoryPrefKey, value ?? string.Empty, true);
			}
		}

		public static string MapRepositoryGitHubBranch
		{
			get
			{
				string fallback = instance != null ? instance.mapRepositoryGitHubBranch : "main";
				return PlayerPrefsX.GetString(MapRepositoryGitHubBranchPrefKey, fallback ?? "main");
			}
			set
			{
				if (instance != null)
					instance.mapRepositoryGitHubBranch = value;

				PlayerPrefsX.SetString(MapRepositoryGitHubBranchPrefKey, value ?? string.Empty, true);
			}
		}

		[Header("content roots")]
		[SerializeField] private string[] contentRoots = new[] { AssetPath.ImmutableRootFolder, AssetPath.GlobalRootFolder, "ClassicTS", "Evolution" };
		private static readonly string[] DefaultContentRoots = new[] { AssetPath.ImmutableRootFolder, AssetPath.GlobalRootFolder, "ClassicTS", "Evolution" };
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
			private const string TokenPrompt = "<paste your access token here>";

			public override void OnInspectorGUI()
			{
				serializedObject.Update();

				DrawProperty(nameof(loadMapName));
				DrawProperty(nameof(remapGeometry));
				DrawProperty(nameof(scrambled));
				DrawProperty(nameof(difficulty));
				DrawProperty(nameof(music));
				DrawProperty(nameof(showEditorGrid));
				DrawProperty(nameof(detailLevel));
				DrawProperty(nameof(showHiddenTiles));
				DrawProperty(nameof(showTileSelection));
				DrawProperty(nameof(jsonDataPath));
				EditorGUILayout.Space(8);
				DrawMapRepositoryInspector();
				EditorGUILayout.Space(8);
				DrawProperty(nameof(contentRoots));
				DrawProperty(nameof(previewMode));

				serializedObject.ApplyModifiedProperties();
				EditorGUILayout.Space(10);

				if (GUILayout.Button("Locate Export Folder", GUILayout.Height(30)))
				{
					string folder = ExportFolder;
					System.IO.Directory.CreateDirectory(folder);
					EditorUtility.RevealInFinder(folder);
					Debug.Log($"Opened export folder: {folder}");
				}
			}

			private void DrawProperty(string propertyName)
			{
				var property = serializedObject.FindProperty(propertyName);
				if (property != null)
					EditorGUILayout.PropertyField(property, true);
			}

			private void DrawMapRepositoryInspector()
			{
				EditorGUILayout.LabelField("online map repository", EditorStyles.boldLabel);
				DrawProperty(nameof(mapRepositoryBaseUrl));
				DrawProperty(nameof(mapRepositoryGitHubRepository));
				DrawProperty(nameof(mapRepositoryGitHubBranch));

				string currentToken = ReadPrivateMapRepositoryUploadKey();
				string displayedToken = string.IsNullOrWhiteSpace(currentToken) ? TokenPrompt : currentToken;
				EditorGUI.BeginChangeCheck();
				string editedToken = EditorGUILayout.TextField("Map Repository Upload Key", displayedToken);
				if (EditorGUI.EndChangeCheck())
				{
					string trimmed = string.IsNullOrWhiteSpace(editedToken) || string.Equals(editedToken, TokenPrompt, StringComparison.Ordinal)
						? string.Empty
						: editedToken.Trim();
					WritePrivateMapRepositoryUploadKey(trimmed);
				}

				EditorGUILayout.HelpBox(
					"Stored in Assets/Private/TileStormMapRepositoryToken.txt and ignored by git. Leave blank to disable publishing on this machine.",
					MessageType.Info);
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
			var cleaned = (DefaultContentRoots ?? Array.Empty<string>())
				.Concat(roots ?? Array.Empty<string>())
				.Select(AssetPath.NormalizePath)
				.Where(root => !string.IsNullOrWhiteSpace(root))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			return cleaned.Length > 0 ? cleaned : DefaultContentRoots;
		}

		private static string ReadPrivateMapRepositoryUploadKey()
		{
			try
			{
#if !UNITY_EDITOR
				var tokenAsset = Resources.Load<TextAsset>(MapRepositoryPrivateTokenResourceName);
				if (tokenAsset != null && !string.IsNullOrWhiteSpace(tokenAsset.text))
					return tokenAsset.text.Trim();
#endif
				var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
				if (string.IsNullOrWhiteSpace(projectRoot))
					return string.Empty;

				string path = Path.Combine(projectRoot, MapRepositoryPrivateTokenFile);
				if (!File.Exists(path))
					return string.Empty;

				return File.ReadAllText(path).Trim();
			}
			catch
			{
				return string.Empty;
			}
		}

		private static void WritePrivateMapRepositoryUploadKey(string value)
		{
			try
			{
				var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
				if (string.IsNullOrWhiteSpace(projectRoot))
					return;

				string path = Path.Combine(projectRoot, MapRepositoryPrivateTokenFile);
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, value ?? string.Empty);

				string resourcePath = Path.Combine(projectRoot, MapRepositoryPrivateTokenResourceFile);
				Directory.CreateDirectory(Path.GetDirectoryName(resourcePath));
				File.WriteAllText(resourcePath, value ?? string.Empty);
#if UNITY_EDITOR
				AssetDatabase.Refresh();
#endif
				Debug.Log(string.IsNullOrWhiteSpace(value)
					? $"Cleared private map repository token file: {path}"
					: $"Updated private map repository token file: {path}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to write private map repository token file: {ex.Message}");
			}
		}

#if UNITY_EDITOR
		[InitializeOnLoadMethod]
		private static void Editor_SyncPrivateMapRepositoryUploadKeyResourceOnLoad()
		{
			EditorApplication.delayCall += Editor_SyncPrivateMapRepositoryUploadKeyResource;
		}

		private static void Editor_SyncPrivateMapRepositoryUploadKeyResource()
		{
			try
			{
				var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
				if (string.IsNullOrWhiteSpace(projectRoot))
					return;

				string tokenPath = Path.Combine(projectRoot, MapRepositoryPrivateTokenFile);
				string resourcePath = Path.Combine(projectRoot, MapRepositoryPrivateTokenResourceFile);
				string token = File.Exists(tokenPath) ? File.ReadAllText(tokenPath) : string.Empty;

				Directory.CreateDirectory(Path.GetDirectoryName(resourcePath));
				if (!File.Exists(resourcePath) || !string.Equals(File.ReadAllText(resourcePath), token, StringComparison.Ordinal))
				{
					File.WriteAllText(resourcePath, token);
					AssetDatabase.ImportAsset(MapRepositoryPrivateTokenResourceFile);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to sync private map repository token resource: {ex.Message}");
			}
		}
#endif

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
			Editor_SyncPrivateMapRepositoryUploadKeyResource();

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
