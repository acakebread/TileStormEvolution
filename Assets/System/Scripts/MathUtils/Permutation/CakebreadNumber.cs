using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public static class CakebreadNumber
{
	private static BigInteger DistinctPermutations(int[] v)
	{
		BigInteger n = Factorial(v.Length);
		var groups = v.GroupBy(x => x);
		foreach (var group in groups)
			n /= Factorial(group.Count());
		return n;
	}

	private static BigInteger BinomialCoefficient(int n, int k)
	{
		if (k > n || k < 0) return 0;
		k = Math.Min(k, n - k);
		BigInteger result = 1;
		for (int i = 0; i < k; i++)
			result = result * (n - i) / (i + 1);
		return result;
	}

	public static BigInteger ToCakebreadNumber(int[] v)
	{
		BigInteger result = 0;
		int[] arr = v.ToArray();
		while (arr.Length > 1)
		{
			BigInteger total = 0;
			var max = arr.Max();
			var pos = 0;
			var count = arr.Count(x => x == max);
			while (count > 1)
			{
				if (max == arr[pos++])
				{
					total += BinomialCoefficient(arr.Length - pos, count);
					count--;
				}
			}
			total += arr.Length - Array.LastIndexOf(arr, max) - 1;
			arr = arr.Where(x => x != max).ToArray();
			result += total * DistinctPermutations(arr);
		}
		return result;
	}

	public static int[] FromCakebreadNumber(int[] v, BigInteger n)
	{
		var output = new List<int>();
		int[] arr = v.ToArray();
		while (arr.Any())
		{
			var pos = 0;
			var min = arr.Min();
			var count = arr.Count(x => x == min);
			var combinations = BinomialCoefficient(output.Count + count, count);
			var mod = n % combinations;
			n /= combinations;
			while (count > 0)
			{
				var coef = BinomialCoefficient(output.Count + count - pos - 1, count);
				if (mod >= coef)
				{
					count--;
					mod -= coef;
					output.Insert(pos, min);
				}
				pos++;
			}
			arr = arr.Where(x => x != min).ToArray();
		}
		return output.ToArray();
	}

	private static BigInteger Factorial(int n)
	{
		if (n <= 1) return 1;
		BigInteger v = n;
		while (--n > 1) v *= n;
		return v;
	}
}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Numerics;

//public static class CakebreadNumber
//{
//	private static BigInteger DistinctPermutations(int[] v)
//	{
//		BigInteger n = Factorial(v.Length);
//		var groups = v.GroupBy(x => x);
//		foreach (var group in groups)
//			n /= Factorial(group.Count());
//		return n;
//	}

//	private static BigInteger BinomialCoefficient(int n, int k)
//	{
//		if (k > n || k < 0) return 0;
//		k = Math.Min(k, n - k);
//		BigInteger result = 1;
//		for (int i = 0; i < k; i++)
//			result = result * (n - i) / (i + 1);
//		return result;
//	}

//	public static BigInteger ToCakebreadNumber(int[] v)
//	{
//		BigInteger result = 0;
//		int[] arr = v.ToArray();
//		while (arr.Length > 1)
//		{
//			BigInteger total = 0;
//			var max = arr.Max();
//			var pos = 0;
//			var count = arr.Count(x => x == max);
//			while (count > 1)
//			{
//				if (max == arr[pos++])
//				{
//					total += BinomialCoefficient(arr.Length - pos, count);
//					count--;
//				}
//			}
//			total += arr.Length - Array.LastIndexOf(arr, max) - 1;
//			arr = arr.Where(x => x != max).ToArray();
//			result += total * DistinctPermutations(arr);
//		}
//		return result;
//	}

//	public static int[] FromCakebreadNumber(int[] v, BigInteger n)
//	{
//		var output = new List<int>();
//		int[] arr = v.ToArray();
//		while (arr.Any())
//		{
//			var pos = 0;
//			var min = arr.Min();
//			var count = arr.Count(x => x == min);
//			var combinations = BinomialCoefficient(output.Count + count, count);
//			var mod = n % combinations;
//			n /= combinations;
//			while (count > 0)
//			{
//				var coef = BinomialCoefficient(output.Count + count - pos - 1, count);
//				if (mod >= coef)
//				{
//					count--;
//					mod -= coef;
//					output.Insert(pos, min);
//				}
//				pos++;
//			}
//			arr = arr.Where(x => x != min).ToArray();
//		}
//		return output.ToArray();
//	}

