using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
		}

		private static string StripExtensions(string path)
		{
			// Resources.Load uses forward slashes and no extension
			string normalized = path.Replace('\\', '/');

			string directory = Path.GetDirectoryName(normalized).Replace('\\', '/');
			string fileName = Path.GetFileNameWithoutExtension(normalized);

			// In case there are multiple extensions (e.g. .tga.png from bad export), keep stripping
			while (TextureExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
			{
				fileName = Path.GetFileNameWithoutExtension(fileName);
			}

			if (string.IsNullOrEmpty(directory))
				return fileName;

			return $"{directory}/{fileName}";
		}

		public static void ClearCache() => cache.Clear();
	}
}