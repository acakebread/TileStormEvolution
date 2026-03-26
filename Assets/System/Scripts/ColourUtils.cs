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
	}
}