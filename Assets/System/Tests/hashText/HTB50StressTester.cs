using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;
using System.Numerics;

namespace MassiveHadronLtd.IDs.HTB50
{
	public class HTB50StressTester : MonoBehaviour
	{
		[Header("Initial Settings (can be changed at runtime)")]
		public int roundTripTestsTarget = 50000;
		public int hashTestsTarget = 200000;
		public int fixedLengthToTest = 6;           // ← now dynamic

		[Header("GUI Style")]
		public int labelFontSize = 16;
		public int buttonHeight = 40;
		public int spacing = 8;

		// Runtime controls / state
		private bool isRunning = false;
		private bool pauseRequested = false;

		private int currentBatch = 0;
		private int totalRoundTrips = 0;
		private int totalFailures = 0;
		private int totalHashes = 0;

		private Dictionary<BigInteger, int> bucketCounts = new();

		private System.Diagnostics.Stopwatch stopwatch = new();

		// Current batch progress
		private int roundTripDone = 0;
		private int hashDone = 0;
		private bool doingRoundTrips = true;

		private string statusMessage = "Ready";

		// Cache modulus once per run (for this fixed length)
		private BigInteger currentModulus;

		void Start()
		{
			Debug.Log($"HTB50 Interactive Tester attached to {gameObject.name}");
			Debug.Log("Use on-screen GUI to start / control testing.");
			UpdateModulus();
		}

		void UpdateModulus()
		{
			currentModulus = BigInteger.Pow(HTB50.Radix, fixedLengthToTest);
		}

		void Update()
		{
			if (!isRunning || pauseRequested) return;

			if (doingRoundTrips)
			{
				int chunkSize = Mathf.Min(2000, roundTripTestsTarget - roundTripDone);
				if (chunkSize > 0)
				{
					RunRoundTripChunk(chunkSize);
					roundTripDone += chunkSize;
				}
				else
				{
					doingRoundTrips = false;
					hashDone = 0;
					statusMessage = "Round-trips done → starting hashes...";
				}
			}
			else
			{
				int chunkSize = Mathf.Min(5000, hashTestsTarget - hashDone);
				if (chunkSize > 0)
				{
					RunHashChunk(chunkSize);
					hashDone += chunkSize;
				}
				else
				{
					FinishBatch();
				}
			}
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = labelFontSize;
			GUI.skin.button.fontSize = labelFontSize - 2;

			float y = 20;
			float w = Screen.width * 0.9f;
			float x = Screen.width * 0.05f;

			GUI.Label(new Rect(x, y, w, 40), "HTB50 Stress Tester", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 22 });
			y += 50;

