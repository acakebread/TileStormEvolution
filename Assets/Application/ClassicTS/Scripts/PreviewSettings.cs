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

		/// <summary>
		/// The original pristine database that ships with the game (never modified)
		/// </summary>
		public static TextAsset PristineDatabaseJsonFile => instance?.databaseJsonFile;

		/// <summary>
		/// The current mutable database (auto-copied to persistentDataPath if missing)
		/// </summary>
		public static TextAsset DatabaseJsonFile
		{
			get
			{
				if (instance == null)
				{
					Debug.LogError("PreviewSettings.instance is null! Make sure a GameObject with PreviewSettings exists in the scene.");
					return null;
				}

				if (instance.databaseJsonFile == null)
				{
					Debug.LogError("PreviewSettings: No pristine databaseJsonFile assigned in the Inspector!");
					return null;
				}

				return ResourceFileIO.GetMutableDatabaseTextAsset(instance.databaseJsonFile);
			}
		}

		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => instance.geometryPath;

		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => instance.texturePath;

		[SerializeField, ResourcePath] private string materialPath = "ClassicTS/Materials/";
		public static string MaterialPath => instance.materialPath;

		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
		public static string SkycubesPath => instance.skycubesPath;

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

				if (GUILayout.Button("Locate Mutable Database", GUILayout.Height(30)))
				{
					string folder = System.IO.Path.Combine(Application.persistentDataPath, "Data");
					System.IO.Directory.CreateDirectory(folder);
					EditorUtility.RevealInFinder(folder);
					Debug.Log($"Opened mutable database folder: {folder}");
				}
			}
		}
#endif

		private static PreviewSettings instance;

		private void Awake()
		{
			instance = this;
		}
	}
}
