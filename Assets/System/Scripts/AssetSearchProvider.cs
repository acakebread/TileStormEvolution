using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Generic, reusable asset search provider for Resources folders.
	/// Works for any UnityEngine.Object type (GameObject, Material, AudioClip, etc.).
	/// Replicates and improves on your old GeometrySearchProvider.
	/// Fully decoupled — project registers roots and optional remapper.
	/// </summary>
	public static class AssetSearchProvider<T> where T : UnityEngine.Object
	{
		private static readonly HashSet<string> SearchRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, Dictionary<string, T>> RootCaches = new();

		private static Func<string, string> _nameRemapper;
		private static bool _remappingEnabled = true;

		/// <summary>
		/// Register a Resources subfolder to search in (e.g. "ClassicTS/Geometry", "ClassicTS/SkyCubes")
		/// </summary>
		public static void RegisterRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				SearchRoots.Add(root.Trim('/'));
		}

		/// <summary>
		/// Optional project-specific name remapper (e.g. old_name → old_name_hd)
		/// </summary>
		public static Func<string, string> NameRemapper
		{
			get => _nameRemapper;
			set
			{
				_nameRemapper = value;
				if (value == null) _remappingEnabled = false;
			}
		}

		/// <summary>
		/// Toggle remapping on/off — clears caches automatically (like old UseRemapping)
		/// </summary>
		public static bool RemappingEnabled
		{
			get => _remappingEnabled && _nameRemapper != null;
			set
			{
				if (_remappingEnabled == value) return;

				_remappingEnabled = value;
				RootCaches.Clear();
				Debug.Log($"[AssetSearchProvider<{typeof(T).Name}>] Remapping {(value ? "enabled" : "disabled")}. Caches cleared.");
			}
		}

		/// <summary>
		/// Find an asset by name (filename without extension)
		/// Tries remapped name first (if enabled), falls back to original.
		/// </summary>
		public static T FindByName(string assetName)
		{
			if (string.IsNullOrEmpty(assetName)) return null;

			string cleanName = System.IO.Path.GetFileNameWithoutExtension(assetName).Trim();

			string targetName = cleanName;

			if (RemappingEnabled && _nameRemapper != null)
			{
				string preferred = _nameRemapper(cleanName);
				if (preferred != cleanName)
				{
					T preferredAsset = FindInAnyRoot(preferred);
					if (preferredAsset != null)
					{
						Debug.Log($"[AssetSearchProvider] Remapped: '{cleanName}' → '{preferred}'");
						return preferredAsset;
					}
					// Fall back to original
				}
			}

			return FindInAnyRoot(targetName);
		}

		private static T FindInAnyRoot(string candidateName)
		{
			foreach (string root in SearchRoots)
			{
				string cacheKey = string.IsNullOrEmpty(root) ? "" : root;

				if (!RootCaches.TryGetValue(cacheKey, out var nameDict))
				{
					nameDict = new Dictionary<string, T>(System.StringComparer.OrdinalIgnoreCase);

					T[] allInRoot = Resources.LoadAll<T>(root);

					foreach (var loadedAsset in allInRoot)  // ← Changed 'asset' to 'loadedAsset'
					{
						if (loadedAsset != null && !nameDict.ContainsKey(loadedAsset.name))
						{
							nameDict[loadedAsset.name] = loadedAsset;
						}
					}

					RootCaches[cacheKey] = nameDict;

					Debug.Log($"[AssetSearchProvider<{typeof(T).Name}>] Scanned root '{root}' → {allInRoot.Length} assets cached.");
				}

				if (nameDict.TryGetValue(candidateName, out T asset))
					return asset;
			}

			return null;
		}

		public static void ClearCaches()
		{
			RootCaches.Clear();
		}
	}
}