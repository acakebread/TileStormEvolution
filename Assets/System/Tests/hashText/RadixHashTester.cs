using UnityEngine;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using MassiveHadronLtd;

public class RadixHashCollisionTester : MonoBehaviour
{
	public enum TestMode
	{
		BigIntegerMod,      // BigInteger + modulus
		Int64Mod,           // long + modulus
		Int32Mod,           // int + modulus
		Int32FullRange,     // full 32-bit hash, no modulus
		Int64FullRange,     // full 64-bit hash, no modulus
		TrueRandomBaseline  // uniform random (ideal case)
	}

	[Header("Test Settings")]
	public TestMode mode = TestMode.Int32FullRange;
	public int hashesPerBatch = 50000;
	public int stringLength = 6;
	public int radix = 50;

	[Header("Alert & Display")]
	public float alertFactor = 4.0f;
	public int labelFontSize = 16;
	public int buttonHeight = 40;
	public int spacing = 8;

	// State
	private bool isRunning;
	private bool paused;

	private int batchNumber;
	private long totalHashes;
	private long totalCollisionPairs;
	private long batchStartPairs;

	private bool showedHighCollisionWarning;

	private Dictionary<long, int> countMap64 = new();
	private Dictionary<int, int> countMap32 = new();           // ← added for Int32 modes
	private Dictionary<BigInteger, int> countMapBig = new();

	private System.Diagnostics.Stopwatch stopwatch = new();

	private BigInteger modulus;

	void Awake()
	{
		UpdateModulus();
	}

	void UpdateModulus()
	{
		modulus = BigInteger.Pow(radix, stringLength);

		// Only warn when we're actually reducing via modulus
		bool modulusUsed = mode == TestMode.Int32Mod ||
						   mode == TestMode.Int64Mod ||
						   mode == TestMode.BigIntegerMod;

		if (modulusUsed)
		{
			if (mode == TestMode.Int32Mod && modulus > int.MaxValue)
			{
				Debug.LogWarning($"Modulus {modulus} > int.MaxValue — Int32Mod will truncate / behave unexpectedly");
			}

			if (mode == TestMode.Int64Mod && modulus > long.MaxValue)
			{
				Debug.LogWarning($"Modulus {modulus} > long.MaxValue — Int64Mod will truncate / behave unexpectedly");
			}
		}

		// Optional: log the computed modulus only in modulo modes for debugging
		if (modulusUsed)
		{
			Debug.Log($"Modulus computed: {modulus} (used in {mode})");
		}
	}

	int GetMaxSafeLengthForMode()
	{
		if (mode == TestMode.BigIntegerMod || mode == TestMode.TrueRandomBaseline)
			return 18;

		if (mode == TestMode.Int32FullRange || mode == TestMode.Int64FullRange)
			return 30;  // just let user choose long inputs if they want — doesn't hurt

		if (mode == TestMode.Int64FullRange || mode == TestMode.Int64Mod)
			return 13;

		return 18;
	}

	double CurrentExpectedPairs(long n)
	{
		if (mode == TestMode.Int32FullRange || mode == TestMode.Int32Mod)
			return (double)n * (n - 1) / (2.0 * (double)uint.MaxValue + 1);

		if (mode == TestMode.Int64FullRange || mode == TestMode.Int64Mod)
			return (double)n * (n - 1) / (2.0 * (double)ulong.MaxValue + 1);

		if (modulus.IsZero || modulus.IsOne) return 0;
		return (double)n * (n - 1) / (2.0 * (double)modulus);
	}

	void Update()
	{
		if (!isRunning || paused) return;

		int toDo = Mathf.Min(2000, hashesPerBatch - GetCurrentMapCount());
		if (toDo <= 0)
		{
			FinishBatch();
			return;
		}

		GenerateHashes(toDo);
	}

	private int GetCurrentMapCount()
	{
		if (mode == TestMode.Int32Mod || mode == TestMode.Int32FullRange)
			return countMap32.Count;
		if (mode == TestMode.Int64Mod || mode == TestMode.Int64FullRange)
			return countMap64.Count;
		return countMapBig.Count;
	}

