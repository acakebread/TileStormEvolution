using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;

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

			var path = ModelResourceTable.GetPathForHash(modelHash);
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
				return false;

			filePath = path;
			return true;
		}
	}
}
