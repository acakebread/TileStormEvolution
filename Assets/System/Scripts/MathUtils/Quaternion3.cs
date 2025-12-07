// File: Assets/Scripts/MassiveHadronLtd/Quaternion3.cs
using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// 3-float quaternion compression — drops W, reconstructs with w ≥ 0.
	/// Industry standard (Doom Eternal, id Tech, Frostbite).
	/// </summary>
	public static class Quaternion3
	{
		public static Vector3 Encode(Quaternion q)
		{
			q = q.normalized;
			if (q.w < 0f)
			{
				q.x = -q.x;
				q.y = -q.y;
				q.z = -q.z;
			}
			return new Vector3(q.x, q.y, q.z);
		}

		public static Quaternion Decode(Vector3 v)
		{
			float x = v.x, y = v.y, z = v.z;
			float ww = 1f - x * x - y * y - z * z;
			float w = ww > 0f ? Mathf.Sqrt(ww) : 0f;
			return new Quaternion(x, y, z, w);
		}
	}
}