	void GenerateHashes(int count)
	{
		var rng = new System.Random();

		for (int i = 0; i < count; i++)
		{
			string input;

			if (mode == TestMode.TrueRandomBaseline)
			{
				BigInteger randVal = BigIntegerRandom(modulus);
				if (modeUsesBigInt())
					AddHash(randVal);
				else if (mode == TestMode.Int32FullRange || mode == TestMode.Int32Mod)
				{
					uint u = (uint)randVal;                     // lowest 32 bits
					AddHash((int)u);
				}
				else
				{
					ulong u = (ulong)randVal;                   // lowest 64 bits
					AddHash((long)u);
				}
				totalHashes++;
				continue;
			}

			// Normal case: high-entropy input string
			input = RandomHighEntropyString(rng, 24);

			if (mode == TestMode.Int32FullRange)
			{
				int hash32 = RadixHash.GetStableHash32(input); // ← you need this method
				AddHash(hash32);
			}
			else if (mode == TestMode.Int64FullRange)
			{
				long hash64 = RadixHash.GetStableHash64(input); // ← you need this too
				AddHash(hash64);
			}
			else
			{
				// Modulo-based modes (old behavior)
				BigInteger hashValue = RadixHash.HashToRange(input, modulus);

				switch (mode)
				{
					case TestMode.BigIntegerMod:
						AddHash(hashValue);
						break;

					case TestMode.Int64Mod:
						if (modulus > long.MaxValue) goto default;
						AddHash((long)hashValue);
						break;

					case TestMode.Int32Mod:
						if (modulus > int.MaxValue) goto default;
						AddHash((int)hashValue);
						break;

					default:
						Debug.LogError("Invalid mode or modulus too large for target type");
						isRunning = false;
						return;
				}
			}

			totalHashes++;

			if (totalHashes % 1000 == 0)
				UpdateStatus();
		}
	}

	static string RandomHighEntropyString(System.Random rng, int length)
	{
		const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
		var sb = new StringBuilder(length);
		for (int i = 0; i < length; i++)
			sb.Append(chars[rng.Next(chars.Length)]);
		return sb.ToString() + "-" + rng.NextDouble().ToString("F14").Substring(2);
	}

	static BigInteger BigIntegerRandom(BigInteger max)
	{
		if (max <= 0) throw new ArgumentException();
		int byteLen = (int)Math.Ceiling(BigInteger.Log(max, 2) / 8.0) + 4;
		byte[] bytes = new byte[byteLen];
		using var rng = RandomNumberGenerator.Create();
		BigInteger val;
		do
		{
			rng.GetBytes(bytes);
			val = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
		} while (val >= max);
		return val;
	}

	// Overloads for adding hashes
	void AddHash(BigInteger val) => AddToMap(countMapBig, val);
	void AddHash(long val) => AddToMap(countMap64, val);
	void AddHash(int val) => AddToMap(countMap32, val);

	private void AddToMap<T>(Dictionary<T, int> map, T key) where T : notnull
	{
		if (!map.TryGetValue(key, out int cnt))
		{
			map[key] = 1;
		}
		else
		{
			map[key] = cnt + 1;
			totalCollisionPairs += cnt; // incremental birthday collision count
		}
	}

	bool modeUsesBigInt() =>
		mode == TestMode.BigIntegerMod || mode == TestMode.TrueRandomBaseline;

	bool modeUsesInt32() =>
		mode == TestMode.Int32Mod || mode == TestMode.Int32FullRange;

