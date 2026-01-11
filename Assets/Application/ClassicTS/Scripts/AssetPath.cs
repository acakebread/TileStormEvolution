namespace ClassicTilestorm
{
	public static class AssetPath
	{
		private static string geometryPath = null;//"ClassicTS/Geometry/"
		public static string GeometryPath { get => geometryPath ?? ApplicationSettings.GeometryPath; set => geometryPath = value; }

		private static string texturePath = null;//"ClassicTS/Textures/"
		public static string TexturePath { get => texturePath ?? ApplicationSettings.TexturePath; set => texturePath = value; }

		private static string materialPath = null;//"ClassicTS/Materials/"
		public static string MaterialPath { get => materialPath ?? ApplicationSettings.MaterialPath; set => materialPath = value; }

		private static string prefabPath = null;//"ClassicTS/Prefabs/"
		public static string PrefabPath { get => prefabPath ?? ApplicationSettings.PrefabPath; set => prefabPath = value; }

		private static string skycubesPath = null;//"ClassicTS/SkyCubes/"
		public static string SkycubesPath { get => skycubesPath ?? ApplicationSettings.SkycubesPath; set => skycubesPath = value; }

		private static string soundPath = null;//"ClassicTS/Sounds/"
		public static string SoundPath { get => soundPath ?? ApplicationSettings.SoundPath; set => soundPath = value; }

		private static string musicPath = null;//"ClassicTS/Music/"
		public static string MusicPath { get => musicPath ?? ApplicationSettings.MusicPath; set => musicPath = value; }
	}
}
