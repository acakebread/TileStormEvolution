using UnityEngine;
using System;

/// <summary>
/// Test the baseline 45-bit packer.
/// 1) Test full integer domain [-16383..16383]
/// 2) Test sub-range near zero for fractional-like precision (we treat floats as if they are fractional values)
/// Reports max absolute error per component.
/// </summary>
public class FloatPacker48Test : MonoBehaviour
{
	void Start()
	{
		const int N = 200000;
		var rng = new System.Random(123456);

		TestFullIntegerRange(N, rng);
		TestSmallFractionRange(N, rng);
	}

	void TestFullIntegerRange(int N, System.Random rng)
	{
		int maxAbsX = 0, maxAbsY = 0, maxAbsZ = 0;
		int worstX = 0, worstY = 0, worstZ = 0;

		const int MAX_RANGE = 256;

		// sample integers in [-16383, 16383]
		for (int i = 0; i < N; i++)
		{
			int ix = rng.Next() % (1 << 24);
			int iy = rng.Next() % (1 << 24);
			int iz = rng.Next() % (1 << 24);

			float x = ((float)ix) / (1 << 24) * MAX_RANGE;
			float y = ((float)ix) / (1 << 24) * MAX_RANGE;
			float z = ((float)ix) / (1 << 24) * MAX_RANGE;

			FixedPointFloatPacker48.Pack(x, y, z, out float a, out float b);
			FixedPointFloatPacker48.Unpack(a, b, out float rx, out float ry, out float rz);

			int ex = Mathf.Abs((int)rx - ix);
			int ey = Mathf.Abs((int)ry - iy);
			int ez = Mathf.Abs((int)rz - iz);

			if (ex > maxAbsX) { maxAbsX = ex; worstX = ix; }
			if (ey > maxAbsY) { maxAbsY = ey; worstY = iy; }
			if (ez > maxAbsZ) { maxAbsZ = ez; worstZ = iz; }
		}

		Debug.Log($"Integer-domain test samples={N}");
		Debug.Log($"  Max integer deviation: X={maxAbsX} (example ix={worstX}), Y={maxAbsY} (iy={worstY}), Z={maxAbsZ} (iz={worstZ})");
	}

	void TestSmallFractionRange(int N, System.Random rng)
	{
		// Simulate fractional values by sampling small floats and checking absolute error
		float range = 16f; // test small values around zero
		float maxAbsX = 0f, maxAbsY = 0f, maxAbsZ = 0f;

		for (int i = 0; i < N; i++)
		{
			float x = (float)(rng.NextDouble() * 2.0 * range - range);
			float y = (float)(rng.NextDouble() * 2.0 * range - range);
			float z = (float)(rng.NextDouble() * 2.0 * range - range);

			FixedPointFloatPacker48.Pack(x, y, z, out float a, out float b);
			FixedPointFloatPacker48.Unpack(a, b, out float rx, out float ry, out float rz);

			float ex = Mathf.Abs(rx - x);
			float ey = Mathf.Abs(ry - y);
			float ez = Mathf.Abs(rz - z);

			maxAbsX = Mathf.Max(maxAbsX, ex);
			maxAbsY = Mathf.Max(maxAbsY, ey);
			maxAbsZ = Mathf.Max(maxAbsZ, ez);
		}

		Debug.Log($"Small-fraction-domain test samples={N}");
		Debug.Log($"  Max ABS error: X={maxAbsX:G9}, Y={maxAbsY:G9}, Z={maxAbsZ:G9}");
	}
}
