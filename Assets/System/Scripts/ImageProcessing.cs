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
		/// Finds the UV of the brightest area (sun / dominant light source).
		/// Works on both bright day skies and low-luminance night skies.
		/// No hard threshold — always returns the brightest region found.
		/// </summary>
		public static Vector2 FindSunUV(
			Texture2D tex,
			bool scanAboveHorizonOnly = true,
			float searchRadiusFactor = 0.08f)
		{
			if (tex == null)
				return new Vector2(0.5f, 0.5f);

			int width = tex.width;
			int height = tex.height;
			var pixels = tex.GetPixels();

			// Based on your inspector tests: sky is in the BOTTOM half of the pixel array
			int startY = scanAboveHorizonOnly ? height / 2 : 0;
			int endY = height;

			// Pass 1: Find the single brightest pixel in the allowed region
			float maxLum = -1f;
			int bestX = width / 2;
			int bestY = height * 3 / 4;   // bias toward sky (bottom half)

			//for (int y = startY; y < endY; y++)
			for (int y = endY - 1; y > startY; y--)//for now invert the search because the old method search from sky to horizon and favoured higher light sources
			{
				for (int x = 0; x < width; x++)
				{
					Color c = pixels[y * width + x];
					if (c.r == 0f && c.g == 0f && c.b == 0f) continue;

					float lum = Luminance(c);
					if (lum > maxLum)
					{
						maxLum = lum;
						bestX = x;
						bestY = y;
					}
				}
			}

			// If nothing found (completely black texture), fallback
			if (maxLum <= 0f)
				return new Vector2(0.5f, 0.75f);

			// Pass 2: Local weighted centroid around the brightest pixel (handles small extended sources)
			float radius = Mathf.Max(3, (int)(Mathf.Min(width, height) * searchRadiusFactor));
			float totalWeight = 0f;
			float sumX = 0f;
			float sumY = 0f;

			int minX = Mathf.Max(0, bestX - (int)radius);
			int maxX = Mathf.Min(width - 1, bestX + (int)radius);
			int minY = Mathf.Max(startY, bestY - (int)radius);
			int maxY = Mathf.Min(endY - 1, bestY + (int)radius);

			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					Color c = pixels[y * width + x];
					float lum = Luminance(c);

					float w = lum * lum;// square weight emphasizes brighter pixels
					sumX += x * w;
					sumY += y * w;
					totalWeight += w;
				}
			}

			float finalX = totalWeight > 0f ? sumX / totalWeight : bestX;
			float finalY = totalWeight > 0f ? sumY / totalWeight : bestY;

			return new Vector2(
				finalX / (width - 1f),
				1f - finalY / (height - 1f)
			);
		}

		private static float Luminance(Color c)
		{
			return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
		}
	}
}