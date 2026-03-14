using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GeomUtils
	{
		/// <summary>
		/// Triangulates a quad (a,b,c,d) choosing the diagonal that produces 
		/// the shortest split. Assumes vertices are in order (convex quad).
		/// </summary>
		public static List<int> TriangulateQuad(int i0, int i1, int i2, int i3, Vector3[] verts)
		{
			Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2], v3 = verts[i3];
			float d0 = (v0 - v2).sqrMagnitude;
			float d1 = (v1 - v3).sqrMagnitude;
			List<int> tris = new List<int>();
			if (d0 < d1) tris.AddRange(new int[] { i0, i1, i2, i0, i2, i3 });
			else tris.AddRange(new int[] { i0, i1, i3, i1, i2, i3 });
			return tris;
		}

		/// <summary>
		/// Chooses the shorter diagonal for a quad split.
		/// Input: quad vertices v0,v1,v2,v3 in order.
		/// Returns true if diagonal v0-v2 is shorter than v1-v3
		/// </summary>
		public static bool PreferShorterDiagonal(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
		{
			float diag0 = (v0 - v2).sqrMagnitude;
			float diag1 = (v1 - v3).sqrMagnitude;
			return diag0 <= diag1;
		}


		/// <summary>
		/// Returns true if the two triangles form a quad and the diagonal choice should favor the shorter distance.
		/// </summary>
		public static int ChooseDiagonal(Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
		{
			float diag1 = (a0 - a2).sqrMagnitude;
			float diag2 = (a1 - a3).sqrMagnitude;
			return diag1 <= diag2 ? 0 : 1;
		}

		/// <summary>
		/// Interpolates between two values.
		/// </summary>
		public static T Lerp<T>(T a, T b, float t) where T : struct
		{
			if (typeof(T) == typeof(Vector3))
				return (T)(object)Vector3.Lerp((Vector3)(object)a, (Vector3)(object)b, t);
			if (typeof(T) == typeof(Vector2))
				return (T)(object)Vector2.Lerp((Vector2)(object)a, (Vector2)(object)b, t);
			throw new System.NotImplementedException("Unsupported type for Lerp");
		}

		public static RectInt PointArrayBoundsInt(IEnumerable<Vector2Int> points)
		{
			if (!points.Any()) return new RectInt(0, 0, 0, 0);
			var minX = points.Min(p => p.x);
			var minY = points.Min(p => p.y);
			var maxX = points.Max(p => p.x);
			var maxY = points.Max(p => p.y);
			return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
		}

		//public static RectInt PointArrayBoundsInt(IEnumerable<Vector2Int> points)
		//{
		//	if (!points.Any()) return new RectInt(0, 0, 0, 0);
		//	var minX = points.Min(p => p.x);
		//	var minY = points.Min(p => p.y);
		//	var maxX = points.Max(p => p.x) + 1;
		//	var maxY = points.Max(p => p.y) + 1;
		//	return new RectInt(minX, minY, maxX - minX, maxY - minY);
		//}

		public static RectInt GetBoundingRect(IEnumerable<Vector2Int> points, RectInt? seed = null)
		{
			var r = seed ?? RectInt.zero;
			bool hasAny = false;

			foreach (var p in points)
			{
				hasAny = true;
				r.xMin = Math.Min(r.xMin, p.x);
				r.yMin = Math.Min(r.yMin, p.y);
				r.xMax = Math.Max(r.xMax, p.x + 1);
				r.yMax = Math.Max(r.yMax, p.y + 1);
			}

			if (!hasAny && seed == null)
				return new RectInt(0, 0, 0, 0);

			return r;
		}

		public static RectInt GetBoundingRect(Vector2Int point, RectInt? seed = null) => GetBoundingRect(new[] { point }, seed);

		//public static RectInt IncludingPoint(this RectInt rect, Vector2Int point)
		//{
		//	rect.xMin = Math.Min(rect.xMin, point.x);
		//	rect.yMin = Math.Min(rect.yMin, point.y);
		//	rect.xMax = Math.Max(rect.xMax, point.x + 1);
		//	rect.yMax = Math.Max(rect.yMax, point.y + 1);
		//	return rect;
		//}

		//public static RectInt GetBoundingRect(RectInt initial, Vector2Int point)
		//{
		//	initial.xMin = Math.Min(initial.xMin, point.x);
		//	initial.yMin = Math.Min(initial.yMin, point.y);
		//	initial.xMax = Math.Max(initial.xMax, point.x + 1);
		//	initial.yMax = Math.Max(initial.yMax, point.y + 1);
		//	return initial;
		//}
	}

	public static class RectIntExtensions
	{
		public static void Encapsulate(this ref RectInt rect, Vector2Int point)
		{
			rect.xMin = Math.Min(rect.xMin, point.x);
			rect.yMin = Math.Min(rect.yMin, point.y);
			rect.xMax = Math.Max(rect.xMax, point.x + 1);   // because xMax/yMax are exclusive
			rect.yMax = Math.Max(rect.yMax, point.y + 1);
		}

		// Optional overloads
		public static void Encapsulate(this ref RectInt rect, int x, int y)
			=> Encapsulate(ref rect, new Vector2Int(x, y));

		public static RectInt Encapsulate(RectInt rect, Vector2Int point)
		{
			rect.Encapsulate(point);
			return rect;
		}
	}
}