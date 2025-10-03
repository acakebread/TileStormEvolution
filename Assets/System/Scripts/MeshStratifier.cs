using UnityEngine;
using System.Collections.Generic;
using System.Text;

public static class MeshStratifier
{
	public static Mesh StratifyMesh(Mesh mesh, Plane minPlane, int numStrata = 1)
	{
		if (mesh == null || numStrata < 1)
		{
			Debug.LogError("Invalid input: Mesh is null or numStrata < 1");
			return mesh;
		}

		Vector3 planeNormal = minPlane.normal.normalized;
		float offset = -minPlane.distance;

		Vector3[] vertices = mesh.vertices;
		float maxDist = float.MinValue;
		foreach (Vector3 vertex in vertices)
		{
			float dist = Vector3.Dot(vertex, planeNormal);
			maxDist = Mathf.Max(maxDist, dist);
		}
		float maxOffset = maxDist;

		// Build slicing planes
		List<Plane> strataPlanes = new List<Plane>();
		float step = (maxOffset - offset) / (numStrata + 1);
		for (int i = 1; i <= numStrata; i++)
		{
			float strataOffset = offset + step * i;
			strataPlanes.Add(new Plane(planeNormal, -strataOffset));
		}

		// Working buffers
		List<Vector3> newVertices = new List<Vector3>(vertices);
		List<Vector3> newNormals = new List<Vector3>(mesh.normals);
		List<Vector2> newUVs = new List<Vector2>(mesh.uv);
		List<int> currentTriangles = new List<int>(mesh.triangles);

		// Slice against each plane
		foreach (var plane in strataPlanes)
		{
			List<int> nextTriangles = new List<int>();

			for (int i = 0; i < currentTriangles.Count; i += 3)
			{
				int v0 = currentTriangles[i];
				int v1 = currentTriangles[i + 1];
				int v2 = currentTriangles[i + 2];

				// Clip this triangle and get front/back triangles
				List<int> backTris;
				List<int> frontTris = ClipTriangleAgainstPlane(
					v0, v1, v2, plane,
					newVertices, newNormals, newUVs,
					out backTris);

				// Keep both halves (stratification → not discarding either side)
				nextTriangles.AddRange(frontTris);
				nextTriangles.AddRange(backTris);
			}

			currentTriangles = nextTriangles;
		}

		Mesh result = new Mesh
		{
			vertices = newVertices.ToArray(),
			normals = newNormals.ToArray(),
			uv = newUVs.ToArray(),
			triangles = currentTriangles.ToArray()
		};

		// Debug log
		StringBuilder sb = new StringBuilder();
		sb.AppendLine($"Final mesh: {result.vertexCount} vertices, {result.triangles.Length / 3} triangles");
		sb.AppendLine("Vertices:");
		for (int i = 0; i < result.vertexCount; i++)
			sb.AppendLine($"v{i}: {result.vertices[i]}");
		sb.AppendLine("Triangles:");
		for (int i = 0; i < result.triangles.Length; i += 3)
			sb.AppendLine($"t{i / 3}: ({result.triangles[i]}, {result.triangles[i + 1]}, {result.triangles[i + 2]})");
		Debug.Log(sb.ToString());

		result.RecalculateBounds();
		result.RecalculateTangents();
		return result;
	}

	// Small helper struct (safer & clearer than tuple fields)
	private struct PolyVertex
	{
		public Vector3 pos;
		public Vector3 normal;
		public Vector2 uv;
		public int idx;
		public PolyVertex(Vector3 p, Vector3 n, Vector2 u, int i) { pos = p; normal = n; uv = u; idx = i; }
	}

	// Clip a single triangle against a plane. Returns front triangles and outputs back triangles.
	private static List<int> ClipTriangleAgainstPlane(
		int v0, int v1, int v2, Plane plane,
		List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
		out List<int> backTriangles)
	{
		backTriangles = new List<int>();

		List<PolyVertex> inVerts = new List<PolyVertex>()
		{
			new PolyVertex(verts[v0], norms[v0], uvs[v0], v0),
			new PolyVertex(verts[v1], norms[v1], uvs[v1], v1),
			new PolyVertex(verts[v2], norms[v2], uvs[v2], v2)
		};

		System.Func<Vector3, float> Dist = (Vector3 p) => plane.GetDistanceToPoint(p);

		List<PolyVertex> frontPoly = new List<PolyVertex>();
		List<PolyVertex> backPoly = new List<PolyVertex>();

		for (int i = 0; i < inVerts.Count; i++)
		{
			PolyVertex a = inVerts[i];
			PolyVertex b = inVerts[(i + 1) % inVerts.Count];
			float da = Dist(a.pos);
			float db = Dist(b.pos);

			bool aFront = da >= 0f;
			bool bFront = db >= 0f;

			if (aFront) frontPoly.Add(a);
			else backPoly.Add(a);

			if ((aFront && !bFront) || (!aFront && bFront))
			{
				// Edge crosses plane — compute intersection
				float t = da / (da - db); // safe since signs differ
				Vector3 p = Vector3.Lerp(a.pos, b.pos, t);
				Vector3 n = Vector3.Lerp(a.normal, b.normal, t).normalized;
				Vector2 uv = Vector2.Lerp(a.uv, b.uv, t);

				int idx = verts.Count;
				verts.Add(p);
				norms.Add(n);
				uvs.Add(uv);

				PolyVertex pv = new PolyVertex(p, n, uv, idx);
				frontPoly.Add(pv);
				backPoly.Add(pv);
			}
		}

		List<int> frontTris = Triangulate(frontPoly, verts);
		backTriangles = Triangulate(backPoly, verts);
		return frontTris;
	}

	private static List<int> Triangulate(List<PolyVertex> poly, List<Vector3> verts)
	{
		List<int> tris = new List<int>();
		if (poly.Count < 3) return tris;

		if (poly.Count == 4)
		{
			// Quad: pick shortest diagonal
			return GeomUtils.TriangulateQuad(
				poly[0].idx, poly[1].idx, poly[2].idx, poly[3].idx,
				verts.ToArray()
			);
		}

		// Fallback: simple fan triangulation
		int first = poly[0].idx;
		for (int i = 1; i < poly.Count - 1; i++)
		{
			tris.Add(first);
			tris.Add(poly[i].idx);
			tris.Add(poly[i + 1].idx);
		}
		return tris;
	}
}
