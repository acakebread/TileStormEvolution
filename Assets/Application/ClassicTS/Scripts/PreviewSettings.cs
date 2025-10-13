using UnityEngine;

namespace ClassicTilestorm
{
	public class PreviewSettings : MonoBehaviour
	{
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
		public static TextAsset DatabaseJsonFile => instance.databaseJsonFile;

		[SerializeField, ResourcePath] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => instance.geometryPath;

		[SerializeField, ResourcePath] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => instance.texturePath;

		[SerializeField, ResourcePath] private string skycubesPath = "ClassicTS/SkyCubes/";
		public static string SkycubesPath => instance.skycubesPath;

		[Header("player mode")]
		[SerializeField] private bool playerMode = false;
		public static bool PlayerMode { set => instance.playerMode = value; get => instance.playerMode; }

		[Header("cinema mode")]
		[SerializeField] private bool cinemaMode = false;
		public static bool CinemaMode { set => instance.cinemaMode = value; get => instance.cinemaMode; }

		[Header("editor mode")]
		[SerializeField] private bool editorMode = false;
		public static bool EditorMode { set => instance.editorMode = value; get => instance.editorMode; }

		private static PreviewSettings instance;
		void Awake() => instance = this;
	}
}
