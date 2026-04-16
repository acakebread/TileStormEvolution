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

		AssetDatabase.Refresh();
		Debug.Log("<color=cyan>Asset Manifests generated successfully.</color>");
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

//#if UNITY_EDITOR
//using System.IO;
//using System.Linq;
//using UnityEditor;
//using UnityEditor.Build;
//using UnityEditor.Build.Reporting;
//using UnityEngine;
//using System.Collections.Generic;
//using ClassicTilestorm.Assets;
//using ClassicTilestorm;
//using System;

//public class AssetManifestGenerator : IPreprocessBuildWithReport
//{
//	public int callbackOrder => 10;

//	public void OnPreprocessBuild(BuildReport report)
//	{
//		if (report.summary.platform == BuildTarget.WebGL)
//			GenerateAllManifests();
//	}

//	[MenuItem("Tools/Generate Asset Manifests %&M")]
//	public static void GenerateAllManifests()
//	{
//		const string manifestFolder = "Assets/Resources/" + AssetManifestConfig.ManifestRootFolder;

//		if (!Directory.Exists(manifestFolder))
//			Directory.CreateDirectory(manifestFolder);

//		ApplicationSettings.Editor_ForceLoadInstance();
//		AssetConfiguration.Initialize();

//		int totalAssets = 0;

//		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
//		{
//			var roots = getRoots().ToArray();

//			var names = GetAssetNamesFromAllResources(assetType, roots);

//			WriteManifest(manifestName, names);

//			totalAssets += names.Count;
//			Debug.Log($"Generated {manifestName}.txt → {names.Count} assets");
//		}

//		AssetDatabase.Refresh();
//		Debug.Log($"<color=cyan>All Asset Manifests generated successfully! Total assets: {totalAssets}</color>");
//	}

//	private static void WriteManifest(string manifestName, IEnumerable<string> names)
//	{
//		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
//		File.WriteAllLines(path, names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
//	}

//	/// <summary>
//	/// Scans only under the provided roots (if they exist), but finds any Resources folder inside them.
//	/// This mimics the old ResourceUtils behavior without hard-coded paths.
//	/// </summary>
//	private static List<string> GetAssetNamesFromAllResources(System.Type assetType, string[] roots)
//	{
//		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//		if (roots == null || roots.Length == 0)
//		{
//			// Rare fallback
//			return GetAllResourcesAssets(assetType);
//		}

//		foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
//		{
//			string cleanRoot = root.Trim('/');
//			string searchFolder = "Assets/" + cleanRoot;

//			if (!AssetDatabase.IsValidFolder(searchFolder))
//				continue;

//			// Find all assets of this type under this root (recursive)
//			string[] guids = AssetDatabase.FindAssets($"t:{assetType.Name}", new[] { searchFolder });

//			foreach (string guid in guids)
//			{
//				string fullPath = AssetDatabase.GUIDToAssetPath(guid);

//				// Extract the Resources-relative path (after the LAST Resources folder)
//				int idx = fullPath.LastIndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase);
//				if (idx >= 0)
//				{
//					string afterResources = fullPath.Substring(idx + "/Resources/".Length);
//					string name = Path.GetFileNameWithoutExtension(afterResources);

//					if (!string.IsNullOrEmpty(name))
//						result.Add(name);
//				}
//			}
//		}

//		return result.ToList();
//	}

//	// True full fallback (only used if no roots are valid)
//	private static List<string> GetAllResourcesAssets(System.Type assetType)
//	{
//		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
//		string[] guids = AssetDatabase.FindAssets($"t:{assetType.Name}");

//		foreach (string guid in guids)
//		{
//			string fullPath = AssetDatabase.GUIDToAssetPath(guid);
//			int idx = fullPath.LastIndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase);
//			if (idx >= 0)
//			{
//				string name = Path.GetFileNameWithoutExtension(fullPath.Substring(idx + "/Resources/".Length));
//				if (!string.IsNullOrEmpty(name))
//					result.Add(name);
//			}
//		}
//		return result.ToList();
//	}
//}
//#endif

//#if UNITY_EDITOR
//using System.IO;
//using System.Linq;
//using UnityEditor;
//using UnityEditor.Build;
//using UnityEditor.Build.Reporting;
//using UnityEngine;
//using System.Collections.Generic;
//using ClassicTilestorm.Assets;
//using ClassicTilestorm;
//using System;

