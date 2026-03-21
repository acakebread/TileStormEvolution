using UnityEngine;

namespace MassiveHadronLtd
{
	public static class Vector3Extensions
	{
		public static Vector3 Rounded(this Vector3 v, int decimals = 2)
		{
			float mult = Mathf.Pow(10f, decimals);
			return new Vector3(
				Mathf.Round(v.x * mult) / mult,
				Mathf.Round(v.y * mult) / mult,
				Mathf.Round(v.z * mult) / mult
			);
		}

		public static Vector3 WithDecimals(this Vector3 v, int decimals)
		{
			return v.Rounded(decimals);
		}
	}
}