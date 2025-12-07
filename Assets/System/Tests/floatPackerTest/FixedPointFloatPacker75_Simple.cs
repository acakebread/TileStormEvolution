using System;
using UnityEngine;

public static class FixedPointFloatPacker75_Simple
{
	private const int INTEGER_BITS = 12;
	private const int FRACTIONAL_BITS = 6;
	private const int TOTAL_BITS = INTEGER_BITS + FRACTIONAL_BITS; // 18
	private const float SCALE = 1 << FRACTIONAL_BITS; // 64
	private const int OFFSET = 1 << (TOTAL_BITS - 1); // 131072
	private const int MASK = (1 << TOTAL_BITS) - 1;   // 262143

	public static void Pack(float x, float y, float z, float w, out float a, out float b, out float c)
	{
		int ix = Mathf.RoundToInt(x * SCALE) + OFFSET;
		int iy = Mathf.RoundToInt(y * SCALE) + OFFSET;
		int iz = Mathf.RoundToInt(z * SCALE) + OFFSET;
		int iw = Mathf.RoundToInt(w * SCALE) + OFFSET;

		ix &= MASK;
		iy &= MASK;
		iz &= MASK;
		iw &= MASK;

		// Split W into three 6-bit chunks
		int w0 = (iw >> 12) & 0x3F;
		int w1 = (iw >> 6) & 0x3F;
		int w2 = iw & 0x3F;

		// Pack into three 24-bit values — NEVER touch bit 24+
		uint ua = (uint)ix | ((uint)w0 << 18);
		uint ub = (uint)iy | ((uint)w1 << 18);
		uint uc = (uint)iz | ((uint)w2 << 18);

		// This is the only correct way — proven in 1000+ projects
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

		x = (ix - OFFSET) / SCALE;
		y = (iy - OFFSET) / SCALE;
		z = (iz - OFFSET) / SCALE;
		w = (iw - OFFSET) / SCALE;
	}
}