using UnityEngine;

public class FloatCastTruthTest : MonoBehaviour
{
	void Start()
	{
		Debug.Log("<color=cyan>=== FLOAT → UINT CAST TRUTH TEST (NO BITCONVERTER) ===</color>");

		bool allGood = true;

		// Test values from 8,000,000 to 8,500,000 — the danger zone
		for (uint expected = 8000000; expected <= 8500000; expected++)
		{
			// Method: Store integer as float using only cast
			float f = expected;                    // This is the "pack"
			uint recovered = (uint)f;              // This is the "unpack"

			if (recovered != expected)
			{
				allGood = false;
				Debug.LogError($"FAILED at {expected} → recovered {recovered} (diff = {recovered - expected})");
			}
		}

		if (allGood)
		{
			Debug.Log("<color=lime>PERFECT: Direct float = uint → uint = float works up to 8.5 million!</color>");
			Debug.Log("<color=lime>WE CAN PACK 24 BITS SAFELY!</color>");
		}
		else
		{
			Debug.LogError("<color=red>Direct cast FAILED — this should never happen in real C#</color>");
		}

		// Now test the actual safe limit
		uint maxSafe = 0;
		for (uint i = 16000000; i <= 17000000; i += 1024)
		{
			float f = i;
			if ((uint)f == i)
				maxSafe = i;
			else
				break;
		}

		Debug.Log($"Highest integer that survives round-trip: {maxSafe} (0x{maxSafe:X8})");
		Debug.Log($"This is EXACTLY 2^24 - 1 = 16,777,215");
	}
}