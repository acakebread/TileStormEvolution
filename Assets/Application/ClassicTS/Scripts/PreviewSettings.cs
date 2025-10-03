using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

namespace ClassicTilestorm
{
	public class PreviewSettings : MonoBehaviour
	{
		[Header("map to load")]
		[SerializeField] private string loadMapName = "Industrial 01";
		public static string LoadMapName { get => instance.loadMapName; set => instance.loadMapName = value; }// => instance.loadMapName;

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

		[Header("cinema mode")]
		[SerializeField] private bool launchInCinemaMode = false;
		public static bool LaunchInCinemaMode => instance.launchInCinemaMode;

		[Header("resource paths")]
		[SerializeField] private TextAsset databaseJsonFile;
		public static TextAsset DatabaseJsonFile => instance.databaseJsonFile;

		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => instance.geometryPath;

		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => instance.texturePath;

		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
		public static string SkycubesPath => instance.skycubesPath;

		//[SerializeField] private string geometryPath = "ClassicTS/Geometry/";
		//public static string GeometryPath => instance.geometryPath;

		//[SerializeField] private string texturePath = "ClassicTS/Textures/";
		//public static string TexturePath => instance.texturePath;


		//[SerializeField] private string prefabPath = "Prefabs/";
		//public static string PrefabPath => instance.prefabPath;

		//[SerializeField] private string databasePath = "ClassicTS/";
		//public static string DatabasePath => instance.databasePath;

		[Header("debug mode")]
		[SerializeField] private bool debugMode = false;
		public static bool DebugMode { set => instance.debugMode = value; get => instance.debugMode; }

		private static PreviewSettings instance;
		void Awake() => instance = this;
	}
}