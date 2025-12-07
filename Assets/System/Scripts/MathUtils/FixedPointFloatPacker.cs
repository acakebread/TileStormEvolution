using UnityEngine;

/// <summary>
/// Packs 3 floats (x,y,z) into two floats (a,b) using 3x15-bit signed fixed-point values (45 bits)
/// and 3 mode bits (8 modes). Total = 48 bits spread across two 24-bit-exact float integers.
/// 
/// Layout (integers, little-endian bit numbering):
/// ia (24 bits used): bits 0..14 = X (15 bits), bits 15..23 = Z_hi (9 bits)
/// ib (24 bits used): bits 0..14 = Y (15 bits), bits 15..20 = Z_lo (6 bits), bits 21..23 = mode (3 bits)
/// 
/// Mode interpretation: each mode is defined by (intBits, fracBits) where intBits+fracBits == 15.
/// The stored integer represents: value = (signed_int) / (2^fracBits)
/// fracBits may be negative (meaning the decoded value is scaled up by 2^(-fracBits)).
/// —</ summary >

public static class FixedPointFloatPackerFP15
{
	// Fixed per-component storage
	const int COMPONENT_BITS = 15;
	const int SIGNED_HALF = 1 << (COMPONENT_BITS - 1); // 16384
	const int COMPONENT_MASK = (1 << COMPONENT_BITS) - 1; // 0x7FFF

	// Split Z into hi 9 bits and lo 6 bits
	const int Z_HI_BITS = 9;
	const int Z_LO_BITS = 6;
	const int Z_HI_MASK = (1 << Z_HI_BITS) - 1; // 0x1FF
	const int Z_LO_MASK = (1 << Z_LO_BITS) - 1; // 0x3F

	// Mode bits: 3 bits at bits 21..23 of ib
	const int MODE_SHIFT = 21;
	const int MODE_MASK = 0x7;

	// Ensure per-word integers never exceed 24 bits so float cast is exact
	// ia: bits 0..23 used (24 bits)
	// ib: bits 0..23 used (24 bits)

	// Define the 8 modes as you requested.
	// Interpreting your list "20,-5; 18,-3; 16,-1; 14,1; 12,3; 10,5; 8,7" etc.
	private struct ModeInfo { public int intBits; public int fracBits; public float maxAbs; public float scale; public string name; }

	private static readonly ModeInfo[] Modes = new ModeInfo[8]
	{
        // mode 0: 20, -5  -> intBits=20, fracBits=-5 (value = int / 2^-5 => int * 32)
        MakeMode(20, -5, "20,-5"),
		MakeMode(18, -3, "18,-3"),
		MakeMode(16, -1, "16,-1"),
		MakeMode(14,  1, "14,1"),
		MakeMode(12,  3, "12,3"),
		MakeMode(10,  5, "10,5"),
		MakeMode(8,   7, "8,7"),
        // added one more balanced intermediate (6,9) would also be valid but keep 8 modes;
        // if you prefer a different eighth mode, replace below.
        MakeMode(6,   9, "6,9")
	};

	private static ModeInfo MakeMode(int intBits, int fracBits, string name)
	{
		// sanity: intBits + fracBits == 15 by your FP15 plan
		// But we do not strictly assert here — instead compute effective maxAbs from signed integer max
		int signedMax = (1 << (COMPONENT_BITS - 1)) - 1; // 16383
														 // decoded value max = signedMax / (2^fracBits)
														 // implement scale = 2^fracBits (may be <1 if fracBits negative)
		float scale = Mathf.Pow(2f, fracBits);
		float maxAbs = signedMax / scale;
		return new ModeInfo { intBits = intBits, fracBits = fracBits, maxAbs = maxAbs, scale = scale, name = name };
	}

	// Add this public accessor so tests can use exact bounds
	public static float GetModeMaxAbs(int mode)
	{
		if (mode < 0 || mode >= Modes.Length) return 0f;
		return Modes[mode].maxAbs;
	}

	public static string GetModeNameSafe(int mode)
	{
		if (mode < 0 || mode >= Modes.Length) return "??";
		return Modes[mode].name;
	}

