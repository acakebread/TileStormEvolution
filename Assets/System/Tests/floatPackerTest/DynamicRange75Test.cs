//using System;
//using UnityEngine;

//public class DynamicRange75Test : MonoBehaviour
//{
//	private const float MAX_ALLOWED_ABS_ERROR = 1.1f; // Even in largest range, error should never exceed ~1.0

//	private void Start()
//	{
//		var rng = new System.Random(123456);
//		bool allPassed = true;

//		Debug.Log("<color=orange>=== DYNAMIC RANGE 75 DEBUG TEST – FAIL-FAST MODE ===</color>");

//		for (int i = 0; i < 500; i++) // Small N so we can read everything
//		{
//			// Generate value in known safe range
//			int forcedRange = i % 7; // Force every range
//			float maxVal = forcedRange switch
//			{
//				0 => 30000000f,   // Ultra
//				1 => 2000000f,    // Extreme
//				2 => 100000f,     // High
//				3 => 8000f,       // Normal
//				4 => 500f,        // Fine
//				5 => 30f,         // Precision
//				6 => 1.9f,        // UltraFine
//				_ => 500f
//			};

//			float v = (float)(rng.NextDouble() * 2 - 1) * maxVal;
//			float x = v;
//			float y = v * 0.97f;
//			float z = v * 1.03f;
//			float w = v * 0.88f;

//			FixedPointFloatPackerDynamicRange75.Pack(x, y, z, w, out float pa, out float pb, out float pc);
//			FixedPointFloatPackerDynamicRange75.Unpack(pa, pb, pc, out float rx, out float ry, out float rz, out float rw);

//			float errX = Mathf.Abs(rx - x);
//			float errY = Mathf.Abs(ry - y);
//			float errZ = Mathf.Abs(rz - z);
//			float errW = Mathf.Abs(rw - w);
//			float maxErr = Mathf.Max(errX, errY, errZ, errW);

//			if (maxErr > MAX_ALLOWED_ABS_ERROR)
//			{
//				allPassed = false;

//				Debug.Log($"<color=red>=== CATASTROPHIC FAILURE #{i} (Range should be {forcedRange}) ===</color>");
//				Debug.Log($"Original  → X:{x,12:G9}  Y:{y,12:G9}  Z:{z,12:G9}  W:{w,12:G9}");
//				Debug.Log($"Reconst   → X:{rx,12:G9}  Y:{ry,12:G9}  Z:{rz,12:G9}  W:{rw,12:G9}");
//				Debug.Log($"Errors    → X:{errX,12:G9}  Y:{errY,12:G9}  Z:{errZ,12:G9}  W:{errW,12:G9}  ← MAX = {maxErr:G9}");

//				// Show which range was actually encoded
//				uint ua = (uint)BitConverter.SingleToInt32Bits(pa) & 0xFFFFFFu;
//				uint ub = (uint)BitConverter.SingleToInt32Bits(pb) & 0xFFFFFFu;
//				uint uc = (uint)BitConverter.SingleToInt32Bits(pc) & 0xFFFFFFu;
//				int encodedRange = (int)(
//					((ua >> 18) & 1) |
//					(((ub >> 19) & 1) << 1) |
//					(((uc >> 20) & 1) << 2)
//				);

//				Debug.Log($"Encoded range bits: {encodedRange} (expected ~{forcedRange})");
//				Debug.Log($"Raw packed floats: a={pa:G9} b={pb:G9} c={pc:G9}");
//				Debug.Log($"Raw hex: a=0x{ua:X6} b=0x{ub:X6} c=0x{uc:X6}");

//				// Stop on first disaster
//				Debug.Log("<color=yellow>=== STOPPING ON FIRST FAILURE ===</color>");
//				break;
//			}
//		}

//		if (allPassed)
//			Debug.Log("<color=lime>ALL 500 TESTS PASSED – DynamicRange75 is PERFECT</color>");
//	}
//}