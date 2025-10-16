using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Numerics;

namespace MassiveHadronLtd
{
	public class CakebreadNumberTest : MonoBehaviour
	{
		void Start()
		{
			BigInteger Factorial(int n)
			{
				if (n <= 1) return 1;
				BigInteger result = n;
				while (--n > 1) result *= n;
				return result;
			}

			BigInteger DistinctPermutations(int[] v)
			{
				if (v.Length == 0) return 1;
				BigInteger n = Factorial(v.Length);
				var groups = v.GroupBy(x => x);
				foreach (var group in groups)
					n /= Factorial(group.Count());
				return n;
			}

			BigInteger BinomialCoefficient(int n, int k)
			{
				if (k > n || k < 0) return 0;
				k = Math.Min(k, n - k);
				BigInteger result = 1;
				for (int i = 0; i < k; i++)
					result = result * (n - i) / (i + 1);
				return result;
			}

			BigInteger ToPermuradic(int[] v)
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
						if (max == arr[pos++]) total += BinomialCoefficient(arr.Length - pos, count--);
					}
					total += arr.Length - Array.LastIndexOf(arr, max) - 1;
					arr = arr.Where(x => x != max).ToArray();
					result += total * DistinctPermutations(arr);
				}
				return result;
			}

			int[] FromPermuradic(int[] v, BigInteger n)
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



			char[] initialState = {
		'r', 'n', 'b', 'q', 'k', 'b', 'n', 'r',
		'p', 'p', 'p', 'p', 'p', 'p', 'p', 'p',
		'.', '.', '.', '.', '.', '.', '.', '.',
		'.', '.', '.', '.', '.', '.', '.', '.',
		'.', '.', '.', '.', '.', '.', '.', '.',
		'.', '.', '.', '.', '.', '.', '.', '.',
		'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P',
		'R', 'N', 'B', 'Q', 'K', 'B', 'N', 'R' };

			char[] midGameState = {
		'r', '.', 'b', 'q', 'k', 'b', 'n', 'r',
		'p', 'p', 'p', '.', '.', 'p', 'p', 'p',
		'.', '.', 'n', '.', 'p', '.', '.', '.',
		'.', '.', '.', 'p', '.', '.', '.', '.',
		'.', '.', '.', '.', 'P', '.', '.', '.',
		'.', '.', 'N', '.', '.', 'N', '.', '.',
		'P', 'P', 'P', '.', '.', 'P', 'P', 'P',
		'R', '.', 'B', 'Q', 'K', 'B', '.', 'R' };



			//var test = new[] { 5, 1, 1, 2, 6, 2, 3, 3, 4, 8, 11, 15, 15, 17, 17, 17, 12, 18, 99, 98, 97, 96, 95, 100, 11, 11, 11, 11, 11, 11, 11, 11 };//
			var test = new int[64];//
								   //for (var i = 0; i < test.Length; ++i) test[i] = UnityEngine.Random.Range(0, 64);
			for (var i = 0; i < test.Length; ++i) test[i] = initialState[i];

			//var test = new[] { 3, 2, 2, 1, 1, 4 };//

			var totalPerms = DistinctPermutations(test);
			Debug.Log($"Total distinct permutations: {totalPerms}");

			var break_ct = 0;
			for (BigInteger i = 0; i < totalPerms; i += 1000000000000)
			//for (BigInteger i = 0; i < totalPerms; ++i)
			{
				if (break_ct++ > 1000) break;
				var perm = FromPermuradic(test, i);
				var cakebreadNum = CakebreadNumber.ToCakebreadNumber(perm);
				var cakebreadPerm = CakebreadNumber.FromCakebreadNumber(test, cakebreadNum);
				var permuradicNum = ToPermuradic(cakebreadPerm);

				Debug.Log($"{i} FromPermuradic: [{string.Join(", ", perm)}] " +
						  $"ToCakebreadNumber: {cakebreadNum} " +
						  $"FromCakebreadNumber: [{string.Join(", ", cakebreadPerm)}] " +
						  $"ToPermuradic: {permuradicNum}");

				if (cakebreadNum != i || permuradicNum != i || !perm.SequenceEqual(cakebreadPerm))
				{
					Debug.LogError($"Test failed at i={i}!");
					break;
				}
			}
		}
	}
}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;
//using System.Numerics;

//public class CakebreadNumberTest : MonoBehaviour
//{
//	void Start()
//	{
//		int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
//		int DistinctPermutations(int[] v) => v.Length == 0 ? 1 : Factorial(v.Length) / v.GroupBy(x => x).Aggregate(1, (a, g) => a * Factorial(g.Count()));
//		int BinomialCoefficient(int n, int k) => k > n ? 0 : Enumerable.Range(0, Math.Min(k, n - k)).Aggregate(1, (r, i) => r * (n - i) / (i + 1));

//		int ToPermuradic(int[] v)
//		{
//			var result = 0;
//			while (v.Length > 1)
//			{
//				var pos = 0;
//				var max = v.Max();
//				var total = 0;
//				var count = v.Count(x => x == max);
//				while (count > 1)
//				{
//					if (max == v[pos++]) total += BinomialCoefficient(v.Length - pos, count--);
//				}
//				total += v.Length - Array.LastIndexOf(v, max) - 1;
//				v = v.Where(x => x != max).ToArray();
//				result += total * DistinctPermutations(v);
//			}
//			return result;
//		}

