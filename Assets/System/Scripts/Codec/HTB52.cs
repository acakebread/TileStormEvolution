using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

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

		// ---------------------------------
		// ENCODE BigInteger
		// ---------------------------------
		public static string Encode(BigInteger value, bool appendFlavor = false)
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value));

			if (value == 0)
				return appendFlavor ? Alphabet[0] + "_" + Flavor : Alphabet[0].ToString();

			var sb = new StringBuilder();
			BigInteger current = value;

			while (current > 0)
			{
				current = BigInteger.DivRem(current, 52, out var rem);
				sb.Insert(0, Alphabet[(int)rem]);
			}

			if (appendFlavor)
				sb.Append("_").Append(Flavor);

			return sb.ToString();
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