//public class AssetManifestGenerator : IPreprocessBuildWithReport
//{
//	public int callbackOrder => 10;

//	public void OnPreprocessBuild(BuildReport report)
//	{
//		if (report.summary.platform == BuildTarget.WebGL)
//			GenerateAllManifests();
//	}

//	[MenuItem("Tools/Generate Asset Manifests %&M")]
//	public static void GenerateAllManifests()
//	{
//		const string manifestFolder = "Assets/Resources/" + AssetManifestConfig.ManifestRootFolder;

//		if (!Directory.Exists(manifestFolder))
//			Directory.CreateDirectory(manifestFolder);

//		ApplicationSettings.Editor_ForceLoadInstance();
//		AssetConfiguration.Initialize();

//		int totalAssets = 0;

//		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
//		{
//			var roots = getRoots().ToArray();

//			var names = GetAssetNamesFromAssetDatabase(assetType, roots);

//			WriteManifest(manifestName, names);

//			totalAssets += names.Count;
//			Debug.Log($"Generated {manifestName}.txt → {names.Count} assets");
//		}

//		AssetDatabase.Refresh();
//		Debug.Log($"<color=cyan>All Asset Manifests generated successfully! Total assets: {totalAssets}</color>");
//	}

//	private static void WriteManifest(string manifestName, IEnumerable<string> names)
//	{
//		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
//		File.WriteAllLines(path, names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
//	}

//	/// <summary>
//	/// Supports both:
//	/// - Assets/Application/ClassicTS/Resources/ClassicTS/XXX
//	/// - Assets/Application/Production/Resources/Levels (and Med)
//	/// </summary>
//	private static List<string> GetAssetNamesFromAssetDatabase(System.Type assetType, string[] roots)
//	{
//		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//		foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
//		{
//			string cleanRoot = root.Trim('/');

//			// Try the main ClassicTS Resources path first
//			string searchFolder1 = $"Assets/Application/ClassicTS/Resources/{cleanRoot}";
//			if (AssetDatabase.IsValidFolder(searchFolder1))
//			{
//				AddAssetsFromFolder(searchFolder1, assetType, result);
//				continue;
//			}

//			// Try the Production Levels path
//			string searchFolder2 = $"Assets/Application/Production/Resources/{cleanRoot}";
//			if (AssetDatabase.IsValidFolder(searchFolder2))
//			{
//				AddAssetsFromFolder(searchFolder2, assetType, result);
//				continue;
//			}

//			// Optional: you can add more paths here later if needed
//			Debug.LogWarning($"Folder not found for root '{root}' (tried both ClassicTS and Production Resources)");
//		}

//		return result.ToList();
//	}

//	private static void AddAssetsFromFolder(string folderPath, System.Type assetType, HashSet<string> result)
//	{
//		string filter = $"t:{assetType.Name}";
//		string[] guids = AssetDatabase.FindAssets(filter, new[] { folderPath });

//		foreach (string guid in guids)
//		{
//			string fullPath = AssetDatabase.GUIDToAssetPath(guid);

//			int idx = fullPath.LastIndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase);
//			if (idx >= 0)
//			{
//				string afterResources = fullPath.Substring(idx + "/Resources/".Length);
//				string name = Path.GetFileNameWithoutExtension(afterResources);

//				if (!string.IsNullOrEmpty(name))
//					result.Add(name);
//			}
//		}
//	}
//}
//#endif

//#if UNITY_EDITOR
//using System.IO;
//using System.Linq;
//using UnityEditor;
//using UnityEditor.Build;
//using UnityEditor.Build.Reporting;
//using UnityEngine;
//using System.Collections.Generic;
//using ClassicTilestorm.Assets;
//using ClassicTilestorm;
//using System;

//public class AssetManifestGenerator : IPreprocessBuildWithReport
//{
//	public int callbackOrder => 10;

//	public void OnPreprocessBuild(BuildReport report)
//	{
//		if (report.summary.platform == BuildTarget.WebGL)
//			GenerateAllManifests();
//	}

//	[MenuItem("Tools/Generate Asset Manifests %&M")]
//	public static void GenerateAllManifests()
//	{
//		const string manifestFolder = "Assets/Resources/" + AssetManifestConfig.ManifestRootFolder;

