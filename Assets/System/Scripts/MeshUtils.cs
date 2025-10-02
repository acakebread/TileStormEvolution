using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class MeshUtils
	{
		public struct SplitResult
		{
			public Mesh topMesh;
			public Mesh bottomMesh;
		}

		private struct VertexData
		{
			public Vector3 pos;
			public Vector3 normal;
			public Vector2 uv;

			public VertexData(Vector3 p, Vector3 n, Vector2 u)
			{
				pos = p;
				normal = n;
				uv = u;
			}
		}

		public static List<Mesh> SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
		{
			List<Mesh> resultMeshes = new List<Mesh>();
			divisionAxis = divisionAxis.normalized;

			// Compute bounds along axis
			float minProj = float.MaxValue;
			float maxProj = float.MinValue;
			foreach (Vector3 v in inputMesh.vertices)
			{
				float proj = Vector3.Dot(v - meshCenter, divisionAxis);
				minProj = Mathf.Min(minProj, proj);
				maxProj = Mathf.Max(maxProj, proj);
			}
			float length = maxProj - minProj;
			int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f)); // BSP depth

			if (Debug.isDebugBuild)
			{
				Debug.Log($"SubdivideMeshLongEdges: Length = {length:F3}, maxSegmentLength = {maxSegmentLength:F3}, maxDepth = {maxDepth}");
			}

			// Start BSP recursion
			SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, resultMeshes);

			return resultMeshes;
		}

		private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<Mesh> resultMeshes)
		{
			float length = maxProj - minProj;
			if (length <= maxSegmentLength || depth >= maxDepth)
			{
				if (mesh.vertexCount > 0)
				{
					resultMeshes.Add(mesh);
					if (Debug.isDebugBuild)
					{
						Debug.Log($"Leaf at depth {depth}: {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles");
					}
				}
				return;
			}

			// Split at midpoint
			float midProj = (minProj + maxProj) / 2f;
			// Compute offset: distance from origin to the plane along the normal (divisionAxis)
			float offset = Vector3.Dot(meshCenter + divisionAxis * midProj, divisionAxis);
			var splitResult = SplitMeshAlongPlane(mesh, divisionAxis, offset);

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Split at depth {depth}, offset {offset:F3}: top mesh {splitResult.topMesh.vertexCount} vertices, {splitResult.topMesh.triangles.Length / 3} triangles; bottom mesh {splitResult.bottomMesh.vertexCount} vertices, {splitResult.bottomMesh.triangles.Length / 3} triangles");
			}

			// Recurse on top and bottom meshes
			if (splitResult.topMesh.vertexCount > 0)
			{
				SubdivideMeshBSP(splitResult.topMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, resultMeshes);
			}
			if (splitResult.bottomMesh.vertexCount > 0)
			{
				SubdivideMeshBSP(splitResult.bottomMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, resultMeshes);
			}
		}

		public static SplitResult SplitMeshAlongPlane(Mesh mesh, Vector3 planeNormal, float offset)
		{
			planeNormal.Normalize();

			var verts = mesh.vertices;
			var tris = mesh.triangles;
			var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
			var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

			List<VertexData> topVerts = new List<VertexData>();
			List<int> topTris = new List<int>();
			List<VertexData> bottomVerts = new List<VertexData>();
			List<int> bottomTris = new List<int>();

			List<VertexData> tri = new List<VertexData>(3);

			for (int i = 0; i < tris.Length; i += 3)
			{
				tri.Clear();
				for (int j = 0; j < 3; j++)
				{
					int idx = tris[i + j];
					Vector3 p = verts[idx];
					Vector3 n = norms != null ? norms[idx] : Vector3.up;
					Vector2 uv = uvs != null ? uvs[idx] : Vector2.zero;
					tri.Add(new VertexData(p, n, uv));
				}

				ClipTriangle(tri, planeNormal, offset, topVerts, topTris, bottomVerts, bottomTris);
			}

			Mesh topMesh = BuildMesh(topVerts, topTris);
			Mesh bottomMesh = BuildMesh(bottomVerts, bottomTris);

			return new SplitResult { topMesh = topMesh, bottomMesh = bottomMesh };
		}

		private static void ClipTriangle(List<VertexData> tri,
			Vector3 planeNormal, float offset,
			List<VertexData> topVerts, List<int> topTris,
			List<VertexData> bottomVerts, List<int> bottomTris)
		{
			List<VertexData> topPoly, bottomPoly;
			ClipPolygonAgainstPlane(tri, planeNormal, offset, out topPoly, out bottomPoly);
			AddPolygon(topPoly, topVerts, topTris);
			AddPolygon(bottomPoly, bottomVerts, bottomTris);
		}

		private static void ClipPolygonAgainstPlane(
			List<VertexData> poly,
			Vector3 planeNormal, float offset,
			out List<VertexData> topPoly, out List<VertexData> bottomPoly)
		{
			topPoly = new List<VertexData>();
			bottomPoly = new List<VertexData>();

			int count = poly.Count;
			for (int i = 0; i < count; i++)
			{
				VertexData curr = poly[i];
				VertexData next = poly[(i + 1) % count];

				// Distance from origin to point along plane normal
				float d1 = Vector3.Dot(curr.pos, planeNormal) - offset;
				float d2 = Vector3.Dot(next.pos, planeNormal) - offset;

				bool currAbove = d1 >= 0;
				bool nextAbove = d2 >= 0;

				if (currAbove) topPoly.Add(curr);
				else bottomPoly.Add(curr);

				if (currAbove != nextAbove)
				{
					float t = d1 / (d1 - d2);
					VertexData interp = Lerp(curr, next, t);
					topPoly.Add(interp);
					bottomPoly.Add(interp);
				}
			}
		}

		private static void AddPolygon(List<VertexData> poly,
			List<VertexData> verts, List<int> tris)
		{
			if (poly.Count < 3) return;

			int baseIndex = verts.Count;
			verts.AddRange(poly);

			for (int i = 1; i < poly.Count - 1; i++)
			{
				tris.Add(baseIndex);
				tris.Add(baseIndex + i);
				tris.Add(baseIndex + i + 1);
			}
		}

		private static VertexData Lerp(VertexData a, VertexData b, float t)
		{
			return new VertexData(
				Vector3.Lerp(a.pos, b.pos, t),
				Vector3.Normalize(Vector3.Lerp(a.normal, b.normal, t)),
				Vector2.Lerp(a.uv, b.uv, t)
			);
		}

		private static Mesh BuildMesh(List<VertexData> verts, List<int> tris)
		{
			Mesh mesh = new Mesh();
			var positions = new Vector3[verts.Count];
			var normals = new Vector3[verts.Count];
			var uvs = new Vector2[verts.Count];

			for (int i = 0; i < verts.Count; i++)
			{
				positions[i] = verts[i].pos;
				normals[i] = verts[i].normal;
				uvs[i] = verts[i].uv;
			}

			mesh.SetVertices(positions);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(tris, 0);

			mesh.RecalculateBounds();
			return mesh;
		}
	}
}