using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public static class AssetPath
	{
		public const string DataRootFolder = "Data";
		public const string UserRootFolder = "User";
		public const string SystemRootFolder = "System";
		public const string ImmutableRootFolder = "Immutable";
		public const string GenericRootFolder = "Generic";
		public const string MapsFolder = "Maps";
		public const string DefinitionsFolder = "Definitions";
		public const string ModelsFolder = "Models";
		public const string GeometryFolder = "Geometry";
		public const string TextureFolder = "Textures";
		public const string MaterialFolder = "Materials";
		public const string PrefabFolder = "Prefabs";
		public const string SkyCubesFolder = "SkyCubes";
		public const string SoundFolder = "Sounds";
		public const string MusicFolder = "Music";
		public const string ConfigFolder = "Config";

		public static string NormalizePath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return null;

			return path.Trim().Trim('/');
		}

		public static string Combine(string root, string subfolder)
		{
			root = NormalizePath(root);
			subfolder = NormalizePath(subfolder);

			if (string.IsNullOrWhiteSpace(root))
				return subfolder ?? string.Empty;

			if (string.IsNullOrWhiteSpace(subfolder))
				return root;

			return $"{root}/{subfolder}";
		}

		public static IEnumerable<string> BuildPaths(IEnumerable<string> roots, string subfolder)
		{
			return (roots ?? Enumerable.Empty<string>())
				.Select(root => Combine(root, subfolder))
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Distinct(StringComparer.OrdinalIgnoreCase);
		}
	}
}
