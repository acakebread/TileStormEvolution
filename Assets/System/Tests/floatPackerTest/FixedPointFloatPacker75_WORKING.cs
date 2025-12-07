using System;
using UnityEngine;

public static class FixedPointFloatPacker75_WORKING
{
	private const int BITS = 18;
	private const int OFFSET = 131072;
	private const int MASK = 262143;

	// Precomputed table: uint24 → float with exact bit pattern
	private static readonly float[] Uint24ToFloat = new float[1 << 24];

	static FixedPointFloatPacker75_WORKING()
	{
		for (int i = 0; i < (1 << 24); i++)
		{
			Uint24ToFloat[i] = BitConverter.Int32BitsToSingle(i << 8); // shift into mantissa
		}
	}

	public static void Pack(float x, float y, float z, float w, out float a, out float b, out float c)
	{
		int ix = (Mathf.RoundToInt(x * 64f) + OFFSET) & MASK;
		int iy = (Mathf.RoundToInt(y * 64f) + OFFSET) & MASK;
		int iz = (Mathf.RoundToInt(z * 64f) + OFFSET) & MASK;
		int iw = (Mathf.RoundToInt(w * 64f) + OFFSET) & MASK;

		int w0 = (iw >> 12) & 0x3F;
		int w1 = (iw >> 6) & 0x3F;
		int w2 = iw & 0x3F;

		uint ua = (uint)ix | ((uint)w0 << 18);
		uint ub = (uint)iy | ((uint)w1 << 18);
		uint uc = (uint)iz | ((uint)w2 << 18);

		a = Uint24ToFloat[ua];
		b = Uint24ToFloat[ub];
		c = Uint24ToFloat[uc];
	}

	public static void Unpack(float a, float b, float c, out float x, out float y, out float z, out float w)
	{
		uint ua = (uint)BitConverter.SingleToInt32Bits(a) >> 8; // extract 24 bits
		uint ub = (uint)BitConverter.SingleToInt32Bits(b) >> 8;
		uint uc = (uint)BitConverter.SingleToInt32Bits(c) >> 8;

		int ix = (int)(ua & 0x3FFFF);
		int iy = (int)(ub & 0x3FFFF);
		int iz = (int)(uc & 0x3FFFF);

		int w0 = (int)((ua >> 18) & 0x3F);
		int w1 = (int)((ub >> 18) & 0x3F);
		int w2 = (int)((uc >> 18) & 0x3F);
		int iw = (w0 << 12) | (w1 << 6) | w2;

		x = (ix - OFFSET) / 64f;
		y = (iy - OFFSET) / 64f;
		z = (iz - OFFSET) / 64f;
		w = (iw - OFFSET) / 64f;
	}
}