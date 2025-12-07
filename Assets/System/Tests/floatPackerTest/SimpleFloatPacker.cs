using UnityEngine;
using System;

public static class SimpleFloatPacker
{
	// 8.7 fixed point → range -256 to +255.9921875, step = 1/128 = 0.0078125
	private const float TO_FIXED = 128f;        // 2^7
	private const float FROM_FIXED = 1f / 128f;

	public static void Pack(float x, float y, float z, out float a, out float b)
	{
		// Convert to 8.7 fixed-point (16-bit signed, but we only use 8 integer bits)
		int ix = Mathf.RoundToInt(x * TO_FIXED);
		int iy = Mathf.RoundToInt(y * TO_FIXED);
		int iz = Mathf.RoundToInt(z * TO_FIXED);

		// Hard clamp to safe range (prevents any overflow)
		ix = Mathf.Clamp(ix, -32768, 32767);
		iy = Mathf.Clamp(iy, -32768, 32767);
		iz = Mathf.Clamp(iz, -32768, 32767);

		// Pack: X (8 bits) + Y (8 bits) into first float
		//       Z (8 bits) + 8 padding bits into second
		uint packed1 = ((uint)(ix & 0xFF) << 16) | ((uint)(iy & 0xFF) << 8);
		uint packed2 = (uint)(iz & 0xFF);

		// Store as float with exponent = 127 (normalized ~1.0)
		a = BitConverter.Int32BitsToSingle((int)(packed1 << 8) | 0x3F800000);
		b = BitConverter.Int32BitsToSingle((int)(packed2 << 8) | 0x3F800000);
	}

	public static void Unpack(float a, float b, out float x, out float y, out float z)
	{
		int ia = BitConverter.SingleToInt32Bits(a);
		int ib = BitConverter.SingleToInt32Bits(b);

		uint bits1 = (uint)(ia << 8) >> 8;  // lower 24 bits
		uint bits2 = (uint)(ib << 8) >> 8;

		int ix = (int)(bits1 >> 16) << 24 >> 24;  // extract top 8 bits, sign extend
		int iy = (int)((bits1 >> 8) & 0xFF) << 24 >> 24;
		int iz = (int)(bits2 & 0xFF) << 24 >> 24;

		x = ix * FROM_FIXED;
		y = iy * FROM_FIXED;
		z = iz * FROM_FIXED;
	}
}