using System;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ArrayExtensions
	{
		// ────────────────────────────────────────────────────────────────
		// Existing RollArray method (unchanged)
		// ────────────────────────────────────────────────────────────────
		public static void RollArray<T>(T[] array, int first, int size, int roll, int stride = 1)
		{
			if (array == null || size <= 0 || stride == 0)
				return;

			int lastIndex = first + (size - 1) * stride;
			int minIndex = stride > 0 ? first : lastIndex;
			int maxIndex = stride > 0 ? lastIndex : first;

			if (minIndex < 0 || maxIndex >= array.Length)
				return;

			int nRoll = roll % size;
			if (nRoll < 0)
				nRoll += size;

			if (nRoll == 0)
				return;

			int nSrc = 0;
			int nDst = nRoll % size;
			T nVal = array[first];

			for (int i = size; i > 0; i--)
			{
				int dstIndex = first + nDst * stride;
				T nTmp = array[dstIndex];
				array[dstIndex] = nVal;
				nVal = nTmp;

				if (nDst == nSrc)
				{
					nSrc++;
					nDst++;
					if (nDst < size)
						nVal = array[first + nDst * stride];
				}
				nDst = (nDst + nRoll) % size;
			}
		}

		// ────────────────────────────────────────────────────────────────
		// Standard RLE (value, count pairs) - used internally
		// ────────────────────────────────────────────────────────────────
		private static int[] ComputeRawRle(int[] source)
		{
			if (source == null || source.Length == 0)
				return Array.Empty<int>();

			var result = new List<int>();

			int current = source[0];
			int count = 1;

			for (int i = 1; i < source.Length; i++)
			{
				if (source[i] == current)
				{
					count++;
				}
				else
				{
					result.Add(current);
					result.Add(count);
					current = source[i];
					count = 1;
				}
			}

			result.Add(current);
			result.Add(count);

			return result.ToArray();
		}

		private static int[] RleDecodeInternal(int[] encoded)
		{
			if (encoded == null || encoded.Length == 0 || encoded.Length % 2 != 0)
				return Array.Empty<int>();

			var result = new List<int>();

			for (int i = 0; i < encoded.Length; i += 2)
			{
				int value = encoded[i];
				int count = encoded[i + 1];

				if (count <= 0) continue;

				for (int j = 0; j < count; j++)
					result.Add(value);
			}

			return result.ToArray();
		}

		// ────────────────────────────────────────────────────────────────
		// Public: Smart RLE Encode - chooses smaller of plain vs RLE
		// ────────────────────────────────────────────────────────────────
		public static int[] SmartRleEncode(this int[] source)
		{
			if (source == null || source.Length == 0)
				return Array.Empty<int>();

			var rle = ComputeRawRle(source);

			// RLE is smaller only if it saves space (no prefix overhead)
			if (rle.Length < source.Length)
				return rle;

			// Otherwise keep original (plain)
			return (int[])source.Clone();
		}

		// ────────────────────────────────────────────────────────────────
		// Public: Smart RLE Decode - tries RLE first, falls back to plain
		// ────────────────────────────────────────────────────────────────
		public static int[] SmartRleDecode(this int[] encoded)
		{
			if (encoded == null || encoded.Length == 0)
				return Array.Empty<int>();

			// Try as RLE: must be even length + all counts > 0
			if (encoded.Length % 2 == 0 && IsValidRlePairs(encoded))
			{
				var decoded = RleDecodeInternal(encoded);

				// Extra safety: if decoded length is wildly wrong, fall back
				if (decoded.Length > 0 && Math.Abs(decoded.Length - encoded.Length * 10) < encoded.Length * 20)
					return decoded;
			}

			// Not valid RLE → treat as plain array
			return (int[])encoded.Clone();
		}

		private static bool IsValidRlePairs(int[] encoded)
		{
			for (int i = 1; i < encoded.Length; i += 2)
			{
				if (encoded[i] <= 0)
					return false;
			}
			return true;
		}

		// For debugging
		public static string RleSummary(this int[] arr)
		{
			if (arr == null || arr.Length == 0) return "empty";
			if (arr.Length % 2 == 0 && IsValidRlePairs(arr))
				return $"RLE ({arr.Length / 2} pairs → ~{arr[1] + arr[3]} elements)";
			return $"plain ({arr.Length} elements)";
		}
	}
}