	void FinishBatch()
	{
		long pairsThisBatch = totalCollisionPairs - batchStartPairs;
		long itemsThisBatch = totalHashes - (batchNumber > 1 ? (totalHashes - hashesPerBatch) : 0);

		double expectedThisBatch = CurrentExpectedPairs(itemsThisBatch);
		double ratio = expectedThisBatch > 0 ? pairsThisBatch / expectedThisBatch : 0;

		int bucketsUsed = modeUsesInt32() ? countMap32.Count :
						 modeUsesBigInt() ? countMapBig.Count : countMap64.Count;

		int maxOccupancy = modeUsesInt32() ? (countMap32.Count > 0 ? countMap32.Values.Max() : 0) :
						   modeUsesBigInt() ? (countMapBig.Count > 0 ? countMapBig.Values.Max() : 0) :
						   (countMap64.Count > 0 ? countMap64.Values.Max() : 0);

		Debug.Log($"[Batch {batchNumber}]  n = {itemsThisBatch:N0}   pairs = {pairsThisBatch:N0}   " +
				  $"exp ≈ {expectedThisBatch:F4}   ratio = {ratio:F2}×   " +
				  $"buckets = {bucketsUsed:N0}   max = {maxOccupancy}");

		if (ratio > alertFactor)
		{
			Debug.LogWarning($"!!! HIGH COLLISION RATIO DETECTED: {ratio:F2}× expected !!!");
			showedHighCollisionWarning = true;
		}

		batchNumber++;
		batchStartPairs = totalCollisionPairs;
		countMap32.Clear();
		countMap64.Clear();
		countMapBig.Clear();

		if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
		{
			PrintFinalSummary();
			isRunning = false;
		}
	}

	void PrintFinalSummary()
	{
		double overallExpected = CurrentExpectedPairs(totalHashes);
		double overallRatio = overallExpected > 0 ? totalCollisionPairs / overallExpected : 0;

		string rangeDesc = mode switch
		{
			TestMode.Int32FullRange => "0 .. 2³²-1 (full 32-bit)",
			TestMode.Int64FullRange => "0 .. 2⁶⁴-1 (full 64-bit)",
			_ => $"0 .. {modulus} (modulo)"
		};

		Debug.Log("\n═══════════════════════════════════════════════════════");
		Debug.Log("         COLLISION TEST FINAL SUMMARY");
		Debug.Log("═══════════════════════════════════════════════════════");
		Debug.Log($"Mode             : {mode}");
		Debug.Log($"Range            : {rangeDesc}");
		Debug.Log($"Radix            : {radix}    Length: {stringLength}");
		Debug.Log($"Modulus          : {modulus}");
		Debug.Log($"Total hashes     : {totalHashes:N0}");
		Debug.Log($"Total pairs      : {totalCollisionPairs:N0}");
		Debug.Log($"Expected pairs   : ~{overallExpected:F2}");
		Debug.Log($"Observed / Exp   : {overallRatio:F2} ×");
		Debug.Log($"Max bucket size  : {GetMaxBucketSize()}");
		Debug.Log($"High ratio alert : {(showedHighCollisionWarning ? "YES" : "no")}");
		Debug.Log($"Runtime          : {stopwatch.Elapsed.TotalSeconds:F1} s");
		Debug.Log("═══════════════════════════════════════════════════════\n");
	}

	private int GetMaxBucketSize()
	{
		if (modeUsesInt32()) return countMap32.Values.MaxOrDefault(0);
		if (modeUsesBigInt()) return countMapBig.Values.MaxOrDefault(0);
		return countMap64.Values.MaxOrDefault(0);
	}

	void UpdateStatus()
	{
		string m = mode.ToString();
		if (mode == TestMode.TrueRandomBaseline) m = "RANDOM (baseline)";
		statusText = $"Batch {batchNumber} • {totalHashes:N0} hashes • {m}";
	}

	string statusText = "Ready";

	void OnGUI()
	{
		GUI.skin.label.fontSize = labelFontSize;
		GUI.skin.button.fontSize = labelFontSize - 1;

		float x = Screen.width * 0.06f;
		float w = Screen.width * 0.88f;
		float y = 20;

		GUI.Label(new Rect(x, y, w, 40), "Collision Tester — Radix Hash", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 24 });
		y += 50;

