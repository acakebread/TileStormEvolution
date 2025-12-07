using System;
using UnityEngine;

public static class FixedPointFloatPackerDynamicRange75
{
	private const int BITS = 18;
	private const int OFFSET = 131072;  // 1 << 17
	private const int MASK = 262143;    // (1 << 18) - 1

	private static readonly float[] ScaleFactors = new float[]
	{
		65536f,      // 0
        4096f,       // 1
        256f,        // 2
        16f,         // 3
        1f,          // 4
        0.0625f,     // 5
        0.00390625f, // 6
        1f           // 7 reserved
    };

	public static void Pack(float x, float y, float z, float w, out float a, out float b, out float c)
	{
		float maxAbs = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z), Mathf.Abs(w));

		int range = 4;
		if (maxAbs > 131072f) range = 0;
		else if (maxAbs > 8192f) range = 1;
		else if (maxAbs > 512f) range = 2;
		else if (maxAbs > 32f) range = 3;
		else if (maxAbs > 2f) range = 4;
		else if (maxAbs > 0.125f) range = 5;
		else range = 6;

		float scale = ScaleFactors[range];

		x *= scale; y *= scale; z *= scale; w *= scale;

		int ix = (Mathf.RoundToInt(x) + OFFSET) & MASK;
		int iy = (Mathf.RoundToInt(y) + OFFSET) & MASK;
		int iz = (Mathf.RoundToInt(z) + OFFSET) & MASK;
		int iw = (Mathf.RoundToInt(w) + OFFSET) & MASK;

		int w0 = (iw >> 12) & 0x3F;
		int w1 = (iw >> 6) & 0x3F;
		int w2 = iw & 0x3F;

		// FIXED: Range bits go into bits 21, 22, 23 — NEVER overlap with w0/w1/w2
		uint ua = (uint)ix | ((uint)w0 << 18) | ((uint)(range & 1) << 21);        // bit 21
		uint ub = (uint)iy | ((uint)w1 << 18) | ((uint)((range >> 1) & 1) << 22); // bit 22
		uint uc = (uint)iz | ((uint)w2 << 18) | ((uint)((range >> 2) & 1) << 23); // bit 23

		// Safe: always < 2^24
		a = BitConverter.Int32BitsToSingle((int)(ua & 0xFFFFFFu));
		b = BitConverter.Int32BitsToSingle((int)(ub & 0xFFFFFFu));
		c = BitConverter.Int32BitsToSingle((int)(uc & 0xFFFFFFu));
	}

	public static void Unpack(float a, float b, float c, out float x, out float y, out float z, out float w)
	{
		uint ua = (uint)BitConverter.SingleToInt32Bits(a) & 0xFFFFFFu;
		uint ub = (uint)BitConverter.SingleToInt32Bits(b) & 0xFFFFFFu;
		uint uc = (uint)BitConverter.SingleToInt32Bits(c) & 0xFFFFFFu;

		int ix = (int)(ua & 0x3FFFF);
		int iy = (int)(ub & 0x3FFFF);
		int iz = (int)(uc & 0x3FFFF);

		int w0 = (int)((ua >> 18) & 0x3F);
		int w1 = (int)((ub >> 18) & 0x3F);
		int w2 = (int)((uc >> 18) & 0x3F);
		int iw = (w0 << 12) | (w1 << 6) | w2;

		int range = (int)(
			((ua >> 21) & 1) |
			(((ub >> 22) & 1) << 1) |
			(((uc >> 23) & 1) << 2)
		);

		float scale = range < 7 ? ScaleFactors[range] : 1f;

		x = (ix - OFFSET) / scale;
		y = (iy - OFFSET) / scale;
		z = (iz - OFFSET) / scale;
		w = (iw - OFFSET) / scale;
	}
}