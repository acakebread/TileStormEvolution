#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using ClassicTilestorm;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;

public class AssetManifestGenerator : IPreprocessBuildWithReport
{
	private static bool _isGeneratingManifests;

	public int callbackOrder => 0;

	public static bool IsGeneratingManifests => _isGeneratingManifests;

	public void OnPreprocessBuild(BuildReport report)
	{
		if (report.summary.platform == BuildTarget.WebGL)
			GenerateAllManifests();
	}

	[MenuItem("Tools/Generate Asset Manifests %&M")]
	public static void GenerateAllManifests()
	{
		if (_isGeneratingManifests)
			return;

		_isGeneratingManifests = true;

		try
		{
			string manifestFolder = Path.Combine(ApplicationSettings.InternalResourcesProjectPath, AssetManifestConfig.ManifestRootFolder);

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
			AssetConfiguration.ClearAllCaches();
			MapCatalog.ClearCache();
			Debug.Log("<color=cyan>Asset Manifests generated successfully.</color>");
		}
		finally
		{
			_isGeneratingManifests = false;
		}
	}

	[MenuItem("Tools/Admin/Migrate Resource Hashes To Folder Seeds")]
	public static void MigrateResourceHashesToFolderSeeds()
	{
		if (!IsResourceHashMigrationUnlocked())
		{
			Debug.LogWarning($"Resource hash seed migration is admin locked. Create '{ResourceHashMigrationAuthorisationFile}' containing '{ResourceHashMigrationActivationWord}' if this migration is genuinely required.");
			return;
		}

		ApplicationSettings.Editor_ForceLoadInstance();
		AssetConfiguration.Initialize();

		var remaps = BuildLegacyHashRemaps();
		int changedFiles = ApplyHashRemapsToJsonFiles(remaps);

		GenerateAllManifests();
		ProjectAssets.RefreshAllNameCaches();
		MapCatalog.ClearCache();

		Debug.Log($"<color=cyan>Resource hash seed migration complete.</color> Remaps: {CountRemaps(remaps)}, json files changed: {changedFiles}.");
	}

	[MenuItem("Tools/Admin/Migrate Resource Hashes To Folder Seeds", true)]
	private static bool ValidateMigrateResourceHashesToFolderSeeds()
	{
		return IsResourceHashMigrationUnlocked();
	}

	public static bool IsResourceHashMigrationUnlocked()
	{
		string keyPath = GetResourceHashMigrationKeyPath();
		if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
			return false;

		try
		{
			return string.Equals(
				File.ReadAllText(keyPath).Trim(),
				ResourceHashMigrationActivationWord,
				StringComparison.Ordinal);
		}
		catch
		{
			return false;
		}
	}

	private const string ResourceHashMigrationAuthorisationFile = "Assets/Private/ResourceMigrationAuthorisation.txt";
	private const string ResourceHashMigrationActivationWord = "MigrationToolActivation";

	private static string GetResourceHashMigrationKeyPath()
	{
		var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
		return string.IsNullOrWhiteSpace(projectRoot)
			? null
			: Path.Combine(projectRoot, ResourceHashMigrationAuthorisationFile);
	}

	private static int WriteHashedManifest(string manifestName, IEnumerable<ResourceManifestEntry> entries)
	{
		string path = Path.Combine(ApplicationSettings.InternalResourcesProjectPath, AssetManifestConfig.ManifestRootFolder, $"{manifestName}.txt");
		var sorted = ValidateAndSortEntries(manifestName, entries);
		var lines = sorted.Select(e => $"{e.HashId}\t{e.ResourceKey}").ToList();

		lines.Insert(0, "# hashId<TAB>resourceKey");
		File.WriteAllLines(path, lines);
		return sorted.Count;
	}

	private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildLegacyHashRemaps()
	{
		var remaps = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
		{
			var entries = ValidateAndSortEntries(
				manifestName,
				GetResourceEntries(manifestName, assetType, getRoots().ToArray()));

			var candidates = entries.Where(e =>
				!e.HasForcedHash &&
				!string.IsNullOrWhiteSpace(e.LegacyHashId) &&
				!string.Equals(e.LegacyHashId, e.HashId, StringComparison.OrdinalIgnoreCase));

			var manifestRemaps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var group in candidates.GroupBy(e => e.LegacyHashId, StringComparer.OrdinalIgnoreCase))
			{
				var newHashes = group
					.Select(e => e.HashId)
					.Where(h => !string.IsNullOrWhiteSpace(h))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToArray();

				if (newHashes.Length == 1)
				{
					manifestRemaps[group.Key] = newHashes[0];
					continue;
				}

				Debug.LogWarning(
					$"Resource hash migration skipped ambiguous {manifestName} legacy hash '{group.Key}'. " +
					$"Candidates: {string.Join(", ", group.Select(e => $"{e.ResourceKey}->{e.HashId}"))}");
			}

			remaps[manifestName] = manifestRemaps;
		}

		return remaps;
	}

	private static int ApplyHashRemapsToJsonFiles(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> remaps)
	{
		if (remaps == null || CountRemaps(remaps) == 0)
			return 0;

		int changedFiles = 0;
		foreach (var file in EnumerateJsonFiles())
		{
			if (!File.Exists(file))
				continue;

			string text = File.ReadAllText(file);
			if (string.IsNullOrWhiteSpace(text))
				continue;

			string updated = ReplaceTypedHashFields(text, remaps);
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

	private static string ReplaceTypedHashFields(string input, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> remaps)
	{
		if (string.IsNullOrEmpty(input))
			return input;

		return Regex.Replace(
			input,
			"\"(?<field>model|material|music|skybox|sound|texture|prefab)\"\\s*:\\s*\"(?<hash>[^\"]+)\"",
			match =>
			{
				string field = match.Groups["field"].Value;
				string hash = match.Groups["hash"].Value;
				string manifestName = GetManifestNameForJsonField(field);
				if (string.IsNullOrWhiteSpace(manifestName) ||
					!remaps.TryGetValue(manifestName, out var manifestRemaps) ||
					!manifestRemaps.TryGetValue(hash, out var replacement) ||
					string.IsNullOrWhiteSpace(replacement))
				{
					return match.Value;
				}

				return match.Value.Replace($"\"{hash}\"", $"\"{replacement}\"");
			},
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	}

	private static string GetManifestNameForJsonField(string field)
	{
		if (string.IsNullOrWhiteSpace(field))
			return null;

		switch (field.Trim().ToLowerInvariant())
		{
			case "model":
				return "Models";
			case "material":
				return "Materials";
			case "music":
				return "Music";
			case "skybox":
				return "SkyCubes";
			case "sound":
				return "Sounds";
			case "texture":
				return "Textures";
			case "prefab":
				return "Prefabs";
			default:
				return null;
		}
	}

	private static int CountRemaps(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> remaps)
		=> remaps?.Values.Sum(map => map?.Count ?? 0) ?? 0;

	private static void WriteMapManifest()
	{
		const string manifestName = "Maps";
		string path = Path.Combine(ApplicationSettings.InternalResourcesProjectPath, AssetManifestConfig.ManifestRootFolder, $"{manifestName}.txt");
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
