using UnityEngine;
using System;
using System.Numerics;

namespace MassiveHadronLtd.IDs.HTB50
{
	public class HTB50CodecStressTester : MonoBehaviour
	{
		[Header("Initial Settings")]
		public int testsPerBatchBase = 10000;      // Starting point — will grow with intensity
		public int maxFixedLength = 6;
		public bool testWithFlavor = false;
		public char padChar = '0';

		[Header("GUI Style")]
		public int labelFontSize = 16;
		public int buttonHeight = 40;
		public int spacing = 8;

		// Runtime state
		private bool isRunning = false;
		private bool pauseRequested = false;

		private int currentBatch = 0;
		private int totalTests = 0;
		private int totalFailures = 0;
		private int skippedValues = 0;

		private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

		private int testsDoneThisBatch = 0;
		private string statusMessage = "Ready";

		private int currentIntensity = 1;

		void Start()
		{
			Debug.Log($"HTB50 Codec Long-Running Stress Tester attached to {gameObject.name}");
			Debug.Log("Use GUI to start / pause / stop. Can run for hours.");
		}

		void Update()
		{
			if (!isRunning || pauseRequested) return;

			int targetThisBatch = GetTestsPerBatch();
			int remaining = targetThisBatch - testsDoneThisBatch;
			if (remaining <= 0)
			{
				FinishBatch();
				return;
			}

			int chunk = Mathf.Min(2000, remaining);
			RunTestChunk(chunk);
			testsDoneThisBatch += chunk;
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = labelFontSize;
			GUI.skin.button.fontSize = labelFontSize - 2;

			float y = 20f;
			float w = Screen.width * 0.9f;
			float x = Screen.width * 0.05f;

			GUI.Label(new Rect(x, y, w, 40), "HTB50 Codec Stress Tester", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 22 });
			y += 50;

