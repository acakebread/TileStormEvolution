using UnityEngine;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Random = UnityEngine.Random;

namespace MassiveHadronLtd.IDs.HTB50
{
	public class RadixHashTester : MonoBehaviour
	{
		[Header("Settings")]
		public int hashesPerBatch = 1000;
		public int fixedLength = 6;

		[Header("Logging & Alerts")]
		[Tooltip("Alert if observed pairs > expected pairs × this factor")]
		public float alertThreshold = 5.0f; // 5× is reasonable for small batches (rare false positives)

		[Header("GUI")]
		public int labelFontSize = 16;
		public int buttonHeight = 40;
		public int spacing = 8;

		// State
		private bool isRunning;
		private bool paused;

		private int batchCount;
		private int doneThisBatch;

		private long totalHashes;
		private long totalPairs;
		private long batchPairsBefore;

		private bool highCollisionAlert;
		private string status = "Ready";

		private Dictionary<BigInteger, int> bucketCounts = new();

		private System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

		private BigInteger modulus;
		private readonly int radix = HTB50.Radix;

		void Start()
		{
			UpdateModulus();
		}

		void UpdateModulus()
		{
			modulus = BigInteger.Pow(radix, fixedLength);
		}

		void Update()
		{
			if (!isRunning || paused) return;

			int remaining = hashesPerBatch - doneThisBatch;
			if (remaining <= 0)
			{
				FinishBatch();
				return;
			}

			RunChunk(Mathf.Min(2000, remaining));
		}

		void RunChunk(int count)
		{
			var rng = new System.Random(batchCount * 10007 + doneThisBatch);

			for (int i = 0; i < count; i++)
			{
				string input = GenerateInput(rng, doneThisBatch);
				BigInteger h = RadixHash.HashToRange(input, modulus);

				if (!bucketCounts.TryGetValue(h, out int c))
				{
					bucketCounts[h] = 1;
				}
				else
				{
					bucketCounts[h] = c + 1;
					totalPairs += c; // correct incremental pair count
				}

				doneThisBatch++;
				totalHashes++;
			}

			EvaluateAlert();
			status = $"Batch {batchCount} – {doneThisBatch:N0} / {hashesPerBatch:N0}";
		}

		void EvaluateAlert()
		{
			long pairsThisBatch = totalPairs - batchPairsBefore;
			double expectedPairs = (double)doneThisBatch * (doneThisBatch - 1) / (2.0 * (double)modulus);

			if (pairsThisBatch > expectedPairs * alertThreshold)
			{
				highCollisionAlert = true;
			}
		}

		void FinishBatch()
		{
			PrintBatchSummary();

			if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
			{
				isRunning = false;
				PrintFinalReport();
				status = "Stopped (Shift held)";
			}
			else
			{
				NewBatch();
			}
		}

		void NewBatch()
		{
			batchCount++;
			doneThisBatch = 0;
			batchPairsBefore = totalPairs;
			bucketCounts.Clear();
			status = $"Batch {batchCount} – 0 / {hashesPerBatch:N0}";
		}

		void StartRun()
		{
			isRunning = true;
			paused = false;
			highCollisionAlert = false;

			batchCount = 0;
			doneThisBatch = 0;
			totalHashes = 0;
			totalPairs = 0;
			bucketCounts.Clear();

			watch.Reset();
			watch.Start();

			NewBatch();
		}

		void PrintBatchSummary()
		{
			long pairsThisBatch = totalPairs - batchPairsBefore;
			double expectedPairs = (double)hashesPerBatch * (hashesPerBatch - 1) / (2.0 * (double)modulus);

			if (pairsThisBatch > 0)
			{
				Debug.Log(
					$"Batch #{batchCount} | Hashes {hashesPerBatch:N0} | " +
					$"Pairs {pairsThisBatch:N0} (exp ~{expectedPairs:F4}) | " +
					$"Buckets {bucketCounts.Count:N0}");
			}

			if (pairsThisBatch > expectedPairs * alertThreshold)
			{
				Debug.LogWarning(
					$">>> HIGH COLLISION RATE in batch {batchCount}! " +
					$"Pairs {pairsThisBatch:N0} vs exp ~{expectedPairs:F4}");
			}
		}

