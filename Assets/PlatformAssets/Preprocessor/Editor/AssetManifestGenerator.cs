#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.Collections.Generic;
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

		// 🔴 CRITICAL: load ApplicationSettings so AssetPath values are valid
		ApplicationSettings.Editor_ForceLoadInstance();

		// 🔴 Register all roots
		AssetConfiguration.Initialize();

		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
		{
			var roots = getRoots().ToArray();

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

		if (string.Equals(manifestName, "Models", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Prefabs", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Textures", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Music", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(manifestName, "Skycubes", StringComparison.OrdinalIgnoreCase) ||
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
}
#endif

