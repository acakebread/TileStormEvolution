#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ClassicTilestorm;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;

public class AssetManifestGenerator : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		if (report.summary.platform == BuildTarget.WebGL)
			GenerateAllManifests();
	}

	[MenuItem("Tools/Generate Asset Manifests %&M")]
	public static void GenerateAllManifests()
	{
		const string manifestFolder = "Assets/Resources/" + AssetManifestConfig.ManifestRootFolder;

		if (!Directory.Exists(manifestFolder))
			Directory.CreateDirectory(manifestFolder);

		ApplicationSettings.Editor_ForceLoadInstance();
		AssetConfiguration.Initialize();

		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
		{
			var roots = getRoots().ToArray();
			var entries = GetResourceEntries(manifestName, assetType, roots).ToList();
			int written = WriteHashedManifest(manifestName, entries);
			Debug.Log($"Generated {manifestName}: {written} assets");
		}

		WriteMapManifest();

		AssetDatabase.Refresh();
		Debug.Log("<color=cyan>Asset Manifests generated successfully.</color>");
	}

	[MenuItem("Tools/Migrate Resource Hashes To Folder Seeds")]
	public static void MigrateResourceHashesToFolderSeeds()
	{
		ApplicationSettings.Editor_ForceLoadInstance();
		AssetConfiguration.Initialize();

		var remaps = BuildLegacyHashRemap();
		int changedFiles = ApplyHashRemapToJsonFiles(remaps);

		GenerateAllManifests();
		ProjectAssets.RefreshAllNameCaches();
		MapCatalog.ClearCache();

		Debug.Log($"<color=cyan>Resource hash seed migration complete.</color> Remaps: {remaps.Count}, json files changed: {changedFiles}.");
	}

	private static int WriteHashedManifest(string manifestName, IEnumerable<ResourceManifestEntry> entries)
	{
		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
		var sorted = ValidateAndSortEntries(manifestName, entries);
		var lines = sorted.Select(e => $"{e.HashId}\t{e.ResourceKey}").ToList();

		lines.Insert(0, "# hashId<TAB>resourceKey");
		File.WriteAllLines(path, lines);
		return sorted.Count;
	}

	private static IReadOnlyDictionary<string, string> BuildLegacyHashRemap()
	{
		var candidates = new List<ResourceManifestEntry>();

		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
		{
			var entries = ValidateAndSortEntries(
				manifestName,
				GetResourceEntries(manifestName, assetType, getRoots().ToArray()));

			candidates.AddRange(entries.Where(e =>
				!e.HasForcedHash &&
				!string.IsNullOrWhiteSpace(e.LegacyHashId) &&
				!string.Equals(e.LegacyHashId, e.HashId, StringComparison.OrdinalIgnoreCase)));
		}

		var remaps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var group in candidates.GroupBy(e => e.LegacyHashId, StringComparer.OrdinalIgnoreCase))
		{
			var newHashes = group
				.Select(e => e.HashId)
				.Where(h => !string.IsNullOrWhiteSpace(h))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			if (newHashes.Length == 1)
			{
				remaps[group.Key] = newHashes[0];
				continue;
			}

			Debug.LogWarning(
				$"Resource hash migration skipped ambiguous legacy hash '{group.Key}'. " +
				$"Candidates: {string.Join(", ", group.Select(e => $"{e.ResourceKey}->{e.HashId}"))}");
		}

		return remaps;
	}

	private static int ApplyHashRemapToJsonFiles(IReadOnlyDictionary<string, string> remaps)
	{
		if (remaps == null || remaps.Count == 0)
			return 0;

		int changedFiles = 0;
		foreach (var file in EnumerateJsonFiles())
		{
			if (!File.Exists(file))
				continue;

			string text = File.ReadAllText(file);
			if (string.IsNullOrWhiteSpace(text))
				continue;

			string updated = ReplaceExactHashTokens(text, remaps);
			if (string.Equals(text, updated, StringComparison.Ordinal))
				continue;

			File.WriteAllText(file, updated);
			changedFiles++;
		}

		return changedFiles;
	}

	private static IEnumerable<string> EnumerateJsonFiles()
	{
		foreach (var root in new[] { Application.dataPath, Application.persistentDataPath })
		{
			if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
				continue;

			foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
				yield return file;
		}
	}

	private static string ReplaceExactHashTokens(string input, IReadOnlyDictionary<string, string> remaps)
	{
		string output = input;
		var replacements = remaps
			.Where(pair => !string.IsNullOrWhiteSpace(pair.Key) &&
				!string.IsNullOrWhiteSpace(pair.Value) &&
				!string.Equals(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase))
			.Select((pair, index) => (OldHash: pair.Key, NewHash: pair.Value, Placeholder: $"__TS_HASH_REMAP_{index}__"))
			.ToArray();

		foreach (var replacement in replacements)
			output = output.Replace($"\"{replacement.OldHash}\"", $"\"{replacement.Placeholder}\"");

		foreach (var replacement in replacements)
			output = output.Replace($"\"{replacement.Placeholder}\"", $"\"{replacement.NewHash}\"");

		return output;
	}

	private static void WriteMapManifest()
	{
		const string manifestName = "Maps";
		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
		var mapRoot = $"{ApplicationSettings.JsonDataPath}/Maps";
		var names = ResourceUtils.GetAssetNamesFromResources<TextAsset>(new[] { mapRoot }, manifestName)
			.Where(n => !string.IsNullOrWhiteSpace(n))
			.Select(n => n.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
			.ToList();

		var lines = names.Select(n =>
		{
			if (!MapCatalog.TryGetMapHashFromFileStem(n, out var hash))
				return null;
			return $"{HTB50Settings.ToString(hash)}\t{n}";
		}).ToList();

		lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
		lines.Insert(0, "# hashId<TAB>resourceName");
		File.WriteAllLines(path, lines);
		Debug.Log($"Generated {manifestName}: {names.Count} assets");
	}

	private static IEnumerable<ResourceManifestEntry> GetResourceEntries(string manifestName, Type assetType, string[] roots)
	{
		var normalizedRoots = (roots ?? Array.Empty<string>())
			.Where(r => !string.IsNullOrWhiteSpace(r))
			.Select(r => r.Trim('/'))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var extensions = GetManifestExtensions(manifestName, assetType);
		bool isModelManifest = string.Equals(manifestName, "Models", StringComparison.OrdinalIgnoreCase);

		foreach (var resFolder in ResourceUtils.GetAllResourcesFolders(skipDotFolders: true))
		{
			foreach (var root in normalizedRoots)
			{
				string fullPath = Path.Combine(resFolder, root).Replace('\\', '/');
				if (!Directory.Exists(fullPath))
					continue;

				foreach (var file in Directory.EnumerateFiles(fullPath, "*.*", SearchOption.AllDirectories))
				{
					var ext = Path.GetExtension(file);
					if (!extensions.Contains(ext, StringComparer.OrdinalIgnoreCase) || IsManifestSidecarFile(file))
						continue;

					string displayName = Path.GetFileNameWithoutExtension(file);
					string resourceKey = ToResourceKey(resFolder, file);
					string folderHash = null;
					bool forcedHash = isModelManifest && TryGetFolderBasedModelHash(file, out folderHash);
					string hashId = forcedHash
						? folderHash
						: GetSeededHashId(file);

					yield return new ResourceManifestEntry(
						hashId,
						resourceKey,
						displayName,
						file.Replace('\\', '/'),
						GetLegacyFilenameHash(file),
						forcedHash);
				}
			}
		}
	}

	private static List<ResourceManifestEntry> ValidateAndSortEntries(string manifestName, IEnumerable<ResourceManifestEntry> entries)
	{
		var candidates = (entries ?? Enumerable.Empty<ResourceManifestEntry>())
			.Where(e => !string.IsNullOrWhiteSpace(e.HashId) && !string.IsNullOrWhiteSpace(e.ResourceKey))
			.ToList();

		var uniqueByResourceKey = new List<ResourceManifestEntry>();
		foreach (var group in candidates.GroupBy(e => e.ResourceKey, StringComparer.OrdinalIgnoreCase))
		{
			var sourcePaths = group
				.Select(e => e.SourcePath)
				.Where(p => !string.IsNullOrWhiteSpace(p))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();

			if (sourcePaths.Length > 1)
			{
				throw new InvalidOperationException(
					$"{manifestName} contains duplicate Resources key '{group.Key}'. " +
					$"Unity cannot disambiguate these assets: {string.Join(", ", sourcePaths)}");
			}

			uniqueByResourceKey.Add(group.First());
		}

		var duplicateHashGroups = uniqueByResourceKey
			.GroupBy(e => e.HashId, StringComparer.OrdinalIgnoreCase)
			.Where(g => g.Select(e => e.ResourceKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
			.ToArray();

		if (duplicateHashGroups.Length > 0)
		{
			string details = string.Join("; ", duplicateHashGroups.Select(g =>
				$"{g.Key}: {string.Join(", ", g.Select(e => e.ResourceKey).Distinct(StringComparer.OrdinalIgnoreCase))}"));
			throw new InvalidOperationException($"{manifestName} contains hash collision(s): {details}");
		}

		return uniqueByResourceKey
			.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
			.ThenBy(e => e.ResourceKey, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	private static string[] GetManifestExtensions(string manifestName, Type assetType)
	{
		if (string.Equals(manifestName, "Models", StringComparison.OrdinalIgnoreCase))
			return new[] { ".obj", ".fbx" };
		if (string.Equals(manifestName, "Prefabs", StringComparison.OrdinalIgnoreCase))
			return new[] { ".prefab" };
		if (string.Equals(manifestName, "Textures", StringComparison.OrdinalIgnoreCase))
			return new[] { ".png", ".jpg", ".jpeg", ".tga" };
		if (string.Equals(manifestName, "Materials", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "SkyCubes", StringComparison.OrdinalIgnoreCase))
			return new[] { ".mat" };
		if (string.Equals(manifestName, "Sounds", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Music", StringComparison.OrdinalIgnoreCase))
			return new[] { ".wav", ".mp3", ".ogg" };

		Debug.LogWarning($"Unsupported asset type for manifest: {assetType} / {manifestName}");
		return Array.Empty<string>();
	}

	private static string GetSeededHashId(string filePath)
	{
		if (!ResourceFolderIdentity.TryComputeHashForAssetPath(filePath, createSeedIfMissing: true, out var hashId))
			throw new InvalidOperationException($"Unable to compute seeded resource hash for '{filePath}'.");

		return hashId;
	}

	private static string GetLegacyFilenameHash(string filePath)
	{
		string displayName = Path.GetFileNameWithoutExtension(filePath);
		return string.IsNullOrWhiteSpace(displayName)
			? null
			: HTB50.EncodeFixed(RadixHash.GetStableHash32(displayName), 6);
	}

	private static bool IsManifestSidecarFile(string filePath)
		=> string.Equals(Path.GetFileName(filePath), ResourceFolderIdentity.SeedFileName, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(Path.GetExtension(filePath), ".meta", StringComparison.OrdinalIgnoreCase);

	private static string ToResourceKey(string resourcesFolder, string filePath)
	{
		string normalizedRoot = NormalizePath(resourcesFolder).TrimEnd('/');
		string normalizedFile = NormalizePath(filePath);

		if (!normalizedFile.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException($"'{filePath}' is not under Resources folder '{resourcesFolder}'.");

		string relative = normalizedFile.Substring(normalizedRoot.Length).TrimStart('/');
		string extension = Path.GetExtension(relative);
		if (!string.IsNullOrEmpty(extension))
			relative = relative.Substring(0, relative.Length - extension.Length);

		return relative.Trim('/');
	}

	private static string NormalizePath(string path)
		=> string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/').Trim();

	private static bool TryGetFolderBasedModelHash(string filePath, out string hashId)
	{
		hashId = null;
		if (string.IsNullOrWhiteSpace(filePath))
			return false;

		var normalized = filePath.Replace('\\', '/');
		var parts = normalized.Split('/');
		for (int i = 0; i < parts.Length; i++)
		{
			if (!string.Equals(parts[i], AssetPath.ImmutableRootFolder, StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(parts[i], "Community", StringComparison.OrdinalIgnoreCase))
				continue;

			for (int j = i + 1; j < parts.Length; j++)
			{
				if (ResourceIdUtil.TryParseCanonicalHash(parts[j], out _))
				{
					hashId = parts[j];
					return true;
				}
			}
		}

		return false;
	}

	private readonly struct ResourceManifestEntry
	{
		public string HashId { get; }
		public string ResourceKey { get; }
		public string DisplayName { get; }
		public string SourcePath { get; }
		public string LegacyHashId { get; }
		public bool HasForcedHash { get; }

		public ResourceManifestEntry(string hashId, string resourceKey, string displayName, string sourcePath, string legacyHashId, bool hasForcedHash)
		{
			HashId = hashId;
			ResourceKey = resourceKey;
			DisplayName = displayName;
			SourcePath = sourcePath;
			LegacyHashId = legacyHashId;
			HasForcedHash = hasForcedHash;
		}
	}
}
#endif
