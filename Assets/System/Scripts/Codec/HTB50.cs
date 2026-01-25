using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace MassiveHadronLtd
{
	/*
     * HTB50 — Human-Tolerant Base-50 (v1.0)
     * Human-readable, ambiguity-resistant base-50 encoding
     * Canonical output uses only unambiguous characters
     * Decoder accepts common visual confusions (0/O/o, 1/I/l, etc.)
     */

	public static class HTB50
	{
		public const string Flavor = "HTB50";

		private const string Alphabet =
			"0123456789" +
			"ACDEFGHJKMNPQRTUVWXY" +
			"acdefhijkmnpqrtuvwxy";

		public static readonly int Radix = 50;

		private static readonly Dictionary<char, int> DecodeMap;
		private static readonly Dictionary<char, char> NormalizeMap;

		static HTB50()
		{
			DecodeMap = new Dictionary<char, int>(Alphabet.Length);
			for (int i = 0; i < Alphabet.Length; i++)
				DecodeMap[Alphabet[i]] = i;

			NormalizeMap = new Dictionary<char, char>
			{
				['O'] = '0',
				['o'] = '0',
				['I'] = '1',
				['l'] = '1',
				['Z'] = '2',
				['z'] = '2',
				['S'] = '5',
				['s'] = '5',
				['B'] = '8',
				['g'] = '9'
			};
		}

		// ─────────────────────────────────────────────────────────────────────────
		// ENCODE — BigInteger (unsigned / positive only)
		// ─────────────────────────────────────────────────────────────────────────

		public static string EncodeBigInteger(
			BigInteger value,
			bool appendFlavor = false,
			int? fixedLength = null,
			char padChar = '0')
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value), "BigInteger must be non-negative");

			string result = value == 0 ? padChar.ToString() : BuildBase50(value);

			ApplyPadding(ref result, fixedLength, padChar);

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

			return EncodeBigInteger(value, appendFlavor, length, padChar);
		}

		// ─────────────────────────────────────────────────────────────────────────
		// ENCODE — Int32 with fixed length (full 32-bit range, including negatives)
		// ─────────────────────────────────────────────────────────────────────────

		public static string EncodeFixed(
			int value,
			int length,
			bool appendFlavor = false,
			char padChar = '0')
		{
			if (length < 1)
				throw new ArgumentOutOfRangeException(nameof(length));

			uint uvalue = (uint)value;

			string result = uvalue == 0
				? padChar.ToString()
				: BuildBase50(uvalue);

			// Reuse the shared helper (same as Encode(int?, ...) and EncodeBigInteger)
			ApplyPadding(ref result, fixedLength: length, padChar);

			if (appendFlavor)
				result += "_" + Flavor;

			return result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// ENCODE — Int64 (full 64-bit range, including negatives)
		// ─────────────────────────────────────────────────────────────────────────

		public static string Encode64(
			long value,
			bool appendFlavor = false,
			int? fixedLength = null,
			char padChar = '0')
		{
			ulong uvalue = (ulong)value;
			string result = uvalue == 0 ? padChar.ToString() : BuildBase50(uvalue);

			ApplyPadding(ref result, fixedLength, padChar);

			if (appendFlavor)
				result += "_" + Flavor;

			return result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// ENCODE — Int32 (full 32-bit range, including negatives)
		// ─────────────────────────────────────────────────────────────────────────

		public static string Encode(
			int value,
			bool appendFlavor = false,
			int? fixedLength = null,
			char padChar = '0')
		{
			uint uvalue = (uint)value;
			string result = uvalue == 0 ? padChar.ToString() : BuildBase50(uvalue);

			ApplyPadding(ref result, fixedLength, padChar);

			if (appendFlavor)
				result += "_" + Flavor;

			return result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// ENCODE — byte[]
		// ─────────────────────────────────────────────────────────────────────────

		public static string Encode(byte[] bytes, bool appendFlavor = false)
		{
			if (bytes == null || bytes.Length == 0)
				throw new ArgumentException("Byte array cannot be null or empty", nameof(bytes));

			byte[] extended = new byte[bytes.Length + 1];
			Array.Copy(bytes, 0, extended, 1, bytes.Length);

			BigInteger value = new BigInteger(extended);
			return EncodeBigInteger(value, appendFlavor);
		}

		// ─────────────────────────────────────────────────────────────────────────
		// DECODE — BigInteger
		// ─────────────────────────────────────────────────────────────────────────

		public static BigInteger DecodeBigInteger(string input)
		{
			string raw = StripFlavor(input);
			BigInteger result = 0;

			foreach (char r in raw)
			{
				char c = Normalize(r);
				if (!DecodeMap.TryGetValue(c, out int digit))
					throw new FormatException($"Invalid HTB50 character '{r}'");

				result = result * Radix + digit;
			}

			return result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// DECODE — Int64
		// ─────────────────────────────────────────────────────────────────────────

		public static long Decode64(string input)
		{
			string raw = StripFlavor(input);
			ulong result = 0;

			foreach (char r in raw)
			{
				char c = Normalize(r);
				if (!DecodeMap.TryGetValue(c, out int digit))
					throw new FormatException($"Invalid HTB50 character '{r}'");

				ulong d = (ulong)digit;
				if (result > (ulong.MaxValue - d) / (ulong)Radix)
					throw new OverflowException("Decoded value exceeds UInt64 range");

				result = result * (ulong)Radix + d;
			}

			return (long)result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// DECODE — Int32
		// ─────────────────────────────────────────────────────────────────────────

		public static int Decode(string input)
		{
			string raw = StripFlavor(input);
			uint result = 0;

			foreach (char r in raw)
			{
				char c = Normalize(r);
				if (!DecodeMap.TryGetValue(c, out int digit))
					throw new FormatException($"Invalid HTB50 character '{r}'");

				uint d = (uint)digit;
				if (result > (uint.MaxValue - d) / (uint)Radix)
					throw new OverflowException("Decoded value exceeds UInt32 range");

				result = result * (uint)Radix + d;
			}

			return (int)result;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// DECODE — byte[]
		// ─────────────────────────────────────────────────────────────────────────

		public static byte[] DecodeToBytes(string input)
		{
			BigInteger value = DecodeBigInteger(input);
			byte[] bytes = value.ToByteArray();

			if (bytes.Length > 1 && bytes[^1] == 0)
			{
				var trimmed = new byte[bytes.Length - 1];
				Array.Copy(bytes, trimmed, trimmed.Length);
				return trimmed;
			}

			return bytes;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// Internal helpers
		// ─────────────────────────────────────────────────────────────────────────

		private static string BuildBase50(uint number)
		{
			if (number == 0) return string.Empty;

			var sb = new StringBuilder();
			while (number > 0)
			{
				uint rem = number % (uint)Radix;
				number /= (uint)Radix;
				sb.Insert(0, Alphabet[(int)rem]);
			}
			return sb.ToString();
		}

		private static string BuildBase50(ulong number)
		{
			if (number == 0) return string.Empty;

			var sb = new StringBuilder();
			while (number > 0)
			{
				ulong rem = number % (ulong)Radix;
				number /= (ulong)Radix;
				sb.Insert(0, Alphabet[(int)rem]);
			}
			return sb.ToString();
		}

		private static string BuildBase50(BigInteger number)
		{
			if (number == 0) return string.Empty;

			var sb = new StringBuilder();
			while (number > 0)
			{
				number = BigInteger.DivRem(number, Radix, out BigInteger rem);
				sb.Insert(0, Alphabet[(int)rem]);
			}
			return sb.ToString();
		}

		private static void ApplyPadding(ref string s, int? fixedLength, char padChar)
		{
			if (!fixedLength.HasValue) return;

			int needed = fixedLength.Value - s.Length;
			if (needed < 0)
				throw new ArgumentException($"Value too large for fixed length {fixedLength.Value}");

			if (needed > 0)
				s = new string(padChar, needed) + s;
		}

		private static char Normalize(char c)
			=> NormalizeMap.TryGetValue(c, out var mapped) ? mapped : c;

		private static string StripFlavor(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				throw new ArgumentException("Input cannot be empty or whitespace", nameof(input));

			int idx = input.LastIndexOf("_" + Flavor, StringComparison.Ordinal);
			return idx >= 0 ? input.Substring(0, idx) : input;
		}

		// ─────────────────────────────────────────────────────────────────────────
		// Pretty-printing (unchanged)
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
				if (++count % groupSize == 0 && count < htb50.Length && htb50[count] != '_')
					sb.Append(separator);
			}

			return sb.ToString();
		}
	}
}
