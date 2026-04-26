using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace MassiveHadronLtd
{
	public static class Base52
	{
		// Canonical Base52 alphabet (index = value)
		private const string Alphabet =
			"0123456789" +
			"ACDEFGHJKMNPQRTUVWXY" +
			"acdefhijkmnpqrtuvwxy";

		private static readonly Dictionary<char, int> DecodeMap;
		private static readonly Dictionary<char, char> NormalizeMap;

		static Base52()
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
		// Encoder (canonical output only)
		// ---------------------------------
		public static string Encode(BigInteger value)
		{
			if (value < 0)
				throw new ArgumentOutOfRangeException(nameof(value));

			if (value == 0)
				return Alphabet[0].ToString();

			var sb = new StringBuilder();

			while (value > 0)
			{
				value = BigInteger.DivRem(value, 52, out var rem);
				sb.Insert(0, Alphabet[(int)rem]);
			}

			return sb.ToString();
		}

		// ---------------------------------
		// Decoder (grey / tolerant)
		// ---------------------------------
		public static BigInteger Decode(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				throw new ArgumentException("Input is empty.");

			BigInteger result = 0;

			foreach (char raw in input)
			{
				char c = Normalize(raw);

				if (!DecodeMap.TryGetValue(c, out int value))
					throw new FormatException($"Invalid Base52 character: '{raw}'");

				result = result * 52 + value;
			}

			return result;
		}

		// ---------------------------------
		// Normalization
		// ---------------------------------
		private static char Normalize(char c)
		{
			return NormalizeMap.TryGetValue(c, out var mapped) ? mapped : c;
		}
	}
}