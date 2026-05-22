#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicTilestorm;
using ClassicTilestorm.Assets;
using UnityEditor;
using UnityEngine;

public sealed class AssetManifestAutoRefresh : AssetPostprocessor
{
	private static bool _refreshQueued;

	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
	{
		if (AssetManifestGenerator.IsGeneratingManifests)
			return;

		if (!HasRelevantAssetChange(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths))
			return;

		QueueRefresh();
	}

	private static void QueueRefresh()
	{
		if (_refreshQueued)
			return;

		_refreshQueued = true;
		EditorApplication.delayCall += RefreshManifests;
	}

	private static void RefreshManifests()
	{
		if (!_refreshQueued)
			return;

		_refreshQueued = false;

		if (AssetManifestGenerator.IsGeneratingManifests)
			return;

		try
		{
			AssetManifestGenerator.GenerateAllManifests();
			Debug.Log("<color=cyan>Asset manifests refreshed after import.</color>");
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"AssetManifestAutoRefresh: failed to refresh manifests -> {ex.Message}");
		}
	}

	private static bool HasRelevantAssetChange(
		IEnumerable<string> importedAssets,
		IEnumerable<string> deletedAssets,
		IEnumerable<string> movedAssets,
		IEnumerable<string> movedFromAssetPaths)
	{
		return ContainsRelevantPath(importedAssets) ||
			ContainsRelevantPath(deletedAssets) ||
			ContainsRelevantPath(movedAssets) ||
			ContainsRelevantPath(movedFromAssetPaths);
	}

	private static bool ContainsRelevantPath(IEnumerable<string> paths)
		=> paths != null && paths.Any(IsManifestRelevantPath);

	private static bool IsManifestRelevantPath(string assetPath)
	{
		if (string.IsNullOrWhiteSpace(assetPath))
			return false;

		string normalized = assetPath.Replace('\\', '/').Trim();
		if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
			return false;

		if (normalized.IndexOf($"/{AssetManifestConfig.ManifestRootFolder}/", StringComparison.OrdinalIgnoreCase) >= 0)
			return false;

		if (normalized.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
			return false;

		if (normalized.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) < 0)
			return false;

		string extension = Path.GetExtension(normalized).ToLowerInvariant();
		return extension is ".fbx" or ".obj" or ".prefab" or ".png" or ".jpg" or ".jpeg" or ".tga" or ".mat" or ".wav" or ".mp3" or ".ogg";
	}
}
#endif
