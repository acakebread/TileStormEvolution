using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Security.Cryptography;
using System.Linq;

namespace MassiveHadronLtd.IDs.HTB50
{
	/*
	 * ─────────────────────────────────────────────────────────────────────────────
	 * HTB50 — Human-Tolerant Base-50 (v1.0)
	 * ─────────────────────────────────────────────────────────────────────────────
	 *
	 * PURPOSE
	 * -------
	 * HTB50 is a human-tolerant positional encoding designed for identifiers that
	 * may be read, transcribed, copied, or visually inspected by humans.
	 *
	 * Unlike traditional base encodings (Base32 / Base52 / Base58 / Base64),
	 * HTB50 aggressively removes all widely accepted visual-ambiguity collisions
	 * from its *canonical alphabet*, while still accepting them during decoding.
	 *
	 *
	 * COLLISION PHILOSOPHY
	 * -------------------
	 * The following character sets are treated as visually equivalent and are
	 * therefore collapsed to a single canonical value:
	 *
	 *   { 0, O, o }
	 *   { 1, I, l }
	 *   { 2, Z, z }
	 *   { 5, S, s }
	 *   { 8, B }
	 *   { 9, g }
	 *
	 * Canonical output contains ONLY unambiguous characters.
	 * Decoder normalization accepts all members of each collision set.
	 *
	 *
	 * RESULTING RADIX
	 * ---------------
	 * Starting from full alphanumeric (0-9, A-Z, a-z = 62 symbols),
	 * aggressive collision removal yields exactly:
	 *
	 *   50 canonical symbols  →  Base-50
	 *
	 * This loss of radix is intentional and traded for maximal human safety.
	 *
	 *
	 * DESIGN GOALS
	 * ------------
	 * ✓ Canonical output (single preferred form)
	 * ✓ Tolerant decoding (accepts common mistakes)
	 * ✓ Case-sensitive where safe
	 * ✓ No visually ambiguous glyphs in output
	 * ✓ Deterministic, reversible encoding
	 * ✓ Suitable for IDs, filenames, text formats
	 *
	 *
	 * NON-GOALS
	 * ---------
	 * ✗ Error detection / checksum (by design)
	 * ✗ Cryptographic guarantees
	 *
	 * VERSIONING
	 * ----------
	 * v1.0 — Alphabet, normalization sets, and radix are frozen.
	 * Any future change MUST introduce a new flavor suffix.
	 *
	 * ─────────────────────────────────────────────────────────────────────────────
	 */

	public static class HTB50
	{
		/// <summary>Flavor suffix for optional namespacing.</summary>
		public const string Flavor = "HTB50";

		/// <summary>
		/// Canonical Base-50 alphabet (index == numeric value).
		/// Length MUST remain exactly 50.
		/// </summary>
		private const string Alphabet =
			"0123456789" +                  // 10 digits
			"ACDEFGHJKMNPQRTUVWXY" +         // 20 uppercase (ambiguous removed)
			"acdefhijkmnpqrtuvwxy";          // 20 lowercase (ambiguous removed)

		private const int Radix = 50;

		private static readonly Dictionary<char, int> DecodeMap;
		private static readonly Dictionary<char, char> NormalizeMap;

		static HTB50()
		{
			DecodeMap = new Dictionary<char, int>(Alphabet.Length);
			for (int i = 0; i < Alphabet.Length; i++)
				DecodeMap[Alphabet[i]] = i;

			NormalizeMap = new Dictionary<char, char>
			{
				// Zero
				['O'] = '0',
				['o'] = '0',
				// One
				['I'] = '1',
				['l'] = '1',
				// Two
				['Z'] = '2',
				['z'] = '2',
				// Five
				['S'] = '5',
				['s'] = '5',
				// Eight
				['B'] = '8',
				// Nine
				['g'] = '9'
			};
		}

		// ─────────────────────────────────────────────────────────────────────────
		// ENCODE — BigInteger
		// ─────────────────────────────────────────────────────────────────────────