//		int[] FromPermuradic(int[] v, int n)
//		{
//			var output = new List<int>();
//			while (v.Any())
//			{
//				var pos = 0;
//				var min = v.Min();
//				var count = v.Count(x => x == min);
//				var combinations = BinomialCoefficient(output.Count + count, count);
//				var mod = n % combinations;
//				n /= combinations;
//				while (count > 0)
//				{
//					var coef = BinomialCoefficient(output.Count + count - pos - 1, count);
//					if (mod >= coef)
//					{
//						count--;
//						mod -= coef;
//						output.Insert(pos, min);
//					}
//					pos++;
//				}
//				v = v.Where(x => x != min).ToArray();
//			}
//			return output.ToArray();
//		}

//		var test = new[] { 5, 1, 1, 2, 6, 2, 3, 3, 4, 8, 11, 15, 15, 17, 17, 17, 12 };
//		var totalPerms = DistinctPermutations(test);
//		Debug.Log($"Total distinct permutations: {totalPerms}");

//		for (int i = 0; i < totalPerms && i < DistinctPermutations(test); i += 10000000)
//		{
//			// Generate permutation using FromPermuradic
//			var perm = FromPermuradic(test, i);
//			// Convert to cakebread number
//			var cakebreadNum = CakebreadNumber.ToCakebreadNumber(perm);
//			// Convert back to permutation
//			var cakebreadPerm = CakebreadNumber.FromCakebreadNumber(test, cakebreadNum);
//			// Verify consistency with ToPermuradic
//			var permuradicNum = ToPermuradic(cakebreadPerm);

//			Debug.Log($"{i} FromPermuradic: [{string.Join(", ", perm)}] " +
//					  $"ToCakebreadNumber: {cakebreadNum} " +
//					  $"FromCakebreadNumber: [{string.Join(", ", cakebreadPerm)}] " +
//					  $"ToPermuradic: {permuradicNum}");

//			if (cakebreadNum != i || permuradicNum != i || !perm.SequenceEqual(cakebreadPerm))
//			{
//				Debug.LogError($"Test failed at i={i}!");
//				break;
//			}
//		}
//	}
//}



//using System;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//public class CakebreadNumberTest : MonoBehaviour
//{
//	//// Start is called once before the first execution of Update after the MonoBehaviour is created
//	//void Start()
//	//{
//	//	char[,] midGameState = {
//	//		{'r', '.', 'b', 'q', 'k', 'b', 'n', 'r'},
//	//		{'p', 'p', 'p', '.', '.', 'p', 'p', 'p'},
//	//		{'.', '.', 'n', '.', 'p', '.', '.', '.'},
//	//		{'.', '.', '.', 'p', '.', '.', '.', '.'},
//	//		{'.', '.', '.', '.', 'P', '.', '.', '.'},
//	//		{'.', '.', 'N', '.', '.', 'N', '.', '.'},
//	//		{'P', 'P', 'P', '.', '.', 'P', 'P', 'P'},
//	//		{'R', '.', 'B', 'Q', 'K', 'B', '.', 'R'}
//	//	};

//	//	ChessBoard board = new ChessBoard(midGameState);
//	//	Debug.Log("Current Board State:");
//	//	board.PrintBoard();

//	//	int encoded = board.Encode();
//	//	Debug.Log("\nEncoded Board: " + encoded);

//	//	board.Decode(encoded);
//	//	Debug.Log("\nDecoded Board:");
//	//	board.PrintBoard();
//	//}

//	void Start()
//	{
//		int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
//		int DistinctPermutations(int[] v) => v.Length == 0 ? 1 : Factorial(v.Length) / v.GroupBy(x => x).Aggregate(1, (a, g) => a * Factorial(g.Count()));
//		int BinomialCoefficient(int n, int k) => k > n ? 0 : Enumerable.Range(0, Math.Min(k, n - k)).Aggregate(1, (r, i) => r * (n - i) / (i + 1));

//		// Convert permutation array to permuradic number
//		int ToPermuradic(int[] v)
//		{
//			var result = 0;
//			while (v.Length > 1)
//			{
//				var pos = 0;
//				var max = v.Max();
//				var total = 0;
//				var count = v.Count(x => x == max);
//				while (count > 1)
//				{
//					if (max == v[pos++]) total += BinomialCoefficient(v.Length - pos, count--);
//				}

//				total += v.Length - Array.LastIndexOf(v, max) - 1;
//				v = v.Where(x => x != max).ToArray();
//				result += total * DistinctPermutations(v);
//			}
//			return result;
//		}

//		// Convert permuradic number back to permutation array
//		int[] FromPermuradic(int[] v, int n)
//		{
//			var output = new List<int>();

//			while (v.Any())
//			{
//				var pos = 0;
//				var min = v.Min();
//				var count = v.Count(x => x == min);
//				var combinations = BinomialCoefficient(output.Count + count, count);
//				var mod = n % combinations;//the working combination
//				n /= combinations;
//				while (count > 0)
//				{
//					var coef = BinomialCoefficient(output.Count + count - pos - 1, count);
//					if (mod >= coef)
//					{
//						count--;
//						mod -= coef;
//						output.Insert(pos, min);
//					}
//					pos++;
//				}
//				v = v.Where(x => x != min).ToArray();//clear processed values from source array
//			}
//			return output.ToArray();
//		}

//		var test = new[] { 5, 1, 1, 2, 6, 2, 3, 3, 4 };
//		//var test = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
//		for (var i = 0; i < DistinctPermutations(test); ++i)
//		{
//			var arr = FromPermuradic(test, i);
//			var num = ToPermuradic(arr);
//			Debug.Log(i + " FromPermuradic: " + string.Join(", ", arr) + " ToPermuradic: " + num);

//			if (num != i) { Debug.LogError("fail!"); break; }
//		}
//	}
//}

