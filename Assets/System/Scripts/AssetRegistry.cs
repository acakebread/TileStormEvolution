using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class AssetRegistry<T> where T : UnityEngine.Object
	{
		// Separate root lists for models and prefabs (since both are GameObject)
		private static readonly HashSet<string> ModelRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> PrefabRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> MaterialRoots = new(StringComparer.OrdinalIgnoreCase);

		private static readonly Dictionary<string, T> ModelCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, T> PrefabCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, T> MaterialCache = new(StringComparer.OrdinalIgnoreCase);

		public static Func<string, string> NameRemapper { get; set; }

		public static void RegisterRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				ModelRoots.Add(root.Trim('/'));
		}

		public static void RegisterPrefabRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				PrefabRoots.Add(root.Trim('/'));
		}

		public static void RegisterMaterialRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				MaterialRoots.Add(root.Trim('/'));
		}

		public static void ClearCache() => ModelCache.Clear();
		public static void ClearPrefabCache() => PrefabCache.Clear();
		public static void ClearMaterialCache() => MaterialCache.Clear();

		public static T Find(string assetName) => Find(assetName, ModelCache, ModelRoots);

		public static T FindPrefab(string assetName) => Find(assetName, PrefabCache, PrefabRoots);

		public static T FindMaterial(string assetName) => Find(assetName, MaterialCache, MaterialRoots);

		private static T Find(string assetName, Dictionary<string, T> cache, HashSet<string> roots)
		{
			if (string.IsNullOrEmpty(assetName)) return null;

			string key = assetName.Trim();

			if (cache.TryGetValue(key, out var cached)) return cached;

			T asset = null;

			if (null != NameRemapper)
			{
				string remapped = NameRemapper(key);
				if (remapped != key)
				{
					asset = TryLoad(remapped, roots);
					if (asset != null)
					{
						cache[key] = asset;
						cache[remapped] = asset;
						return asset;
					}
				}
			}

			asset = TryLoad(key, roots);

			if (null != asset)
				cache[key] = asset;

			return asset;
		}

		private static T TryLoad(string name, HashSet<string> roots)
		{
			foreach (var root in roots)
			{
				string path = string.IsNullOrEmpty(root) ? name : $"{root}/{name}";
				var loaded = Resources.Load<T>(path);
				if (null != loaded) return loaded;
			}
			return null;
		}
	}
}