//	//private static BigInteger CalculateCombinations(int[] arr)
//	//{
//	//	if (arr.Length == 0) return 1;
//	//	BigInteger n = Factorial(arr.Length);
//	//	Dictionary<int, int> freq = new Dictionary<int, int>();
//	//	foreach (var num in arr)
//	//	{
//	//		if (freq.ContainsKey(num)) freq[num]++;
//	//		else freq[num] = 1;
//	//	}
//	//	foreach (var count in freq.Values)
//	//		n /= Factorial(count);
//	//	return n;
//	//}

//	private static BigInteger Factorial(int n)
//	{
//		if (n <= 1) return 1;
//		BigInteger v = n;
//		while (--n > 1) v *= n;
//		return v;
//	}
//}




//using System;
//using System.Collections.Generic;
//using System.Linq;

//public static class CakebreadNumber
//{
//	private static int DistinctPermutations(int[] v)
//	{
//		int n = Factorial(v.Length);
//		var groups = v.GroupBy(x => x);
//		foreach (var group in groups) n /= Factorial(group.Count());
//		return n;
//	}

//	private static int BinomialCoefficient(int n, int k)
//	{
//		if (k > n) return 0;
//		int result = 1;
//		for (int i = 0; i < Math.Min(k, n - k); i++) result = result * (n - i) / (i + 1);
//		return result;
//	}

//	public static int ToCakebreadNumber(int[] v)
//	{
//		var result = 0;
//		while (v.Length > 1)
//		{
//			var total = 0;
//			var max = v.Max();
//			var pos = 0;
//			var count = v.Count(x => x == max);
//			while (count > 1)
//			{
//				if (max == v[pos++])
//				{
//					total += BinomialCoefficient(v.Length - pos, count); //total += v.Length - pos >= count ? BinomialCoefficient(v.Length - pos, count) : 0;
//					count--;
//				}
//			}

//			total += v.Length - Array.LastIndexOf(v, max) - 1;
//			v = v.Where(x => x != max).ToArray();
//			result += total * DistinctPermutations(v);
//		}
//		return result;
//	}


//	public static int[] FromCakebreadNumber(int[] sortedArray, int cakebreadNumber)
//	{
//		// Clone the array to work with
//		int[] result = new int[sortedArray.Length];
//		Dictionary<int, int> freq = new Dictionary<int, int>();
//		foreach (var num in sortedArray)
//		{
//			if (freq.ContainsKey(num)) freq[num]++;
//			else freq[num] = 1;
//		}

//		// Calculate total number of distinct permutations for validation
//		int totalPerms = CalculateCombinations(sortedArray);
//		if (cakebreadNumber < 0 || cakebreadNumber >= totalPerms)
//			throw new ArgumentException("Cakebread number out of range");

//		// Build the permutation
//		int pos = 0;
//		while (pos < sortedArray.Length)
//		{
//			// Try each possible number at this position
//			foreach (var pair in freq.OrderBy(x => x.Key)) // Try in order for consistency
//			{
//				int num = pair.Key;
//				int count = pair.Value;
//				if (count == 0) continue;

//				// Calculate how many permutations start with this number
//				int[] remaining = new int[sortedArray.Length - pos - 1];
//				int k = 0;
//				foreach (var p in freq)
//					if (p.Key != num) for (int i = 0; i < p.Value; i++) remaining[k++] = p.Key;
//				int ways = CalculateCombinations(remaining);
//				if (cakebreadNumber < ways)
//				{
//					// This is the number to use
//					result[pos] = num;
//					freq[num]--;
//					break;
//				}
//				else
//				{
//					cakebreadNumber -= ways;
//				}
//			}
//			pos++;
//		}

//		return result;
//	}

//	// Helper function to calculate combinations (number of distinct permutations)
//	private static int CalculateCombinations(int[] arr)
//	{
//		if (arr.Length == 0) return 1;
//		int n = Factorial(arr.Length);
//		Dictionary<int, int> freq = new Dictionary<int, int>();
//		foreach (var num in arr)
//		{
//			if (freq.ContainsKey(num)) freq[num]++;
//			else freq[num] = 1;
//		}
//		foreach (var count in freq.Values)
//			n /= Factorial(count);
//		return n;
//	}

//	// Factorial function (same as in ToCakebreadNumber)
//	private static int Factorial(int n)
//	{
//		if (n <= 1) return 1;
//		int v = n;
//		while (--n > 1) v *= n;
//		return v;
//	}
//}