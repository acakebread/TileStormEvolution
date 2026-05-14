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

		WriteModelResourceTable(ProjectAssets.GetModelNames(forceRefresh: true));

		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
		{
			var roots = getRoots().ToArray();

			var names = GetAssetNames(assetType, roots);

			WriteManifest(manifestName, names);

			Debug.Log($"Generated {manifestName}: {names.Count} assets");
		}

		AssetDatabase.Refresh();
		Debug.Log("<color=cyan>Asset Manifests generated successfully.</color>");
	}

	private static void WriteModelResourceTable(IEnumerable<string> names)
	{
		const string tableFolder = "Assets/Resources/" + AssetManifestConfig.ManifestRootFolder;
		if (!Directory.Exists(tableFolder))
			Directory.CreateDirectory(tableFolder);

		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/ModelResourceTable.txt";
		var lines = names
			.Where(n => !string.IsNullOrWhiteSpace(n))
			.Select(n => n.Trim())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
			.Select(n => $"{HTB50.EncodeFixed(RadixHash.GetStableHash32(n), 6)}\t{n}")
			.ToList();

		lines.Insert(0, "# hashId<TAB>resourceName");
		File.WriteAllLines(path, lines);
	}

	private static void WriteManifest(string manifestName, IEnumerable<string> names)
	{
		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";

		File.WriteAllLines(path, names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
	}

	private static List<string> GetAssetNames(Type assetType, string[] roots)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		var normalizedRoots = (roots ?? Array.Empty<string>())
			.Where(r => !string.IsNullOrWhiteSpace(r))
			.Select(r => r.Trim('/'))
			.ToArray();

		string[] guids = AssetDatabase.FindAssets($"t:{assetType.Name}");

		foreach (string guid in guids)
		{
			string fullPath = AssetDatabase.GUIDToAssetPath(guid);

			// Must be inside Resources
			int resIndex = fullPath.LastIndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
			if (resIndex < 0)
				continue;

			string resourcePath = fullPath.Substring(resIndex + "/Resources/".Length);
			resourcePath = Path.ChangeExtension(resourcePath, null);

			// 🔴 STRICT root filtering (fixes your bug)
			if (normalizedRoots.Length > 0)
			{
				bool matches = false;

				foreach (var root in normalizedRoots)
				{
					if (resourcePath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
						resourcePath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
					{
						matches = true;
						break;
					}
				}

				if (!matches)
					continue;
			}

			// 🔴 KEEP filename-only (matches your runtime loader)
			string name = Path.GetFileName(resourcePath);

			if (!string.IsNullOrEmpty(name))
				result.Add(name);
		}

		return result.ToList();
	}
}
#endif

