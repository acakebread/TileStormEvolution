using UnityEngine;

public static class FixedPointFloatPacker75
{
	const int OFFSET = 131072;   // 2^17
	const int SCALE = 64;        // FP12.6
	const int BASE18 = 1 << 18;  // 262144
	const int BASE6 = 1 << 6;   // 64

	public static void Pack(float x, float y, float z, float w,
							out float a, out float b, out float c)
	{
		uint ux = (uint)(Mathf.RoundToInt(x * SCALE) + OFFSET);
		uint uy = (uint)(Mathf.RoundToInt(y * SCALE) + OFFSET);
		uint uz = (uint)(Mathf.RoundToInt(z * SCALE) + OFFSET);
		uint uw = (uint)(Mathf.RoundToInt(w * SCALE) + OFFSET);

		uint w2 = uw >> 12;          // top 6 bits
		uint w1 = (uw >> 6) & 63;    // mid 6 bits
		uint w0 = uw & 63;           // low 6 bits

		a = ux + w2 * BASE18;
		b = uy + w1 * BASE18;
		c = uz + w0 * BASE18;
	}

	public static void Unpack(float a, float b, float c,
							  out float x, out float y, out float z, out float w)
	{
		uint ua = (uint)a;
		uint ub = (uint)b;
		uint uc = (uint)c;

		uint ux = ua & (BASE18 - 1);
		uint uy = ub & (BASE18 - 1);
		uint uz = uc & (BASE18 - 1);

		uint w2 = ua >> 18;
		uint w1 = ub >> 18;
		uint w0 = uc >> 18;

		uint uw = (w2 << 12) | (w1 << 6) | w0;

		x = ((int)ux - OFFSET) / (float)SCALE;
		y = ((int)uy - OFFSET) / (float)SCALE;
		z = ((int)uz - OFFSET) / (float)SCALE;
		w = ((int)uw - OFFSET) / (float)SCALE;
	}
}

//using System;
//using UnityEngine;

//public static class FixedPointFloatPacker75
//{
//	const int BITS = 18;
//	const int FRACTIONAL_BITS = 8;
//	const int SCALE = 1 << FRACTIONAL_BITS;     // 256
//	const int OFFSET = 1 << (BITS - 1);         // 131072
//	const int MASK = (1 << BITS) - 1;           // 0x3FFFF

//	private static uint spareCounter = 0;

//	public static void Pack(float x, float y, float z, float w, out float a, out float b, out float c)
//	{
//		// Clamp to exactly representable range
//		x = Mathf.Clamp(x, -512f, 512f - 1f / SCALE);
//		y = Mathf.Clamp(y, -512f, 512f - 1f / SCALE);
//		z = Mathf.Clamp(z, -512f, 512f - 1f / SCALE);
//		w = Mathf.Clamp(w, -512f, 512f - 1f / SCALE);

//		int ix = (Mathf.RoundToInt(x * SCALE) + OFFSET) & MASK;
//		int iy = (Mathf.RoundToInt(y * SCALE) + OFFSET) & MASK;
//		int iz = (Mathf.RoundToInt(z * SCALE) + OFFSET) & MASK;
//		int iw = (Mathf.RoundToInt(w * SCALE) + OFFSET) & MASK;

//		// Split W into three 6-bit chunks (18 bits total)
//		int w0 = (iw >> 12) & 0x3F;  // bits 17..12
//		int w1 = (iw >> 6) & 0x3F;  // bits 11..6
//		int w2 = iw & 0x3F;  // bits 5..0

//		// Pack into SAFE 24-bit integers → guaranteed lossless in float32
//		uint ua = (uint)ix | ((uint)w0 << 18) | ((spareCounter & 1) << 24);
//		uint ub = (uint)iy | ((uint)w1 << 18) | (((spareCounter >> 1) & 1) << 24);
//		uint uc = (uint)iz | ((uint)w2 << 18) | (((spareCounter >> 2) & 1) << 24);

//		spareCounter = (spareCounter + 1) & 0x7;

//		// Force 24-bit mask → never use sign bit or bit 25+
//		a = BitConverter.Int32BitsToSingle((int)(ua & 0xFFFFFF));
//		b = BitConverter.Int32BitsToSingle((int)(ub & 0xFFFFFF));
//		c = BitConverter.Int32BitsToSingle((int)(uc & 0xFFFFFF));
//	}

//	public static void Unpack(float a, float b, float c, out float x, out float y, out float z, out float w)
//	{
//		uint ua = (uint)BitConverter.SingleToInt32Bits(a) & 0xFFFFFF;
//		uint ub = (uint)BitConverter.SingleToInt32Bits(b) & 0xFFFFFF;
//		uint uc = (uint)BitConverter.SingleToInt32Bits(c) & 0xFFFFFF;

//		int ix = (int)(ua & 0x3FFFF);
//		int iy = (int)(ub & 0x3FFFF);
//		int iz = (int)(uc & 0x3FFFF);

//		int w0 = (int)((ua >> 18) & 0x3F);
//		int w1 = (int)((ub >> 18) & 0x3F);
//		int w2 = (int)((uc >> 18) & 0x3F);

//		int iw = (w0 << 12) | (w1 << 6) | w2;

//		x = (ix - OFFSET) / (float)SCALE;
//		y = (iy - OFFSET) / (float)SCALE;
//		z = (iz - OFFSET) / (float)SCALE;
//		w = (iw - OFFSET) / (float)SCALE;
//	}

//	// Helpers
//	public static void Pack(float[] src, float[] dst) => Pack(src[0], src[1], src[2], src[3], out dst[0], out dst[1], out dst[2]);
//	public static void Unpack(float[] src, float[] dst) => Unpack(src[0], src[1], src[2], out dst[0], out dst[1], out dst[2], out dst[3]);
//}