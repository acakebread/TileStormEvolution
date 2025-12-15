using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class MaterialCache
	{
		private static readonly Dictionary<string, Material> cache = new();

		// Common extensions that might appear in legacy data or file references
		private static readonly string[] MaterialExtensions = { ".mat", ".material" }; // add more if needed

		/// <summary>
		/// Loads a Material from Resources, caching it for future use.
		/// Pass the full path as it appears in your data (e.g. "Materials/Ground/Grass.mat")
		/// </summary>
		public static Material Get(string fullPath)
		{
			if (string.IsNullOrEmpty(fullPath))
			{
				Debug.LogWarning("MaterialCache: Empty path provided.");
				return null;
			}

			string loadPath = StripExtensions(fullPath);

			if (cache.TryGetValue(loadPath, out var material))
				return material;

			material = Resources.Load<Material>(loadPath);
			if (material == null)
			{
				Debug.LogWarning($"Material not found: {loadPath} (original request: {fullPath})");
			}

			cache[loadPath] = material;
			return material;
		
			static string StripExtensions(string path) => ResourcePathUtils.NormalizeForResourcesLoad(path, MaterialExtensions);
		}

		/// <summary>
		/// Clears the entire material cache (useful for scene transitions or hot-reloading)
		/// </summary>
		public static void ClearCache() => cache.Clear();
	}
}