			GUI.Label(new Rect(x, y, w, 30), $"Status: {statusMessage}", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.cyan } });
			y += 40;

			GUI.Label(new Rect(x, y, w, 25), $"Batch: {currentBatch}   Intensity: {currentIntensity}");
			y += 25;
			float failureRate = totalTests > 0 ? (totalFailures * 100f / totalTests) : 0f;
			GUI.Label(new Rect(x, y, w, 25), $"Total tests: {totalTests:N0}   Failures: {totalFailures} ({failureRate:F5}%)   Skipped: {skippedValues}");
			y += 40;

			if (isRunning)
			{
				float prog = (float)testsDoneThisBatch / GetTestsPerBatch();
				GUI.Label(new Rect(x, y, w, 25), $"Progress: {(prog * 100):F1}%");
				GUI.HorizontalScrollbar(new Rect(x, y + 25, w, 20), 0, prog, 0, 1);
				y += 60;
			}

			if (!isRunning)
			{
				if (GUI.Button(new Rect(x, y, 180, buttonHeight), "Start Testing"))
				{
					StartTesting();
				}
			}
			else
			{
				if (GUI.Button(new Rect(x, y, 180, buttonHeight), pauseRequested ? "Resume" : "Pause"))
				{
					pauseRequested = !pauseRequested;
					statusMessage = pauseRequested ? "PAUSED" : "Running...";
				}

				if (GUI.Button(new Rect(x + 200, y, 180, buttonHeight), "Stop & Reset"))
				{
					ResetTester();
				}
			}

			y += buttonHeight + spacing;

			if (!isRunning)
			{
				if (GUI.Button(new Rect(x, y, 260, buttonHeight), "Run One Batch Now"))
				{
					StartSingleBatch();
				}
			}

			y += buttonHeight + spacing + 10;

			GUI.Label(new Rect(x, y, 220, 30), "Base tests / batch:");
			testsPerBatchBase = (int)GUI.HorizontalSlider(new Rect(x + 230, y, 200, 30), testsPerBatchBase, 1000, 100000);
			GUI.Label(new Rect(x + 440, y, 100, 30), testsPerBatchBase.ToString("N0"));
			y += 40;

			GUI.Label(new Rect(x, y, 220, 30), "Max fixed length:");
			maxFixedLength = (int)GUI.HorizontalSlider(new Rect(x + 230, y, 200, 30), maxFixedLength, 2, 20);
			GUI.Label(new Rect(x + 440, y, 100, 30), maxFixedLength.ToString());
			y += 50;
		}

		private int GetTestsPerBatch() => testsPerBatchBase * currentIntensity;

		private void StartTesting()
		{
			ResetTester(false);
			isRunning = true;
			pauseRequested = false;
			statusMessage = "Starting...";
			stopwatch.Start();
			StartNewBatch();
		}

		private void StartSingleBatch()
		{
			if (isRunning) return;
			ResetTester(false);
			isRunning = true;
			pauseRequested = false;
			statusMessage = "One batch...";
			stopwatch.Start();
			StartNewBatch();
		}

		private void ResetTester(bool log = true)
		{
			isRunning = false;
			pauseRequested = false;
			currentBatch = 0;
			currentIntensity = 1;
			totalTests = 0;
			totalFailures = 0;
			skippedValues = 0;
			testsDoneThisBatch = 0;
			stopwatch.Reset();
			statusMessage = "Ready (reset)";
			if (log) Debug.Log("Tester reset.");
		}

		private void StartNewBatch()
		{
			currentBatch++;
			testsDoneThisBatch = 0;
			Debug.Log($"\n════ Batch #{currentBatch}  Intensity {currentIntensity}  {stopwatch.Elapsed.TotalSeconds:F1}s ════");
			statusMessage = $"Batch {currentBatch}  0 / {GetTestsPerBatch():N0}";
		}

		private void FinishBatch()
		{
			Debug.Log($"Batch #{currentBatch} finished  {stopwatch.Elapsed.TotalSeconds:F1}s  Failures: {totalFailures}");

			PrintBatchSummary();

			currentIntensity = Mathf.Min(currentIntensity + 1, 20);  // cap growth

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
			{
				isRunning = false;
				statusMessage = "Stopped (Shift held)";
				Debug.Log("Stopped after batch (Shift held)");
			}
			else if (isRunning)
			{
				StartNewBatch();
			}
			else
			{
				statusMessage = "Finished";
			}
		}

		private void RunTestChunk(int count)
		{
			int failuresThisChunk = 0;
			var rng = new System.Random(currentBatch * 987654321 + 123456);

			for (int i = 0; i < count; i++)
			{
				int testType = rng.Next(3);               // 0=Big, 1=long, 2=int
				int len = rng.Next(1, maxFixedLength + 1 + currentIntensity / 2);

				BigInteger mod = BigInteger.Pow(HTB50.Radix, len);

				if (testType == 0) // BigInteger
				{
					BigInteger val = GeneratePositiveBigInt(rng, mod, currentIntensity);
					if (val < 0)
					{
						skippedValues++;
						continue;
					}
					failuresThisChunk += TestBigInteger(val, len, mod);
				}
				else if (testType == 1) // long
				{
					long val = GeneratePositiveLong(rng, mod);
					if (val < 0) { skippedValues++; continue; }
					failuresThisChunk += TestLong(val, len);
				}
				else // int
				{
					int val = GeneratePositiveInt(rng, mod);
					if (val < 0) { skippedValues++; continue; }
					failuresThisChunk += TestInt(val, len);
				}
			}

			totalTests += count;
			totalFailures += failuresThisChunk;
			statusMessage = $"Batch {currentBatch}  {testsDoneThisBatch + count:N0} / {GetTestsPerBatch():N0}";
		}

		// ────────────────────────────────────────────────
		// Value generators — always positive
		// ────────────────────────────────────────────────

		private BigInteger GeneratePositiveBigInt(System.Random rng, BigInteger mod, int intensity)
		{
			int style = rng.Next(5);
			if (style == 0) return rng.Next(0, 100000);
			if (style == 1) return (BigInteger)(rng.NextDouble() * 1e18);
			if (style == 2) return mod - 1 - rng.Next(500);

			int byteLen = rng.Next(1, (maxFixedLength + intensity) * 2 + 4);
			byte[] bytes = new byte[byteLen];
			rng.NextBytes(bytes);

			// Treat bytes as unsigned
			return new BigInteger(bytes, isUnsigned: true, isBigEndian: false) % (mod * BigInteger.Max(BigInteger.One, intensity));
		}

		private long GeneratePositiveLong(System.Random rng, BigInteger mod)
		{
			if (mod > long.MaxValue) return (long)(rng.NextDouble() * (long.MaxValue / 2));
			return (long)(rng.NextDouble() * (long)mod);
		}

		private int GeneratePositiveInt(System.Random rng, BigInteger mod)
		{
			if (mod > int.MaxValue) return rng.Next(0, int.MaxValue);
			return (int)(rng.NextDouble() * (int)mod);
		}

		// ────────────────────────────────────────────────
		// Round-trip tests — return number of failures
		// ────────────────────────────────────────────────

		private int TestBigInteger(BigInteger val, int len, BigInteger mod)
		{
			int fail = 0;

			try
			{
				string enc = HTB50.EncodeBigInteger(val, testWithFlavor);
				if (HTB50.DecodeBigInteger(enc) != val) fail++;
			}
			catch (Exception ex)
			{
				Debug.LogError($"EncodeBigInteger threw: {ex.Message}  val={val}");
				fail++;
			}

			if (val < mod)
			{
				try
				{
					string fenc = HTB50.EncodeFixed(val, len, testWithFlavor, padChar);
					if (HTB50.DecodeBigInteger(fenc) != val) fail++;
				}
				catch (Exception ex)
				{
					Debug.LogError($"EncodeFixed threw unexpectedly: {ex.Message}  val={val} len={len}");
					fail++;
				}
			}

			if (fail > 0 && totalFailures < 20)
				Debug.LogError($"Big FAIL  val={val}  len={len}");

			return fail;
		}

		private int TestLong(long val, int len)
		{
			int fail = 0;

			string enc = HTB50.Encode64(val, testWithFlavor);
			if (HTB50.Decode64(enc) != val) fail++;

			string fenc = HTB50.Encode64(val, testWithFlavor, fixedLength: len, padChar: padChar);
			if (HTB50.Decode64(fenc) != val) fail++;

			if (fail > 0 && totalFailures < 20)
				Debug.LogError($"long FAIL  val={val}  len={len}");

			return fail;
		}

		private int TestInt(int val, int len)
		{
			int fail = 0;

			string enc = HTB50.Encode(val, testWithFlavor);
			if (HTB50.Decode(enc) != val) fail++;

			string fenc = HTB50.Encode(val, testWithFlavor, fixedLength: len, padChar: padChar);
			if (HTB50.Decode(fenc) != val) fail++;

			if (fail > 0 && totalFailures < 20)
				Debug.LogError($"int FAIL  val={val}  len={len}");

			return fail;
		}

		private void PrintBatchSummary()
		{
			Debug.Log($"──── Batch #{currentBatch} Summary ────");
			Debug.Log($"Tests:          {GetTestsPerBatch():N0}");
			Debug.Log($"Intensity:      {currentIntensity}");
			Debug.Log($"Failures:       {totalFailures}");
			Debug.Log($"Skipped values: {skippedValues}");
			Debug.Log($"Time:           {stopwatch.Elapsed.TotalSeconds:F1} s");
		}

		void OnDestroy()
		{
			Debug.Log("HTB50 Codec Stress Tester destroyed.");
			if (totalTests > 0)
				PrintBatchSummary();
		}
	}
}