		GUI.Label(new Rect(x, y, w + 50, 30), $"Status: {statusText}", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.cyan } });
		y += 40;

		// ── Progress bar ────────────────────────────────────────────────
		if (isRunning)
		{
			int currentInBatch = GetCurrentMapCount(); // approximate: number of unique hashes so far
			int target = hashesPerBatch;
			float prog = target > 0 ? (float)currentInBatch / target : 0f;

			GUI.Label(new Rect(x, y, w, 25), $"Progress in batch: {(prog * 100):F1}%  ({currentInBatch:N0} / {target:N0})");
			y += 25;

			GUI.HorizontalScrollbar(new Rect(x, y, w, 20), 0, prog, 0, 1);
			y += 30;  // space after bar
		}
		else
		{
			y += 55;  // keep layout consistent when not running
		}
		// ────────────────────────────────────────────────────────────────

		string bucketText = modeUsesInt32() ? countMap32.Count.ToString("N0") :
						   modeUsesBigInt() ? countMapBig.Count.ToString("N0") : countMap64.Count.ToString("N0");

		GUI.Label(new Rect(x, y, w, 28), $"Pairs: {totalCollisionPairs:N0}   Buckets: {bucketText} / {(modeUsesInt32() || modeUsesBigInt() ? "—" : modulus.ToString())}");
		y += 40;

		if (!isRunning)
		{
			if (GUI.Button(new Rect(x, y, 160, buttonHeight), "Start")) StartTest();
		}
		else
		{
			if (GUI.Button(new Rect(x, y, 160, buttonHeight), paused ? "Resume" : "Pause"))
				paused = !paused;

			if (GUI.Button(new Rect(x + 180, y, 160, buttonHeight), "Stop & Report"))
			{
				isRunning = false;
				PrintFinalSummary();
			}
		}
		y += buttonHeight + spacing + 20;

		GUI.Label(new Rect(x, y, 180, 30), "Hashes / batch");
		hashesPerBatch = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 190, y, 220, 30), hashesPerBatch, 1000, 500000));
		GUI.Label(new Rect(x + 420, y, 100, 30), hashesPerBatch.ToString("N0"));
		y += 40;

		GUI.Label(new Rect(x, y, 180, 30), "String length");

		int maxLen = GetMaxSafeLengthForMode();
		int current = Mathf.Clamp(stringLength, 4, maxLen);

		current = Mathf.RoundToInt(GUI.HorizontalSlider(
			new Rect(x + 190, y, 220, 30),
			current, 4, maxLen
		));

		stringLength = current;

		GUI.Label(new Rect(x + 420, y, 300, 30),
			$"{stringLength}  (max {maxLen} for {mode})"
		);
		y += 40;

		GUI.Label(new Rect(x, y, 180, 30), "Mode");
		int m = (int)mode;
		m = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 190, y, 220, 30), m, 0, Enum.GetValues(typeof(TestMode)).Length - 1));
		mode = (TestMode)m;
		GUI.Label(new Rect(x + 420, y, 240, 30), mode.ToString());
		y += 40;

		GUI.Label(new Rect(x, y, 300, 30), $"Radix: {radix}    Modulus: {(mode.ToString().Contains("FullRange") ? "—" : modulus.ToString())}");
	}

	void StartTest()
	{
		isRunning = true;
		paused = false;
		showedHighCollisionWarning = false;

		batchNumber = 1;
		totalHashes = 0;
		totalCollisionPairs = 0;
		batchStartPairs = 0;

		countMap32.Clear();
		countMap64.Clear();
		countMapBig.Clear();

		stopwatch.Reset();
		stopwatch.Start();

		UpdateModulus();
		UpdateStatus();

		Debug.Log($"Started collision test — mode: {mode}  radix^{stringLength} = {modulus:N0}");
	}
}

// Optional helper extension
public static class CollectionExtensions
{
	public static int MaxOrDefault(this IEnumerable<int> source, int defaultValue = 0)
	{
		return source.Any() ? source.Max() : defaultValue;
	}
}


//using UnityEngine;
//using System;
//using System.Collections.Generic;
//using System.Numerics;
//using System.Text;
//using System.Security.Cryptography;
//using MassiveHadronLtd;
//using System.Linq;

//public class RadixHashCollisionTester : MonoBehaviour
//{
//	public enum TestMode { BigInteger, Int64, Int32, TrueRandomBaseline }

