using UnityEngine;
using System.Linq;

namespace MassiveHadronLtd
{
	public static class ColourUtils
	{
		/// <summary>
		/// Returns the perceived luminance (brightness) of this color using Rec.709 coefficients.
		/// </summary>
		public static float Luminance(this Color col)
			=> col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

		/// <summary>
		/// Returns the simple average of all colors in the array.
		/// Returns Color.black if the array is null or empty.
		/// </summary>
		public static Color AverageColour(Color[] value)
		{
			if (value == null || value.Length == 0)
				return Color.black;

			return value.Aggregate(Color.black, (acc, col) => acc + col) / value.Length;
		}

		public static Color ThresholdColour(Color[] colours, float threshold = 0.85f)
		{
			if (colours == null || colours.Length == 0)
				return Color.white;

			var (cutoff, luminances) = GetLuminancesAndCutoff(colours, threshold);
			if (cutoff < 0f)
				return Color.white;

			// LINQ version: filter bright pixels and compute simple average
			var brightColors = colours.Where((col, i) => luminances[i] >= cutoff);

			if (!brightColors.Any())
				return Color.white;

			return brightColors.Aggregate(Color.black, (acc, col) => acc + col) / brightColors.Count();
		}

		public static Color ThresholdLuminance(Color[] colours, float threshold = 0.85f)
		{
			if (colours == null || colours.Length == 0)
				return Color.white;

			var (cutoff, luminances) = GetLuminancesAndCutoff(colours, threshold);
			if (cutoff < 0f)
				return Color.white;

			// LINQ version: weighted average of bright pixels
			var brightPairs = colours.Zip(luminances, (col, lum) => new { col, lum })
									.Where(x => x.lum >= cutoff);

			if (!brightPairs.Any())
				return Color.white;

			var sum = brightPairs.Aggregate(Color.black, (acc, x) => acc + x.col * x.lum);
			var weightSum = brightPairs.Sum(x => x.lum);

			return weightSum > 0f ? sum / weightSum : Color.white;
		}

		/// <summary>
		/// Computes luminance for every color once and returns both the cutoff and the luminance array.
		/// Returns cutoff = -1 if all pixels are black.
		/// </summary>
		private static (float cutoff, float[] luminances) GetLuminancesAndCutoff(Color[] colours, float threshold)
		{
			var luminances = new float[colours.Length];
			float maxLum = 0f;

			for (int i = 0; i < colours.Length; i++)
			{
				float lum = colours[i].Luminance();
				luminances[i] = lum;
				if (lum > maxLum)
					maxLum = lum;
			}

			if (maxLum <= 0f)
				return (-1f, luminances);

			return (maxLum * threshold, luminances);
		}

		/// <summary>
		/// Computes a luminance power-weighted average colour.
		/// Brighter pixels dominate the result (exactly what you asked for).
		/// Perfect for ambient light from a skybox.
		/// </summary>
		/// <param name="power">1.0 = normal average, 1.5–2.5 = bright pixels pull much harder</param>
		public static Color ComputeAmbientColor(Color[] colours, float power = 1.1f)
		{
			if (colours == null || colours.Length == 0)
				return new Color(0.25f, 0.25f, 0.35f); // soft neutral fallback

			Color weightedSum = Color.black;
			float totalWeight = 0f;

			for (int i = 0; i < colours.Length; i++)
			{
				Color c = colours[i];
				float lum = c.Luminance();

				if (lum < 0.001f) continue; // ignore pure black / near-black noise

				float weight = Mathf.Pow(lum, power);   // ← this is the key line

				weightedSum += c * weight;
				totalWeight += weight;
			}

			if (totalWeight < 0.0001f)
				return new Color(0.25f, 0.25f, 0.35f);

			Color ambient = weightedSum / totalWeight;

			// Post-process for nicer "ambient fill" feel in AmbientMode.Flat
			float avgLum = ambient.Luminance();
			ambient = Color.Lerp(ambient, new Color(avgLum, avgLum, avgLum), 0.30f); // mild desaturation
			ambient *= 1.15f;                                                       // gentle lift

			ambient.r = Mathf.Clamp01(ambient.r);
			ambient.g = Mathf.Clamp01(ambient.g);
			ambient.b = Mathf.Clamp01(ambient.b);
			ambient.a = 1f;

			return ambient;
		}
	}
}