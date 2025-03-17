using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EbbgotNavigation : MonoBehaviour
{
	//// Start is called once before the first execution of Update after the MonoBehaviour is created
	//void Start()
	//{
	//	char[,] midGameState = {
	//		{'r', '.', 'b', 'q', 'k', 'b', 'n', 'r'},
	//		{'p', 'p', 'p', '.', '.', 'p', 'p', 'p'},
	//		{'.', '.', 'n', '.', 'p', '.', '.', '.'},
	//		{'.', '.', '.', 'p', '.', '.', '.', '.'},
	//		{'.', '.', '.', '.', 'P', '.', '.', '.'},
	//		{'.', '.', 'N', '.', '.', 'N', '.', '.'},
	//		{'P', 'P', 'P', '.', '.', 'P', 'P', 'P'},
	//		{'R', '.', 'B', 'Q', 'K', 'B', '.', 'R'}
	//	};

	//	ChessBoard board = new ChessBoard(midGameState);
	//	Debug.Log("Current Board State:");
	//	board.PrintBoard();

	//	int encoded = board.Encode();
	//	Debug.Log("\nEncoded Board: " + encoded);

	//	board.Decode(encoded);
	//	Debug.Log("\nDecoded Board:");
	//	board.PrintBoard();
	//}


	void Start()
    {

		Debug.Log(GetComponent<Unity.VisualScripting.Variables>());

		//local functions
		//int Factorial(int n) { var v = n; while (--n > 1) v *= n; return v > 0 ? v : 1; }

		//int DistinctPermutations(int[] v)
		//{
		//	int n = Factorial(v.Length);
		//	var groups = v.GroupBy(x => x);
		//	foreach (var group in groups) n /= Factorial(group.Count());
		//	return n;
		//}

		//int BinomialCoefficient(int n, int k)
		//{
		//	if (k > n) return 0;
		//	int result = 1;
		//	for (int i = 0; i < Math.Min(k, n - k); i++) result = result * (n - i) / (i + 1);
		//	return result;
		//}

		//decimal result = 1;
		//for (int i = 1; i <= K; i++)
		//{
		//	result *= N - (K - i);
		//	result /= i;
		//}
		//return result;

		//int BinomialCoefficient(int n, int k)
		//{
		//	var result = 1;
		//	for (int i = 1; i <= k; i++) result = (result * n - (k - i)) / i;
		//	return result;
		//}

		int Factorial(int n) => n <= 1 ? 1 : n * Factorial(n - 1);
		int DistinctPermutations(int[] v) => v.Length == 0 ? 1 : Factorial(v.Length) / v.GroupBy(x => x).Aggregate(1, (a, g) => a * Factorial(g.Count()));
		int BinomialCoefficient(int n, int k) => k > n ? 0 : Enumerable.Range(0, Math.Min(k, n - k)).Aggregate(1, (r, i) => r * (n - i) / (i + 1));

		// Convert permutation array to permuradic number
		int ToPermuradic(int[] v)
		{
			var result = 0;
			while (v.Length > 1)
			{
				var pos = 0;
				var max = v.Max();
				var total = 0;
				var count = v.Count(x => x == max);
				while (count > 1)
				{
					if (max == v[pos++]) total += BinomialCoefficient(v.Length - pos, count--);
				}

				total += v.Length - Array.LastIndexOf(v, max) - 1;
				v = v.Where(x => x != max).ToArray();
				result += total * DistinctPermutations(v);
			}
			return result;
		}

		// Convert permuradic number back to permutation array
		int[] FromPermuradic(int[] v, int n)
		{
			var output = new List<int>();

			while (v.Any())
			{
				var pos = 0;
				var min = v.Min();
				var count = v.Count(x => x == min);
				var combinations = BinomialCoefficient(output.Count + count, count);
				var mod = n % combinations;//the working combination
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
				v = v.Where(x => x != min).ToArray();//clear processed values from source array
			}
			return output.ToArray();
		}

		var test = new[] { 5, 1, 1, 2, 6, 2, 3, 3, 4 };
		//var test = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
		for (var i = 0; i < DistinctPermutations(test); ++i)
		{
			var arr = FromPermuradic(test, i);
			var num = ToPermuradic(arr);
			Debug.Log(i + " FromPermuradic: " + string.Join(", ", arr) + " ToPermuradic: " + num);

			if (num != i) { Debug.LogError("fail!"); break; }
		}


		//for (var i = 0; i < 1680; ++i)
		//{
		//	var arr = FromCakebreadNumber(new[] { 1, 1, 2, 2, 2, 3, 3, 4 }, i);
		//	var c = ToCakebreadNumber(arr);
		//	Debug.Log(i + " FromCakebreadNumber: " + CakebreadNumber.ArrayDebugString(arr) + " ToCakebreadNumber: " + c);

		//	if (c != i) { Debug.LogError("fail!"); break; }
		//}
		//for (var i = 0; i < 420; ++i)
		//{
		//	var arr = FromCakebreadNumber(new[] { 1, 1, 2, 2, 2, 3, 4 }, i);
		//	var c = ToCakebreadNumber(arr);
		//	Debug.Log(i + " FromCakebreadNumber: " + CakebreadNumber.ArrayDebugString(arr) + " ToCakebreadNumber: " + c);

		//	if (c != i) { Debug.LogError("fail!"); break; }
		//}
	}

	// Update is called once per frame
	void Update()
    {
        transform.position += new Vector3(0, 0, 1f * Time.smoothDeltaTime);
    }
}
