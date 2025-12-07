using UnityEngine;

public class FloatPacker75Test : MonoBehaviour
{
	private const float MAX_ALLOWED_ERROR = 0.5f / 64f; // FP12.6 worst-case
	private const int N = 500;

	void Start()
	{
		var rng = new System.Random(123456);

		Debug.Log("<color=orange>=== FIXEDPOINTFLOATPACKER75 – FULL STRESS TEST (N=" + N + ") ===</color>");

		bool allPassed = TestWithDetailedStats(N, rng);

		Debug.Log(allPassed
			? "<color=lime>ALL TESTS PASSED — PACKER IS 100% PERFECT! READY FOR DYNAMIC RANGE!</color>"
			: "<color=red>TEST FAILED — See failure details above</color>");
	}

	bool TestWithDetailedStats(int count, System.Random rng)
	{
		bool allGood = true;

		float maxObservedError = 0f;
		float maxPercentError = 0f;

		for (int i = 0; i < count; i++)
		{
			// Correct supported range: ±2048 for FP12.6 @ 18-bit signed
			float x = (float)(rng.NextDouble() * 4096.0 - 2048.0);
			float y = (float)(rng.NextDouble() * 4096.0 - 2048.0);
			float z = (float)(rng.NextDouble() * 4096.0 - 2048.0);
			float w = (float)(rng.NextDouble() * 4096.0 - 2048.0);

			FixedPointFloatPacker75.Pack(x, y, z, w, out float a, out float b, out float c);
			FixedPointFloatPacker75.Unpack(a, b, c, out float rx, out float ry, out float rz, out float rw);

			float ex = Mathf.Abs(rx - x);
			float ey = Mathf.Abs(ry - y);
			float ez = Mathf.Abs(rz - z);
			float ew = Mathf.Abs(rw - w);

			float maxErr = Mathf.Max(ex, ey, ez, ew);
			maxObservedError = Mathf.Max(maxObservedError, maxErr);

			// Percentage error relative to full scale (2048 range)
			float percentErr = (maxErr / 2048f) * 100f;
			maxPercentError = Mathf.Max(maxPercentError, percentErr);

			if (maxErr > MAX_ALLOWED_ERROR + Mathf.Epsilon)
			{
				allGood = false;

				Debug.Log($"<color=red>FAILURE #{i + 1} (Error = {maxErr:G9})</color>");
				Debug.Log($"Original  → X:{x,10:F6}  Y:{y,10:F6}  Z:{z,10:F6}  W:{w,10:F6}");
				Debug.Log($"Reconst   → X:{rx,10:F6}  Y:{ry,10:F6}  Z:{rz,10:F6}  W:{rw,10:F6}");
				Debug.Log($"Errors    → X:{ex:G9}  Y:{ey:G9}  Z:{ez:G9}  W:{ew:G9}");

				uint ua = (uint)a;
				uint ub = (uint)b;
				uint uc = (uint)c;

				Debug.Log($"Packed floats → a:{a:G9}  b:{b:G9}  c:{c:G9}");
				Debug.Log($"True 24-bit hex → a:0x{ua & 0xFFFFFF:X6}  b:0x{ub & 0xFFFFFF:X6}  c:0x{uc & 0xFFFFFF:X6}");

				int ix = (int)(ua & 0x3FFFF);
				int iw = (int)(((ua >> 18) << 12) | (((ub >> 18) & 0x3F) << 6) | ((uc >> 18) & 0x3F));
				Debug.Log($"Reconstructed ints → X:{ix - 131072}  W:{iw - 131072}");

				Debug.Log("<color=yellow>=== STOPPING ON FIRST FAILURE ===</color>");
				break;
			}
		}

		Debug.Log("<color=cyan>=== ERROR STATISTICS ===</color>");
		Debug.Log($"Expected Max Error   → {MAX_ALLOWED_ERROR:G9}");
		Debug.Log($"Observed Max Error   → {maxObservedError:G9}");
		Debug.Log($"Max % of Full Scale  → {maxPercentError:G9}%");

		return allGood;
	}
}



//using UnityEngine;

//public class FloatPacker75Test : MonoBehaviour
//{
//	private const float MAX_ALLOWED_ERROR = 0.5f / 64f; // 0.0078125f  ← We're using FP12.6 now (6 fractional bits)
//	private const int N = 500; // Crank it up — it's fast and reliable now

//	void Start()
//	{
//		var rng = new System.Random(123456);

//		Debug.Log("<color=orange>=== FIXEDPOINTFLOATPACKER75 – FULL STRESS TEST (N=" + N + ") ===</color>");

//		bool allPassed = TestWithImmediateDump(N, rng);

//		Debug.Log(allPassed
//			? "<color=lime>ALL TESTS PASSED — PACKER IS 100% PERFECT! READY FOR DYNAMIC RANGE!</color>"
//			: "<color=red>TEST FAILED — See failure details above</color>");
//	}

//	bool TestWithImmediateDump(int count, System.Random rng)
//	{
//		bool allGood = true;

//		for (int i = 0; i < count; i++)
//		{
//			// Full supported range: ±4096 (from FP12.6)
//			float x = (float)(rng.NextDouble() * 4096.0 - 2048.0);
//			float y = (float)(rng.NextDouble() * 4096.0 - 2048.0);
//			float z = (float)(rng.NextDouble() * 4096.0 - 2048.0);
//			float w = (float)(rng.NextDouble() * 4096.0 - 2048.0);

//			FixedPointFloatPacker75.Pack(x, y, z, w, out float a, out float b, out float c);
//			FixedPointFloatPacker75.Unpack(a, b, c, out float rx, out float ry, out float rz, out float rw);