//		if (!Directory.Exists(manifestFolder))
//			Directory.CreateDirectory(manifestFolder);

//		ApplicationSettings.Editor_ForceLoadInstance();
//		AssetConfiguration.Initialize();

//		int totalAssets = 0;

//		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
//		{
//			var roots = getRoots().ToArray();

//			var names = GetAssetNamesFromAssetDatabase(assetType, roots);

//			WriteManifest(manifestName, names);

//			totalAssets += names.Count;
//			Debug.Log($"Generated {manifestName}.txt → {names.Count} assets");
//		}

//		AssetDatabase.Refresh();
//		Debug.Log($"<color=cyan>All Asset Manifests generated successfully! Total assets: {totalAssets}</color>");
//	}

//	private static void WriteManifest(string manifestName, IEnumerable<string> names)
//	{
//		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
//		File.WriteAllLines(path, names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
//	}

//	/// <summary>
//	/// FINAL VERSION - matches your exact folder structure:
//	/// Assets/Application/ClassicTS/Resources/ClassicTS/XXX
//	/// </summary>
//	private static List<string> GetAssetNamesFromAssetDatabase(System.Type assetType, string[] roots)
//	{
//		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

//		foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
//		{
//			string cleanRoot = root.Trim('/');                    // e.g. "ClassicTS/Geometry" or "Levels"
//			string searchFolder = $"Assets/Application/ClassicTS/Resources/{cleanRoot}";

//			if (!AssetDatabase.IsValidFolder(searchFolder))
//			{
//				Debug.LogWarning($"Folder not found: {searchFolder} (registered root: '{root}')");
//				continue;
//			}

//			string filter = $"t:{assetType.Name}";
//			string[] guids = AssetDatabase.FindAssets(filter, new[] { searchFolder });

//			foreach (string guid in guids)
//			{
//				string fullPath = AssetDatabase.GUIDToAssetPath(guid);

//				// Extract name after the last "Resources/" 
//				int idx = fullPath.LastIndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase);
//				if (idx >= 0)
//				{
//					string afterResources = fullPath.Substring(idx + "/Resources/".Length);
//					string name = Path.GetFileNameWithoutExtension(afterResources);

//					if (!string.IsNullOrEmpty(name))
//						result.Add(name);
//				}
//			}
//		}

//		return result.ToList();
//	}
//}
//#endif


//#if UNITY_EDITOR
//using System.IO;
//using System.Linq;
//using UnityEditor;
//using UnityEditor.Build;
//using UnityEditor.Build.Reporting;
//using UnityEngine;
//using MassiveHadronLtd;
//using ClassicTilestorm.Assets;
//using System.Collections.Generic;
//using System;

//public class AssetManifestGenerator : IPreprocessBuildWithReport
//{
//	public int callbackOrder => 0;

//	public void OnPreprocessBuild(BuildReport report)
//	{
//		if (report.summary.platform == BuildTarget.WebGL)
//			GenerateAllManifests();
//	}

//	[MenuItem("Tools/Generate Asset Manifests %&M")]
//	public static void GenerateAllManifests()
//	{
//		const string manifestFolder = "Assets/Resources/" + AssetManifestConfig.ManifestRootFolder;
//		if (!Directory.Exists(manifestFolder))
//			Directory.CreateDirectory(manifestFolder);

//		// Use the safe editor initializer instead of calling directly
//		AssetConfiguration.Initialize();
//		//AssetConfigurationEditorInitializer.EnsureInitialized();

//		foreach (var (manifestName, assetType, getRoots) in AssetManifestConfig.GetAllManifestDefinitions())
//		{
//			var roots = getRoots().ToArray();
//			var names = ResourceUtils.GetAssetNamesFromResourcesForEditor(assetType, roots);
//			WriteManifest(manifestName, names);
//		}

//		AssetDatabase.Refresh();
//		Debug.Log("<color=cyan>Asset Manifests generated successfully for WebGL!</color>");
//	}

//	private static void WriteManifest(string manifestName, IEnumerable<string> names)
//	{
//		string path = $"Assets/Resources/{AssetManifestConfig.ManifestRootFolder}/{manifestName}.txt";
//		File.WriteAllLines(path, names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
//	}
//}
//#endif