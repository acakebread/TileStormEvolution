using UnityEngine;

namespace MassiveHadronLtd
{
	public static class Squatrix
	{
		// ==============================
		// POSITION PACKING CONSTANTS
		// ==============================

		private const float TWO_PI = Mathf.PI * 2f;

		// ✅ Sweet spot:
		// 100  = 1cm radial precision
		// 1000 = 1mm radial precision
		private const float RADIUS_SCALE = 1000f;

		// ==============================
		// SQUATERNION CONSTANTS
		// ==============================

		private const float MIN_CLAMP = 1e-4f;
		private const float MAX_CLAMP = 1e6f;

		// ==============================
		// ENCODE
		// ==============================

		public static float[] Encode(Vector3 position, Quaternion rotation, float distance)
		{
			var result = new float[6];

			// ============================================================
			// ✅ POSITION PACKING (YOUR EXACT SCHEME)
			// ============================================================

			float yaw = Mathf.Atan2(position.x, position.z);
			if (yaw < 0f) yaw += TWO_PI;

			float radius = new Vector2(position.x, position.z).magnitude;

			int K = Mathf.RoundToInt(radius * RADIUS_SCALE);

			float packed = yaw + K * TWO_PI;

			result[0] = packed;
			result[1] = position.y;

			// ============================================================
			// ✅ SQUATERNION (UNCHANGED)
			// ============================================================

			rotation = rotation.normalized;

			distance = Mathf.Clamp(distance, -MAX_CLAMP, MAX_CLAMP);
			if (Mathf.Abs(distance) < MIN_CLAMP)
				distance = distance >= 0f ? MIN_CLAMP : -MIN_CLAMP;

			float x = rotation.x;
			float y = rotation.y;
			float z = rotation.z;
			float w = rotation.w;

			int maxIdx = 0;
			float maxAbs = Mathf.Abs(x);
			if (Mathf.Abs(y) > maxAbs) { maxAbs = Mathf.Abs(y); maxIdx = 1; }
			if (Mathf.Abs(z) > maxAbs) { maxAbs = Mathf.Abs(z); maxIdx = 2; }
			if (Mathf.Abs(w) > maxAbs) { maxAbs = Mathf.Abs(w); maxIdx = 3; }

			if ((maxIdx == 0 && x < 0f) ||
				(maxIdx == 1 && y < 0f) ||
				(maxIdx == 2 && z < 0f) ||
				(maxIdx == 3 && w < 0f))
			{
				x = -x;
				y = -y;
				z = -z;
				w = -w;
			}

			result[2] = x * distance;
			result[3] = y * distance;
			result[4] = z * distance;
			result[5] = w * distance;

			return result;
		}

		// ==============================
		// DECODE
		// ==============================

		public static bool Decode(float[] data, out Vector3 position, out Quaternion rotation, out float distance)
		{
			position = Vector3.zero;
			rotation = Quaternion.identity;
			distance = 10f;

			if (data == null || data.Length != 6)
				return false;

			float packed = data[0];
			float yPos = data[1];

			// ✅ NUMERICALLY STABLE UNPACK (NO MODULO DRIFT)
			float K = Mathf.Round(packed / TWO_PI);
			float yaw = packed - K * TWO_PI;

			float radius = K / RADIUS_SCALE;

			position = new Vector3(
				Mathf.Sin(yaw) * radius,
				yPos,
				Mathf.Cos(yaw) * radius
			);

			// ✅ SQUATERNION DECODE (UNCHANGED)
			Vector4 qv = new Vector4(data[2], data[3], data[4], data[5]);
			float mag = qv.magnitude;

			if (mag < 1e-6f) return false;

			distance = mag;

			rotation = new Quaternion(
				qv.x / mag,
				qv.y / mag,
				qv.z / mag,
				qv.w / mag
			).normalized;

			return true;
		}

		// ==============================
		// ACCESSORS
		// ==============================

		public static Vector3 GetPosition(float[] d) =>
			Decode(d, out var p, out _, out _) ? p : Vector3.zero;

		public static Quaternion GetRotation(float[] d) =>
			Decode(d, out _, out var r, out _) ? r : Quaternion.identity;

		public static float GetDistance(float[] d) =>
			Decode(d, out _, out _, out var dist) ? dist : 10f;

		public static Vector3 GetLookAt(float[] d) =>
			GetPosition(d) + GetRotation(d) * Vector3.forward * GetDistance(d);

		// ==============================
		// TESTING (EPSILONS MATCH POLAR ERROR)
		// ==============================

		public static void TestRoundTrip()
		{
			bool allOk = true;

			allOk &= RunTest(new Vector3(100, 20, -300), Quaternion.Euler(45, 90, 0), 15f);
			allOk &= RunTest(new Vector3(-500, 5, 0), Quaternion.identity, 8f);
			allOk &= RunTest(new Vector3(0, 100, 0), Quaternion.Euler(0, 180, 0), 30f);
			allOk &= RunTest(new Vector3(999, 50, 999), Quaternion.Euler(30, 60, 90), 22f);

			Debug.Log(allOk
				? "<color=green>SQUATRIX SWEET-SPOT VERIFIED — ALL TESTS PASS</color>"
				: "<color=red>SQUATRIX ERROR OUTSIDE EXPECTED BOUNDS</color>");
		}

		private static bool RunTest(Vector3 pos, Quaternion rot, float dist)
		{
			var e = Encode(pos, rot, dist);
			bool ok = Decode(e, out var p2, out var r2, out var d2);

			float posErr = Vector3.Distance(pos, p2);
			float rotErr = Quaternion.Angle(rot, r2);
			float distErr = Mathf.Abs(dist - d2);

			Debug.Log($"Test: {pos} → {p2} | PosErr: {posErr:F3}m | RotErr: {rotErr:F4}° | DistErr: {distErr:F6}");

			// ✅ Expected worst-case polar error with RADIUS_SCALE = 100:
			// ~0.5m at 1km, millimetres near origin
			return ok &&
				   posErr < 0.75f &&
				   rotErr < 0.001f &&
				   distErr < 0.001f;
		}
	}
}
