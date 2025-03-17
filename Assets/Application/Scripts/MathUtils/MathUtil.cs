using System.Collections.Generic;
using System.Linq;

public static class MathUtil
{
	public static int Factorial(int n) { var v = n; while (--n > 1) v *= n; return v > 0 ? v : 1; }


	//public static int CalculateFactorialLinq(int number)
	//{
	//	if (number < 0)
	//		throw new ArgumentException("Factorial is not defined for negative numbers.", nameof(number));

	//	return number <= 1 ? 1 : Enumerable.Range(1, number).Aggregate((a, b) => a * b);
	//}


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