// Copyright 2016 massivehadron.com ltd. 
// Refactored for modern usage by MassiveHadronLtd
// Extended with Color ↔ Hex conversion 2025/2026

using System;
using System.Security.Cryptography;
using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// General-purpose string and text utilities.
	/// </summary>
	public static class StringUtil
	{
		// ── Clean string ─────────────────────────────
		public static string Clean(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";
			return input.Trim()
						.Replace("\u00A0", " ")
						.Replace("\u200B", "")
						.Replace("\uFEFF", "");
		}

		public static bool CleanEquals(string a, string b)
		{
			return string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);
		}

		// ── Random HEX generator ─────────────────────
		public static string GenerateRandomHex64()
		{
			byte[] bytes = new byte[8];
			System.Random rng = new System.Random();
			rng.NextBytes(bytes);
			return BitConverter.ToUInt64(bytes, 0).ToString("X16");
		}

		public static string GenerateAssetId()
		{
			byte[] bytes = new byte[8];
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(bytes);
			}
			return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
		}

		// ── Money / Number formatting ───────────────
		public static string FormatNumber(int number)
		{
			if (number == 0) return "0";

			string sign = number < 0 ? "-" : "";
			number = Mathf.Abs(number);

			string str = "";
			while (number != 0)
			{
				if (number >= 1000)
					str = "," + (number % 1000).ToString("000") + str;
				else
					str = number.ToString() + str;

				number /= 1000;
			}

			return sign + str;
		}

		public static string FormatMoney(int value)
		{
			return "$" + FormatNumber(value);
		}

		// ── Time formatting ─────────────────────────
		public static string FormatTime(int duration)
		{
			return (duration / 3600).ToString() + ":"
				 + ((duration / 60) % 60).ToString("00") + ":"
				 + (duration % 60).ToString("00");
		}

		public static string FormatTimeMinutesSeconds(int duration)
		{
			int minutes = Mathf.FloorToInt(duration / 60F);
			int seconds = Mathf.FloorToInt(duration - minutes * 60);
			return string.Format("{0:0}:{1:00}", minutes, seconds);
		}

		// ── Text formatting ─────────────────────────
		public static string ToTitleCase(string str)
		{
			if (string.IsNullOrEmpty(str)) return "";
			return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
		}

		// ── Color <-> Hex conversion ────────────────

		/// <summary>
		/// Converts a Unity Color to a web-style hex string.
		/// </summary>
		/// <param name="color">The color to convert</param>
		/// <param name="includeAlpha">If true, includes alpha as #RRGGBBAA. If false, uses #RRGGBB (unless alpha &lt; 1, then still includes alpha).</param>
		/// <returns>Hex color string starting with # (uppercase)</returns>
		public static string ToHexString(this Color color, bool includeAlpha = true)
		{
			// Clamp HDR values before conversion
			Color clamped = color.Clamp();

			byte r = (byte)Mathf.RoundToInt(clamped.r * 255f);
			byte g = (byte)Mathf.RoundToInt(clamped.g * 255f);
			byte b = (byte)Mathf.RoundToInt(clamped.b * 255f);
			byte a = (byte)Mathf.RoundToInt(clamped.a * 255f);

			if (includeAlpha && a < 255)
			{
				return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
			}
			else
			{
				return $"#{r:X2}{g:X2}{b:X2}";
			}
		}

		/// <summary>
		/// Parses a web-style hex color string into a Unity Color.
		/// Supports: #RGB, #RGBA, #RRGGBB, #RRGGBBAA (with or without #)
		/// </summary>
		/// <param name="hex">Hex string (e.g. "#FF0000", "00FF00FF", "#1E90FF")</param>
		/// <param name="defaultColor">Color to return if parsing fails</param>
		/// <returns>Parsed Color (or defaultColor on failure)</returns>
		public static Color FromHexString(string hex, Color defaultColor = default)
		{
			if (string.IsNullOrWhiteSpace(hex))
				return defaultColor;

			// Remove # if present and trim whitespace
			hex = hex.Trim().TrimStart('#');

			if (hex.Length != 3 && hex.Length != 4 && hex.Length != 6 && hex.Length != 8)
				return defaultColor;

			try
			{
				int r, g, b, a = 255;

				if (hex.Length == 3 || hex.Length == 4) // shorthand
				{
					r = Convert.ToInt32(hex[0].ToString() + hex[0], 16);
					g = Convert.ToInt32(hex[1].ToString() + hex[1], 16);
					b = Convert.ToInt32(hex[2].ToString() + hex[2], 16);

					if (hex.Length == 4)
						a = Convert.ToInt32(hex[3].ToString() + hex[3], 16);
				}
				else // full 6 or 8 digits
				{
					r = Convert.ToInt32(hex.Substring(0, 2), 16);
					g = Convert.ToInt32(hex.Substring(2, 2), 16);
					b = Convert.ToInt32(hex.Substring(4, 2), 16);

					if (hex.Length == 8)
						a = Convert.ToInt32(hex.Substring(6, 2), 16);
				}

				return new Color(
					r / 255f,
					g / 255f,
					b / 255f,
					a / 255f
				);
			}
			catch
			{
				return defaultColor;
			}
		}

		// Convenience overload with alpha control on parse
		public static Color FromHexString(string hex, bool assumeOpaqueIfMissingAlpha = true, Color defaultColor = default)
		{
			Color c = FromHexString(hex, defaultColor);
			if (assumeOpaqueIfMissingAlpha && c.a == 0f && hex.TrimStart('#').Length <= 6)
			{
				c.a = 1f;
			}
			return c;
		}

		/// <summary>
		/// Formats a float cleanly: whole numbers are shown without decimal point (e.g. 90.0 → 90),
		/// while non-whole numbers keep limited decimal places.
		/// Useful for angles, positions, scales, etc.
		/// </summary>
		public static string ToCleanString(this float value, int maxDecimals = 1, float epsilon = 0.0001f)
		{
			if (float.IsNaN(value) || float.IsInfinity(value))
				return value.ToString(System.Globalization.CultureInfo.InvariantCulture);

			float rounded = Mathf.Round(value);

			if (Mathf.Abs(value - rounded) < epsilon)
			{
				return rounded.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
			}

			return value.ToString($"F{maxDecimals}", System.Globalization.CultureInfo.InvariantCulture);
		}
	}
}