//	[Header("Test Settings")]
//	public TestMode mode = TestMode.BigInteger;
//	public int hashesPerBatch = 50000;
//	public int stringLength = 6;
//	public int radix = 50;                      // ← change here for HTB50 etc.

//	[Header("Alert & Display")]
//	public float alertFactor = 4.0f;
//	public int labelFontSize = 16;
//	public int buttonHeight = 40;
//	public int spacing = 8;

//	// State
//	private bool isRunning;
//	private bool paused;

//	private int batchNumber;
//	private long totalHashes;
//	private long totalCollisionPairs;
//	private long batchStartPairs;

//	private bool showedHighCollisionWarning;

//	private Dictionary<long, int> countMap64 = new();
//	private Dictionary<BigInteger, int> countMapBig = new();

//	private System.Diagnostics.Stopwatch stopwatch = new();

//	private BigInteger modulus;

//	void Awake()
//	{
//		UpdateModulus();
//	}

//	void UpdateModulus()
//	{
//		modulus = BigInteger.Pow(radix, stringLength);

//		if (mode == TestMode.Int32 && modulus > int.MaxValue)
//			Debug.LogError($"Modulus  {modulus} > int.MaxValue — Int32 mode disabled");
//		if (mode == TestMode.Int64 && modulus > long.MaxValue)
//			Debug.LogError($"Modulus  {modulus} > long.MaxValue — Int64 mode disabled");
//	}

//	int GetMaxSafeLengthForMode()
//	{
//		if (mode == TestMode.BigInteger || mode == TestMode.TrueRandomBaseline)
//			return 18; // arbitrary reasonable cap for BigInteger / UI

//		BigInteger limit = mode == TestMode.Int32 ? int.MaxValue : long.MaxValue;

//		BigInteger v = BigInteger.One;
//		int len = 0;

//		while (true)
//		{
//			BigInteger next = v * radix;
//			if (next > limit) return len;
//			v = next;
//			len++;
//		}
//	}

//	double CurrentExpectedPairs(long n)
//	{
//		if (modulus.IsZero || modulus.IsOne) return 0;
//		return (double)n * (n - 1) / (2.0 * (double)modulus);
//	}

//	void Update()
//	{
//		if (!isRunning || paused) return;

//		int toDo = Mathf.Min(2000, hashesPerBatch - countMap64.Count - countMapBig.Count); // rough
//		if (toDo <= 0)
//		{
//			FinishBatch();
//			return;
//		}

//		GenerateHashes(toDo);
//	}

//	void GenerateHashes(int count)
//	{
//		var rng = new System.Random();

//		for (int i = 0; i < count; i++)
//		{
//			string input;

//			if (mode == TestMode.TrueRandomBaseline)
//			{
//				// Baseline: truly uniform random values (no hash function)
//				BigInteger randVal = BigIntegerRandom(modulus);
//				if (modeUsesBigInt())
//					AddHash(randVal);
//				else
//					AddHash((long)randVal);
//				totalHashes++;
//				continue;
//			}

//			// Normal case: high-entropy random-ish string
//			input = RandomHighEntropyString(rng, 24);

//			BigInteger hashValue = RadixHash.HashToRange(input, modulus);

//			switch (mode)
//			{
//				case TestMode.BigInteger:
//					AddHash(hashValue);
//					break;

//				case TestMode.Int64:
//					if (modulus > long.MaxValue) goto default;
//					AddHash((long)hashValue);
//					break;

//				case TestMode.Int32:
//					if (modulus > int.MaxValue) goto default;
//					AddHash((int)hashValue);
//					break;

//				default:
//					Debug.LogError("Invalid mode or modulus too large for target type");
//					isRunning = false;
//					return;
//			}

//			totalHashes++;

//			// Light GUI refresh during long batches
//			if (totalHashes % 1000 == 0)
//				UpdateStatus();
//		}
//	}

