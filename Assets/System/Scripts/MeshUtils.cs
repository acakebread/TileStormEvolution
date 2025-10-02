using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class MeshUtils
	{
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

		public static Mesh SplitMeshAlongPlane(Mesh mesh, Vector3 planeNormal, float offset)
		{
			planeNormal.Normalize();

			var verts = mesh.vertices;
			var tris = mesh.triangles;
			var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
			var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

			List<VertexData> allVerts = new List<VertexData>();
			List<int> allTris = new List<int>();
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

				ClipTriangle(tri, planeNormal, offset, allVerts, allTris);
			}

			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);
			return BuildMesh(uniqueVerts, uniqueTris);
		}

		// Updated to return a single Mesh
		public static Mesh SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
		{
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
			int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));

			if (Debug.isDebugBuild)
			{
				Debug.Log($"SubdivideMeshLongEdges: Length = {length:F3}, maxSegmentLength = {maxSegmentLength:F3}, maxDepth = {maxDepth}");
			}

			// Accumulate all vertices and triangles
			List<VertexData> allVerts = new List<VertexData>();
			List<int> allTris = new List<int>();

			// Start BSP recursion
			SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, allVerts, allTris);

			if (allVerts.Count == 0 || allTris.Count == 0)
			{
				if (Debug.isDebugBuild)
				{
					Debug.LogWarning("SubdivideMeshLongEdges: No valid geometry after subdivision. Returning empty mesh.");
				}
				return new Mesh();
			}

			// Deduplicate vertices across all subdivisions
			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

			// Build and return the unified mesh
			Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
			}

			return resultMesh;
		}

		// Updated to accumulate vertices and triangles
		private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<VertexData> allVerts, List<int> allTris)
		{
			float length = maxProj - minProj;
			if (length <= maxSegmentLength || depth >= maxDepth)
			{
				if (mesh.vertexCount > 0)
				{
					// Add this mesh's geometry to the shared lists
					var verts = mesh.vertices;
					var tris = mesh.triangles;
					var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
					var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

					int baseIndex = allVerts.Count;
					for (int i = 0; i < verts.Length; i++)
					{
						Vector3 p = verts[i];
						Vector3 n = norms != null ? norms[i] : Vector3.up;
						Vector2 uv = uvs != null ? uvs[i] : Vector2.zero;
						allVerts.Add(new VertexData(p, n, uv));
					}
					allTris.AddRange(tris.Select(t => t + baseIndex));

					if (Debug.isDebugBuild)
					{
						Debug.Log($"Leaf at depth {depth}: {verts.Length} vertices, {tris.Length / 3} triangles added to unified mesh");
					}
				}
				return;
			}

			// Split at midpoint
			float midProj = (minProj + maxProj) / 2f;
			float offset = Vector3.Dot(meshCenter + divisionAxis * midProj, divisionAxis);

			// Use SplitMeshAlongPlane to get a single mesh (top and bottom combined)
			Mesh splitMesh = SplitMeshAlongPlane(mesh, divisionAxis, offset);

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Split at depth {depth}, offset {offset:F3}: split mesh {splitMesh.vertexCount} vertices, {splitMesh.triangles.Length / 3} triangles");
			}

			// Recurse on the split mesh
			if (splitMesh.vertexCount > 0)
			{
				SubdivideMeshBSP(splitMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
				SubdivideMeshBSP(splitMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
				Object.Destroy(splitMesh); // Clean up temporary mesh
			}
		}

		private static void ClipTriangle(List<VertexData> tri,
			Vector3 planeNormal, float offset,
			List<VertexData> allVerts, List<int> allTris)
		{
			List<VertexData> topPoly, bottomPoly;
			ClipPolygonAgainstPlane(tri, planeNormal, offset, out topPoly, out bottomPoly);
			AddPolygon(topPoly, allVerts, allTris);
			AddPolygon(bottomPoly, allVerts, allTris);
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

		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
		{
			var uniqueVerts = new List<VertexData>();
			var vertexMap = new Dictionary<Vector3, int>(new VertexPositionEqualityComparer(1e-6f));
			var newTris = new List<int>();

			int[] indexMap = new int[verts.Count];
			for (int i = 0; i < verts.Count; i++)
			{
				Vector3 pos = verts[i].pos;
				if (!vertexMap.TryGetValue(pos, out int index))
				{
					index = uniqueVerts.Count;
					uniqueVerts.Add(verts[i]);
					vertexMap[pos] = index;
				}
				indexMap[i] = index;
			}

			foreach (int oldIndex in tris)
			{
				if (oldIndex >= 0 && oldIndex < indexMap.Length)
				{
					newTris.Add(indexMap[oldIndex]);
				}
				else
				{
					Debug.LogWarning($"Invalid triangle index {oldIndex} encountered. Skipping.");
				}
			}

			return (uniqueVerts, newTris);
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

		private class VertexPositionEqualityComparer : IEqualityComparer<Vector3>
		{
			private readonly float _tolerance;

			public VertexPositionEqualityComparer(float tolerance)
			{
				_tolerance = tolerance;
			}

			public bool Equals(Vector3 a, Vector3 b)
			{
				return Vector3.SqrMagnitude(a - b) < _tolerance * _tolerance;
			}

			public int GetHashCode(Vector3 obj)
			{
				return ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
					   ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
					   ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
			}
		}
	}
}
//using System.Collections.Generic;
//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public static class MeshUtils
//	{
//		private struct VertexData
//		{
//			public Vector3 pos;
//			public Vector3 normal;
//			public Vector2 uv;

//			public VertexData(Vector3 p, Vector3 n, Vector2 u)
//			{
//				pos = p;
//				normal = n;
//				uv = u;
//			}
//		}

//		public static Mesh SplitMeshAlongPlane(Mesh mesh, Vector3 planeNormal, float offset)
//		{
//			planeNormal.Normalize();

//			var verts = mesh.vertices;
//			var tris = mesh.triangles;
//			var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//			var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//			List<VertexData> allVerts = new List<VertexData>();
//			List<int> allTris = new List<int>();
//			List<VertexData> tri = new List<VertexData>(3);

//			// Process triangles and clip them
//			for (int i = 0; i < tris.Length; i += 3)
//			{
//				tri.Clear();
//				for (int j = 0; j < 3; j++)
//				{
//					int idx = tris[i + j];
//					Vector3 p = verts[idx];
//					Vector3 n = norms != null ? norms[idx] : Vector3.up;
//					Vector2 uv = uvs != null ? uvs[idx] : Vector2.zero;
//					tri.Add(new VertexData(p, n, uv));
//				}

//				ClipTriangle(tri, planeNormal, offset, allVerts, allTris);
//			}

//			// Deduplicate vertices and update triangle indices
//			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

//			// Build and return the unified mesh
//			return BuildMesh(uniqueVerts, uniqueTris);
//		}

//		private static void ClipTriangle(List<VertexData> tri,
//			Vector3 planeNormal, float offset,
//			List<VertexData> allVerts, List<int> allTris)
//		{
//			List<VertexData> topPoly, bottomPoly;
//			ClipPolygonAgainstPlane(tri, planeNormal, offset, out topPoly, out bottomPoly);

//			// Add both top and bottom polygons to the unified lists
//			AddPolygon(topPoly, allVerts, allTris);
//			AddPolygon(bottomPoly, allVerts, allTris);
//		}

//		private static void ClipPolygonAgainstPlane(
//			List<VertexData> poly,
//			Vector3 planeNormal, float offset,
//			out List<VertexData> topPoly, out List<VertexData> bottomPoly)
//		{
//			topPoly = new List<VertexData>();
//			bottomPoly = new List<VertexData>();

//			int count = poly.Count;
//			for (int i = 0; i < count; i++)
//			{
//				VertexData curr = poly[i];
//				VertexData next = poly[(i + 1) % count];

//				// Distance from origin to point along plane normal
//				float d1 = Vector3.Dot(curr.pos, planeNormal) - offset;
//				float d2 = Vector3.Dot(next.pos, planeNormal) - offset;

//				bool currAbove = d1 >= 0;
//				bool nextAbove = d2 >= 0;

//				if (currAbove) topPoly.Add(curr);
//				else bottomPoly.Add(curr);

//				if (currAbove != nextAbove)
//				{
//					float t = d1 / (d1 - d2);
//					VertexData interp = Lerp(curr, next, t);
//					topPoly.Add(interp);
//					bottomPoly.Add(interp);
//				}
//			}
//		}

//		private static void AddPolygon(List<VertexData> poly,
//			List<VertexData> verts, List<int> tris)
//		{
//			if (poly.Count < 3) return;

//			int baseIndex = verts.Count;
//			verts.AddRange(poly);

//			for (int i = 1; i < poly.Count - 1; i++)
//			{
//				tris.Add(baseIndex);
//				tris.Add(baseIndex + i);
//				tris.Add(baseIndex + i + 1);
//			}
//		}

//		private static VertexData Lerp(VertexData a, VertexData b, float t)
//		{
//			return new VertexData(
//				Vector3.Lerp(a.pos, b.pos, t),
//				Vector3.Normalize(Vector3.Lerp(a.normal, b.normal, t)),
//				Vector2.Lerp(a.uv, b.uv, t)
//			);
//		}

//		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
//		{
//			var uniqueVerts = new List<VertexData>();
//			var vertexMap = new Dictionary<Vector3, int>(new VertexPositionEqualityComparer(1e-6f));
//			var newTris = new List<int>();

//			// Map original vertex indices to new unique indices
//			int[] indexMap = new int[verts.Count]; // Maps old indices to new indices
//			for (int i = 0; i < verts.Count; i++)
//			{
//				Vector3 pos = verts[i].pos;
//				if (!vertexMap.TryGetValue(pos, out int index))
//				{
//					index = uniqueVerts.Count;
//					uniqueVerts.Add(verts[i]);
//					vertexMap[pos] = index;
//				}
//				indexMap[i] = index;
//			}

//			// Remap triangle indices
//			foreach (int oldIndex in tris)
//			{
//				if (oldIndex >= 0 && oldIndex < indexMap.Length)
//				{
//					newTris.Add(indexMap[oldIndex]);
//				}
//				else
//				{
//					Debug.LogWarning($"Invalid triangle index {oldIndex} encountered. Skipping.");
//				}
//			}

//			return (uniqueVerts, newTris);
//		}

//		private static Mesh BuildMesh(List<VertexData> verts, List<int> tris)
//		{
//			Mesh mesh = new Mesh();
//			var positions = new Vector3[verts.Count];
//			var normals = new Vector3[verts.Count];
//			var uvs = new Vector2[verts.Count];

//			for (int i = 0; i < verts.Count; i++)
//			{
//				positions[i] = verts[i].pos;
//				normals[i] = verts[i].normal;
//				uvs[i] = verts[i].uv;
//			}

//			mesh.SetVertices(positions);
//			mesh.SetNormals(normals);
//			mesh.SetUVs(0, uvs);
//			mesh.SetTriangles(tris, 0);

//			mesh.RecalculateBounds();
//			return mesh;
//		}

//		private class VertexPositionEqualityComparer : IEqualityComparer<Vector3>
//		{
//			private readonly float _tolerance;

//			public VertexPositionEqualityComparer(float tolerance)
//			{
//				_tolerance = tolerance;
//			}

//			public bool Equals(Vector3 a, Vector3 b)
//			{
//				return Vector3.SqrMagnitude(a - b) < _tolerance * _tolerance;
//			}

//			public int GetHashCode(Vector3 obj)
//			{
//				// Simple hash based on rounded coordinates to handle floating-point precision
//				return ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
//					   ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
//					   ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
//			}
//		}
//	}
//}


