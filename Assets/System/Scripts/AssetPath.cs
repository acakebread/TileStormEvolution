using ClassicTilestorm;

namespace MassiveHadronLtd
{
	public static class AssetPath
	{
		private static string geometryPath = null;//"ClassicTS/Geometry/"
		public static string GeometryPath { get => geometryPath ?? PreviewSettings.GeometryPath; set => geometryPath = value; }

		private static string texturePath = null;//"ClassicTS/Textures/"
		public static string TexturePath { get => texturePath ?? PreviewSettings.TexturePath; set => texturePath = value; }

		private static string materialPath = null;//"ClassicTS/Materials/"
		public static string MaterialPath { get => materialPath ?? PreviewSettings.MaterialPath; set => materialPath = value; }

		private static string prefabPath = null;//"ClassicTS/Prefabs/"
		public static string PrefabPath { get => prefabPath ?? PreviewSettings.PrefabPath; set => prefabPath = value; }

		private static string skycubesPath = null;//"ClassicTS/SkyCubes/"
		public static string SkycubesPath { get => skycubesPath ?? PreviewSettings.SkycubesPath; set => skycubesPath = value; }
	}
}
