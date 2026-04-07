using UnityEngine;
using System.Linq;

namespace MassiveHadronLtd
{
	public static class ColourUtils
	{
		public enum ClampMode
		{
			/// <summary>Hard clamp: anything >1 becomes 1.0, anything <0 becomes 0.0</summary>
			SimpleClamp,

			/// <summary>Scales the color so the brightest component = 1.0 (best for preserving vibrancy)</summary>
			ClampToMax,

			/// <summary>Simple Reinhard tone mapping (smooth highlight compression)</summary>
			Reinhard
		}

		/// <summary>
		/// Clamps an HDR Color to the 0-1 range before conversion to LDR (e.g. for hex, UI, etc.).
		/// Uses extension method so you can call it as color.Clamp().
		/// </summary>
		/// <param name="color">The color to clamp (supports HDR values > 1)</param>
		/// <param name="mode">How to handle values above 1.0</param>
		/// <returns>A new Color with all components clamped to [0, 1]</returns>
		public static Color Clamp(this Color color, ClampMode mode = ClampMode.SimpleClamp)
		{
			switch (mode)
			{
				case ClampMode.SimpleClamp:
					return new Color(
						Mathf.Clamp01(color.r),
						Mathf.Clamp01(color.g),
						Mathf.Clamp01(color.b),
						Mathf.Clamp01(color.a)
					);

				case ClampMode.ClampToMax:
					// Preserves hue and saturation by scaling based on the brightest channel
					float max = Mathf.Max(color.r, color.g, color.b);
					if (max <= 1f)
						return color; // already in range or negative (will be clamped below)

					float scale = 1f / max;
					return new Color(
						color.r * scale,
						color.g * scale,
						color.b * scale,
						Mathf.Clamp01(color.a)
					);

				case ClampMode.Reinhard:
					// Simple Reinhard tone mapping - good for natural highlight roll-off
					return new Color(
						color.r / (1f + color.r),
						color.g / (1f + color.g),
						color.b / (1f + color.b),
						Mathf.Clamp01(color.a)
					);

				default:
					return color;
			}
		}


		/// <summary>
		/// Clamps all components of the color to the [0, 1] range.
		/// Useful for HDR colors before converting to hex, Color32, etc.
		/// </summary>
		public static Color Clamp(this Color color)
		{
			return new Color(
				Mathf.Clamp01(color.r),
				Mathf.Clamp01(color.g),
				Mathf.Clamp01(color.b),
				Mathf.Clamp01(color.a)
			);
		}

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