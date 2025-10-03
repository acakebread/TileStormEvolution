using System.Collections.Generic;
using UnityEngine;

public static class GeomUtils
{
	/// <summary>
	/// Triangulates a quad (a,b,c,d) choosing the diagonal that produces 
	/// the shortest split. Assumes vertices are in order (convex quad).
	/// </summary>
	public static List<int> TriangulateQuad(int a, int b, int c, int d, Vector3[] verts)
	{
		List<int> tris = new List<int>(6);

		float diagAC = (verts[a] - verts[c]).sqrMagnitude;
		float diagBD = (verts[b] - verts[d]).sqrMagnitude;

		if (diagAC <= diagBD)
		{
			// Use diagonal AC
			tris.Add(a); tris.Add(b); tris.Add(c);
			tris.Add(a); tris.Add(c); tris.Add(d);
		}
		else
		{
			// Use diagonal BD
			tris.Add(a); tris.Add(b); tris.Add(d);
			tris.Add(b); tris.Add(c); tris.Add(d);
		}

		return tris;
	}
}
