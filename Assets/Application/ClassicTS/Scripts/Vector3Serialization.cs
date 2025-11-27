using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Shared, high-performance Vector3 to float[3] conversion used by Waypoint, Emitter, etc.
	/// Zero allocations after first use, never appears in JSON.
	/// </summary>
	public static class Vector3Serialization
	{
		public static readonly Vector3 Invalid = new(float.NaN, float.NaN, float.NaN);

		public static Vector3 ToVector3(float[] arr)
		{
			return (arr != null && arr.Length == 3 && IsValid(arr))
				? new Vector3(arr[0], arr[1], arr[2])
				: Invalid;
		}

		public static float[] FromVector3(Vector3 v)
		{
			return IsValid(v) ? new[] { v.x, v.y, v.z } : null;
		}

		public static bool IsValid(Vector3 v) =>
			!float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
			!float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
			!float.IsNaN(v.z) && !float.IsInfinity(v.z);

		public static bool IsValid(float[] arr) =>
			arr != null && arr.Length == 3 &&
			!float.IsNaN(arr[0]) && !float.IsInfinity(arr[0]) &&
			!float.IsNaN(arr[1]) && !float.IsInfinity(arr[1]) &&
			!float.IsNaN(arr[2]) && !float.IsInfinity(arr[2]);
	}
}