using UnityEngine;

namespace MassiveHadronLtd
{
	public static class TextureUtils
	{
		/// <summary>
		/// Generates a Perlin noise texture for use in shaders (e.g., frosted glass effect).
		/// </summary>
		/// <param name="width">The width of the texture in pixels (default: 256).</param>
		/// <param name="height">The height of the texture in pixels (default: 256).</param>
		/// <param name="scale">The scale of the Perlin noise pattern (default: 10).</param>
		/// <returns>A Texture2D with Perlin noise, or null if width/height are invalid.</returns>
		public static Texture2D GeneratePerlinNoiseTexture(int width = 256, int height = 256, float scale = 10f)
		{
			if (width <= 0 || height <= 0)
			{
				Debug.LogError($"TextureUtils: Invalid texture dimensions (width: {width}, height: {height})");
				return null;
			}

			Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					float sample = Mathf.PerlinNoise(x / (float)width * scale, y / (float)height * scale);
					texture.SetPixel(x, y, new Color(sample, sample, sample, 1));
				}
			}
			texture.Apply();
			texture.wrapMode = TextureWrapMode.Repeat; // Ensures seamless tiling
			texture.filterMode = FilterMode.Bilinear; // Smooths the noise
			return texture;
		}

		// Helper to create a texture for the tile selector background
		public static Texture2D MakeTex(int width, int height, Color col)
		{
			Color[] pix = new Color[width * height];
			for (int i = 0; i < pix.Length; i++)
				pix[i] = col;
			Texture2D result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();
			return result;
		}
	}
}