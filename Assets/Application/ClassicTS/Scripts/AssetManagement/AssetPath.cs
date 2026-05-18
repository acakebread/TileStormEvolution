namespace ClassicTilestorm
{
	public static class AssetPath
	{
		public const string MaterialsFolder = "Materials";

		private static string geometryPath = null;//"ClassicTS/Geometry/"
		public static string GeometryPath { get => geometryPath ?? ApplicationSettings.GeometryPath; set => geometryPath = value; }

		public static string GeometryMaterialsPath
		{
			get
			{
				var root = GeometryPath?.Trim('/')?.Trim();
				if (string.IsNullOrEmpty(root))
					return MaterialsFolder;

				return $"{root}/{MaterialsFolder}";
			}
		}

		private static string texturePath = null;//"ClassicTS/Textures/"
		public static string TexturePath { get => texturePath ?? ApplicationSettings.TexturePath; set => texturePath = value; }

		private static string materialPath = null;//"ClassicTS/Materials/"
		public static string MaterialPath { get => materialPath ?? ApplicationSettings.MaterialPath; set => materialPath = value; }

		private static string prefabPath = null;//"ClassicTS/Prefabs/"
		public static string PrefabPath { get => prefabPath ?? ApplicationSettings.PrefabPath; set => prefabPath = value; }

		private static string skycubesPath = null;//"ClassicTS/SkyCubes/"
		public static string SkyCubesPath { get => skycubesPath ?? ApplicationSettings.SkyCubesPath; set => skycubesPath = value; }

		private static string soundPath = null;//"ClassicTS/Sounds/"
		public static string SoundPath { get => soundPath ?? ApplicationSettings.SoundPath; set => soundPath = value; }

		private static string musicPath = null;//"ClassicTS/Music/"
		public static string MusicPath { get => musicPath ?? ApplicationSettings.MusicPath; set => musicPath = value; }
	}
}