			GUI.Label(new Rect(x, y, w, 30), $"Status: {statusMessage}", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.cyan } });
			y += 40;

			GUI.Label(new Rect(x, y, w, 25), $"Batches completed: {currentBatch}");
			y += 30;
			float failureRate = totalRoundTrips > 0 ? (totalFailures * 100f / totalRoundTrips) : 0f;
			GUI.Label(new Rect(x, y, w, 25), $"Total round-trips: {totalRoundTrips:N0}   Failures: {totalFailures}  ({failureRate:F5}%)"); y += 30;
			GUI.Label(new Rect(x, y, w, 25), $"Total hashes:      {totalHashes:N0}   Unique buckets: {bucketCounts.Count:N0} / {currentModulus:N0}");
			y += 40;

			if (isRunning)
			{
				float prog = doingRoundTrips
					? (float)roundTripDone / roundTripTestsTarget
					: 0.5f + (float)hashDone / hashTestsTarget * 0.5f;

				GUI.Label(new Rect(x, y, w, 25), $"Current batch progress: {(prog * 100):F1}%");
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

			if (!isRunning || (!doingRoundTrips && hashDone == 0))
			{
				if (GUI.Button(new Rect(x, y, 260, buttonHeight), "Run One Batch Now"))
				{
					StartSingleBatch();
				}
			}

			y += buttonHeight + spacing + 10;

			// Controls
			GUI.Label(new Rect(x, y, 220, 30), $"Round-trip tests / batch:");
			roundTripTestsTarget = (int)GUI.HorizontalSlider(new Rect(x + 230, y, 200, 30),
				roundTripTestsTarget, 1000, 200000);
			GUI.Label(new Rect(x + 440, y, 100, 30), roundTripTestsTarget.ToString("N0"));
			y += 40;

			GUI.Label(new Rect(x, y, 220, 30), $"Hash tests / batch:");
			hashTestsTarget = (int)GUI.HorizontalSlider(new Rect(x + 230, y, 200, 30),
				hashTestsTarget, 10000, 1000000);
			GUI.Label(new Rect(x + 440, y, 100, 30), hashTestsTarget.ToString("N0"));
			y += 40;

			// New: allow changing test length (affects modulus)
			GUI.Label(new Rect(x, y, 220, 30), $"Fixed length to test:");
			int newLength = (int)GUI.HorizontalSlider(new Rect(x + 230, y, 200, 30),
				fixedLengthToTest, 2, 12);
			if (newLength != fixedLengthToTest)
			{
				fixedLengthToTest = newLength;
				UpdateModulus();
			}
			GUI.Label(new Rect(x + 440, y, 100, 30), fixedLengthToTest.ToString());
			y += 50;

			GUI.Label(new Rect(x, y, w, 40), "Detailed results & errors appear in Unity Console", new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.9f, 0.9f, 0.6f) } });
		}

		private void StartTesting()
		{
			ResetTester(false);
			isRunning = true;
			pauseRequested = false;
			statusMessage = "Starting continuous testing...";
			stopwatch.Start();
			StartNewBatch();
		}

		private void StartSingleBatch()
		{
			if (isRunning) return;
			ResetTester(false);
			isRunning = true;
			pauseRequested = false;
			statusMessage = "Running one batch...";
			stopwatch.Start();
			StartNewBatch();
		}

		private void ResetTester(bool log = true)
		{
			isRunning = false;
			pauseRequested = false;
			currentBatch = 0;
			totalRoundTrips = 0;
			totalFailures = 0;
			totalHashes = 0;
			bucketCounts.Clear();
			roundTripDone = 0;
			hashDone = 0;
			doingRoundTrips = true;
			stopwatch.Reset();
			statusMessage = "Ready (reset)";
			if (log) Debug.Log("Tester reset.");
		}

		private void StartNewBatch()
		{
			currentBatch++;
			roundTripDone = 0;
			hashDone = 0;
			doingRoundTrips = true;
			Debug.Log($"\n════ Batch #{currentBatch} started  {stopwatch.Elapsed.TotalSeconds:F1} s ════");
			statusMessage = $"Batch {currentBatch} – Round-trips 0/{roundTripTestsTarget:N0}";
		}

		private void FinishBatch()
		{
			Debug.Log($"Batch #{currentBatch} completed in {stopwatch.Elapsed.TotalSeconds:F1} s total");

			if (totalFailures > 0)
				Debug.LogWarning($"Round-trip failures so far: {totalFailures}");

			PrintBatchSummary();

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
			{
				isRunning = false;
				statusMessage = $"Batch {currentBatch} finished (Shift was held)";
				Debug.Log("Stopped after single batch (Shift held during finish)");
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

		private void RunRoundTripChunk(int count)
		{
			int failuresThisChunk = 0;

			for (int i = 0; i < count; i++)
			{
				int globalIndex = roundTripDone + i;
				BigInteger val;

				if (globalIndex < 5000)
					val = globalIndex;
				else if (globalIndex < 25000)
					val = (BigInteger)(Random.value * 1e12);
				else
				{
					var rng = new System.Random(globalIndex ^ 0x5A5A5A5A);
					byte[] bytes = new byte[Random.Range(8, 33)];
					rng.NextBytes(bytes);
					val = new BigInteger(bytes.Concat(new byte[] { 0 }).ToArray());
				}

				// Use the renamed method
				string encoded = HTB50.EncodeBigInteger(val);
				BigInteger decoded = HTB50.DecodeBigInteger(encoded);

				if (decoded != val)
				{
					failuresThisChunk++;
					if (failuresThisChunk <= 3 || totalFailures < 10)
					{
						Debug.LogError($"Round-trip FAIL @ {globalIndex}\n  {val} → \"{encoded}\" → {decoded}");
					}
				}

				// Test fixed length
				if (val < currentModulus)
				{
					string fixedEnc = HTB50.EncodeFixed(val, fixedLengthToTest, padChar: '0');
					BigInteger decFixed = HTB50.DecodeBigInteger(fixedEnc);
					if (decFixed != val)
					{
						failuresThisChunk++;
						Debug.LogError($"Fixed-len FAIL @ {globalIndex}  length={fixedLengthToTest}  {val} → \"{fixedEnc}\"");
					}
				}
			}

			totalRoundTrips += count;
			totalFailures += failuresThisChunk;

			statusMessage = $"Batch {currentBatch} – Round-trips {roundTripDone + count:N0}/{roundTripTestsTarget:N0}";
		}

		private void RunHashChunk(int count)
		{
			for (int i = 0; i < count; i++)
			{
				int seed = hashDone + i + currentBatch * 1000000;
				string input = GenerateTestString(seed);

				// Use RadixHash instead of HTB50.HashToRange
				BigInteger h = RadixHash.HashToRange(input, modulus: currentModulus);

				bucketCounts.TryGetValue(h, out int cnt);
				bucketCounts[h] = cnt + 1;
			}

			totalHashes += count;
			statusMessage = $"Batch {currentBatch} – Hashes {hashDone + count:N0}/{hashTestsTarget:N0}";
		}

		private string GenerateTestString(int seed)
		{
			var rng = new System.Random(seed);
			int style = rng.Next(7);
			switch (style)
			{
				case 0: return "u/" + rng.Next(1000000, 9999999);
				case 1: return "prod-" + Guid.NewGuid().ToString("N").Substring(0, 12);
				case 2: return "file/2026/" + new string('x', rng.Next(4, 30));
				case 3: return "order#" + DateTime.UtcNow.Ticks + "-" + rng.Next(9999);
				case 4: return "test." + string.Concat(Enumerable.Repeat("a", rng.Next(6, 25)));
				case 5: return "Lorem ipsum " + rng.Next(10000000).ToString();
				default: return "user." + rng.Next(1000, 999999).ToString("D6") + "@example.net";
			}
		}

		private void PrintBatchSummary()
		{
			Debug.Log($"──── Batch #{currentBatch} Summary ────");
			Debug.Log($"Round-trips this batch: {roundTripTestsTarget:N0}");
			Debug.Log($"Hashes this batch:      {hashTestsTarget:N0}");
			Debug.Log($"Test length:            {fixedLengthToTest} chars (modulus = {currentModulus:N0})");

			if (bucketCounts.Count > 0)
			{
				var sizes = bucketCounts.Values;
				int max = sizes.Max();
				int min = sizes.Min();
				double avg = (double)totalHashes / bucketCounts.Count;
				Debug.Log($"Buckets used: {bucketCounts.Count:N0} / {currentModulus:N0}");
				Debug.Log($"Max bucket: {max}   Min: {min}   Avg: {avg:F2}");
			}

			Debug.Log($"Total time: {stopwatch.Elapsed.TotalSeconds:F1} s");
		}

		void OnDestroy()
		{
			Debug.Log("HTB50 Tester destroyed.");
			if (totalRoundTrips > 0 || totalHashes > 0)
				PrintBatchSummary();
		}
	}
}