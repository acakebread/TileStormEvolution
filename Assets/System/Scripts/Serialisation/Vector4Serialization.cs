// File: Assets/Application/ClassicTS/Scripts/Vector4Serialization.cs
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class Vector4Serialization
	{
		public static Vector4 ToVector4(float[] arr)
		{
			return (arr != null && arr.Length == 4 && IsValid(arr))
				? new Vector4(arr[0], arr[1], arr[2], arr[3])
				: Vector4.zero;
		}

		public static float[] FromVector4(Vector4 v)
		{
			return IsValid(v) ? new[] { v.x, v.y, v.z, v.w } : null;
		}

		public static bool IsValid(Vector4 v) =>
			!float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
			!float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
			!float.IsNaN(v.z) && !float.IsInfinity(v.z) &&
			!float.IsNaN(v.w) && !float.IsInfinity(v.w);

		public static bool IsValid(float[] arr) =>
			arr != null && arr.Length == 4 &&
			!float.IsNaN(arr[0]) && !float.IsInfinity(arr[0]) &&
			!float.IsNaN(arr[1]) && !float.IsInfinity(arr[1]) &&
			!float.IsNaN(arr[2]) && !float.IsInfinity(arr[2]) &&
			!float.IsNaN(arr[3]) && !float.IsInfinity(arr[3]);
	}
}