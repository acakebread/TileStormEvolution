using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public static class TextureSetManager
	{
		private static readonly Dictionary<string, TextureFrame[]> cache = new();

		public static TextureFrame[] GetTextureFrames(string themeName)
		{
			if (string.IsNullOrEmpty(themeName)) return null;
			if (cache.TryGetValue(themeName, out var cached)) return cached;

			var theme = ResourceManager.Themes.FirstOrDefault(t => t.name == themeName);
			if (theme == null || string.IsNullOrEmpty(theme.szTileTextureSet)) return null;

			var textureSet = ResourceManager.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
			if (textureSet == null || textureSet.frames == null || textureSet.frames.Length == 0) return null;

			// Resolve textures from szTexture → Texture2D at runtime
			for (int i = 0; i < textureSet.frames.Length; i++)
			{
				var f = textureSet.frames[i];
				if (f.texture == null && !string.IsNullOrEmpty(f.szTexture))
				{
					f.texture = TextureManager.Get(f.szTexture);
					textureSet.frames[i] = f; // write back only the texture
				}
			}

			cache[themeName] = textureSet.frames;
			return textureSet.frames;
		}
	}
}