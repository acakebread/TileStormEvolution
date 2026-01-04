using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class AssetFactory
	{
		// Optional: let projects override specific lookups entirely
		public static Func<string, GameObject> CustomProvider { get; set; }

		// Configurable search roots (e.g. "Geometry", "Prefabs", "SkyCubes", "Levels")
		private static readonly HashSet<string> SearchRoots = new(StringComparer.OrdinalIgnoreCase)
		{
			"Geometry",   // default fallback
			"Levels"
		};

		// Optional name remapping (legacy → new asset names)
		public static bool EnableNameRemapping { get; set; } = true;
		public static Func<string, string> NameRemapper { get; set; } // e.g. ClassicTileStormAssetRemapHelper.RemapName

		private static readonly Dictionary<string, GameObject> Cache = new(StringComparer.OrdinalIgnoreCase);

		public static void RegisterSearchRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				SearchRoots.Add(root.Trim('/'));
		}

		public static void ClearCache() => Cache.Clear();

		public static GameObject FindAsset(string assetName)
		{
			if (string.IsNullOrEmpty(assetName)) return null;

			var key = assetName.Trim();

			if (Cache.TryGetValue(key, out var cached)) return cached;

			string finalName = key;

			// Optional remapping
			if (EnableNameRemapping && NameRemapper != null)
			{
				var remapped = NameRemapper(key);
				if (remapped != key)
				{
					var fromRemap = TryLoadFromRoots(remapped);
					if (fromRemap != null)
					{
						Cache[key] = fromRemap;
						Cache[remapped] = fromRemap;
						return fromRemap;
					}
					// Fall back to original
				}
			}

			var result = TryLoadFromRoots(finalName)
					  ?? CustomProvider?.Invoke(finalName);

			if (result != null)
				Cache[key] = result;

			return result;
		}

		private static GameObject TryLoadFromRoots(string name)
		{
			foreach (var root in SearchRoots)
			{
				var path = string.IsNullOrEmpty(root) ? name : $"{root}/{name}";
				var asset = Resources.Load<GameObject>(path);
				if (asset != null) return asset;
			}
			return null;
		}

		// Convenience: Instantiate helpers
		public static GameObject Instantiate(string assetName, Transform parent = null)
		{
			var asset = FindAsset(assetName);
			if (asset == null) return null;

			var instance = UnityEngine.Object.Instantiate(asset, parent);
			instance.name = Path.GetFileNameWithoutExtension(assetName);
			return instance;
		}

		public static GameObject Instantiate(string assetName, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			var instance = Instantiate(assetName, parent);
			if (instance)
			{
				instance.transform.position = position;
				instance.transform.rotation = rotation;
			}
			return instance;
		}

		// Add more overloads as needed...
	}
}