using UnityEngine;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public enum PreviewMode
    {
        Player,
        Direct,
        Cinema,
        Editor
    }

    public class PreviewSettings : MonoBehaviour
    {
        // Public constant for the subfolder name in persistentDataPath (can be empty for root)
        public const string MutableDatabaseSubfolder = "Data";

        [Header("map to load")]
        [SerializeField] private string loadMapName = "Industrial 01";
        public static string LoadMapName { get => instance.loadMapName; set => instance.loadMapName = value; }

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
        public static TextAsset DatabaseJsonFile
        {
            get
            {
                if (instance == null || instance.databaseJsonFile == null)
                {
                    Debug.LogWarning("PreviewSettings instance or databaseJsonFile is null.");
                    return null;
                }

                string targetPath = GetDatabaseFilePath();
                string fileContent = SerializerUtility.ReadTextAsset(targetPath, instance.databaseJsonFile);
                if (string.IsNullOrEmpty(fileContent))
                {
                    Debug.LogError("PreviewSettings: Failed to read text asset.");
                    return null;
                }

                TextAsset textAsset = new TextAsset(fileContent);
                textAsset.name = instance.databaseJsonFile.name;
                return textAsset;
            }
            set
            {
                if (value == null)
                {
                    Debug.LogError("PreviewSettings: Cannot set DatabaseJsonFile to null.");
                    return;
                }

                string targetPath = GetDatabaseFilePath();
                if (string.IsNullOrEmpty(targetPath))
                {
                    Debug.LogError("PreviewSettings: Cannot set DatabaseJsonFile because target path is null.");
                    return;
                }

                SerializerUtility.WriteTextFile(targetPath, value.text);
                instance.databaseJsonFile = value;
                Debug.Log($"PreviewSettings: Updated DatabaseJsonFile and saved to {targetPath}");
            }
        }

        [SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
        public static string GeometryPath => instance.geometryPath;

        [SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
        public static string TexturePath => instance.texturePath;

        [SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
        public static string SkycubesPath => instance.skycubesPath;

        [Header("Game Mode")]
        [SerializeField] private PreviewMode previewMode = PreviewMode.Player;

        public static PreviewMode CurrentMode
        {
            get => instance != null ? instance.previewMode : PreviewMode.Player;
            set
            {
                if (instance != null)
                {
                    instance.previewMode = value;
                }
            }
        }

        private static PreviewSettings instance;
        void Awake() => instance = this;

        public static string GetDatabaseFilePath()
        {
            if (instance == null || instance.databaseJsonFile == null)
            {
                Debug.LogError("PreviewSettings: instance or databaseJsonFile is null.");
                return null;
            }

            string targetFolder = string.IsNullOrEmpty(MutableDatabaseSubfolder)
                ? Application.persistentDataPath
                : Path.Combine(Application.persistentDataPath, MutableDatabaseSubfolder);
            string fullFileName = instance.databaseJsonFile.name.EndsWith(".json")
                ? instance.databaseJsonFile.name
                : instance.databaseJsonFile.name + ".json";
            return Path.Combine(targetFolder, fullFileName);
        }

        public static string GetDatabaseFolderPath()
        {
            return string.IsNullOrEmpty(MutableDatabaseSubfolder)
                ? Application.persistentDataPath
                : Path.Combine(Application.persistentDataPath, MutableDatabaseSubfolder);
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(PreviewSettings))]
        private class PreviewSettingsEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                EditorGUILayout.Space();

                if (GUILayout.Button("Locate Mutable Database"))
                {
                    string targetFolder = GetDatabaseFolderPath();
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                        Debug.Log($"Created directory: {targetFolder}");
                    }

                    try
                    {
                        string normalizedPath = targetFolder.Replace("/", Path.DirectorySeparatorChar.ToString());
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = normalizedPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                        Debug.Log($"Opened file explorer at: {normalizedPath}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to open file explorer at {targetFolder}: {ex.Message}");
                    }
                }
            }
        }
#endif
    }
}

//using UnityEngine;
//using System.IO;

//namespace ClassicTilestorm
//{
//	public enum PreviewMode
//	{
//		Player,
//		Direct,
//		Cinema,
//		Editor
//	}

