// File: Assets/Scripts/MassiveHadronLtd/Squatrix.cs
using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// 7-float ultimate format: full position + squaternion
	/// Zero position loss. 1 float saved vs naive 8-float.
	/// Production-ready, reversible format.
	/// </summary>
	public static class Squatrix
	{
		private const float MIN_CLAMP = 1e-4f;
		private const float MAX_CLAMP = 1e6f;

		public static float[] Encode(Vector3 position, Quaternion rotation, float distance)
		{
			var result = new float[7];

			result[0] = position.x;
			result[1] = position.y;
			result[2] = position.z;

			distance = Mathf.Clamp(distance, -MAX_CLAMP, MAX_CLAMP);
			if (Mathf.Abs(distance) < MIN_CLAMP)
				distance = distance >= 0f ? MIN_CLAMP : -MIN_CLAMP;

			var qscale = Squaternion.Encode(rotation.normalized, distance);

			result[3] = qscale.x;
			result[4] = qscale.y;
			result[5] = qscale.z;
			result[6] = qscale.w;

			return result;
		}

		public static bool Decode(float[] data, out Vector3 position, out Quaternion rotation, out float distance)
		{
			position = Vector3.zero;
			rotation = Quaternion.identity;
			distance = 10f;

			if (data == null || data.Length != 7) return false;

			position = new Vector3(data[0], data[1], data[2]);

			Vector4 qv = new Vector4(data[3], data[4], data[5], data[6]);
			return Squaternion.Decode(qv, out rotation, out distance);
		}

		public static Vector3 GetPosition(float[] d) =>
			d != null && d.Length >= 3 ? new Vector3(d[0], d[1], d[2]) : Vector3.zero;

		public static Quaternion GetRotation(float[] d) =>
			d != null && d.Length == 7 && Squaternion.Decode(new Vector4(d[3], d[4], d[5], d[6]), out var r, out _) ? r : Quaternion.identity;

		public static float GetDistance(float[] d) =>
			d != null && d.Length == 7 && Squaternion.Decode(new Vector4(d[3], d[4], d[5], d[6]), out _, out var dist) ? Mathf.Abs(dist) : 10f;

		public static Vector3 GetLookAt(float[] d) =>
			GetPosition(d) + GetRotation(d) * Vector3.forward * GetDistance(d);
	}
}