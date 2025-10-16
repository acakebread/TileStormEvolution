using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace MassiveHadronLtd
{
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

			// Build BSP-style visit order (balanced binary search tree order)
			List<int> planeVisitOrder = new List<int>();
			BuildBSPOrder(0, strataPlanes.Count - 1, planeVisitOrder);

			// Working buffers
			List<Vector3> newVertices = new List<Vector3>(vertices);
			List<Vector3> newNormals = new List<Vector3>(mesh.normals);
			List<Vector2> newUVs = new List<Vector2>(mesh.uv);
			List<int> currentTriangles = new List<int>(mesh.triangles);

			// Slice against planes in BSP order
			foreach (int planeIndex in planeVisitOrder)
			{
				Plane plane = strataPlanes[planeIndex];
				List<int> nextTriangles = new List<int>();

				for (int i = 0; i < currentTriangles.Count; i += 3)
				{
					int v0 = currentTriangles[i];
					int v1 = currentTriangles[i + 1];
					int v2 = currentTriangles[i + 2];

					List<int> backTris;
					List<int> frontTris = ClipTriangleAgainstPlane(
						v0, v1, v2, plane,
						newVertices, newNormals, newUVs,
						out backTris);

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

			result.RecalculateBounds();
			result.RecalculateTangents();

			// Optional debug log
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"Final mesh: {result.vertexCount} vertices, {result.triangles.Length / 3} triangles");
			Debug.Log(sb.ToString());

			return result;
		}

		private static void BuildBSPOrder(int start, int end, List<int> order)
		{
			if (start > end) return;
			int mid = (start + end) / 2;
			order.Add(mid);
			BuildBSPOrder(start, mid - 1, order);
			BuildBSPOrder(mid + 1, end, order);
		}

		private struct PolyVertex
		{
			public Vector3 pos;
			public Vector3 normal;
			public Vector2 uv;
			public int idx;
			public PolyVertex(Vector3 p, Vector3 n, Vector2 u, int i) { pos = p; normal = n; uv = u; idx = i; }
		}

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
					float t = da / (da - db);
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
				// Use the original quad triangulation you specified
				return GeomUtils.TriangulateQuad(poly[0].idx, poly[1].idx, poly[2].idx, poly[3].idx, verts.ToArray());
			}

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
}