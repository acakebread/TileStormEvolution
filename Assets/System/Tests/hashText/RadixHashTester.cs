using UnityEngine;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Security.Cryptography;
using MassiveHadronLtd;
using System.Linq;

public class RadixHashCollisionTester : MonoBehaviour
{
	public enum TestMode { BigInteger, Int64, Int32, TrueRandomBaseline }

	[Header("Test Settings")]
	public TestMode mode = TestMode.BigInteger;
	public int hashesPerBatch = 50000;
	public int stringLength = 6;
	public int radix = 50;                      // ← change here for HTB50 etc.

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

		if (mode == TestMode.Int32 && modulus > int.MaxValue)
			Debug.LogError($"Modulus  {modulus} > int.MaxValue — Int32 mode disabled");
		if (mode == TestMode.Int64 && modulus > long.MaxValue)
			Debug.LogError($"Modulus  {modulus} > long.MaxValue — Int64 mode disabled");
	}

	int GetMaxSafeLengthForMode()
	{
		if (mode == TestMode.BigInteger || mode == TestMode.TrueRandomBaseline)
			return 18; // arbitrary reasonable cap for BigInteger / UI

		BigInteger limit = mode == TestMode.Int32 ? int.MaxValue : long.MaxValue;

		BigInteger v = BigInteger.One;
		int len = 0;

		while (true)
		{
			BigInteger next = v * radix;
			if (next > limit) return len;
			v = next;
			len++;
		}
	}

	double CurrentExpectedPairs(long n)
	{
		if (modulus.IsZero || modulus.IsOne) return 0;
		return (double)n * (n - 1) / (2.0 * (double)modulus);
	}

	void Update()
	{
		if (!isRunning || paused) return;

		int toDo = Mathf.Min(2000, hashesPerBatch - countMap64.Count - countMapBig.Count); // rough
		if (toDo <= 0)
		{
			FinishBatch();
			return;
		}

		GenerateHashes(toDo);
	}

	void GenerateHashes(int count)
	{
		var rng = new System.Random();

		for (int i = 0; i < count; i++)
		{
			string input;

			if (mode == TestMode.TrueRandomBaseline)
			{
				// Baseline: truly uniform random values (no hash function)
				BigInteger randVal = BigIntegerRandom(modulus);
				if (modeUsesBigInt())
					AddHash(randVal);
				else
					AddHash((long)randVal);
				totalHashes++;
				continue;
			}

			// Normal case: high-entropy random-ish string
			input = RandomHighEntropyString(rng, 24);

			BigInteger hashValue = RadixHash.HashToRange(input, modulus);

			switch (mode)
			{
				case TestMode.BigInteger:
					AddHash(hashValue);
					break;

				case TestMode.Int64:
					if (modulus > long.MaxValue) goto default;
					AddHash((long)hashValue);
					break;

				case TestMode.Int32:
					if (modulus > int.MaxValue) goto default;
					AddHash((int)hashValue);
					break;

				default:
					Debug.LogError("Invalid mode or modulus too large for target type");
					isRunning = false;
					return;
			}

			totalHashes++;

			// Light GUI refresh during long batches
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

	void AddHash(BigInteger val)
	{
		if (!countMapBig.TryGetValue(val, out int cnt))
		{
			countMapBig[val] = 1;
		}
		else
		{
			countMapBig[val] = cnt + 1;
			totalCollisionPairs += cnt; // incremental → correct for \binom{k}{2}
		}
	}

	void AddHash(long val)
	{
		if (!countMap64.TryGetValue(val, out int cnt))
		{
			countMap64[val] = 1;
		}
		else
		{
			countMap64[val] = cnt + 1;
			totalCollisionPairs += cnt;
		}
	}

	bool modeUsesBigInt() => mode == TestMode.BigInteger || mode == TestMode.TrueRandomBaseline;

	void FinishBatch()
	{
		long pairsThisBatch = totalCollisionPairs - batchStartPairs;
		long itemsThisBatch = totalHashes - (batchNumber > 1 ? (totalHashes - hashesPerBatch) : 0);

		double expectedThisBatch = CurrentExpectedPairs(itemsThisBatch);
		double ratio = expectedThisBatch > 0 ? pairsThisBatch / expectedThisBatch : 0;

		int bucketsUsed = modeUsesBigInt() ? countMapBig.Count : countMap64.Count;
		int maxOccupancy = modeUsesBigInt()
			? (countMapBig.Count > 0 ? countMapBig.Values.Max() : 0)
			: (countMap64.Count > 0 ? countMap64.Values.Max() : 0);

		Debug.Log($"[Batch {batchNumber}]  n = {itemsThisBatch:N0}   pairs = {pairsThisBatch:N0}   " +
				  $"exp ≈ {expectedThisBatch:F4}   ratio = {ratio:F2}×   " +
				  $"buckets = {bucketsUsed:N0} / {modulus}   max = {maxOccupancy}");

		if (ratio > alertFactor)
		{
			Debug.LogWarning($"!!! HIGH COLLISION RATIO DETECTED: {ratio:F2}× expected !!!");
			showedHighCollisionWarning = true;
		}

		// Prepare next batch
		batchNumber++;
		batchStartPairs = totalCollisionPairs;
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

		Debug.Log("\n═══════════════════════════════════════════════════════");
		Debug.Log("         COLLISION TEST FINAL SUMMARY");
		Debug.Log("═══════════════════════════════════════════════════════");
		Debug.Log($"Mode             : {mode}");
		Debug.Log($"Radix            : {radix}");
		Debug.Log($"Length           : {stringLength}  → modulus = {modulus}");
		Debug.Log($"Total hashes     : {totalHashes:N0}");
		Debug.Log($"Total pairs      : {totalCollisionPairs:N0}");
		Debug.Log($"Expected pairs   : ~{overallExpected:F2}");
		Debug.Log($"Observed / Exp   : {overallRatio:F2} ×");
		Debug.Log($"Max bucket size  : {(modeUsesBigInt() ? countMapBig.Values.Max() : countMap64.Values.Max())}");
		Debug.Log($"High ratio alert : {(showedHighCollisionWarning ? "YES" : "no")}");
		Debug.Log($"Runtime          : {stopwatch.Elapsed.TotalSeconds:F1} s");
		Debug.Log("═══════════════════════════════════════════════════════\n");
	}

	void UpdateStatus()
	{
		string m = mode.ToString();
		if (mode == TestMode.TrueRandomBaseline) m = "RANDOM (baseline)";
		statusText = $"Batch {batchNumber} • {totalHashes:N0} hashes • {mode}";
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

		GUI.Label(new Rect(x, y, w, 28), $"Pairs: {totalCollisionPairs:N0}   Buckets: {(modeUsesBigInt() ? countMapBig.Count : countMap64.Count):N0} / {modulus}");
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

		// Sliders
		GUI.Label(new Rect(x, y, 180, 30), "Hashes / batch");
		hashesPerBatch = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 190, y, 220, 30), hashesPerBatch, 1000, 500000));
		GUI.Label(new Rect(x + 420, y, 100, 30), hashesPerBatch.ToString("N0"));
		y += 40;

		// --- String length slider with automatic max clamping ---
		GUI.Label(new Rect(x, y, 180, 30), "String length");

		int maxLen = GetMaxSafeLengthForMode();
		int current = Mathf.Clamp(stringLength, 4, maxLen); // force into valid range

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
		m = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x + 190, y, 220, 30), m, 0, 3));
		mode = (TestMode)m;
		GUI.Label(new Rect(x + 420, y, 240, 30), mode.ToString());
		y += 40;

		GUI.Label(new Rect(x, y, 300, 30), $"Radix: {radix}    Modulus: {modulus}");
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

		countMap64.Clear();
		countMapBig.Clear();

		stopwatch.Reset();
		stopwatch.Start();

		UpdateModulus();
		UpdateStatus();

		Debug.Log($"Started collision test — mode: {mode}  radix^{stringLength} = {modulus:N0}");
	}
}