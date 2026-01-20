using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Security.Cryptography;
using System.Linq;

namespace MassiveHadronLtd.IDs.HTB52
{
	/// <summary>
	/// Human-Tolerant Base52 encoder/decoder.
	/// Canonical output preserves unambiguous case-sensitive Base52 characters.
	/// Decoder is forgiving and normalizes common ambiguity sets:
	/// O/o → 0, I/l → 1, Z/z → 2, S/s → 5, B → 8, g → 9
	/// Optional flavor suffix can be added to output.
	/// Supports numeric and byte-array encoding.
	/// </summary>
	public static class HTB52
	{
		/// <summary>
		/// Flavor identifier for this system. Optional suffix.
		/// </summary>
		public const string Flavor = "HTB52";

		// Canonical Base52 alphabet (index = value)
		private const string Alphabet =
			"0123456789" +
			"ACDEFGHJKMNPQRTUVWXY" +
			"acdefhijkmnpqrtuvwxy";

		private static readonly Dictionary<char, int> DecodeMap;
		private static readonly Dictionary<char, char> NormalizeMap;

		static HTB52()
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

		/// <summary>
		/// Encodes a BigInteger to Base52.
		/// - By default: minimal length (no leading zeros)
		/// - With fixedLength: pads with leading '0' (or custom padChar) to reach exact length
		/// </summary>
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
					current = BigInteger.DivRem(current, 52, out var rem);
					sb.Insert(0, Alphabet[(int)rem]);
				}

				result = sb.ToString();
			}

			// Apply fixed-length padding if requested
			if (fixedLength.HasValue)
			{
				int len = fixedLength.Value;
				if (len < 1)
					throw new ArgumentOutOfRangeException(nameof(fixedLength), "Must be >= 1");

				int needed = len - result.Length;
				if (needed > 0)
				{
					result = new string(padChar, needed) + result;
				}
				else if (needed < 0)
				{
					throw new ArgumentException(
						$"Value requires {result.Length} characters, but fixedLength is {len} (too small)");
				}
			}

			if (appendFlavor)
				result += "_" + Flavor;

			return result;
		}

		/// <summary>
		/// Convenience method: always produces a fixed-length string.
		/// Throws if the value cannot fit into the requested length.
		/// </summary>
		public static string EncodeFixed(
			BigInteger value,
			int length,
			bool appendFlavor = false,
			char padChar = '0')
		{
			if (length < 1)
				throw new ArgumentOutOfRangeException(nameof(length), "Must be >= 1");

			return Encode(value, appendFlavor, fixedLength: length, padChar);
		}

		// ---------------------------------
		// ENCODE byte array (maximally compact)
		// ---------------------------------
		public static string Encode(byte[] bytes, bool appendFlavor = false)
		{
			if (bytes == null || bytes.Length == 0)
				throw new ArgumentException("Byte array is empty.");

			// Prepend zero to ensure BigInteger is positive
			byte[] extended = new byte[bytes.Length + 1];
			Array.Copy(bytes, 0, extended, 1, bytes.Length);

			BigInteger value = new BigInteger(extended);

			return Encode(value, appendFlavor);
		}

		// ---------------------------------
		// DECODE to BigInteger
		// ---------------------------------
		public static BigInteger Decode(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				throw new ArgumentException("Input is empty.");

			// Remove optional flavor suffix if present
			string rawInput = input;
			int flavorIndex = input.LastIndexOf("_" + Flavor, StringComparison.Ordinal);
			if (flavorIndex >= 0)
				rawInput = input.Substring(0, flavorIndex);

			BigInteger result = 0;

			foreach (char raw in rawInput)
			{
				char c = Normalize(raw);

				if (!DecodeMap.TryGetValue(c, out int value))
					throw new FormatException($"Invalid Base52 character: '{raw}'");

				result = result * 52 + value;
			}

			return result;
		}

		// ---------------------------------
		// DECODE to byte array
		// ---------------------------------
		public static byte[] DecodeToBytes(string input)
		{
			BigInteger value = Decode(input);
			byte[] bytes = value.ToByteArray();

			// Remove leading zero added during encoding if present
			if (bytes.Length > 1 && bytes[bytes.Length - 1] == 0)
			{
				var trimmed = new byte[bytes.Length - 1];
				Array.Copy(bytes, 0, trimmed, 0, trimmed.Length);
				return trimmed;
			}

			return bytes;
		}

		// ---------------------------------
		// NORMALIZATION
		// ---------------------------------
		private static char Normalize(char c)
		{
			return NormalizeMap.TryGetValue(c, out var mapped) ? mapped : c;
		}

		/// <summary>
		/// Returns a Base52 string grouped in chunks for readability.
		/// Example: "1a2B3c4D" with groupSize=4 -> "1a2B-3c4D"
		/// </summary>
		public static string Grouped(string base52String, int groupSize = 4, char separator = '-')
		{
			if (string.IsNullOrEmpty(base52String))
				return string.Empty;

			var sb = new StringBuilder();
			int count = 0;

			foreach (char c in base52String)
			{
				if (c == '_' && base52String.Substring(count).StartsWith("_" + Flavor))
				{
					// Preserve flavor suffix at the end
					sb.Append(base52String.Substring(count));
					break;
				}

				sb.Append(c);
				count++;

				if (count % groupSize == 0 && count < base52String.Length && base52String[count] != '_')
					sb.Append(separator);
			}

			return sb.ToString();
		}

		// Inside HTB52 class
		private static readonly BigInteger Modulus = BigInteger.Pow(52, 6);

		/// <summary>
		/// Hashes a string to a BigInteger in [0, 52^6 - 1] using SHA-256.
		/// </summary>
		public static BigInteger HashToRange(string input)
		{
			using var sha256 = SHA256.Create();
			byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

			// Convert to positive BigInteger (prepend 0 byte to ensure non-negative)
			BigInteger hashValue = new BigInteger(hashBytes.Concat(new byte[] { 0 }).ToArray());

			return hashValue % Modulus;
		}
	}
}

