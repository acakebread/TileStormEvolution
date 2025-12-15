using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class TextureCache
	{
		private static readonly Dictionary<string, Texture2D> cache = new();

		// Known texture extensions that might appear in source data
		private static readonly string[] TextureExtensions = { ".tga", ".png", ".jpg", ".jpeg", ".psd", ".tiff" };

		public static Texture2D Get(string fullPath)
		{
			if (string.IsNullOrEmpty(fullPath))
			{
				Debug.LogWarning("TextureManager: Empty path provided.");
				return null;
			}

			// Normalize the path for Resources.Load (no file extension, forward slashes)
			string loadPath = StripExtensions(fullPath);

			if (cache.TryGetValue(loadPath, out var texture))
				return texture;

			texture = Resources.Load<Texture2D>(loadPath);
			if (texture == null)
			{
				Debug.LogWarning($"Texture not found: {loadPath} (original: {fullPath})");
			}

			cache[loadPath] = texture;
			return texture;

			static string StripExtensions(string path) => ResourcePathUtils.NormalizeForResourcesLoad(path, TextureExtensions);
		}

		public static void ClearCache() => cache.Clear();
	}
}