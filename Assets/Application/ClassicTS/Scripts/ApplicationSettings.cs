using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using MassiveHadronLtd;

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
		[SerializeField] private bool difficulty = false;
		public static bool Difficulty => instance.difficulty;

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
		[SerializeField] private int detailLevel = 1;// Default to Game Only
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

		[Header("resource paths")]
		[SerializeField] private TextAsset databaseJsonFile;
		public static TextAsset DatabaseJsonFile => instance.databaseJsonFile;

		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => instance?.geometryPath;

		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => instance?.texturePath;

		[SerializeField, ResourcePath] private string materialPath = "ClassicTS/Materials/";
		public static string MaterialPath => instance?.materialPath;

		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
		public static string SkycubesPath => instance?.skycubesPath;

		[SerializeField, ResourcePath] private string prefabPath = "ClassicTS/Prefabs/";
		public static string PrefabPath => instance?.prefabPath;

		[SerializeField, ResourcePath] private string soundPath = "ClassicTS/Sounds/";
		public static string SoundPath => instance?.soundPath;

		[SerializeField, ResourcePath] private string musicPath = "ClassicTS/Music/";
		public static string MusicPath => instance?.musicPath;

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

		// ─────── Editor button helper (only thing that needs the path) ───────
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


		//[Header("debug [to be removed]")]
		//public Texture2D testTexture;
		//public static Texture2D TestTexture { get => instance?.testTexture; set => instance.testTexture = value; }

		//public Material outline128x128;
		//public static Material Outline128x128 { get => instance?.outline128x128; set => instance.outline128x128 = value; }

		//public Material background128x128;
		//public static Material Background128x128 { get => instance?.background128x128; set => instance.background128x128 = value; }

		//public UnityEngine.UI.Image panelTarget;
		//public static UnityEngine.UI.Image PanelTarget { get => instance?.panelTarget; set => instance.panelTarget = value; }

		//public UnityEngine.UI.RawImage gridTarget;
		//public static UnityEngine.UI.RawImage GridTarget { get => instance?.gridTarget; set => instance.gridTarget = value; }

		//public UnityEngine.UI.RawImage focusTarget;
		//public static UnityEngine.UI.RawImage FocusTarget { get => instance?.focusTarget; set => instance.focusTarget = value; }

		private void Awake()
		{
			instance = this;
			remapGeometry = RemapGeometry;
			//if (null != outline128x128) ScreenSpaceUtil.SetOutlineMaterial(outline128x128);
		}

		private void OnValidate()
		{
			// Skip runtime-specific actions when not in play mode
			if (!Application.isPlaying) return;

			if (instance != this) return; // Ensure it's the runtime instance

//			var persistedValue = PlayerPrefsX.GetBool("RemapGeometry", remapGeometry);

//			if (remapGeometry != persistedValue)
//			{
//				// Still persist the value immediately
//				RemapGeometry = remapGeometry;

//				// BUT: Defer the event invoke and geometry refresh until safe (after OnValidate)
//#if UNITY_EDITOR
//				EditorApplication.delayCall += () => OnRemapGeometryChanged?.Invoke(remapGeometry);
//#else
//			    // In builds (unlikely to hit this path), invoke immediately
//				OnRemapGeometryChanged?.Invoke(remapGeometry);
//#endif
//			}
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