//	public class PreviewSettings : MonoBehaviour
//	{
//		[Header("map to load")]
//		[SerializeField] private string loadMapName = "Industrial 01";
//		public static string LoadMapName { get => instance.loadMapName; set => instance.loadMapName = value; }

//		[Header("load map scrambled or solved")]
//		[SerializeField] private bool scrambled = true;
//		public static bool Scrambled => instance.scrambled;

//		[Header("enable or disable easy mode")]
//		[SerializeField] private bool difficulty = false;
//		public static bool Difficulty => instance.difficulty;

//		[Header("hidden tiles")]
//		[SerializeField] private bool showHiddenTiles = false;
//		public static bool ShowHiddenTiles => instance.showHiddenTiles;

//		[Header("tile selection")]
//		[SerializeField] private bool showTileSelection = false;
//		public static bool ShowTileSelection => instance.showTileSelection;

//		[Header("resource paths")]
//		[SerializeField] private TextAsset databaseJsonFile;
//		public static TextAsset DatabaseJsonFile
//		{
//			get
//			{
//				if (instance == null || instance.databaseJsonFile == null)
//				{
//					Debug.LogWarning("PreviewSettings instance or databaseJsonFile is null.");
//					return null;
//				}

//				// Use persistentDataPath with storagePath
//				string targetFolder = Path.Combine(Application.persistentDataPath, instance.storagePath);
//				string fileName = instance.databaseJsonFile.name + ".json";
//				string targetPath = Path.Combine(targetFolder, fileName);

//				// Ensure the target directory exists
//				if (!Directory.Exists(targetFolder))
//				{
//					Directory.CreateDirectory(targetFolder);
//					Debug.Log($"Created directory: {targetFolder}");
//				}

//				// Check if the file already exists in the target path
//				if (!File.Exists(targetPath))
//				{
//					// Copy the original TextAsset content to the target path
//					File.WriteAllText(targetPath, instance.databaseJsonFile.text);
//					Debug.Log($"Copied database JSON to: {targetPath}");
//				}

//				// Load the file as a TextAsset
//				string fileContent = File.ReadAllText(targetPath);
//				TextAsset textAsset = new TextAsset(fileContent);
//				textAsset.name = instance.databaseJsonFile.name; // Preserve the original name
//				return textAsset;
//			}
//		}

//		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
//		public static string GeometryPath => instance.geometryPath;

//		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
//		public static string TexturePath => instance.texturePath;

//		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
//		public static string SkycubesPath => instance.skycubesPath;

//		[SerializeField, FolderPath] private string storagePath = "ClassicTS/Data/";
//		public static string StoragePath => instance.storagePath;

//		[Header("Game Mode")]
//		[SerializeField] private PreviewMode previewMode = PreviewMode.Player;

//		public static PreviewMode CurrentMode
//		{
//			get => instance != null ? instance.previewMode : PreviewMode.Player;
//			set
//			{
//				if (instance != null)
//				{
//					instance.previewMode = value;
//				}
//			}
//		}

//		private static PreviewSettings instance;
//		void Awake() => instance = this;
//	}
//}

//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//#if UNITY_EDITOR
//		[CustomEditor(typeof(PreviewSettings))]
//		public class PreviewSettingsEditor : Editor
//		{
//			public override void OnInspectorGUI()
//			{
//				serializedObject.Update();

//				// Draw all properties except the previewMode
//				SerializedProperty prop = serializedObject.GetIterator();
//				bool enterChildren = true;
//				while (prop.NextVisible(enterChildren))
//				{
//					if (prop.name == "previewMode")
//					{
//						continue; // Skip the default enum field
//					}

//					EditorGUILayout.PropertyField(prop, true);
//				}

//				// Custom radio buttons for previewMode
//				EditorGUILayout.Space();
//				EditorGUILayout.LabelField("Game Mode", EditorStyles.boldLabel);
//				SerializedProperty modeProp = serializedObject.FindProperty("previewMode");
//				int selectedIndex = (int)modeProp.enumValueIndex;
//				string[] modeNames = { "Player Mode", "Cinema Mode", "Editor Mode" };
//				selectedIndex = GUILayout.SelectionGrid(selectedIndex, modeNames, 3, EditorStyles.radioButton);
//				modeProp.enumValueIndex = (int)selectedIndex;

//				serializedObject.ApplyModifiedProperties();
//			}
//		}
//#endif