//	static string RandomHighEntropyString(System.Random rng, int length)
//	{
//		const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
//		var sb = new StringBuilder(length);
//		for (int i = 0; i < length; i++)
//			sb.Append(chars[rng.Next(chars.Length)]);
//		return sb.ToString() + "-" + rng.NextDouble().ToString("F14").Substring(2);
//	}

//	static BigInteger BigIntegerRandom(BigInteger max)
//	{
//		if (max <= 0) throw new ArgumentException();
//		int byteLen = (int)Math.Ceiling(BigInteger.Log(max, 2) / 8.0) + 4;
//		byte[] bytes = new byte[byteLen];
//		using var rng = RandomNumberGenerator.Create();
//		BigInteger val;
//		do
//		{
//			rng.GetBytes(bytes);
//			val = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
//		} while (val >= max);
//		return val;
//	}

//	void AddHash(BigInteger val)
//	{
//		if (!countMapBig.TryGetValue(val, out int cnt))
//		{
//			countMapBig[val] = 1;
//		}
//		else
//		{
//			countMapBig[val] = cnt + 1;
//			totalCollisionPairs += cnt; // incremental → correct for \binom{k}{2}
//		}
//	}

//	void AddHash(long val)
//	{
//		if (!countMap64.TryGetValue(val, out int cnt))
//		{
//			countMap64[val] = 1;
//		}
//		else
//		{
//			countMap64[val] = cnt + 1;
//			totalCollisionPairs += cnt;
//		}
//	}

//	bool modeUsesBigInt() => mode == TestMode.BigInteger || mode == TestMode.TrueRandomBaseline;

//	void FinishBatch()
//	{
//		long pairsThisBatch = totalCollisionPairs - batchStartPairs;
//		long itemsThisBatch = totalHashes - (batchNumber > 1 ? (totalHashes - hashesPerBatch) : 0);

//		double expectedThisBatch = CurrentExpectedPairs(itemsThisBatch);
//		double ratio = expectedThisBatch > 0 ? pairsThisBatch / expectedThisBatch : 0;

//		int bucketsUsed = modeUsesBigInt() ? countMapBig.Count : countMap64.Count;
//		int maxOccupancy = modeUsesBigInt()
//			? (countMapBig.Count > 0 ? countMapBig.Values.Max() : 0)
//			: (countMap64.Count > 0 ? countMap64.Values.Max() : 0);

//		Debug.Log($"[Batch {batchNumber}]  n = {itemsThisBatch:N0}   pairs = {pairsThisBatch:N0}   " +
//				  $"exp ≈ {expectedThisBatch:F4}   ratio = {ratio:F2}×   " +
//				  $"buckets = {bucketsUsed:N0} / {modulus}   max = {maxOccupancy}");

//		if (ratio > alertFactor)
//		{
//			Debug.LogWarning($"!!! HIGH COLLISION RATIO DETECTED: {ratio:F2}× expected !!!");
//			showedHighCollisionWarning = true;
//		}

//		// Prepare next batch
//		batchNumber++;
//		batchStartPairs = totalCollisionPairs;
//		countMap64.Clear();
//		countMapBig.Clear();

//		if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
//		{
//			PrintFinalSummary();
//			isRunning = false;
//		}
//	}

//	void PrintFinalSummary()
//	{
//		double overallExpected = CurrentExpectedPairs(totalHashes);
//		double overallRatio = overallExpected > 0 ? totalCollisionPairs / overallExpected : 0;

//		Debug.Log("\n═══════════════════════════════════════════════════════");
//		Debug.Log("         COLLISION TEST FINAL SUMMARY");
//		Debug.Log("═══════════════════════════════════════════════════════");
//		Debug.Log($"Mode             : {mode}");
//		Debug.Log($"Radix            : {radix}");
//		Debug.Log($"Length           : {stringLength}  → modulus = {modulus}");
//		Debug.Log($"Total hashes     : {totalHashes:N0}");
//		Debug.Log($"Total pairs      : {totalCollisionPairs:N0}");
//		Debug.Log($"Expected pairs   : ~{overallExpected:F2}");
//		Debug.Log($"Observed / Exp   : {overallRatio:F2} ×");
//		Debug.Log($"Max bucket size  : {(modeUsesBigInt() ? countMapBig.Values.Max() : countMap64.Values.Max())}");
//		Debug.Log($"High ratio alert : {(showedHighCollisionWarning ? "YES" : "no")}");
//		Debug.Log($"Runtime          : {stopwatch.Elapsed.TotalSeconds:F1} s");
//		Debug.Log("═══════════════════════════════════════════════════════\n");
//	}