//			float ex = Mathf.Abs(rx - x);
//			float ey = Mathf.Abs(ry - y);
//			float ez = Mathf.Abs(rz - z);
//			float ew = Mathf.Abs(rw - w);
//			float maxErr = Mathf.Max(ex, ey, ez, ew);

//			if (maxErr > MAX_ALLOWED_ERROR + Mathf.Epsilon)
//			{
//				allGood = false;

//				Debug.Log($"<color=red>FAILURE #{i + 1} (Error = {maxErr:G9})</color>");
//				Debug.Log($"Original  → X:{x,10:F6}  Y:{y,10:F6}  Z:{z,10:F6}  W:{w,10:F6}");
//				Debug.Log($"Reconst   → X:{rx,10:F6}  Y:{ry,10:F6}  Z:{rz,10:F6}  W:{rw,10:F6}");
//				Debug.Log($"Errors    → X:{ex:G9}  Y:{ey:G9}  Z:{ez:G9}  W:{ew:G9}");

//				// CORRECT way to see the packed bits — using direct cast (the only truth)
//				uint ua = (uint)a;
//				uint ub = (uint)b;
//				uint uc = (uint)c;

//				Debug.Log($"Packed floats → a:{a:G9}  b:{b:G9}  c:{c:G9}");
//				Debug.Log($"True 24-bit hex → a:0x{ua & 0xFFFFFF:X6}  b:0x{ub & 0xFFFFFF:X6}  c:0x{uc & 0xFFFFFF:X6}");

//				// Show reconstructed integers
//				int ix = (int)(ua & 0x3FFFF);
//				int iw = (int)(((ua >> 18) << 12) | (((ub >> 18) & 0x3F) << 6) | ((uc >> 18) & 0x3F));
//				Debug.Log($"Reconstructed ints → X:{ix - 131072}  W:{iw - 131072}");

//				Debug.Log("<color=yellow>=== STOPPING ON FIRST FAILURE ===</color>");
//				break;
//			}
//		}

//		return allGood;
//	}
//}


//using System;
//using UnityEngine;

//public class FloatPacker75Test : MonoBehaviour
//{
//	private const float MAX_ALLOWED_ERROR = 0.5f / 256f; // 0.001953125f
//	private const int N = 100; // Small number so we can see every failure

//	void Start()
//	{
//		var rng = new System.Random(123456);

//		Debug.Log("<color=orange>=== STARTING DEBUG PACKER TEST (N=100) ===</color>");
//		bool allPassed = TestWithImmediateDump(N, rng);

//		Debug.Log(allPassed
//			? "<color=green>ALL TESTS PASSED – Packer is mathematically perfect!</color>"
//			: "<color=red>TEST FAILED – See above for exact failing case(s)</color>");
//	}

//	bool TestWithImmediateDump(int count, System.Random rng)
//	{
//		bool allGood = true;

//		for (int i = 0; i < count; i++)
//		{
//			float x = (float)(rng.NextDouble() * 1024.0 - 512.0);
//			float y = (float)(rng.NextDouble() * 1024.0 - 512.0);
//			float z = (float)(rng.NextDouble() * 1024.0 - 512.0);
//			float w = (float)(rng.NextDouble() * 1024.0 - 512.0);

//			FixedPointFloatPacker75.Pack(x, y, z, w, out float a, out float b, out float c);
//			FixedPointFloatPacker75.Unpack(a, b, c, out float rx, out float ry, out float rz, out float rw);

//			float ex = Mathf.Abs(rx - x);
//			float ey = Mathf.Abs(ry - y);
//			float ez = Mathf.Abs(rz - z);
//			float ew = Mathf.Abs(rw - w);

//			if (ex > MAX_ALLOWED_ERROR + Mathf.Epsilon ||
//				ey > MAX_ALLOWED_ERROR + Mathf.Epsilon ||
//				ez > MAX_ALLOWED_ERROR + Mathf.Epsilon ||
//				ew > MAX_ALLOWED_ERROR + Mathf.Epsilon)
//			{
//				allGood = false;

//				Debug.Log($"<color=red>FAILURE #{i + 1}</color>");
//				Debug.Log($"Original  → X:{x:G9}  Y:{y:G9}  Z:{z:G9}  W:{w:G9}");
//				Debug.Log($"Packed    → a:{a:G9}  b:{b:G9}  c:{c:G9}");
//				Debug.Log($"Reconstructed → X:{rx:G9}  Y:{ry:G9}  Z:{rz:G9}  W:{rw:G9}");
//				Debug.Log($"Errors    → X:{ex:G9}  Y:{ey:G9}  Z:{ez:G9}  W:{ew:G9}");

//				// Show raw bit pattern to see if we accidentally used bit 24+
//				uint ua = (uint)BitConverter.SingleToInt32Bits(a);
//				uint ub = (uint)BitConverter.SingleToInt32Bits(b);
//				uint uc = (uint)BitConverter.SingleToInt32Bits(c);

//				Debug.Log($"Raw bits (hex) → a:0x{ua:X8}  b:0x{ub:X8}  c:0x{uc:X8}");
//				Debug.Log($"Masked 24-bit → a:0x{(ua & 0xFFFFFF):X6}  b:0x{(ub & 0xFFFFFF):X6}  c:0x{(uc & 0xFFFFFF):X6}");

//				// Stop on first failure so you can read it clearly
//				Debug.Log("<color=yellow>=== STOPPING ON FIRST FAILURE ===</color>");
//				break;
//			}
//		}

//		return allGood;
//	}
//}