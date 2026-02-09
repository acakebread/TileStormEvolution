using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public class Vector3LexComparer : IComparer<Vector3>
	{
		public static readonly Vector3LexComparer Instance = new Vector3LexComparer();

		private Vector3LexComparer() { }

		public int Compare(Vector3 a, Vector3 b)
		{
			int result = a.x.CompareTo(b.x);
			if (result != 0) return result;

			result = a.y.CompareTo(b.y);
			if (result != 0) return result;

			return a.z.CompareTo(b.z);
		}

		// ─────────────────────────────────────────────────────────────
		// Approximate equality helpers
		// ─────────────────────────────────────────────────────────────

		/// <summary>
		/// Checks if two Vector3 are approximately equal (component-wise).
		/// Uses Mathf.Approximately internally + sqrMagnitude fallback.
		/// </summary>
		public static bool ApproximatelyEqual(Vector3 a, Vector3 b, float sqrEpsilon = 0.0001f)
		{
			// Fast path: exact match
			if (a == b) return true;

			// Use Mathf.Approximately on each component first (good for angles & normalized values)
			if (!Mathf.Approximately(a.x, b.x)) return false;
			if (!Mathf.Approximately(a.y, b.y)) return false;
			if (!Mathf.Approximately(a.z, b.z)) return false;

			// Fallback: allow very small accumulated error
			return (a - b).sqrMagnitude <= sqrEpsilon;
		}

		///// <summary>
		///// Variant with custom per-component epsilon (rarely needed).
		///// </summary>
		//public static bool ApproximatelyEqual(Vector3 a, Vector3 b,
		//	float epsilonX = 0.0001f,
		//	float epsilonY = 0.0001f,
		//	float epsilonZ = 0.0001f)
		//{
		//	return Mathf.Abs(a.x - b.x) <= epsilonX &&
		//		   Mathf.Abs(a.y - b.y) <= epsilonY &&
		//		   Mathf.Abs(a.z - b.z) <= epsilonZ;
		//}

		//// Optional: if you frequently compare Variant structs together
		//public static bool ApproximatelyEqual(Variant a, Variant b,
		//	float angleEpsilon = 0.01f,   // degrees
		//	float deltaSqrEpsilon = 0.0001f)
		//{
		//	return Mathf.Abs(a.angle - b.angle) <= angleEpsilon &&
		//		   ApproximatelyEqual(a.delta, b.delta, deltaSqrEpsilon);
		//}
	}
}