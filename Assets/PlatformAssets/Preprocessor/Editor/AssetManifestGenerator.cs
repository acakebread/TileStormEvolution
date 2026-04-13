#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;
using System.Collections.Generic;
using System;

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

		// Use the safe editor initializer instead of calling directly
		AssetConfiguration.Initialize();
		//AssetConfigurationEditorInitializer.EnsureInitialized();

		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
		{
			var roots = getRoots().ToArray();
			var names = ResourceUtils.GetAssetNamesFromResourcesForEditor(assetType, roots);
			WriteManifest(manifestName, names);
		}

		AssetDatabase.Refresh();
		Debug.Log("<color=cyan>Asset Manifests generated successfully for WebGL!</color>");
	}

	private static void WriteManifest(string manifestName, IEnumerable<string> names)
	{
		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
		File.WriteAllLines(path, names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
	}
}
#endif