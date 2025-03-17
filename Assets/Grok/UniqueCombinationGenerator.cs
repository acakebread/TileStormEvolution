using System;
using System.Collections.Generic;
using System.Linq;

public static class UniqueCombinationGenerator
{
	public static int GenerateUniqueNumber(int[] inputArray)
	{
		if (inputArray == null || inputArray.Length == 0)
			return 0;

		// Count frequencies
		Dictionary<int, int> freqMap = new();
		foreach (int num in inputArray)
		{
			freqMap[num] = freqMap.GetValueOrDefault(num) + 1;
		}

		// Calculate total combinations
		int totalCombinations = CalculateMultinomialCoefficient(inputArray.Length, freqMap.Values);

		// Convert array to lexicographical rank
		int rank = 0;
		int[] workingArray = (int[])inputArray.Clone();
		Dictionary<int, int> remainingFreq = new(freqMap);

		for (int i = 0; i < workingArray.Length - 1; i++)
		{
			int current = workingArray[i];

			// Calculate how many permutations would come before this one
			int positionValue = 0;
			var sortedKeys = remainingFreq.Keys.OrderBy(x => x).ToList();

			foreach (int num in sortedKeys)
			{
				if (num >= current) break;

				if (remainingFreq[num] > 0)
				{
					// Temporarily decrease this number's frequency
					remainingFreq[num]--;
					if (remainingFreq[num] == 0)
						remainingFreq.Remove(num);

					positionValue += CalculateMultinomialCoefficient(
						workingArray.Length - i - 1,
						remainingFreq.Values
					);

					// Restore frequency
					remainingFreq[num] = freqMap[num] - CountOccurrences(workingArray, num, 0, i);
					if (remainingFreq[num] == 0)
						remainingFreq.Remove(num);
				}
			}

			rank += positionValue;
			remainingFreq[current]--;
			if (remainingFreq[current] == 0)
				remainingFreq.Remove(current);
		}

		return rank % totalCombinations;
	}

	private static int CountOccurrences(int[] arr, int value, int start, int end)
	{
		int count = 0;
		for (int i = start; i <= end && i < arr.Length; i++)
		{
			if (arr[i] == value) count++;
		}
		return count;
	}

	private static int CalculateMultinomialCoefficient(int n, IEnumerable<int> frequencies)
	{
		int numerator = Factorial(n);
		int denominator = 1;
		foreach (int freq in frequencies)
		{
			if (freq > 0)
				denominator *= Factorial(freq);
		}
		return numerator / denominator;
	}

	private static int Factorial(int n)
	{
		if (n <= 1) return 1;
		int result = 1;
		for (int i = 2; i <= n; i++)
			result *= i;
		return result;
	}
}