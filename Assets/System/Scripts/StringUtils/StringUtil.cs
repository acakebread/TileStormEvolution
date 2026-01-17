// Copyright 2016 massivehadron.com ltd. 
// Refactored for modern usage by MassiveHadronLtd

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
		/// <summary>
		/// Trims a string and removes invisible or unwanted characters.
		/// </summary>
		public static string Clean(string input)
		{
			if (string.IsNullOrEmpty(input)) return "";
			return input.Trim()
						.Replace("\u00A0", " ")  // non-breaking space
						.Replace("\u200B", "")   // zero-width space
						.Replace("\uFEFF", "");  // BOM / zero-width no-break space
		}

		/// <summary>
		/// Compares two strings for equality after cleaning, ignoring case.
		/// </summary>
		public static bool CleanEquals(string a, string b)
		{
			return string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);
		}

		// ── Random HEX generator ─────────────────────
		/// <summary>
		/// Generates a random 64-bit value as a 16-character uppercase HEX string.
		/// </summary>
		public static string GenerateRandomHex64()
		{
			byte[] bytes = new byte[8];
			System.Random rng = new System.Random();
			rng.NextBytes(bytes);
			return BitConverter.ToUInt64(bytes, 0).ToString("X16");
		}

		/// <summary>
		/// Generates a random 16-character uppercase HEX string for game asset IDs.
		/// Works in all Unity versions that support System.Security.Cryptography.
		/// </summary>
		public static string GenerateAssetId()
		{
			byte[] bytes = new byte[8];

			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(bytes);
			}

			// This ToString format works everywhere in Unity
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
			return (duration / 3600).ToString() + ":" + ((duration / 60) % 60).ToString("00") + ":" + (duration % 60).ToString("00");
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
	}
}
