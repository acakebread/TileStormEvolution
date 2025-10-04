using UnityEngine;

namespace ClassicTilestorm
{
	[CreateAssetMenu(fileName = "PreviewSettings", menuName = "ClassicTilestorm/Preview Settings", order = 1)]
	public class PreviewSettingsMenu : ScriptableObject
	{
		[Header("map to load")]
		[SerializeField] private string loadMapName = "Industrial 01";
		public static string LoadMapName { get => _current.loadMapName ?? "Industrial 01"; set => _current.loadMapName = value; }

		[Header("load map scrambled or solved")]
		[SerializeField] private bool scrambled = true;
		public static bool Scrambled => _current?.scrambled ?? true;

		[Header("enable or disable easy mode")]
		[SerializeField] private bool difficulty = false;
		public static bool Difficulty => _current?.difficulty ?? false;

		[Header("hidden tiles")]
		[SerializeField] private bool showHiddenTiles = false;
		public static bool ShowHiddenTiles => _current?.showHiddenTiles ?? false;

		[Header("tile selection")]
		[SerializeField] private bool showTileSelection = false;
		public static bool ShowTileSelection => _current?.showTileSelection ?? false;

		[Header("cinema mode")]
		[SerializeField] private bool launchInCinemaMode = false;
		public static bool LaunchInCinemaMode => _current?.launchInCinemaMode ?? false;

		[Header("resource paths")]
		[SerializeField] private TextAsset databaseJsonFile;
		public static TextAsset DatabaseJsonFile => _current?.databaseJsonFile;

		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => _current?.geometryPath ?? "ClassicTS/Geometry/";

		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => _current?.texturePath ?? "ClassicTS/Textures/";

		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
		public static string SkycubesPath => _current?.skycubesPath ?? "ClassicTS/SkyCubes/";

		[Header("debug mode")]
		[SerializeField] private bool debugMode = false;
		public static bool DebugMode { set => _current.debugMode = value; get => _current.debugMode; }

		private static PreviewSettingsMenu _current;

		// Public setter for the loader (called in Awake)
		public static void SetCurrent(PreviewSettingsMenu settings)
		{
			_current = settings;
#if UNITY_EDITOR
			if (settings != null) UnityEditor.EditorUtility.SetDirty(settings);
#endif
		}

		// Optional: Quick reset (use context menu on the asset)
		[ContextMenu("Reset to Defaults")]
		public void ResetToDefaults()
		{
			loadMapName = "Industrial 01";
			scrambled = true;
			difficulty = false;
			showHiddenTiles = false;
			showTileSelection = false;
			launchInCinemaMode = false;
			databaseJsonFile = null;
			geometryPath = "ClassicTS/Geometry/";
			texturePath = "ClassicTS/Textures/";
			skycubesPath = "ClassicTS/SkyCubes/";
			debugMode = false;
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}
	}
}