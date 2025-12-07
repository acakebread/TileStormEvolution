using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stress test for FixedPointFloatPackerDynamicRange75
/// </summary>
public class FloatPackerDynamicRange75Test : MonoBehaviour
{
	const int N = 10000;

	void Start()
	{
		var rng = new System.Random(123456);
		Debug.Log("<color=orange>=== FIXEDPOINTFLOATPACKERDYNAMIC75 – FULL STRESS TEST (N=" + N + ") ===</color>");
		RunTest(N, rng);
	}

	void RunTest(int count, System.Random rng)
	{
		int[] usageCounts = new int[7];
		float observedMaxError = 0f;
		float observedMaxPercentFull = 0f;
		bool allGood = true;

		// Keep a small list of representative samples for each range
		List<string>[] sampleLogs = new List<string>[7];
		for (int i = 0; i < 7; i++)
			sampleLogs[i] = new List<string>();

		for (int i = 0; i < count; i++)
		{
			float x = SampleSafe(rng);
			float y = SampleSafe(rng);
			float z = SampleSafe(rng);
			float w = SampleSafe(rng);

			int chosen = FixedPointFloatPackerDynamicRange75.ChooseRangeIndexForValues(x, y, z, w);
			usageCounts[chosen]++;

			// Clamp values within representable range
			float pre = GetPreScale(chosen);
			float maxVal = (131072 - 1) / (256f * pre);
			x = Mathf.Clamp(x, -maxVal, maxVal);
			y = Mathf.Clamp(y, -maxVal, maxVal);
			z = Mathf.Clamp(z, -maxVal, maxVal);
			w = Mathf.Clamp(w, -maxVal, maxVal);

			FixedPointFloatPackerDynamicRange75.Pack(x, y, z, w, out float a, out float b, out float c, out int rangeIndex);
			if (rangeIndex != chosen)
				Debug.LogError($"Range index mismatch: chosen {chosen} vs returned {rangeIndex}");

			FixedPointFloatPackerDynamicRange75.Unpack(a, b, c, rangeIndex, out float rx, out float ry, out float rz, out float rw);

			float ex = Mathf.Abs(rx - x);
			float ey = Mathf.Abs(ry - y);
			float ez = Mathf.Abs(rz - z);
			float ew = Mathf.Abs(rw - w);

			float lsb_xyz = 1f / (256f * pre);
			float lsb_w = 8f / (256f * pre);
			float expectedMaxErr = Mathf.Max(0.5f * lsb_xyz, 0.5f * lsb_w);

			float maxErr = Mathf.Max(Mathf.Max(ex, ey), Mathf.Max(ez, ew));
			observedMaxError = Mathf.Max(observedMaxError, maxErr);
			observedMaxPercentFull = Mathf.Max(observedMaxPercentFull, (maxErr / 2048f) * 100f);

			if (maxErr > expectedMaxErr + 1e-6f)
			{
				allGood = false;
				Debug.LogError($"FAILURE #{i + 1}: rangeIndex={rangeIndex} expectedMaxErr={expectedMaxErr} observed={maxErr}");
				Debug.LogError($"Orig → x:{x:F6} y:{y:F6} z:{z:F6} w:{w:F6}");
				Debug.LogError($"Rec  → x:{rx:F6} y:{ry:F6} z:{rz:F6} w:{rw:F6}");
				Debug.LogError($"Packed floats → a:{a:G9} b:{b:G9} c:{c:G9}");
				break;
			}

			// Keep a few non-boundary samples per range
			if (sampleLogs[rangeIndex].Count < 3)
			{
				sampleLogs[rangeIndex].Add(
					$"Sample #{i + 1} | Orig=({x:F3},{y:F3},{z:F3},{w:F3}) " +
					$"Packed=({a:G9},{b:G9},{c:G9}) " +
					$"Unpacked=({rx:F3},{ry:F3},{rz:F3},{rw:F3}) " +
					$"Err=({ex:F6},{ey:F6},{ez:F6},{ew:F6})"
				);
			}
		}

		Debug.Log($"TEST RESULT: {(allGood ? "<color=lime>ALL PASSED</color>" : "<color=red>FAIL</color>")}");
		Debug.Log($"Observed Max Error: {observedMaxError:G9}");
		Debug.Log($"Observed Max % of Full Scale (~±2048): {observedMaxPercentFull:G9}%");
		for (int i = 0; i < usageCounts.Length; i++)
		{
			Debug.Log($"Range {i} used: {usageCounts[i]} times");
			foreach (var s in sampleLogs[i])
				Debug.Log($"  {s}");
		}
	}

	static float SampleSafe(System.Random rng)
	{
		// Pick a random pre-scale
		//float[] preScales = new float[] { 64f, 16f, 4f, 1f, 1 / 4f, 1f / 16f, 1f / 64f };
		//float[] preScales = new float[] { 512f, 64f, 8f, 1f, 1 / 8f, 1f / 64f, 1f / 512f };
		float[] preScales = new float[] { 4096f, 256f, 16f, 1f, 1 / 16f, 1f / 256f, 1f / 4096f };
		int idx = rng.Next(preScales.Length);
		float pre = preScales[idx];

		float maxVal = (131072 - 1) / (256f * pre);

		return (float)(rng.NextDouble() * 2.0 * maxVal - maxVal);
	}

	static float GetPreScale(int idx)
	{
		//float[] ps = new float[] { 64f, 16f, 4f, 1f, 1 / 4f, 1f / 16f, 1f / 64f };
		//float[] ps = new float[] { 512f, 64f, 16f, 1f, 1 / 8f, 1f / 64f, 1f / 512f };
		float[] ps = new float[] { 4096f, 256f, 16f, 1f, 1 / 16f, 1f / 256f, 1f / 4096f };
		return ps[Mathf.Clamp(idx, 0, ps.Length - 1)];
	}
}