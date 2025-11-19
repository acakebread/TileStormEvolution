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
		//Direct,
		Editor,
		Player,
		Cinema
	}

	public class PreviewSettings : MonoBehaviour
	{
		// Public constant for the subfolder name in persistentDataPath (can be empty for root)
		public const string MutableDatabaseSubfolder = "Data";

		[Header("map to load")]
		[SerializeField] private string loadMapName = "Industrial 01";
		public static string LoadMapName 
		{ 
			get => PlayerPrefsX.GetString("LastLoadedMap", instance.loadMapName);//restore previous session map
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
				string fileContent = SerializerUtility.ReadText(targetPath);
				if (string.IsNullOrEmpty(fileContent))
				{
					// File doesn't exist; copy from source TextAsset
					fileContent = instance.databaseJsonFile.text;
					if (string.IsNullOrEmpty(fileContent))
					{
						Debug.LogError("PreviewSettings: Source TextAsset content is null or empty.");
						return null;
					}

					SerializerUtility.WriteText(targetPath, fileContent);
					// File is now written, re-read to confirm
					fileContent = SerializerUtility.ReadText(targetPath);
					if (string.IsNullOrEmpty(fileContent))
					{
						Debug.LogError("PreviewSettings: Failed to read text after writing to disk.");
						return null;
					}
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

				SerializerUtility.WriteText(targetPath, value.text);
				instance.databaseJsonFile = value;
				Debug.Log($"PreviewSettings: Updated DatabaseJsonFile and saved to {targetPath}");
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

		private static PreviewSettings instance;
		void Awake()
		{
			instance = this;
		}

		public static void ResetMutableDatabaseToDefault()
		{
			if (instance == null || instance.databaseJsonFile == null)
			{
				Debug.LogError("PreviewSettings.ResetMutableDatabaseToDefault(): instance or source databaseJsonFile is null.");
				return;
			}

			string targetPath = GetDatabaseFilePath();
			if (string.IsNullOrEmpty(targetPath))
			{
				Debug.LogError("PreviewSettings.ResetMutableDatabaseToDefault(): target path is null.");
				return;
			}

			string freshContent = instance.databaseJsonFile.text;
			if (string.IsNullOrEmpty(freshContent))
			{
				Debug.LogError("PreviewSettings.ResetMutableDatabaseToDefault(): source TextAsset content is empty.");
				return;
			}

			SerializerUtility.WriteText(targetPath, freshContent);
			Debug.Log($"PreviewSettings: Overwrote corrupted mutable database with pristine internal copy → {targetPath}");
		}

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