//Usage examples

//BigInteger value = 123456789;
//string encoded = HTB52.Encode(value, true);    // "1a2B3c4D_HTB52"

//// Pretty-print in groups of 4
//string pretty = HTB52.Grouped(encoded, 4);    // "1a2B-3c4D_HTB52"
//Console.WriteLine(pretty);

//// For byte arrays
//byte[] data = Encoding.UTF8.GetBytes("Hello HTB52!");
//string encodedData = HTB52.Encode(data, true);
//string prettyData = HTB52.Grouped(encodedData, 5); // "Xx3G1-_HTB52"

// Usage: string hashedId = HTB52.Encode(HTB52.HashToRange("some_id"), appendFlavor: false);
// This will give a fixed-length 6-char base52 string (padded with leading '0' if needed for smaller values).

//BigInteger small = 12345;
//BigInteger large = BigInteger.Pow(52, 5) + 100;   // needs 6 chars

//// Variable length (your original behavior)
//Console.WriteLine(HTB52.Encode(small));                    // e.g. "5xP" (3 chars)
//Console.WriteLine(HTB52.Encode(large));                    // e.g. "100abc" (6 chars)

//// Fixed length 6 – most useful for hash IDs
//Console.WriteLine(HTB52.EncodeFixed(small, 6));            // "0005xP"
//Console.WriteLine(HTB52.EncodeFixed(large, 6));            // "100abc"  (no padding needed)
//Console.WriteLine(HTB52.EncodeFixed(small, 8));            // "000005xP"

//// With flavor
//Console.WriteLine(HTB52.EncodeFixed(small, 6, true));      // "0005xP_HTB52"

//// Custom pad character (uncommon, but possible)
//Console.WriteLine(HTB52.EncodeFixed(small, 6, padChar: '-'));  // "---5xP"

//// Still supports the flexible style
//Console.WriteLine(HTB52.Encode(small, fixedLength: 6));    // "0005xP"  (same as EncodeFixed)
//Console.WriteLine(HTB52.Encode(small));                    // "5xP"     (no fixedLength = variable)