//using UnityEngine;
//using System;
//using System.Numerics;
//using System.Linq;

//namespace MassiveHadronLtd.IDs.HTB50
//{
//	/// <summary>
//	/// Linear / exhaustive-ish stress tester for HTB50 encoding/decoding correctness.
//	/// Tests BigInteger, long (Int64), and int (Int32) paths over sensible ranges.
//	/// Runs once on Start(), logs results to console.
//	/// </summary>
//	public class HTB50LinearStressTest : MonoBehaviour
//	{
//		[Header("Test Settings")]
//		public int bigIntegerTests = 10000;         // Number of random BigInteger tests
//		public long int64MaxTestValue = long.MaxValue / 2;
//		public int int32MaxTestValue = int.MaxValue / 2;
//		public int fixedLength = 6;
//		public bool testWithFlavor = false;
//		public char padChar = '0';

//		private System.Diagnostics.Stopwatch stopwatch = new();
//		private int failures = 0;

//		void Start()
//		{
//			Debug.Log("Starting HTB50 Linear Stress Test...");
//			stopwatch.Start();

//			RunBigIntegerTests();
//			RunInt64Tests();
//			RunInt32Tests();

//			stopwatch.Stop();

//			if (failures == 0)
//			{
//				Debug.Log($"ALL TESTS PASSED! Total time: {stopwatch.Elapsed.TotalSeconds:F2} s");
//			}
//			else
//			{
//				Debug.LogError($"Tests completed with {failures} failures. Total time: {stopwatch.Elapsed.TotalSeconds:F2} s");
//			}
//		}

//		private void RunBigIntegerTests()
//		{
//			Debug.Log($"Running {bigIntegerTests} BigInteger encode/decode tests...");
//			BigInteger modulus = BigInteger.Pow(HTB50.Radix, fixedLength);
//			var rng = new System.Random(123456789);  // fixed seed → reproducible

//			for (int i = 0; i < bigIntegerTests; i++)
//			{
//				BigInteger val;

//				if (i < 2000)
//					val = i;                                // small sequential
//				else if (i < 6000)
//					val = (BigInteger)(rng.NextDouble() * 1e18);  // medium
//				else
//				{
//					// large random, but limit size roughly to fixedLength + a bit
//					int byteLen = rng.Next(1, (fixedLength * 6 / 8) + 8);
//					byte[] bytes = new byte[byteLen];
//					rng.NextBytes(bytes);
//					val = new BigInteger(bytes.Concat(new byte[] { 0 }).ToArray());
//				}

