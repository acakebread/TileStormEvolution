// File: Assets/Scripts/MassiveHadronLtd/Squatrix.cs
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class Squatrix
	{
		public static float[] Encode(Vector3 position, Quaternion rotation, float distance)
		{
			var q3 = Quaternion3.Encode(rotation.normalized);

			return new float[7]
			{
				position.x,
				position.y,
				position.z,
				q3.x,
				q3.y,
				q3.z,
				distance
			};
		}

		public static Vector3 GetPosition(float[] d) =>
			d != null && d.Length >= 3 ? new Vector3(d[0], d[1], d[2]) : Vector3.zero;

		public static Quaternion GetRotation(float[] d)
		{
			if (d == null || d.Length < 7) return Quaternion.identity;
			Vector3 q3 = new Vector3(d[3], d[4], d[5]);
			return Quaternion3.Decode(q3);
		}

		public static float GetDistance(float[] d) =>
			d != null && d.Length >= 7 ? d[6] : 10f;

		public static Vector3 GetDirection(float[] d) =>
		    GetRotation(d) * Vector3.forward;

		public static Vector3 GetLookAt(float[] d) =>
			GetPosition(d) + GetDirection(d) * GetDistance(d);
	}
}
