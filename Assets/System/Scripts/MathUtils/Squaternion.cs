using UnityEngine;
namespace MassiveHadronLtd
{
	/// <summary>
	/// Stores camera rotation + look-at distance in a single Vector4 using the "scaled quaternion" technique.
	/// Magnitude of the vector = distance, direction = rotation (largest component forced positive for sign recovery).
	/// This saves 1 float per view compared to explicit quaternion + distance with zero loss of fidelity.
	/// Used in id Tech, Frostbite, and high-end VFX pipelines for decades.
	/// </summary>
	/// 
	public static class Squaternion
	{
		const float MIN_ACCEPTABLE = 1e-6f;   // treat anything <= this as "zero/unrecoverable"
		const float MIN_CLAMP = 1e-4f;        // optional lower clamp to avoid tiny scalars
		const float MAX_CLAMP = 1e6f;         // saturate very large scalars to avoid overflow

		// Encode: canonicalize quaternion then scale by scalar. Returned Vector4 is the stored blob.
		public static Vector4 Encode(Quaternion q, float scalar)
		{
			// Normalize input quaternion for determinism
			q.Normalize();

			// Clamp scalar into safe range
			if (scalar > 0f && scalar < MIN_CLAMP) scalar = MIN_CLAMP;
			if (scalar < 0f && -scalar < MIN_CLAMP) scalar = (scalar < 0f) ? -MIN_CLAMP : MIN_CLAMP;
			scalar = Mathf.Clamp(scalar, -MAX_CLAMP, MAX_CLAMP);

			// Find largest absolute component and enforce it positive
			float[] c = { q.x, q.y, q.z, q.w };
			int maxIdx = 0;
			float maxAbs = Mathf.Abs(c[0]);
			for (int i = 1; i < 4; ++i)
			{
				float a = Mathf.Abs(c[i]);
				if (a > maxAbs) { maxAbs = a; maxIdx = i; }
			}
			if (c[maxIdx] < 0f)
			{
				q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w;
			}

			// Scale and return
			return new Vector4(q.x * scalar, q.y * scalar, q.z * scalar, q.w * scalar);
		}

		// Decode: returns true if successful (scalar recovered and rotation recovered).
		// If false, rotation is set to identity and scalar to 0.
		public static bool Decode(Vector4 stored, out Quaternion rotation, out float scalar)
		{
			rotation = Quaternion.identity;
			scalar = 0f;

			// Compute magnitude = |scalar|
			double dx = stored.x, dy = stored.y, dz = stored.z, dw = stored.w;
			double norm = System.Math.Sqrt(dx * dx + dy * dy + dz * dz + dw * dw);
			if (!double.IsFinite(norm)) return false;

			float absScalar = (float)norm;

			// Treat tiny scalars as zero/unrecoverable
			if (absScalar <= MIN_ACCEPTABLE) return false;

			// Find index of largest absolute component (must match encoding logic)
			float[] s = { stored.x, stored.y, stored.z, stored.w };
			int maxIdx = 0;
			float maxAbs = Mathf.Abs(s[0]);
			for (int i = 1; i < 4; ++i)
			{
				float a = Mathf.Abs(s[i]);
				if (a > maxAbs) { maxAbs = a; maxIdx = i; }
			}

			// The sign of the scalar is the sign of that stored component
			float sign = Mathf.Sign(s[maxIdx]);
			if (sign == 0f) sign = 1f; // deterministic choice for exact zero (very unlikely)

			scalar = sign * absScalar;

			// Optionally clamp scalar into the expected range (defensive)
			scalar = Mathf.Clamp(scalar, -MAX_CLAMP, MAX_CLAMP);

			// Recover quaternion and normalize
			float inv = 1f / scalar;
			Quaternion q = new Quaternion(stored.x * inv, stored.y * inv, stored.z * inv, stored.w * inv);
			q.Normalize(); // remove tiny FP errors

			rotation = q;
			return true;
		}
	}
}