		void PrintFinalReport()
		{
			double overallExpected = (double)totalHashes * (totalHashes - 1) / (2.0 * (double)modulus);

			Debug.Log("\n═══════════════════════════════════════════════");
			Debug.Log("          FINAL COLLISION TEST REPORT          ");
			Debug.Log("═══════════════════════════════════════════════");
			Debug.Log($"Total batches:          {batchCount}");
			Debug.Log($"Total hashes tested:    {totalHashes:N0}");
			Debug.Log($"Total colliding pairs:  {totalPairs:N0}");
			Debug.Log($"Overall expected pairs: ~{overallExpected:F2}");
			Debug.Log($"Modulus:                {modulus:N0} (length {fixedLength})");
			Debug.Log($"High collision alert?   {(highCollisionAlert ? "YES" : "No")}");
			Debug.Log($"Total run time:         {watch.Elapsed.TotalSeconds:F1} seconds");
			Debug.Log("═══════════════════════════════════════════════\n");
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = labelFontSize;
			GUI.skin.button.fontSize = labelFontSize - 2;

			float x = Screen.width * 0.05f;
			float w = Screen.width * 0.9f;
			float y = 20;

			GUI.Label(new Rect(x, y, w, 40),
				"RadixHash Collision Check",
				new GUIStyle(GUI.skin.label)
				{
					fontStyle = FontStyle.Bold,
					fontSize = 22
				});
			y += 50;

			GUI.Label(new Rect(x, y, w, 30),
				$"Status: {status}",
				new GUIStyle(GUI.skin.label)
				{
					normal = { textColor = Color.cyan }
				});
			y += 40;

			GUI.Label(new Rect(x, y, w, 25),
				$"Batch: {batchCount}   Hashes: {doneThisBatch:N0} / {hashesPerBatch:N0}");
			y += 30;

			GUI.Label(new Rect(x, y, w, 25),
				$"Pairs this batch: {totalPairs - batchPairsBefore:N0} (total {totalPairs:N0})");
			y += 30;

			GUI.Label(new Rect(x, y, w, 25),
				$"Buckets: {bucketCounts.Count:N0} / {modulus:N0}");
			y += 40;

			if (isRunning)
			{
				float p = (float)doneThisBatch / hashesPerBatch;
				GUI.Label(new Rect(x, y, w, 25), $"Progress: {(p * 100):F1}%");
				GUI.HorizontalScrollbar(new Rect(x, y + 25, w, 20), 0, p, 0, 1);
				y += 60;
			}

			// RED ALERT - top-right, only when triggered
			double expected = (double)hashesPerBatch * (hashesPerBatch - 1) / (2.0 * (double)modulus);
			long pairsThisBatch = totalPairs - batchPairsBefore;
			if (pairsThisBatch > expected * alertThreshold)
			{
				GUI.color = new Color(1f, 0.2f, 0.2f, 0.9f);
				GUI.Label(
					new Rect(Screen.width - 420, 20, 400, 90),
					"HIGH COLLISION RATE DETECTED\n" +
					$"Batch pairs: {pairsThisBatch:N0}\n" +
					$"Expected ~{expected:F4}",
					new GUIStyle(GUI.skin.label)
					{
						fontStyle = FontStyle.Bold,
						fontSize = 18,
						alignment = TextAnchor.MiddleRight
					});
				GUI.color = Color.white;
			}

			if (!isRunning)
			{
				if (GUI.Button(new Rect(x, y, 180, buttonHeight), "Start"))
					StartRun();
			}
			else
			{
				if (GUI.Button(new Rect(x, y, 180, buttonHeight), paused ? "Resume" : "Pause"))
					paused = !paused;

				if (GUI.Button(new Rect(x + 200, y, 180, buttonHeight), "Stop & Report"))
				{
					isRunning = false;
					PrintFinalReport();
					status = "Stopped";
				}
			}

			y += buttonHeight + spacing;

			GUI.Label(new Rect(x, y, 220, 30), "Hashes / batch:");
			hashesPerBatch = (int)GUI.HorizontalSlider(
				new Rect(x + 230, y, 200, 30),
				hashesPerBatch, 1000, 100000);
			GUI.Label(new Rect(x + 440, y, 120, 30),
				hashesPerBatch.ToString("N0"));
			y += 40;

			GUI.Label(new Rect(x, y, 220, 30), "Fixed length:");
			int nl = (int)GUI.HorizontalSlider(
				new Rect(x + 230, y, 200, 30),
				fixedLength, 2, 12);
			GUI.Label(new Rect(x + 440, y, 120, 30),
				fixedLength.ToString());

			if (nl != fixedLength)
			{
				fixedLength = nl;
				UpdateModulus();
			}
		}

		string GenerateInput(System.Random rng, int index)
		{
			var sb = new StringBuilder(64);
			sb.Append(index.ToString("D7")).Append('-');

			switch (rng.Next(6))
			{
				case 0: sb.Append("u/").Append(rng.Next(1000000)); break;
				case 1: sb.Append("prod-").Append(Guid.NewGuid().ToString("N")[..12]); break;
				case 2: sb.Append("file/").Append(new string('x', rng.Next(8, 32))); break;
				case 3: sb.Append("order#").Append(DateTime.UtcNow.Ticks); break;
				case 4: sb.Append("user.").Append(rng.Next(100000)).Append("@ex.net"); break;
				default: sb.Append("rand-").Append(rng.NextDouble().ToString("F12")); break;
			}

			return sb.ToString();
		}
	}
}