		public static string Encode(
			BigInteger value,
			bool appendFlavor = false,
			int? fixedLength = null,
			char padChar = '0')
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value));

			string result;

			if (value == 0)
			{
				result = padChar.ToString();
			}
			else
			{
				var sb = new StringBuilder();
				BigInteger current = value;

				while (current > 0)
				{
					current = BigInteger.DivRem(current, Radix, out var rem);
					int digit = (int)rem;

					if (digit < 0 || digit >= Alphabet.Length)
						throw new InvalidOperationException(
							$"Invalid HTB50 digit {digit} (alphabet length {Alphabet.Length})");

					sb.Insert(0, Alphabet[digit]);
				}

				result = sb.ToString();
			}

			// Optional fixed-length padding
			if (fixedLength.HasValue)
			{
				int needed = fixedLength.Value - result.Length;
				if (needed < 0)
					throw new ArgumentException("Value exceeds fixed length");

				if (needed > 0)
					result = new string(padChar, needed) + result;
			}

			if (appendFlavor)
				result += "_" + Flavor;

			return result;
		}

		public static string EncodeFixed(
			BigInteger value,
			int length,
			bool appendFlavor = false,
			char padChar = '0')
		{
			if (length < 1)
				throw new ArgumentOutOfRangeException(nameof(length));

			return Encode(value, appendFlavor, fixedLength: length, padChar);
		}

		// ─────────────────────────────────────────────────────────────────────────
		// ENCODE — byte[]
		// ─────────────────────────────────────────────────────────────────────────

		public static string Encode(byte[] bytes, bool appendFlavor = false)
		{
			if (bytes == null || bytes.Length == 0)
				throw new ArgumentException("Byte array is empty");

			// Ensure positive BigInteger
			byte[] extended = new byte[bytes.Length + 1];
			Array.Copy(bytes, 0, extended, 1, bytes.Length);

			BigInteger value = new BigInteger(extended);
			return Encode(value, appendFlavor);
		}

		// ─────────────────────────────────────────────────────────────────────────
		// DECODE — BigInteger
		// ─────────────────────────────────────────────────────────────────────────

		public static BigInteger Decode(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				throw new ArgumentException("Input is empty");

			// Strip optional flavor
			string raw = input;
			int flavorIndex = input.LastIndexOf("_" + Flavor, StringComparison.Ordinal);
			if (flavorIndex >= 0)
				raw = input.Substring(0, flavorIndex);

			BigInteger result = 0;

			foreach (char r in raw)
			{
				char c = Normalize(r);

				if (!DecodeMap.TryGetValue(c, out int value))
					throw new FormatException($"Invalid HTB50 character '{r}'");

				result = result * Radix + value;
			}

			return result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// DECODE — byte[]
		// ─────────────────────────────────────────────────────────────────────────

		public static byte[] DecodeToBytes(string input)
		{
			BigInteger value = Decode(input);
			byte[] bytes = value.ToByteArray();

			// Remove leading zero if present
			if (bytes.Length > 1 && bytes[^1] == 0)
			{
				var trimmed = new byte[bytes.Length - 1];
				Array.Copy(bytes, trimmed, trimmed.Length);
				return trimmed;
			}

			return bytes;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// HASH → RANGE (fixed-length ID helper)
		// ─────────────────────────────────────────────────────────────────────────

		public static readonly BigInteger Modulus = BigInteger.Pow(Radix, 6);

		public static BigInteger HashToRange(string input)
		{
			if (string.IsNullOrEmpty(input))
				return BigInteger.Zero;

			using var sha = SHA256.Create();
			byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

			byte[] positive = new byte[hash.Length + 1];
			Array.Copy(hash, 0, positive, 1, hash.Length);

			BigInteger value = new BigInteger(positive);
			BigInteger result = value % Modulus;

			if (result < 0)
				result += Modulus;

			return result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// PRETTY-PRINTING
		// ─────────────────────────────────────────────────────────────────────────

		public static string Grouped(string htb50, int groupSize = 4, char separator = '-')
		{
			if (string.IsNullOrEmpty(htb50))
				return string.Empty;

			var sb = new StringBuilder();
			int count = 0;

			foreach (char c in htb50)
			{
				sb.Append(c);
				count++;

				if (count % groupSize == 0 && count < htb50.Length && htb50[count] != '_')
					sb.Append(separator);
			}

			return sb.ToString();
		}

		// ─────────────────────────────────────────────────────────────────────────
		// NORMALIZATION
		// ─────────────────────────────────────────────────────────────────────────

		private static char Normalize(char c)
		{
			return NormalizeMap.TryGetValue(c, out var mapped) ? mapped : c;
		}

		/// <summary>
		/// Generates a random HTB50 ID in the range [0, Modulus-1], encoded to 6 characters (padded with leading '0' if needed).
		/// Uses cryptographically secure random bytes.
		/// </summary>
		public static string GenerateRandomId(int length = 6, bool appendFlavor = false, char padChar = '0')
		{
			// Generate random bytes (8 bytes = 64 bits, plenty for 50^6 ≈ 15.4 million)
			byte[] randomBytes = new byte[8];
			using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
			{
				rng.GetBytes(randomBytes);
			}

			// Convert to BigInteger (ensure positive)
			BigInteger randValue = new BigInteger(randomBytes.Concat(new byte[] { 0 }).ToArray());

			// Modulo to fit within Modulus range
			randValue = randValue % Modulus;
			if (randValue < 0) randValue += Modulus; // ensure non-negative

			// Encode to fixed-length HTB50 string
			return EncodeFixed(randValue, length, appendFlavor, padChar);
		}
	}
}
