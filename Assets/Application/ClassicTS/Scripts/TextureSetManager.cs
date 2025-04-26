using UnityEngine;
using GameDatabase;
using System.Linq;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public struct TextureFrame
	{
		public Texture2D texture;
		public float duration;
	}

	public static class TextureSetManager
	{
		private static Dictionary<string, TextureFrame[]> textureAnimations = new();

		public static TextureFrame[] GetTextureFrames(string szTheme)
		{
			DatabaseLoader.Theme theme = DatabaseLoader.instance.Themes.FirstOrDefault(t => t.name == szTheme);
			if (theme == null || string.IsNullOrEmpty(theme.szTileTextureSet)) return null;

			DatabaseLoader.TextureSet textureSet = DatabaseLoader.instance.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
			if (textureSet == null || textureSet.frames == null || textureSet.frames.Length <= 0) return null;


			var frames = new TextureFrame[textureSet.frames.Length];

			for (int i = 0; i < textureSet.frames.Length; i++)
			{
				var frame = textureSet.frames[i];
				frames[i].texture = TextureManager.Get(frame.szTexture);
				frames[i].duration = frame.fDuration > 0 ? frame.fDuration : 1f; // Default to 1s if 0
			}

			textureAnimations[szTheme] = frames;
			return frames;
		}
	}
}