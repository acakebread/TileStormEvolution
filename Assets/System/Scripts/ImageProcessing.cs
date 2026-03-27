using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ImageProcessing
	{
		/// <summary>
		/// Computes ONLY the luminance histogram (no bright color logic).
		/// Clean and lightweight when you only need the distribution.
		/// </summary>
		public static void ComputeHistogramOnly(
			Color[] pixels,
			out List<float> luminanceHistogram,
			out float maxLuminance,
			int binCount = 64)
		{
			luminanceHistogram = new List<float>(binCount);
			maxLuminance = 0f;

			if (pixels == null || pixels.Length == 0)
			{
				for (int i = 0; i < binCount; i++)
					luminanceHistogram.Add(0f);
				return;
			}

			int[] bins = new int[binCount];
			int validPixels = 0;

			foreach (Color c in pixels)
			{
				float lum = c.Luminance();
				if (lum > maxLuminance)
					maxLuminance = lum;

				if (lum > 0.01f)
				{
					int bin = Mathf.Clamp(Mathf.FloorToInt(lum * binCount), 0, binCount - 1);
					bins[bin]++;
					validPixels++;
				}
			}

			if (validPixels > 0)
			{
				for (int i = 0; i < binCount; i++)
					luminanceHistogram.Add((float)bins[i] / validPixels);
			}
			else
			{
				for (int i = 0; i < binCount; i++)
					luminanceHistogram.Add(0f);
			}
		}

		/// <summary>
		/// Full method: histogram + bright color (threshold defaults to 0 = full range).
		/// </summary>
		public static Color ComputeBrightColorWithHistogram(
			Color[] pixels,
			out List<float> luminanceHistogram,
			out float maxLuminance,
			out int brightPixelCount,
			float threshold = 0f,
			int binCount = 64)
		{
			ComputeHistogramOnly(pixels, out luminanceHistogram, out maxLuminance, binCount);

			brightPixelCount = 0;
			if (threshold > 0f)
			{
				foreach (Color c in pixels)
				{
					if (c.Luminance() >= threshold)
						brightPixelCount++;
				}
			}

			return threshold > 0f
				? ColourUtils.ThresholdColour(pixels, threshold)
				: ColourUtils.AverageColour(pixels);
		}

		/// <summary>
		/// Convenience overload for flattened cubemap Texture2D.
		/// </summary>
		public static Color ComputeBrightColorWithHistogram(
			Texture2D flattenedCubemap,
			out List<float> luminanceHistogram,
			out float maxLuminance,
			out int brightPixelCount,
			float threshold = 0f,
			int binCount = 64)
		{
			if (flattenedCubemap == null)
			{
				luminanceHistogram = new List<float>();
				maxLuminance = 0f;
				brightPixelCount = 0;
				return Color.white;
			}

			return ComputeBrightColorWithHistogram(
				flattenedCubemap.GetPixels(),
				out luminanceHistogram,
				out maxLuminance,
				out brightPixelCount,
				threshold,
				binCount);
		}

		public static Vector2 FindSunUV(Texture2D tex, float threshold = 0.85f)
		{
			int width = tex.width;
			int height = tex.height;

			float totalWeight = 0f;
			Vector2 weightedSum = Vector2.zero;

			var pixels = tex.GetPixels();

			for (int y = height / 2; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					Color c = pixels[y * width + x];
					float lum = Luminance(c);

					if (lum < threshold)
						continue;

					float w = lum; // weight by brightness
					weightedSum += new Vector2(x, y) * w;
					totalWeight += w;
				}
			}

			if (totalWeight == 0f)
				return new Vector2(0.5f, 0.5f); // fallback

			Vector2 pixelPos = weightedSum / totalWeight;
			return new Vector2(pixelPos.x / width, 1f - pixelPos.y / height); // normalized UV
		}

		private static float Luminance(Color c)
		{
			return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
		}
	}
}