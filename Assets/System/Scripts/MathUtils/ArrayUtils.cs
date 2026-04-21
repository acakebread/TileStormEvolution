using System.Linq;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class ArrayUtils
	{
		/// <summary>
		/// Counts occurrences of the first element if it's the maximum value in the array.
		/// Returns 0 if any element is greater than the first element.
		/// </summary>
		/// <param name="numbers">Array of integers to process</param>
		/// <returns>Count of first element if it's maximum, otherwise 0</returns>
		public static int CountOccurrencesIfValueIsMaximum(int[] numbers)
		{
			// Handle null or empty input
			if (numbers == null || numbers.Length == 0)
				return 0;

			// Store first value for comparison
			int firstValue = numbers[0];

			// If any value exceeds first, return 0; otherwise count matches
			return numbers.Any(x => x > firstValue)
				? 0
				: numbers.Count(x => x == firstValue);
		}

		public static int CountOccurrences<T>(T[] items, T value)
		{
			if (items == null) throw new System.ArgumentNullException(nameof(items));
			return items.Count(x => EqualityComparer<T>.Default.Equals(x, value));
		}

		public static int[] RemoveValueFromArray(int[] sourceArray, int valueToRemove)
		{
			if (sourceArray == null || sourceArray.Length == 0)
				return new int[0];

			return sourceArray.Where(x => x != valueToRemove).ToArray();
		}
	}
}