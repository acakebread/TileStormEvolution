using UnityEngine;

namespace ClassicTilestorm
{
	public class PreviewSettings : MonoBehaviour
	{
		[Header("map to load")]
		[SerializeField] private string loadMapName = "Industrial 01";
		public static string LoadMapName { get => instance.loadMapName; set => instance.loadMapName = value; }// => instance.loadMapName;

		[Header("load map scrambled or solved")]
		[SerializeField] private bool scramble = true;
		public static bool Scramble => instance.scramble;

		[Header("show hidden tiles")]
		[SerializeField] private bool show_hidden_tiles = false;
		public static bool ShowHiddenTiles => instance.show_hidden_tiles;

		[Header("show tile selection")]
		[SerializeField] private bool show_tile_selection = false;
		public static bool ShowTileSelection => instance.show_tile_selection;

		[Header("resource paths")]
		[SerializeField] private TextAsset databaseJsonFile;
		public static TextAsset DatabaseJsonFile => instance.databaseJsonFile;

		[SerializeField] private string geometryPath = "ClassicTS/Geometry/";
		public static string GeometryPath => instance.geometryPath;

		[SerializeField] private string texturePath = "ClassicTS/Textures/";
		public static string TexturePath => instance.texturePath;

		//[SerializeField] private string prefabPath = "Prefabs/";
		//public static string PrefabPath => instance.prefabPath;

		//[SerializeField] private string databasePath = "ClassicTS/";
		//public static string DatabasePath => instance.databasePath;

		private static PreviewSettings instance;
		void Awake() => instance = this;
	}
}