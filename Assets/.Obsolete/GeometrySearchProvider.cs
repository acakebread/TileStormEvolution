using System.IO;
using UnityEngine;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class GeometrySearchProvider
	{
		private static bool hasRegistered = false;
		private static string[] searchRoots;

		// Per-root cache: root → dictionary of name → GameObject
		private static readonly Dictionary<string, Dictionary<string, GameObject>> rootCaches = new();

		public static void Register(bool useRemapping = true)
		{
			if (hasRegistered) return;
			hasRegistered = true;

			// Apply persisted value (or default) to the global toggle
			UseRemapping = useRemapping;

			var geometryPath = AssetPath.GeometryPath;
			var configuredRoot = string.IsNullOrWhiteSpace(geometryPath)
				? null
				: geometryPath.Replace("\\", "/").Trim('/');

			var roots = new List<string>();
			if (!string.IsNullOrEmpty(configuredRoot))
				roots.Add(configuredRoot);
			roots.Add("Levels");

			searchRoots = roots.ToArray();

			Debug.Log($"[GeometrySearchProvider] Registered. Roots: {string.Join(", ", searchRoots)}");
		}

		public static GameObject FindModelByName(string modelName)
		{
			if (string.IsNullOrEmpty(modelName)) return null;

			var cleanName = Path.GetFileNameWithoutExtension(modelName).Trim();

			string targetName = cleanName;

			// Only apply remapping if enabled
			if (UseRemapping)
			{
				var preferredName = ClassicTileStormAssetRemapHelper.RemapName(cleanName);
				if (preferredName != cleanName)
				{
					// PHASE 1: Try preferred (remapped) name
					var preferred = FindInAnyRoot(preferredName, isPreferred: true, originalName: cleanName);
					if (preferred != null) return preferred;

					// PHASE 2: Fall back to original
					targetName = cleanName;
				}
			}
			// If remapping is disabled, or remap returned same name, just use original
			var result = FindInAnyRoot(targetName, isPreferred: false, originalName: cleanName);
			if (result != null) return result;

			Debug.LogWarning($"[GeometrySearchProvider] Model '{modelName}' ('{cleanName}') not found.");
			return null;
		}

		private static GameObject FindInAnyRoot(string candidateName, bool isPreferred, string originalName)
		{
			foreach (string root in searchRoots)
			{
				var cacheKey = string.IsNullOrEmpty(root) ? "" : root;

				if (!rootCaches.TryGetValue(cacheKey, out var nameDict))
				{
					nameDict = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

					GameObject[] allInRoot = Resources.LoadAll<GameObject>(root);

					foreach (var go1 in allInRoot)
					{
						if (go1 != null && !nameDict.ContainsKey(go1.name))
						{
							nameDict[go1.name] = go1;
						}
					}

					rootCaches[cacheKey] = nameDict;

					Debug.Log($"[GeometrySearchProvider] Scanned root '{root}' → {allInRoot.Length} assets cached.");
				}

				if (nameDict.TryGetValue(candidateName, out GameObject go))
				{
					// Only log "Upgraded/Remapped" if we actually used a different name
					if (isPreferred && candidateName != originalName)
					{
						Debug.Log($"[GeometrySearchProvider] Upgraded/Remapped: '{originalName}' → '{candidateName}' found in root '{root}'");
					}
					// Optional: quiet success for normal case
					// else Debug.Log($"[GeometrySearchProvider] Found standard model '{originalName}' in root '{root}'");

					return go;
				}
			}
			return null;
		}

		private static bool useRemapping = true;
		public static bool UseRemapping
		{
			get => useRemapping;
			set
			{
				if (useRemapping != value)
				{
					useRemapping = value;
					// When toggled, clear the root caches so next lookup rescans with new logic
					rootCaches.Clear();
					Debug.Log($"[GeometrySearchProvider] Geometry remapping {(value ? "enabled" : "disabled")}. Caches cleared.");
				}
			}
		}
	}
}