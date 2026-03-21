using UnityEngine;

namespace MassiveHadronLtd
{
	public static class PrecisionUtil
	{
		/// <summary>
		/// Rounds each component of a Vector3 to the specified number of decimal places.
		/// Uses normal mathematical rounding (round half away from zero / banker's rounding depending on .NET version).
		/// </summary>
		public static Vector3 Round(Vector3 value, int decimalPlaces)
		{
			if (decimalPlaces < 0) decimalPlaces = 0;

			float multiplier = Mathf.Pow(10f, decimalPlaces);

			return new Vector3(
				Mathf.Round(value.x * multiplier) / multiplier,
				Mathf.Round(value.y * multiplier) / multiplier,
				Mathf.Round(value.z * multiplier) / multiplier
			);
		}

		/// <summary>
		/// Rounds a single float to the specified number of decimal places.
		/// </summary>
		public static float Round(float value, int decimalPlaces)
		{
			if (decimalPlaces < 0) decimalPlaces = 0;

			float multiplier = Mathf.Pow(10f, decimalPlaces);
			return Mathf.Round(value * multiplier) / multiplier;
		}
	}
}