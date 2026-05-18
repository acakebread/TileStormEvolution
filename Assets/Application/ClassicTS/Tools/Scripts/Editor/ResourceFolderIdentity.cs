#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using MassiveHadronLtd;

namespace ClassicTilestorm.Assets
{
	internal static class ResourceFolderIdentity
	{
		public const string SeedFileName = ".massive-hadron-resource-folder";

		public static bool TryComputeHashForAssetPath(string assetPath, bool createSeedIfMissing, out string hashId)
		{
			hashId = null;
			if (string.IsNullOrWhiteSpace(assetPath))
				return false;

			string fullPath = Path.IsPathRooted(assetPath)
				? assetPath
				: Path.GetFullPath(assetPath);

			string folder = Path.GetDirectoryName(fullPath);
			string fileName = Path.GetFileName(fullPath);
			if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(fileName))
				return false;

			string seed = createSeedIfMissing
				? GetOrCreateSeed(folder)
				: TryReadSeed(folder);

			if (string.IsNullOrWhiteSpace(seed))
				return false;

			hashId = ComputeHashId(seed, fileName);
			return !string.IsNullOrWhiteSpace(hashId);
		}

		public static string ComputeHashId(string seed, string fileName)
		{
			if (string.IsNullOrWhiteSpace(seed) || string.IsNullOrWhiteSpace(fileName))
				return null;

			string input = $"{NormalizeSeed(seed)}/{NormalizeFileName(fileName)}";
			return HTB50.EncodeFixed(RadixHash.GetStableHash32(input), 6);
		}

		private static string GetOrCreateSeed(string folder)
		{
			string existing = TryReadSeed(folder);
			if (!string.IsNullOrWhiteSpace(existing))
				return existing;

			Directory.CreateDirectory(folder);
			string seed = Guid.NewGuid().ToString("N");
			File.WriteAllText(GetSeedPath(folder), seed + Environment.NewLine, Encoding.UTF8);
			return seed;
		}

		private static string TryReadSeed(string folder)
		{
			if (string.IsNullOrWhiteSpace(folder))
				return null;

			string path = GetSeedPath(folder);
			if (!File.Exists(path))
				return null;

			foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
			{
				string line = rawLine?.Trim();
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
					continue;

				return NormalizeSeed(line);
			}

			return null;
		}

		private static string GetSeedPath(string folder)
			=> Path.Combine(folder, SeedFileName);

		private static string NormalizeSeed(string seed)
			=> seed.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

		private static string NormalizeFileName(string fileName)
			=> Path.GetFileName(fileName).Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
	}
}
#endif