//				// Variable-length round-trip
//				string encoded = HTB50.EncodeBigInteger(val, appendFlavor: testWithFlavor);
//				BigInteger decoded = HTB50.DecodeBigInteger(encoded);

//				if (decoded != val)
//				{
//					failures++;
//					if (failures <= 10)
//						Debug.LogError($"BigInteger var-len FAIL #{failures}: {val} → \"{encoded}\" → {decoded}");
//				}

//				// Fixed-length round-trip (only when value actually fits)
//				if (val < modulus)
//				{
//					string fixedEnc = HTB50.EncodeFixed(val, fixedLength, appendFlavor: testWithFlavor, padChar: padChar);
//					BigInteger decFixed = HTB50.DecodeBigInteger(fixedEnc);

//					if (decFixed != val)
//					{
//						failures++;
//						if (failures <= 10)
//							Debug.LogError($"BigInteger fixed-len FAIL #{failures} (len={fixedLength}): {val} → \"{fixedEnc}\" → {decFixed}");
//					}
//				}
//			}

//			Debug.Log($"BigInteger tests done. Failures so far: {failures}");
//		}

//		private void RunInt64Tests()
//		{
//			Debug.Log("Running long (Int64) encode/decode tests...");
//			var rng = new System.Random(987654321);  // different seed

//			int testCount = 20000;  // more because fast path

//			for (int i = 0; i < testCount; i++)
//			{
//				long val = (long)(rng.NextDouble() * int64MaxTestValue);

//				// Variable length
//				string encoded = HTB50.Encode64(val, appendFlavor: testWithFlavor);
//				long decoded = HTB50.Decode64(encoded);

//				if (decoded != val)
//				{
//					failures++;
//					if (failures <= 10)
//						Debug.LogError($"Int64 var-len FAIL #{failures}: {val} → \"{encoded}\" → {decoded}");
//				}

//				// Fixed length (only if it fits without overflow)
//				if (val < BigInteger.Pow(HTB50.Radix, fixedLength))
//				{
//					string fixedEnc = HTB50.Encode64(val, appendFlavor: testWithFlavor,
//													 fixedLength: fixedLength, padChar: padChar);
//					long decFixed = HTB50.Decode64(fixedEnc);

//					if (decFixed != val)
//					{
//						failures++;
//						if (failures <= 10)
//							Debug.LogError($"Int64 fixed-len FAIL #{failures} (len={fixedLength}): {val} → \"{fixedEnc}\" → {decFixed}");
//					}
//				}
//			}

//			Debug.Log($"Int64 tests done. Failures so far: {failures}");
//		}

//		private void RunInt32Tests()
//		{
//			Debug.Log("Running int (Int32) encode/decode tests...");
//			var rng = new System.Random(456789123);  // different seed

//			int testCount = 20000;

//			// Precompute the maximum value that fits in fixedLength digits (as BigInteger)
//			BigInteger modulusBI = BigInteger.Pow(HTB50.Radix, fixedLength);

//			// For Int32 path, cap at what int can actually hold
//			int maxSafeValueForInt32;
//			if (modulusBI > int.MaxValue)
//			{
//				maxSafeValueForInt32 = int.MaxValue;
//				Debug.Log($"Note: modulus for length {fixedLength} exceeds int.MaxValue → capping Int32 tests at {int.MaxValue}");
//			}
//			else
//			{
//				maxSafeValueForInt32 = (int)modulusBI;
//			}

//			for (int i = 0; i < testCount; i++)
//			{
//				// Generate value safely within int range
//				int val = rng.Next(0, maxSafeValueForInt32);

//				// Variable length round-trip
//				string encoded = HTB50.Encode(val, appendFlavor: testWithFlavor);
//				int decoded = HTB50.Decode(encoded);

//				if (decoded != val)
//				{
//					failures++;
//					if (failures <= 10)
//						Debug.LogError($"Int32 var-len FAIL #{failures}: {val} → \"{encoded}\" → {decoded}");
//				}

//				// Fixed-length round-trip — only test when value is guaranteed to fit
//				// We already know val < maxSafeValueForInt32 ≤ modulusBI
//				string fixedEnc = HTB50.Encode(val, appendFlavor: testWithFlavor,
//											   fixedLength: fixedLength, padChar: padChar);
//				int decFixed = HTB50.Decode(fixedEnc);

//				if (decFixed != val)
//				{
//					failures++;
//					if (failures <= 10)
//						Debug.LogError($"Int32 fixed-len FAIL #{failures} (len={fixedLength}): {val} → \"{fixedEnc}\" → {decFixed}");
//				}
//			}

//			Debug.Log($"Int32 tests done. Failures so far: {failures}");
//		}
//	}
//}