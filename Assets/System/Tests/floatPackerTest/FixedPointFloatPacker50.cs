using System;
using UnityEngine;

/// <summary>
/// Packs 3 signed 16-bit integers (-32768..32767) into 2 floats (48 bits total - testing 50bit).
/// Layout:
///   float a : bits 0..15 = X, bits 16..23 = Z >> 8
///   float b : bits 0..15 = Y, bits 16..23 = Z & 255
/// Safe because we offset to positive → sign bit = 0 → bits up to 23 are fully usable. - testing full range 0->24 - seems to pass so 50 bit works
/// </summary>
public static class FixedPointFloatPacker50
{
	const int BITS = 16;
	const int OFFSET = 1 << (BITS - 1);  // 32768
	const int MASK = (1 << BITS) - 1;    // 0xFFFF

	// Cycle counters for spare bits
	private static uint spareCounter = 0;

	public static void Pack(float x, float y, float z, out float a, out float b)
	{
		// Clamp to exact representable range
		x = Mathf.Clamp(x, -32768f, 32767f);
		y = Mathf.Clamp(y, -32768f, 32767f);
		z = Mathf.Clamp(z, -32768f, 32767f);

		// Round + map to 0..65535 (but we use 1..65535 → sign bit31 always 0)
		int ix = Mathf.RoundToInt(x * 256f) + OFFSET;
		int iy = Mathf.RoundToInt(y * 256f) + OFFSET;
		int iz = Mathf.RoundToInt(z * 256f) + OFFSET;

		ix &= MASK;
		iy &= MASK;
		iz &= MASK;

		int z_hi = (iz >> 8) & 0xFF;//test for spare bit - 'randomise' it to make sure there are no errors in encode /decode
		int z_lo = iz & 0xFF;//test for spare bit - 'randomise' it to make sure there are no errors in encode /decode

		// Pack into 24-bit values → perfectly safe because positive and < 2^24
		uint ua = (uint)ix | ((uint)z_hi << 16);  // X + Z_hi  → 24 bits
		uint ub = (uint)iy | ((uint)z_lo << 16);  // Y + Z_lo  → 24 bits

		//Cycle spare bits 
		ua |= (spareCounter & 1) << 24;
		ub |= (spareCounter & 2) << 23;
		spareCounter &= 3;

		a = BitConverter.Int32BitsToSingle((int)ua);
		b = BitConverter.Int32BitsToSingle((int)ub);
	}

	public static void Unpack(float a, float b, out float x, out float y, out float z)
	{
		uint ua = (uint)BitConverter.SingleToInt32Bits(a);
		uint ub = (uint)BitConverter.SingleToInt32Bits(b);

		int ix = (int)(ua & 0xFFFF);
		int iy = (int)(ub & 0xFFFF);
		int z_hi = (int)((ua >> 16) & 0xFF);
		int z_lo = (int)((ub >> 16) & 0xFF);

		int iz = (z_hi << 8) | z_lo;

		x = (ix - OFFSET) / 256f;
		y = (iy - OFFSET) / 256f;
		z = (iz - OFFSET) / 256f;
	}

	// Optional helpers
	public static void Pack(float[] src, float[] dst) => Pack(src[0], src[1], src[2], out dst[0], out dst[1]);
	public static void Unpack(float[] src, float[] dst) => Unpack(src[0], src[1], out dst[0], out dst[1], out dst[2]);
}