	// Robust selector: choose the smallest mode whose maxAbs >= m.
	// If none fit, choose the mode with the largest maxAbs (last resort).
	private static int SelectMode(float x, float y, float z)
	{
		float m = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
		m = Mathf.Max(m, Mathf.Abs(z));

		int bestIdx = -1;
		float bestMax = float.PositiveInfinity;

		for (int i = 0; i < Modes.Length; i++)
		{
			float mm = Modes[i].maxAbs;
			if (mm >= m && mm < bestMax)
			{
				bestMax = mm;
				bestIdx = i;
			}
		}

		if (bestIdx >= 0) return bestIdx;

		// If nothing fits (very rare), return the mode with the largest capacity
		int largestIdx = 0;
		float largest = Modes[0].maxAbs;
		for (int i = 1; i < Modes.Length; i++)
			if (Modes[i].maxAbs > largest)
			{
				largest = Modes[i].maxAbs;
				largestIdx = i;
			}

		return largestIdx;
	}

	public static void Pack(float x, float y, float z, out float a, out float b)
	{
		int mode = SelectMode(x, y, z);
		var mi = Modes[mode];

		// scale to integer domain: intVal = trunc(value * scale)
		float scale = mi.scale;
		float maxVal = mi.maxAbs; // we will clamp to this representable maximum
		float maxClamp = maxVal; // already computed

		x = Mathf.Clamp(x, -maxClamp, maxClamp);
		y = Mathf.Clamp(y, -maxClamp, maxClamp);
		z = Mathf.Clamp(z, -maxClamp, maxClamp);

		// Convert to 15-bit signed integers (truncation toward zero)
		int ix = (int)(x * scale) + SIGNED_HALF; // 0..0x7FFF
		int iy = (int)(y * scale) + SIGNED_HALF;
		int iz = (int)(z * scale) + SIGNED_HALF;

		ix &= COMPONENT_MASK;
		iy &= COMPONENT_MASK;
		iz &= COMPONENT_MASK;

		// Split iz into hi and lo to fit into 9+6 bits
		int iz_hi = (iz >> Z_LO_BITS) & Z_HI_MASK; // top 9 bits
		int iz_lo = iz & Z_LO_MASK;                // low 6 bits

		// Compose 24-bit payload words
		int ia_payload = (ix & COMPONENT_MASK) | ((iz_hi & Z_HI_MASK) << 15); // ix bits 0..14, iz_hi bits 15..23
		int ib_payload = (iy & COMPONENT_MASK) | ((iz_lo & Z_LO_MASK) << 15); // iy bits 0..14, iz_lo bits 15..20

		// Insert mode into bits 21..23 of ib
		int ib = ib_payload | ((mode & MODE_MASK) << MODE_SHIFT);

		// ia is already within 24 bits; ensure it
		int ia = ia_payload & 0x00FFFFFF;
		ib &= 0x00FFFFFF;

		// Cast to float (exact because <= 2^24-1)
		a = (float)ia;
		b = (float)ib;
	}

	public static void Unpack(float a, float b, out float x, out float y, out float z)
	{
		int ia = (int)a;
		int ib = (int)b;

		int mode = (ib >> MODE_SHIFT) & MODE_MASK;
		var mi = Modes[mode];
		float scale = mi.scale;

		int ix = ia & COMPONENT_MASK;
		int iz_hi = (ia >> 15) & Z_HI_MASK;
		int iy = ib & COMPONENT_MASK;
		int iz_lo = (ib >> 15) & Z_LO_MASK;

		int iz = (iz_hi << Z_LO_BITS) | iz_lo;
		iz &= COMPONENT_MASK;

		x = (ix - SIGNED_HALF) / scale;
		y = (iy - SIGNED_HALF) / scale;
		z = (iz - SIGNED_HALF) / scale;
	}

	// helpers
	public static void Pack(float[] src, float[] dst) => Pack(src[0], src[1], src[2], out dst[0], out dst[1]);
	public static void Unpack(float[] src, float[] dst) => Unpack(src[0], src[1], out dst[0], out dst[1], out dst[2]);

	// Optional: expose mode names for debugging
	public static string GetModeName(int mode)
	{
		if (mode < 0 || mode >= Modes.Length) return "??";
		return Modes[mode].name;
	}
}
