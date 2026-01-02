using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public enum PreviewMode
	{
		Editor,
		Player,
		Cinema
	}

	public class PreviewSettings : MonoBehaviour
	{
		public const string MutableDatabaseSubfolder = "Data";

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

				// Always update the serialized field and PlayerPrefs
				instance.remapGeometry = value;
				PlayerPrefsX.SetBool("RemapGeometry", value, true);

				// Always trigger the side effects when set via code or OnValidate
				// (We know it's a meaningful change because either:
				//  - It came from OnValidate (inspector toggle)
				//  - Or from external code that explicitly wants the change)
				OnRemapGeometryChanged?.Invoke(value);
				PrefabFactory.ClearCache();
				GeometrySearchProvider.UseRemapping = value; // Optional: direct sync for safety

				Debug.Log($"[PreviewSettings] Remap Geometry set to: {value}. Cache cleared.");
			}
		}

		public static event System.Action<bool> OnRemapGeometryChanged;

		[Header("load map scrambled or solved")]
		[SerializeField] private bool scrambled = true;
		public static bool Scrambled => instance.scrambled;

		[Header("enable or disable easy mode")]
		[SerializeField] private bool difficulty = false;
		public static bool Difficulty => instance.difficulty;

		[Header("hidden tiles")]
		[SerializeField] private bool showHiddenTiles = false;
		public static bool ShowHiddenTiles => instance.showHiddenTiles;

		[Header("tile selection")]
		[SerializeField] private bool showTileSelection = false;
		public static bool ShowTileSelection => instance.showTileSelection;

		[Header("resource paths")]
		[SerializeField] private TextAsset databaseJsonFile;
		public static TextAsset DatabaseJsonFile => instance.databaseJsonFile;

		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => instance.geometryPath;

		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => instance.texturePath;

		[SerializeField, ResourcePath] private string materialPath = "ClassicTS/Materials/";
		public static string MaterialPath => instance.materialPath;

		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
		public static string SkycubesPath => instance.skycubesPath;

		[SerializeField, ResourcePath] private string prefabPath = "ClassicTS/Prefabs/";
		public static string PrefabPath => instance.prefabPath;

		[Header("Game Mode")]
		[SerializeField] private PreviewMode previewMode = PreviewMode.Player;
		public static PreviewMode CurrentMode
		{
			get => (PreviewMode)PlayerPrefsX.GetInt("PreviousMode", (int)instance.previewMode);
			set
			{
				if (instance != null)
				{
					instance.previewMode = value;
					PlayerPrefsX.SetInt("PreviousMode", (int)value, true);
				}
			}
		}

		// ─────── Editor button helper (only thing that needs the path) ───────
#if UNITY_EDITOR
		[CustomEditor(typeof(PreviewSettings))]
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

		private static PreviewSettings instance;

		private void Awake()
		{
			instance = this;

			// Apply persisted value (or default) to the global toggle
			GeometrySearchProvider.UseRemapping = RemapGeometry;

			// Subscribe to future changes (from code or other UI)
			OnRemapGeometryChanged += (enabled) =>
			{
				GeometrySearchProvider.UseRemapping = enabled;
			};
		}

		private void OnValidate()
		{
			if (instance == this && Application.isPlaying) // Only during play mode runtime changes
			{
				// Get the CURRENT persisted value (without using the field as default!)
				bool persistedValue = PlayerPrefsX.GetBool("RemapGeometry", true); // true = original default if key missing

				// If the inspector field differs from what is currently persisted...
				if (remapGeometry != persistedValue)
				{
					// Push the new inspector value through the proper channel
					RemapGeometry = remapGeometry;
				}
			}
		}

		public static string DatabaseFolder => PreviewSettingsStatic.DatabaseFolder;
		public static string ExportFolder => PreviewSettingsStatic.ExportFolder;
	}

	public static class PreviewSettingsStatic
	{
		public static readonly string DatabaseFolder = System.IO.Path.Combine(Application.persistentDataPath, "Data");
		public static readonly string ExportFolder = System.IO.Path.Combine(Application.persistentDataPath, "Maps");
	}
}
