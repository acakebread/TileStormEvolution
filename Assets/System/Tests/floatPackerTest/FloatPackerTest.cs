using UnityEngine;
using System;

public class FloatPackerFP15Test : MonoBehaviour
{
	const int N = 100000;
	const float REL_EPS = 1e-6f;

	void Start()
	{
		var rng = new System.Random(42);

		// We need to query the modes in the packer; reflect the same order
		for (int mode = 0; mode < 8; mode++)
		{
			// Access mode name and maxAbs via reflection-like calls (we rely on public API above)
			// (If you change mode count, update loop)
			TestMode(mode, rng);
		}
	}

	void TestMode(int mode, System.Random rng)
	{
		// Build representative min/max inside the mode's domain.
		// We'll reconstruct the mode's maxAbs by packing a value at the boundary then reading mode back.
		// Simpler: sample from ±(modeMax * 0.999) to stay inside domain.
		// To get the mode's maxAbs, we'll use a probe: pack a big value and read back the mode name.
		// Since FixedPointFloatPackerFP15 does not expose maxAbs directly, we will approximate:
		float probe = 1e6f; // large
		FixedPointFloatPackerFP15.Pack(probe, probe, probe, out float aProbe, out float bProbe);
		int inferredMode = ((int)bProbe >> 21) & 0x7;
		if (inferredMode != mode)
		{
			// find a value that selects this mode by binary search-ish is overkill.
			// Instead: we'll rely on knowledge of modes order and pick representative ranges:
			// This mapping mirrors the internal Modes order used in the packer.
		}

		// Hardcode per-mode interior sampling ranges consistent with the packer Modes in file:
		// (These should match Modes[].maxAbs from the packer; adjust if packer modes change)
		float[,] ranges = new float[8, 2]
		{
			{ -524256f, 524256f }, // mode 0 ~ 20,-5  (approx)
            { -131064f, 131064f }, // mode 1 ~ 18,-3
            { -32768f, 32768f   }, // mode 2 ~ 16,-1
            { -8192f, 8192f     }, // mode 3 ~ 14,1
            { -2048f, 2048f     }, // mode 4 ~ 12,3
            { -512f, 512f       }, // mode 5 ~ 10,5
            { -128f, 128f       }, // mode 6 ~ 8,7
            { -32f, 32f         }  // mode 7 ~ 6,9 (last fallback)
        };

		float min = ranges[mode, 0] * 0.999f;
		float max = ranges[mode, 1] * 0.999f;

		float maxAbsX = 0f, maxAbsY = 0f, maxAbsZ = 0f;
		float maxRelX = 0f, maxRelY = 0f, maxRelZ = 0f;

		for (int i = 0; i < N; i++)
		{
			float x = Mathf.Lerp(min, max, (float)rng.NextDouble());
			float y = Mathf.Lerp(min, max, (float)rng.NextDouble());
			float z = Mathf.Lerp(min, max, (float)rng.NextDouble());

			FixedPointFloatPackerFP15.Pack(x, y, z, out float a, out float b);
			FixedPointFloatPackerFP15.Unpack(a, b, out float rx, out float ry, out float rz);

			int usedMode = ((int)b >> 21) & 0x7;

			// ✅ HARD ASSERT — this should NEVER fail now
			if (usedMode != mode)
			{
				Debug.LogError($"Mode leak! Expected {mode}, got {usedMode}. x={x}, y={y}, z={z}");
				return;
			}

			float ex = Mathf.Abs(rx - x);
			float ey = Mathf.Abs(ry - y);
			float ez = Mathf.Abs(rz - z);

			maxAbsX = Mathf.Max(maxAbsX, ex);
			maxAbsY = Mathf.Max(maxAbsY, ey);
			maxAbsZ = Mathf.Max(maxAbsZ, ez);

			if (Mathf.Abs(x) > REL_EPS) maxRelX = Mathf.Max(maxRelX, ex / Mathf.Abs(x));
			if (Mathf.Abs(y) > REL_EPS) maxRelY = Mathf.Max(maxRelY, ey / Mathf.Abs(y));
			if (Mathf.Abs(z) > REL_EPS) maxRelZ = Mathf.Max(maxRelZ, ez / Mathf.Abs(z));
		}


		Debug.Log($"Mode {mode} ({FixedPointFloatPackerFP15.GetModeName(mode)}): samples={N}");
		Debug.Log($"  Max ABS error: X={maxAbsX:G9}, Y={maxAbsY:G9}, Z={maxAbsZ:G9}");
		Debug.Log($"  Max REL error: X={maxRelX * 100f:G6}%, Y={maxRelY * 100f:G6}%, Z={maxRelZ * 100f:G6}%");
	}
}
