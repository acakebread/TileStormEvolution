#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicTilestorm;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;
using UnityEditor;
using UnityEngine;

namespace ClassicTilestorm.Editor
{
	public sealed class TileStormAssetRenameFixup : AssetPostprocessor
	{
		private static bool _isRunning;

		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
		{
			if (_isRunning || movedAssets == null || movedFromAssetPaths == null || movedAssets.Length == 0)
				return;

			var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			for (var i = 0; i < Math.Min(movedAssets.Length, movedFromAssetPaths.Length); i++)
			{
				var oldPath = movedFromAssetPaths[i];
				var newPath = movedAssets[i];

				if (!IsTrackedResourceRename(oldPath, newPath))
					continue;

				var oldHash = GetFilenameHash(oldPath);
				var newHash = GetFilenameHash(newPath);
				if (string.IsNullOrWhiteSpace(oldHash) || string.IsNullOrWhiteSpace(newHash) || string.Equals(oldHash, newHash, StringComparison.OrdinalIgnoreCase))
					continue;

				renames[oldHash] = newHash;
			}

			if (renames.Count == 0)
				return;

			Debug.Log($"TileStormAssetRenameFixup: detected {renames.Count} tracked rename(s).");
			ApplyRenameFixup(renames);
		}

		private static void ApplyRenameFixup(IReadOnlyDictionary<string, string> renames)
		{
			if (renames == null || renames.Count == 0 || _isRunning)
				return;

			try
			{
				_isRunning = true;

				var jsonFiles = EnumerateJsonFiles().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
				var changedFiles = 0;

				foreach (var file in jsonFiles)
				{
					if (!File.Exists(file))
						continue;

					var text = File.ReadAllText(file);
					if (string.IsNullOrWhiteSpace(text))
						continue;

					var updated = ReplaceExactHashTokens(text, renames);
					if (string.Equals(updated, text, StringComparison.Ordinal))
						continue;

					File.WriteAllText(file, updated);
					changedFiles++;
				}

				AssetManifestGenerator.GenerateAllManifests();
				ProjectAssets.RefreshAllNameCaches();
				MapCatalog.ClearCache();

				Debug.Log($"TileStormAssetRenameFixup: updated {changedFiles} json file(s) for {renames.Count} renamed asset(s).");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"TileStormAssetRenameFixup: rename fix-up failed -> {ex.Message}");
			}
			finally
			{
				_isRunning = false;
			}
		}

		private static IEnumerable<string> EnumerateJsonFiles()
		{
			foreach (var root in EnumerateScanRoots())
			{
				if (!Directory.Exists(root))
					continue;

				foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
					yield return file;
			}
		}

		private static IEnumerable<string> EnumerateScanRoots()
		{
			yield return Application.dataPath;
			yield return Application.persistentDataPath;
		}

		private static string ReplaceExactHashTokens(string input, IReadOnlyDictionary<string, string> renames)
		{
			if (string.IsNullOrEmpty(input) || renames == null || renames.Count == 0)
				return input;

			string output = input;
			foreach (var pair in renames)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
					continue;

				output = output.Replace($"\"{pair.Key}\"", $"\"{pair.Value}\"");
			}

			return output;
		}

		private static bool IsTrackedResourceRename(string oldAssetPath, string newAssetPath)
		{
			if (string.IsNullOrWhiteSpace(oldAssetPath) || string.IsNullOrWhiteSpace(newAssetPath))
				return false;

			if (!IsInsideResourcesFolder(oldAssetPath) || !IsInsideResourcesFolder(newAssetPath))
				return false;

			return HasTrackedHashExtension(oldAssetPath) || HasTrackedHashExtension(newAssetPath);
		}

		private static bool IsInsideResourcesFolder(string assetPath)
		{
			var normalized = NormalizePath(assetPath);
			return !string.IsNullOrWhiteSpace(normalized) &&
			       normalized.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool HasTrackedHashExtension(string assetPath)
		{
			var ext = Path.GetExtension(assetPath)?.ToLowerInvariant();
			return ext is ".obj" or ".fbx" or ".prefab" or ".png" or ".jpg" or ".jpeg" or ".tga" or ".mat" or ".wav" or ".mp3" or ".ogg";
		}

		private static string GetFilenameHash(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
				return null;

			var stem = Path.GetFileNameWithoutExtension(assetPath);
			if (string.IsNullOrWhiteSpace(stem))
				return null;

			return HTB50.EncodeFixed(RadixHash.GetStableHash32(stem), 6);
		}

		private static string NormalizePath(string path)
			=> string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/').Trim();
	}
}
#endif
