using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class AssetRegistry<T> where T : UnityEngine.Object
	{
		private static readonly HashSet<string> SearchRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, T> Cache = new(StringComparer.OrdinalIgnoreCase);

		public static Func<string, T> CustomProvider { get; set; }
		public static Func<string, string> NameRemapper { get; set; }

		public static void RegisterRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				SearchRoots.Add(root.Trim('/'));
		}

		public static void ClearCache() => Cache.Clear();

		public static T Find(string assetName)
		{
			if (string.IsNullOrEmpty(assetName)) return null;

			string key = assetName.Trim();

			if (Cache.TryGetValue(key, out var cached)) return cached;

			string originalLookup = key;

			T asset = null;

			// Always try remapped first if remapper set
			if (NameRemapper != null)
			{
				string remapped = NameRemapper(key);
				if (remapped != key)
				{
					asset = TryLoad(remapped);
					if (asset != null)
					{
						Cache[key] = asset;
						Cache[remapped] = asset;
						return asset;
					}
					// IMPORTANT: Do NOT return null here — fall through to original
				}
			}

			// Always try original name (fallback)
			asset = TryLoad(originalLookup) ?? CustomProvider?.Invoke(originalLookup);

			if (asset != null)
				Cache[key] = asset;

			return asset;
		}

		private static T TryLoad(string name)
		{
			foreach (var root in SearchRoots)
			{
				string path = string.IsNullOrEmpty(root) ? name : $"{root}/{name}";
				var loaded = Resources.Load<T>(path);
				if (loaded != null) return loaded;
			}
			return null;
		}
	}
}