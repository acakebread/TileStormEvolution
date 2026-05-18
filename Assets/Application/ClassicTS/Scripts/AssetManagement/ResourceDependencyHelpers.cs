using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	internal static class ResourceDependencyHelpers
	{
		internal static bool RequiresAtomicArchive(Map originalMap)
		{
			if (originalMap == null)
				return false;

			if (TryResolveImportedFileResource(originalMap.music, MusicResourceTable.TryResolveResourceKey, out _))
				return true;

			if (TryResolveImportedFileResource(originalMap.skybox, SkycubeResourceTable.TryResolveResourceKey, out _))
				return true;

			foreach (var definition in ResourceManager.GetUsedDefinitions(originalMap))
			{
				if (definition == null || DefinitionCatalog.IsInternalDefinition(definition.HashID))
					continue;

				if (!string.IsNullOrWhiteSpace(definition.model) && TryResolveExternalModelPath(definition.model, out _))
					return true;
			}

			return false;
		}

		internal static IEnumerable<string> GetAtomicArchiveModelRoots(Map originalMap)
		{
			if (originalMap == null)
				return Enumerable.Empty<string>();

			var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var definition in ResourceManager.GetUsedDefinitions(originalMap))
			{
				if (definition == null || DefinitionCatalog.IsInternalDefinition(definition.HashID))
					continue;

				if (string.IsNullOrWhiteSpace(definition.model) || !TryResolveExternalModelPath(definition.model, out var modelPath))
					continue;

				string modelRoot = Path.GetDirectoryName(modelPath);
				if (!string.IsNullOrWhiteSpace(modelRoot))
					roots.Add(Path.GetFullPath(modelRoot));
			}

			return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
		}

		internal static bool TryResolveImportedFileResource(string identifier, ResourceKeyResolver tryResolveResourceKey, out string resourcePath)
		{
			resourcePath = null;

			if (string.IsNullOrWhiteSpace(identifier) || tryResolveResourceKey == null)
				return false;

			if (!tryResolveResourceKey(identifier, out var resolved) || string.IsNullOrWhiteSpace(resolved))
				return false;

			if (!File.Exists(resolved))
				return false;

			resourcePath = resolved;
			return true;
		}

		internal static bool TryResolveExternalModelPath(string modelHash, out string filePath)
		{
			filePath = null;

			if (string.IsNullOrWhiteSpace(modelHash))
				return false;

			if (TryResolveImmutableModelSourcePath(modelHash, out filePath))
				return true;

			var path = ModelResourceTable.GetPathForHash(modelHash);
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
				return false;

			filePath = path;
			return true;
		}

		internal static bool TryResolveInternalModelSourcePath(string modelHash, out string filePath)
		{
			filePath = null;

			if (string.IsNullOrWhiteSpace(modelHash))
				return false;

			if (!ModelResourceTable.TryGetEntry(modelHash, out var entry) || entry.Kind != ModelResourceTable.EntryKind.Resource)
				return false;

			filePath = ResolveResourceAssetPath(entry.ResourcePath, includeImmutable: false);
			return !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
		}

		internal static bool TryExportModelToExternal(string modelHash, bool includeLiveContent = false)
		{
			if (string.IsNullOrWhiteSpace(modelHash))
				return false;

			if (!ModelResourceTable.TryGetEntry(modelHash, out var entry))
				return false;

			if (!TryResolveModelExportSourcePath(entry, includeLiveContent, out var sourcePath))
				return false;

			if (IsPathUnderRoot(sourcePath, ApplicationSettings.SystemModelsFolder))
				return true;

			if (!string.Equals(Path.GetExtension(sourcePath), ".obj", StringComparison.OrdinalIgnoreCase))
			{
				Debug.LogWarning($"ResourceDependencyHelpers: skipping non-Wavefront model export for '{modelHash}' ({sourcePath})");
				return false;
			}

			var importedPath = WavefrontAssetImporter.ImportWavefrontModel(
				sourcePath,
				WavefrontAssetImporter.ImportOption.HashIdFolder,
				ApplicationSettings.SystemModelsFolder,
				registerImported: true,
				forcedFolderName: modelHash,
				forcedHashId: modelHash);
			if (string.IsNullOrWhiteSpace(importedPath) || !File.Exists(importedPath))
				return false;

			DeleteExportedModelSource(entry, sourcePath);
			return true;
		}

		internal static bool TryImportExternalModelToImmutable(string modelHash)
		{
			if (string.IsNullOrWhiteSpace(modelHash))
				return false;

			if (!ModelResourceTable.TryGetEntry(modelHash, out var entry))
				return false;

			if (entry.Kind == ModelResourceTable.EntryKind.Resource)
				return true;

			var sourcePath = entry.FilePath;
			if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
				return false;

			if (!string.Equals(Path.GetExtension(sourcePath), ".obj", StringComparison.OrdinalIgnoreCase))
			{
				Debug.LogWarning($"ResourceDependencyHelpers: skipping non-Wavefront model import for '{modelHash}' ({sourcePath})");
				return false;
			}

			var destinationRoot = Path.Combine(Application.dataPath, "Application", "Resources", AssetPath.ImmutableRootFolder, AssetPath.GeometryFolder);
			var importedPath = WavefrontAssetImporter.ImportWavefrontModel(
				sourcePath,
				WavefrontAssetImporter.ImportOption.HashIdFolder,
				destinationRoot,
				registerImported: false,
				forcedFolderName: modelHash,
				forcedHashId: modelHash);

			return !string.IsNullOrWhiteSpace(importedPath) && File.Exists(importedPath);
		}

		private static bool TryResolveModelExportSourcePath(ModelResourceTable.Entry entry, bool includeLiveContent, out string filePath)
		{
			filePath = null;

			if (TryResolveImmutableModelSourcePath(entry, out filePath))
				return true;

			if (entry.Kind == ModelResourceTable.EntryKind.Resource)
				return includeLiveContent && TryResolveLiveModelSourcePath(entry, out filePath);

			filePath = entry.FilePath;
			return !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
		}

		private static void DeleteExportedModelSource(ModelResourceTable.Entry entry, string sourcePath)
		{
			if (string.IsNullOrWhiteSpace(sourcePath) || IsPathUnderRoot(sourcePath, ApplicationSettings.SystemModelsFolder))
				return;

			var immutableRoot = Path.Combine(Application.dataPath, "Application", "Resources", AssetPath.ImmutableRootFolder, AssetPath.GeometryFolder);
			if (IsPathUnderRoot(sourcePath, immutableRoot))
			{
				var sourceRoot = !string.IsNullOrWhiteSpace(entry.HashId)
					? Path.Combine(immutableRoot, entry.HashId)
					: Path.GetDirectoryName(sourcePath);
				TryDeleteDirectoryTree(sourceRoot);
				return;
			}

			if (entry.Kind == ModelResourceTable.EntryKind.File)
			{
				TryDeleteDirectoryTree(Path.GetDirectoryName(sourcePath));
				return;
			}

			TryDeleteWavefrontSourceArtifacts(sourcePath);
		}

		private static bool TryResolveLiveModelSourcePath(ModelResourceTable.Entry entry, out string filePath)
		{
			filePath = null;

			if (entry.Kind == ModelResourceTable.EntryKind.Resource)
				return TryResolveInternalModelSourcePath(entry.HashId, out filePath);

			filePath = entry.FilePath;
			return !string.IsNullOrWhiteSpace(filePath) &&
				File.Exists(filePath) &&
				!IsPathUnderRoot(filePath, Path.Combine(Application.dataPath, "Application", "Resources", AssetPath.ImmutableRootFolder));
		}

		private static bool TryResolveImmutableModelSourcePath(string modelHash, out string filePath)
		{
			filePath = null;

			if (string.IsNullOrWhiteSpace(modelHash))
				return false;

			if (!ModelResourceTable.TryGetEntry(modelHash, out var entry))
				return false;

			return TryResolveImmutableModelSourcePath(entry, out filePath);
		}

		private static bool TryResolveImmutableModelSourcePath(ModelResourceTable.Entry entry, out string filePath)
		{
			filePath = null;

			if (string.IsNullOrWhiteSpace(entry.HashId) || string.IsNullOrWhiteSpace(entry.DisplayName))
				return false;

			var immutableRoot = Path.Combine(
				Application.dataPath,
				"Application",
				"Resources",
				AssetPath.ImmutableRootFolder,
				AssetPath.GeometryFolder,
				entry.HashId);

			if (!Directory.Exists(immutableRoot))
				return false;

			var directCandidate = Path.Combine(immutableRoot, $"{entry.DisplayName}.obj");
			if (File.Exists(directCandidate))
			{
				filePath = directCandidate;
				return true;
			}

			foreach (var candidate in Directory.EnumerateFiles(immutableRoot, "*.obj", SearchOption.AllDirectories))
			{
				if (string.Equals(Path.GetFileNameWithoutExtension(candidate), entry.DisplayName, StringComparison.OrdinalIgnoreCase))
				{
					filePath = candidate;
					return true;
				}
			}

			return false;
		}

		private static bool IsPathUnderRoot(string filePath, string rootPath)
		{
			if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootPath))
				return false;

			var normalizedFile = Path.GetFullPath(filePath).Replace('\\', '/').TrimEnd('/');
			var normalizedRoot = Path.GetFullPath(rootPath).Replace('\\', '/').TrimEnd('/') + "/";
			return normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
		}

		private static void TryDeleteDirectoryTree(string path)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
				{
					Directory.Delete(path, recursive: true);
					DeleteAssetAndMeta(path);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ResourceDependencyHelpers: failed to remove source model tree '{path}': {ex.Message}");
			}
		}

		private static void TryDeleteWavefrontSourceArtifacts(string sourceObjPath)
		{
			try
			{
				var mtlRelative = FindMtlRelativePath(sourceObjPath);
				var sourceDir = Path.GetDirectoryName(sourceObjPath);
				var sourceMtlPath = string.IsNullOrWhiteSpace(mtlRelative) || string.IsNullOrWhiteSpace(sourceDir)
					? null
					: Path.Combine(sourceDir, mtlRelative);

				DeleteAssetAndMeta(sourceObjPath);
				if (!string.IsNullOrWhiteSpace(sourceMtlPath))
					DeleteAssetAndMeta(sourceMtlPath);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ResourceDependencyHelpers: failed to remove source model artifacts '{sourceObjPath}': {ex.Message}");
			}
		}

		private static string FindMtlRelativePath(string objPath)
		{
			if (string.IsNullOrWhiteSpace(objPath) || !File.Exists(objPath))
				return null;

			foreach (var line in File.ReadAllLines(objPath))
			{
				if (line.TrimStart().StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
					return line.Trim().Substring(7).Trim();
			}

			return null;
		}

		private static void DeleteAssetAndMeta(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return;

			if (File.Exists(path))
				File.Delete(path);

			var metaPath = path + ".meta";
			if (File.Exists(metaPath))
				File.Delete(metaPath);
		}

		internal static string ResolveResourceAssetPath(string resourcePath, bool includeImmutable = true)
		{
			var normalized = resourcePath?.Replace('\\', '/').Trim('/');
			if (string.IsNullOrWhiteSpace(normalized))
				return null;

			var searchRoot = Path.Combine(Application.dataPath, "Application");
			if (!Directory.Exists(searchRoot))
				return null;

			foreach (var resFolder in Directory.EnumerateDirectories(searchRoot, "Resources", SearchOption.AllDirectories))
			{
				var exactCandidate = Path.Combine(resFolder, normalized);
				if (File.Exists(exactCandidate) && (includeImmutable || !IsPathUnderRoot(exactCandidate, Path.Combine(Application.dataPath, "Application", "Resources", AssetPath.ImmutableRootFolder))))
					return exactCandidate;
			}

			var normalizedStem = Path.GetFileNameWithoutExtension(normalized);
			var suffix = "/" + normalized;
			foreach (var file in Directory.EnumerateFiles(searchRoot, normalizedStem + ".*", SearchOption.AllDirectories))
			{
				var candidate = file.Replace('\\', '/');
				var candidateStem = Path.GetFileNameWithoutExtension(candidate);
				if (string.Equals(candidateStem, normalizedStem, StringComparison.OrdinalIgnoreCase) &&
					(includeImmutable || !IsPathUnderRoot(file, Path.Combine(Application.dataPath, "Application", "Resources", AssetPath.ImmutableRootFolder))))
					return file;

				if (candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
					(includeImmutable || !IsPathUnderRoot(file, Path.Combine(Application.dataPath, "Application", "Resources", AssetPath.ImmutableRootFolder))))
					return file;
			}

			return null;
		}

		internal static string FindArchiveContentFolder(string parentFolder, string canonicalFolderName)
		{
			if (string.IsNullOrWhiteSpace(parentFolder) || string.IsNullOrWhiteSpace(canonicalFolderName))
				return null;

			string fullPath = Path.Combine(parentFolder, canonicalFolderName);
			return Directory.Exists(fullPath) ? fullPath : null;
		}

		internal static string GetArchiveFolderName(string sourceRoot)
		{
			if (string.IsNullOrWhiteSpace(sourceRoot))
				return null;

			return Path.GetFileName(Path.GetFullPath(sourceRoot));
		}
	}
}
