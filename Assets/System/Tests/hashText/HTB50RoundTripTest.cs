using MassiveHadronLtd.IDs.HTB50;
using System;
using UnityEngine;

public class HTB50ExhaustiveRoundTripTest : MonoBehaviour
{
	private long current = (long)int.MinValue;  // Start from MinValue (as long to avoid overflow in loop)
	private const long TotalValues = 1L << 32;  // 4,294,967,296
	private long tested = 0;
	private bool isRunning = true;
	private bool hasError = false;

	private const int BatchSize = 1000000;      // 1M per frame — adjust if too slow/fast
	private float lastProgress = 0f;

	void Start()
	{
		Debug.Log("=== Starting EXHAUSTIVE HTB50 Encode/Decode Round-trip Test (all 2^32 ints) ===");
		Debug.Log($"Will test {TotalValues:N0} values in batches of {BatchSize:N0} per frame.");
		Debug.Log("Press ESC to stop early.");
	}

	void Update()
	{
		if (!isRunning) return;

		if (Input.GetKeyDown(KeyCode.Escape))
		{
			isRunning = false;
			Debug.LogWarning("Test stopped early by user (ESC pressed).");
			PrintSummary();
			return;
		}

		long processedThisFrame = 0;

		while (processedThisFrame < BatchSize && current <= (long)int.MaxValue)
		{
			int value = (int)current;

			try
			{
				string encoded = HTB50.Encode(value, appendFlavor: false);
				int decoded = HTB50.Decode(encoded);

				if (decoded != value)
				{
					Debug.LogError($"FAIL at {current} (0x{((uint)value):X8}): '{encoded}' → {decoded}");
					hasError = true;
					isRunning = false;
					PrintSummary();
					return;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"EXCEPTION at {current}: {ex.Message}");
				hasError = true;
				isRunning = false;
				PrintSummary();
				return;
			}

			tested++;
			processedThisFrame++;
			current++;
		}

		// Progress reporting (every ~1%)
		float progress = (float)tested / TotalValues * 100f;
		if (progress >= lastProgress + 1f)
		{
			Debug.Log($"Progress: {progress:F2}%  |  {tested:N0} / {TotalValues:N0}  |  current = {current} (0x{((uint)current):X8})");
			lastProgress = (int)progress;
		}

		if (current > int.MaxValue)
		{
			isRunning = false;
			Debug.Log("Reached end of int range.");
			PrintSummary();
		}
	}

	void PrintSummary()
	{
		Debug.Log("\n=== Test Summary ===");
		Debug.Log($"Tested: {tested:N0} values ({(float)tested / TotalValues * 100f:F1}%)");
		Debug.Log($"Errors found: {(hasError ? "YES (see logs)" : "NONE")}");
		Debug.Log("Test complete.");
	}
}

//using UnityEngine;
//using System;
//using MassiveHadronLtd.IDs.HTB50;  // adjust namespace if needed

//public class HTB50RoundTripTest : MonoBehaviour
//{
//	void Start()
//	{
//		TestHTB50RoundTrip();
//		TestHashProducesNegatives();
//	}

//	void TestHTB50RoundTrip()
//	{
//		Debug.Log("=== HTB50 Encode/Decode Round-trip Test ===");

//		// Edge cases
//		TestOne(0, "zero");
//		TestOne(int.MinValue, "int.MinValue");
//		TestOne(int.MaxValue, "int.MaxValue");
//		TestOne(-1, "-1");
//		TestOne(1, "1");
//		TestOne(-123456789, "negative example");
//		TestOne(123456789, "positive example");

//		// A few more random-ish values that often cause off-by-one or sign issues
//		TestOne(unchecked((int)0x80000000), "0x80000000 (minvalue again)");
//		TestOne(unchecked((int)0xFFFFFFFF), "-1 again");
//		TestOne(unchecked((int)0x7FFFFFFF), "maxvalue again");

//		Debug.Log("All selected round-trip tests passed ✓");
//	}

//	void TestOne(int original, string label)
//	{
//		string encoded = HTB50.Encode(original, appendFlavor: false);
//		int decoded = HTB50.Decode(encoded);

//		if (decoded != original)
//		{
//			Debug.LogError($"Round-trip FAILED for {label}: {original} → '{encoded}' → {decoded}");
//		}
//		else
//		{
//			Debug.Log($"OK {label}: {original} → '{encoded}' (len={encoded.Length}) → back to {decoded}");
//		}
//	}

//	void TestHashProducesNegatives()
//	{
//		Debug.Log("\n=== RadixHash.GetStableHash32 sign distribution check ===");

//		int negativeCount = 0;
//		int positiveCount = 0;
//		int zeroCount = 0;
//		const int trials = 10000;

//		for (int i = 0; i < trials; i++)
//		{
//			string input = Guid.NewGuid().ToString() + i;
//			int h = MassiveHadronLtd.RadixHash.GetStableHash32(input);

//			if (h < 0) negativeCount++;
//			else if (h > 0) positiveCount++;
//			else zeroCount++;
//		}

//		Debug.Log($"After {trials} random GUID-based hashes:");
//		Debug.Log($"  Negative: {negativeCount}  ({(negativeCount * 100f / trials):F1}%)");
//		Debug.Log($"  Positive: {positiveCount}  ({(positiveCount * 100f / trials):F1}%)");
//		Debug.Log($"  Zero:     {zeroCount}     ({(zeroCount * 100f / trials):F1}%)");

//		if (negativeCount == 0)
//		{
//			Debug.LogWarning("No negative hashes detected — is your hash function always returning non-negative values?");
//		}
//		else if (negativeCount < trials * 0.3 || negativeCount > trials * 0.7)
//		{
//			Debug.LogWarning("Negative/positive distribution looks very uneven — possible bias in hash function?");
//		}
//		else
//		{
//			Debug.Log("Hash produces both signs reasonably → good for your use-case");
//		}
//	}
//}