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
		/// Extremely simple version: just returns the UV of the single brightest pixel.
		/// No radius, no weighting, no centroid — pure maximum luminance search.
		/// </summary>
		public static Vector2 FindSunUV(
			Texture2D tex,
			bool scanAboveHorizonOnly = true)
		{
			if (tex == null)
				return new Vector2(0.5f, 0.5f);

			int width = tex.width;
			int height = tex.height;
			Color[] pixels = tex.GetPixels();

			int startY = scanAboveHorizonOnly ? height / 2 : 0;
			int endY = height;

			float maxLum = -1f;
			int bestX = width / 2;
			int bestY = height * 3 / 4;   // default bias toward sky

			//for (int y = startY; y < endY; y++)
			for (int y = endY - 1; y > startY; y--)//for now invert the search because the old method search from sky to horizon and favoured higher light sources
			{
				for (int x = 0; x < width; x++)
				{
					Color c = pixels[y * width + x];

					// Skip pure black pixels
					if (c.r == 0f && c.g == 0f && c.b == 0f)
						continue;

					float lum = Luminance(c);

					if (lum > maxLum)
					{
						maxLum = lum;
						bestX = x;
						bestY = y;
					}
				}
			}

			// Fallback if texture was completely black
			if (maxLum <= 0f)
				return new Vector2(0.5f, 0.75f);

			// Convert pixel coordinates to UV (0..1)
			float uvX = bestX / (width - 1f);
			float uvY = bestY / (height - 1f);

			return new Vector2(uvX, uvY);
		}

		private static float Luminance(Color c)
		{
			return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
		}
	}
}