//	void UpdateStatus()
//	{
//		string m = mode.ToString();
//		if (mode == TestMode.TrueRandomBaseline) m = "RANDOM (baseline)";
//		statusText = $"Batch {batchNumber} • {totalHashes:N0} hashes • {mode}";
//	}

//	string statusText = "Ready";

//	void OnGUI()
//	{
//		GUI.skin.label.fontSize = labelFontSize;
//		GUI.skin.button.fontSize = labelFontSize - 1;

//		float x = Screen.width * 0.06f;
//		float w = Screen.width * 0.88f;
//		float y = 20;

//		GUI.Label(new Rect(x, y, w, 40), "Collision Tester — Radix Hash", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 24 });
//		y += 50;

//		GUI.Label(new Rect(x, y, w + 50, 30), $"Status: {statusText}", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.cyan } });
//		y += 40;

//		GUI.Label(new Rect(x, y, w, 28), $"Pairs: {totalCollisionPairs:N0}   Buckets: {(modeUsesBigInt() ? countMapBig.Count : countMap64.Count):N0} / {modulus}");
//		y += 40;

//		if (!isRunning)
//		{
//			if (GUI.Button(new Rect(x, y, 160, buttonHeight), "Start")) StartTest();
//		}
//		else
//		{
//			if (GUI.Button(new Rect(x, y, 160, buttonHeight), paused ? "Resume" : "Pause"))
//				paused = !paused;

//			if (GUI.Button(new Rect(x + 180, y, 160, buttonHeight), "Stop & Report"))
//			{
//				isRunning = false;
//				PrintFinalSummary();
//			}
//		}
//		y += buttonHeight + spacing + 20;

//		// Sliders
//		GUI.Label(new Rect(x, y, 180, 30), "Hashes / batch");
//		hashesPerBatch = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 190, y, 220, 30), hashesPerBatch, 1000, 500000));
//		GUI.Label(new Rect(x + 420, y, 100, 30), hashesPerBatch.ToString("N0"));
//		y += 40;

//		// --- String length slider with automatic max clamping ---
//		GUI.Label(new Rect(x, y, 180, 30), "String length");

//		int maxLen = GetMaxSafeLengthForMode();
//		int current = Mathf.Clamp(stringLength, 4, maxLen); // force into valid range

//		current = Mathf.RoundToInt(GUI.HorizontalSlider(
//			new Rect(x + 190, y, 220, 30),
//			current, 4, maxLen
//		));

//		stringLength = current;

//		GUI.Label(new Rect(x + 420, y, 300, 30),
//			$"{stringLength}  (max {maxLen} for {mode})"
//		);
//		y += 40;

//		GUI.Label(new Rect(x, y, 180, 30), "Mode");
//		int m = (int)mode;
//		m = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 190, y, 220, 30), m, 0, 3));
//		mode = (TestMode)m;
//		GUI.Label(new Rect(x + 420, y, 240, 30), mode.ToString());
//		y += 40;

//		GUI.Label(new Rect(x, y, 300, 30), $"Radix: {radix}    Modulus: {modulus}");
//	}

//	void StartTest()
//	{
//		isRunning = true;
//		paused = false;
//		showedHighCollisionWarning = false;

//		batchNumber = 1;
//		totalHashes = 0;
//		totalCollisionPairs = 0;
//		batchStartPairs = 0;

//		countMap64.Clear();
//		countMapBig.Clear();

//		stopwatch.Reset();
//		stopwatch.Start();

//		UpdateModulus();
//		UpdateStatus();

//		Debug.Log($"Started collision test — mode: {mode}  radix^{stringLength} = {modulus:N0}");
//	}
//}