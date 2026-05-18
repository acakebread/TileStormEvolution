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

			if (string.Equals(manifestName, "Models", StringComparison.OrdinalIgnoreCase))
			{
				var modelEntries = GetModelEntries(roots).ToList();
				WriteModelManifest(manifestName, modelEntries);
				Debug.Log($"Generated {manifestName}: {modelEntries.Count} assets");
				continue;
			}

			var names = GetAssetNames(assetType, roots);
			WriteManifest(manifestName, names);
			Debug.Log($"Generated {manifestName}: {names.Count} assets");
		}

		WriteMapManifest();

		AssetDatabase.Refresh();
		Debug.Log("<color=cyan>Asset Manifests generated successfully.</color>");
	}

	private static void WriteManifest(string manifestName, IEnumerable<string> names)
	{
		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
		var sorted = names
			.Where(n => !string.IsNullOrWhiteSpace(n))
			.Select(n => n.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (string.Equals(manifestName, "Prefabs", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Textures", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Music", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "SkyCubes", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Materials", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Sounds", StringComparison.OrdinalIgnoreCase))
		{
			var lines = sorted
				.Select(n => $"{HTB50.EncodeFixed(RadixHash.GetStableHash32(n), 6)}\t{n}")
				.ToList();
			lines.Insert(0, "# hashId<TAB>resourceName");
			File.WriteAllLines(path, lines);
			return;
		}

		File.WriteAllLines(path, sorted);
	}

	private static void WriteModelManifest(string manifestName, IEnumerable<ModelManifestEntry> entries)
	{
		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
		var lines = entries
			.Where(e => !string.IsNullOrWhiteSpace(e.DisplayName) && !string.IsNullOrWhiteSpace(e.HashId))
			.GroupBy(e => e.HashId, StringComparer.OrdinalIgnoreCase)
			.Select(g => g.First())
			.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
			.Select(e => $"{e.HashId}\t{e.DisplayName}")
			.ToList();

		lines.Insert(0, "# hashId<TAB>resourceName");
		File.WriteAllLines(path, lines);
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

	private static List<string> GetAssetNames(Type assetType, string[] roots)
	{
		var normalizedRoots = (roots ?? Array.Empty<string>())
			.Where(r => !string.IsNullOrWhiteSpace(r))
			.Select(r => r.Trim('/'))
			.ToArray();

		return ResourceUtils.GetAssetNamesFromResourcesForEditor(assetType, normalizedRoots).ToList();
	}

	private static IEnumerable<ModelManifestEntry> GetModelEntries(string[] roots)
	{
		var normalizedRoots = (roots ?? Array.Empty<string>())
			.Where(r => !string.IsNullOrWhiteSpace(r))
			.Select(r => r.Trim('/'))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

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
					if (!string.Equals(ext, ".obj", StringComparison.OrdinalIgnoreCase) &&
						!string.Equals(ext, ".fbx", StringComparison.OrdinalIgnoreCase))
						continue;

					string displayName = Path.GetFileNameWithoutExtension(file);
					string hashId = TryGetFolderBasedModelHash(file, out var folderHash)
						? folderHash
						: HTB50.EncodeFixed(RadixHash.GetStableHash32(displayName), 6);

					yield return new ModelManifestEntry(hashId, displayName);
				}
			}
		}
	}

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

	private readonly struct ModelManifestEntry
	{
		public string HashId { get; }
		public string DisplayName { get; }

		public ModelManifestEntry(string hashId, string displayName)
		{
			HashId = hashId;
			DisplayName = displayName;
		}
	}
}
#endif
