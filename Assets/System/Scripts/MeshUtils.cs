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
				Debug.Log($"SubdivideMeshLongEdges: Length={length:F3}, maxSegmentLength={maxSegmentLength:F3}, maxDepth={maxDepth}, meshCenter={meshCenter}");
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

			// Deduplicate vertices
			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

			// Build and return the unified mesh
			Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
			}

			return resultMesh;
		}

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

			// Split at midpoint, respecting meshCenter for initial split
			float midProj = (minProj + maxProj) / 2f;
			float planeOffset = (depth == 0) ? Vector3.Dot(meshCenter, divisionAxis) : midProj;

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Splitting at depth {depth}, planeOffset={planeOffset:F3}, midProj={midProj:F3}, meshCenter={meshCenter}");
			}

			// Use MeshUtilTest.SplitMeshAlongPlane
			Mesh tempMesh = MeshUtilTest.SplitMeshAlongPlane(mesh, divisionAxis, planeOffset);

			if (tempMesh.vertexCount == 0)
			{
				if (Debug.isDebugBuild)
				{
					Debug.Log($"Split at depth {depth}, planeOffset {planeOffset:F3}: No vertices in split mesh. Skipping.");
				}
				Object.Destroy(tempMesh);
				return;
			}

			// Create separate meshes for top and bottom
			List<Vector3> topVerts = new List<Vector3>();
			List<Vector3> topNorms = new List<Vector3>();
			List<Vector2> topUVs = new List<Vector2>();
			List<int> topTris = new List<int>();
			List<Vector3> bottomVerts = new List<Vector3>();
			List<Vector3> bottomNorms = new List<Vector3>();
			List<Vector2> bottomUVs = new List<Vector2>();
			List<int> bottomTris = new List<int>();

			var splitVerts = tempMesh.vertices;
			var splitNorms = tempMesh.normals;
			var splitUVs = tempMesh.uv;
			var splitTris = tempMesh.triangles;

			// Track vertices near the plane
			HashSet<int> planeVertices = new HashSet<int>();
			for (int i = 0; i < splitVerts.Length; i++)
			{
				float dist = Mathf.Abs(Vector3.Dot(splitVerts[i], divisionAxis) - planeOffset);
				if (dist < 1e-4f)
				{
					planeVertices.Add(i);
				}
			}

			// Assign triangles to top or bottom using centroid
			Dictionary<Vector3, int> topVertexMap = new Dictionary<Vector3, int>(new Vector3EqualityComparer(1e-6f));
			Dictionary<Vector3, int> bottomVertexMap = new Dictionary<Vector3, int>(new Vector3EqualityComparer(1e-6f));

			for (int i = 0; i < splitTris.Length; i += 3)
			{
				Vector3 v0 = splitVerts[splitTris[i]];
				Vector3 v1 = splitVerts[splitTris[i + 1]];
				Vector3 v2 = splitVerts[splitTris[i + 2]];
				Vector3 centroid = (v0 + v1 + v2) / 3f;
				float dCentroid = Vector3.Dot(centroid, divisionAxis) - planeOffset;
				bool isTop = dCentroid >= -1e-4f;

				List<Vector3> targetVerts = isTop ? topVerts : bottomVerts;
				List<Vector3> targetNorms = isTop ? topNorms : bottomNorms;
				List<Vector2> targetUVs = isTop ? topUVs : bottomUVs;
				List<int> targetTris = isTop ? topTris : bottomTris;
				Dictionary<Vector3, int> targetVertexMap = isTop ? topVertexMap : bottomVertexMap;

				int[] newIndices = new int[3];
				bool onPlane = false;
				for (int j = 0; j < 3; j++)
				{
					int idx = splitTris[i + j];
					Vector3 pos = splitVerts[idx];
					if (!targetVertexMap.TryGetValue(pos, out int newIdx))
					{
						newIdx = targetVerts.Count;
						targetVerts.Add(pos);
						targetNorms.Add(splitNorms[idx]);
						targetUVs.Add(splitUVs[idx]);
						targetVertexMap[pos] = newIdx;
					}
					newIndices[j] = newIdx;
					if (planeVertices.Contains(idx))
					{
						onPlane = true;
					}
				}

				// Skip degenerate triangles
				if (newIndices[0] != newIndices[1] && newIndices[1] != newIndices[2] && newIndices[0] != newIndices[2])
				{
					targetTris.Add(newIndices[0]);
					targetTris.Add(newIndices[1]);
					targetTris.Add(newIndices[2]);
				}
				else if (Debug.isDebugBuild)
				{
					Debug.LogWarning($"Skipping degenerate triangle at depth {depth}: [{newIndices[0]},{newIndices[1]},{newIndices[2]}]");
				}

				if (Debug.isDebugBuild && onPlane)
				{
					Debug.Log($"Triangle {i / 3} {(isTop ? "top" : "bottom")}: vertices [{splitTris[i]},{splitTris[i + 1]},{splitTris[i + 2]}], mapped to [{newIndices[0]},{newIndices[1]},{newIndices[2]}], on plane: {onPlane}, centroid dist: {dCentroid:F6}");
				}
			}

			// Build top and bottom meshes
			Mesh topMesh = new Mesh();
			topMesh.SetVertices(topVerts);
			topMesh.SetNormals(topNorms);
			topMesh.SetUVs(0, topUVs);
			topMesh.SetTriangles(topTris, 0);
			topMesh.RecalculateBounds();

			Mesh bottomMesh = new Mesh();
			bottomMesh.SetVertices(bottomVerts);
			bottomMesh.SetNormals(bottomNorms);
			bottomMesh.SetUVs(0, bottomUVs);
			bottomMesh.SetTriangles(bottomTris, 0);
			bottomMesh.RecalculateBounds();

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Split at depth {depth}, planeOffset {planeOffset:F3}: Top mesh {topMesh.vertexCount} verts, {topMesh.triangles.Length / 3} tris; Bottom mesh {bottomMesh.vertexCount} verts, {bottomMesh.triangles.Length / 3} tris; Plane vertices: {planeVertices.Count}");
			}

			// Recurse on both sides
			if (topMesh.vertexCount > 0)
			{
				SubdivideMeshBSP(topMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
			}
			if (bottomMesh.vertexCount > 0)
			{
				SubdivideMeshBSP(bottomMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
			}

			// Clean up temporary meshes
			Object.Destroy(tempMesh);
			Object.Destroy(topMesh);
			Object.Destroy(bottomMesh);
		}

		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
		{
			var uniqueVerts = new List<VertexData>();
			var vertexMap = new Dictionary<VertexData, int>(new VertexEqualityComparer(1e-6f, 1e-6f));
			var newTris = new List<int>();

			int[] indexMap = new int[verts.Count];
			for (int i = 0; i < verts.Count; i++)
			{
				if (!vertexMap.TryGetValue(verts[i], out int idx))
				{
					idx = uniqueVerts.Count;
					uniqueVerts.Add(verts[i]);
					vertexMap[verts[i]] = idx;
				}
				indexMap[i] = idx;
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

		private class VertexEqualityComparer : IEqualityComparer<VertexData>
		{
			private readonly float _posTol;
			private readonly float _normTol;

			public VertexEqualityComparer(float posTolerance = 1e-6f, float normTolerance = 1e-6f)
			{
				_posTol = posTolerance;
				_normTol = normTolerance;
			}

			public bool Equals(VertexData a, VertexData b)
			{
				return Vector3.SqrMagnitude(a.pos - b.pos) < _posTol * _posTol &&
					   Vector3.SqrMagnitude(a.normal - b.normal) < _normTol * _normTol;
			}

			public int GetHashCode(VertexData v)
			{
				int hx = (int)(Mathf.Round(v.pos.x / _posTol) * 73856093);
				int hy = (int)(Mathf.Round(v.pos.y / _posTol) * 19349663);
				int hz = (int)(Mathf.Round(v.pos.z / _posTol) * 83492791);

				int nx = (int)(Mathf.Round(v.normal.x / _normTol) * 73856093);
				int ny = (int)(Mathf.Round(v.normal.y / _normTol) * 19349663);
				int nz = (int)(Mathf.Round(v.normal.z / _normTol) * 83492791);

				return hx ^ hy ^ hz ^ nx ^ ny ^ nz;
			}
		}

		private class Vector3EqualityComparer : IEqualityComparer<Vector3>
		{
			private readonly float _tolerance;
			public Vector3EqualityComparer(float tolerance) { _tolerance = tolerance; }
			public bool Equals(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < _tolerance * _tolerance;
			public int GetHashCode(Vector3 obj) => ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
												  ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
												  ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
		}
	}
}

//using System.Collections.Generic;
//using System.Linq;
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

//		public static Mesh SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
//		{
//			divisionAxis = divisionAxis.normalized;

//			// Compute bounds along axis
//			float minProj = float.MaxValue;
//			float maxProj = float.MinValue;
//			foreach (Vector3 v in inputMesh.vertices)
//			{
//				float proj = Vector3.Dot(v - meshCenter, divisionAxis);
//				minProj = Mathf.Min(minProj, proj);
//				maxProj = Mathf.Max(maxProj, proj);
//			}
//			float length = maxProj - minProj;
//			int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"SubdivideMeshLongEdges: Length={length:F3}, maxSegmentLength={maxSegmentLength:F3}, maxDepth={maxDepth}, meshCenter={meshCenter}");
//			}

//			// Accumulate all vertices and triangles
//			List<VertexData> allVerts = new List<VertexData>();
//			List<int> allTris = new List<int>();

//			// Start BSP recursion
//			SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, allVerts, allTris);

//			if (allVerts.Count == 0 || allTris.Count == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.LogWarning("SubdivideMeshLongEdges: No valid geometry after subdivision. Returning empty mesh.");
//				}
//				return new Mesh();
//			}

//			// Deduplicate vertices
//			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

//			// Build and return the unified mesh
//			Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
//			}

//			return resultMesh;
//		}

//		private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<VertexData> allVerts, List<int> allTris)
//		{
//			float length = maxProj - minProj;
//			if (length <= maxSegmentLength || depth >= maxDepth)
//			{
//				if (mesh.vertexCount > 0)
//				{
//					// Add this mesh's geometry to the shared lists
//					var verts = mesh.vertices;
//					var tris = mesh.triangles;
//					var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//					var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//					int baseIndex = allVerts.Count;
//					for (int i = 0; i < verts.Length; i++)
//					{
//						Vector3 p = verts[i];
//						Vector3 n = norms != null ? norms[i] : Vector3.up;
//						Vector2 uv = uvs != null ? uvs[i] : Vector2.zero;
//						allVerts.Add(new VertexData(p, n, uv));
//					}
//					allTris.AddRange(tris.Select(t => t + baseIndex));

//					if (Debug.isDebugBuild)
//					{
//						Debug.Log($"Leaf at depth {depth}: {verts.Length} vertices, {tris.Length / 3} triangles added to unified mesh");
//					}
//				}
//				return;
//			}

//			// Split at midpoint, using meshCenter as the base point
//			float midProj = (minProj + maxProj) / 2f;
//			// Compute plane offset directly from meshCenter
//			float planeOffset = Vector3.Dot(meshCenter, divisionAxis);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Splitting at depth {depth}, planeOffset={planeOffset:F3}, midProj={midProj:F3}, meshCenter={meshCenter}");
//			}

//			// Use MeshUtilTest.SplitMeshAlongPlane
//			Mesh tempMesh = MeshUtilTest.SplitMeshAlongPlane(mesh, divisionAxis, planeOffset);

//			if (tempMesh.vertexCount == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.Log($"Split at depth {depth}, planeOffset {planeOffset:F3}: No vertices in split mesh. Skipping.");
//				}
//				Object.Destroy(tempMesh);
//				return;
//			}

//			// Create separate meshes for top and bottom
//			List<Vector3> topVerts = new List<Vector3>();
//			List<Vector3> topNorms = new List<Vector3>();
//			List<Vector2> topUVs = new List<Vector2>();
//			List<int> topTris = new List<int>();
//			List<Vector3> bottomVerts = new List<Vector3>();
//			List<Vector3> bottomNorms = new List<Vector3>();
//			List<Vector2> bottomUVs = new List<Vector2>();
//			List<int> bottomTris = new List<int>();

//			var splitVerts = tempMesh.vertices;
//			var splitNorms = tempMesh.normals;
//			var splitUVs = tempMesh.uv;
//			var splitTris = tempMesh.triangles;

//			// Track vertices near the plane
//			HashSet<int> planeVertices = new HashSet<int>();
//			for (int i = 0; i < splitVerts.Length; i++)
//			{
//				float dist = Mathf.Abs(Vector3.Dot(splitVerts[i], divisionAxis) - planeOffset);
//				if (dist < 1e-4f)
//				{
//					planeVertices.Add(i);
//				}
//			}

//			// Assign triangles to top or bottom, deduplicating vertices
//			Dictionary<Vector3, int> topVertexMap = new Dictionary<Vector3, int>(new Vector3EqualityComparer(1e-6f));
//			Dictionary<Vector3, int> bottomVertexMap = new Dictionary<Vector3, int>(new Vector3EqualityComparer(1e-6f));

//			for (int i = 0; i < splitTris.Length; i += 3)
//			{
//				// Use centroid to determine top/bottom
//				Vector3 v0 = splitVerts[splitTris[i]];
//				Vector3 v1 = splitVerts[splitTris[i + 1]];
//				Vector3 v2 = splitVerts[splitTris[i + 2]];
//				Vector3 centroid = (v0 + v1 + v2) / 3f;
//				float dCentroid = Vector3.Dot(centroid, divisionAxis) - planeOffset;
//				bool isTop = dCentroid >= -1e-4f; // Relaxed threshold

//				List<Vector3> targetVerts = isTop ? topVerts : bottomVerts;
//				List<Vector3> targetNorms = isTop ? topNorms : bottomNorms;
//				List<Vector2> targetUVs = isTop ? topUVs : bottomUVs;
//				List<int> targetTris = isTop ? topTris : bottomTris;
//				Dictionary<Vector3, int> targetVertexMap = isTop ? topVertexMap : bottomVertexMap;

//				int[] newIndices = new int[3];
//				bool onPlane = false;
//				for (int j = 0; j < 3; j++)
//				{
//					int idx = splitTris[i + j];
//					Vector3 pos = splitVerts[idx];
//					if (!targetVertexMap.TryGetValue(pos, out int newIdx))
//					{
//						newIdx = targetVerts.Count;
//						targetVerts.Add(pos);
//						targetNorms.Add(splitNorms[idx]);
//						targetUVs.Add(splitUVs[idx]);
//						targetVertexMap[pos] = newIdx;
//					}
//					newIndices[j] = newIdx;
//					if (planeVertices.Contains(idx))
//					{
//						onPlane = true;
//					}
//				}

//				// Skip degenerate triangles
//				if (newIndices[0] != newIndices[1] && newIndices[1] != newIndices[2] && newIndices[0] != newIndices[2])
//				{
//					targetTris.Add(newIndices[0]);
//					targetTris.Add(newIndices[1]);
//					targetTris.Add(newIndices[2]);
//				}
//				else if (Debug.isDebugBuild)
//				{
//					Debug.LogWarning($"Skipping degenerate triangle at depth {depth}: [{newIndices[0]},{newIndices[1]},{newIndices[2]}]");
//				}

//				if (Debug.isDebugBuild && onPlane)
//				{
//					Debug.Log($"Triangle {i / 3} {(isTop ? "top" : "bottom")}: vertices [{splitTris[i]},{splitTris[i + 1]},{splitTris[i + 2]}], mapped to [{newIndices[0]},{newIndices[1]},{newIndices[2]}], on plane: {onPlane}, centroid dist: {dCentroid:F6}");
//				}
//			}

//			// Build top and bottom meshes
//			Mesh topMesh = new Mesh();
//			topMesh.SetVertices(topVerts);
//			topMesh.SetNormals(topNorms);
//			topMesh.SetUVs(0, topUVs);
//			topMesh.SetTriangles(topTris, 0);
//			topMesh.RecalculateBounds();

//			Mesh bottomMesh = new Mesh();
//			bottomMesh.SetVertices(bottomVerts);
//			bottomMesh.SetNormals(bottomNorms);
//			bottomMesh.SetUVs(0, bottomUVs);
//			bottomMesh.SetTriangles(bottomTris, 0);
//			bottomMesh.RecalculateBounds();

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Split at depth {depth}, planeOffset {planeOffset:F3}: Top mesh {topMesh.vertexCount} verts, {topMesh.triangles.Length / 3} tris; Bottom mesh {bottomMesh.vertexCount} verts, {bottomMesh.triangles.Length / 3} tris; Plane vertices: {planeVertices.Count}");
//			}

//			// Recurse on both sides
//			if (topMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(topMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//			}
//			if (bottomMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(bottomMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//			}

//			// Clean up temporary meshes
//			Object.Destroy(tempMesh);
//			Object.Destroy(topMesh);
//			Object.Destroy(bottomMesh);
//		}

//		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
//		{
//			var uniqueVerts = new List<VertexData>();
//			var vertexMap = new Dictionary<VertexData, int>(new VertexEqualityComparer(1e-6f, 1e-6f));
//			var newTris = new List<int>();

//			int[] indexMap = new int[verts.Count];
//			for (int i = 0; i < verts.Count; i++)
//			{
//				if (!vertexMap.TryGetValue(verts[i], out int idx))
//				{
//					idx = uniqueVerts.Count;
//					uniqueVerts.Add(verts[i]);
//					vertexMap[verts[i]] = idx;
//				}
//				indexMap[i] = idx;
//			}

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

//		private class VertexEqualityComparer : IEqualityComparer<VertexData>
//		{
//			private readonly float _posTol;
//			private readonly float _normTol;

//			public VertexEqualityComparer(float posTolerance = 1e-6f, float normTolerance = 1e-6f)
//			{
//				_posTol = posTolerance;
//				_normTol = normTolerance;
//			}

//			public bool Equals(VertexData a, VertexData b)
//			{
//				return Vector3.SqrMagnitude(a.pos - b.pos) < _posTol * _posTol &&
//					   Vector3.SqrMagnitude(a.normal - b.normal) < _normTol * _normTol;
//			}

//			public int GetHashCode(VertexData v)
//			{
//				int hx = (int)(Mathf.Round(v.pos.x / _posTol) * 73856093);
//				int hy = (int)(Mathf.Round(v.pos.y / _posTol) * 19349663);
//				int hz = (int)(Mathf.Round(v.pos.z / _posTol) * 83492791);

//				int nx = (int)(Mathf.Round(v.normal.x / _normTol) * 73856093);
//				int ny = (int)(Mathf.Round(v.normal.y / _normTol) * 19349663);
//				int nz = (int)(Mathf.Round(v.normal.z / _normTol) * 83492791);

//				return hx ^ hy ^ hz ^ nx ^ ny ^ nz;
//			}
//		}

//		private class Vector3EqualityComparer : IEqualityComparer<Vector3>
//		{
//			private readonly float _tolerance;
//			public Vector3EqualityComparer(float tolerance) { _tolerance = tolerance; }
//			public bool Equals(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < _tolerance * _tolerance;
//			public int GetHashCode(Vector3 obj) => ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
//												  ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
//												  ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
//		}
//	}
//}

//using System.Collections.Generic;
//using System.Linq;
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

//		public static Mesh SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
//		{
//			divisionAxis = divisionAxis.normalized;

//			// Compute bounds along axis
//			float minProj = float.MaxValue;
//			float maxProj = float.MinValue;
//			foreach (Vector3 v in inputMesh.vertices)
//			{
//				float proj = Vector3.Dot(v - meshCenter, divisionAxis);
//				minProj = Mathf.Min(minProj, proj);
//				maxProj = Mathf.Max(maxProj, proj);
//			}
//			float length = maxProj - minProj;
//			int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"SubdivideMeshLongEdges: Length={length:F3}, maxSegmentLength={maxSegmentLength:F3}, maxDepth={maxDepth}, meshCenter={meshCenter}");
//			}

//			// Accumulate all vertices and triangles
//			List<VertexData> allVerts = new List<VertexData>();
//			List<int> allTris = new List<int>();

//			// Start BSP recursion
//			SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, allVerts, allTris);

//			if (allVerts.Count == 0 || allTris.Count == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.LogWarning("SubdivideMeshLongEdges: No valid geometry after subdivision. Returning empty mesh.");
//				}
//				return new Mesh();
//			}

//			// Deduplicate vertices
//			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

//			// Build and return the unified mesh
//			Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
//			}

//			return resultMesh;
//		}

//		private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<VertexData> allVerts, List<int> allTris)
//		{
//			float length = maxProj - minProj;
//			if (length <= maxSegmentLength || depth >= maxDepth)
//			{
//				if (mesh.vertexCount > 0)
//				{
//					// Add this mesh's geometry to the shared lists
//					var verts = mesh.vertices;
//					var tris = mesh.triangles;
//					var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//					var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//					int baseIndex = allVerts.Count;
//					for (int i = 0; i < verts.Length; i++)
//					{
//						Vector3 p = verts[i];
//						Vector3 n = norms != null ? norms[i] : Vector3.up;
//						Vector2 uv = uvs != null ? uvs[i] : Vector2.zero;
//						allVerts.Add(new VertexData(p, n, uv));
//					}
//					allTris.AddRange(tris.Select(t => t + baseIndex));

//					if (Debug.isDebugBuild)
//					{
//						Debug.Log($"Leaf at depth {depth}: {verts.Length} vertices, {tris.Length / 3} triangles added to unified mesh");
//					}
//				}
//				return;
//			}

//			// Split at midpoint, respecting meshCenter's offset
//			float midProj = (minProj + maxProj) / 2f;
//			float offset = Vector3.Dot(meshCenter, divisionAxis) + midProj;

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Splitting at depth {depth}, offset={offset:F3}, midProj={midProj:F3}");
//			}

//			// Use MeshUtilTest.SplitMeshAlongPlane (assumed working)
//			Mesh tempMesh = MeshUtilTest.SplitMeshAlongPlane(mesh, divisionAxis, offset);

//			if (tempMesh.vertexCount == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.Log($"Split at depth {depth}, offset {offset:F3}: No vertices in split mesh. Skipping.");
//				}
//				Object.Destroy(tempMesh);
//				return;
//			}

//			// Create separate meshes for top and bottom
//			List<Vector3> topVerts = new List<Vector3>();
//			List<Vector3> topNorms = new List<Vector3>();
//			List<Vector2> topUVs = new List<Vector2>();
//			List<int> topTris = new List<int>();
//			List<Vector3> bottomVerts = new List<Vector3>();
//			List<Vector3> bottomNorms = new List<Vector3>();
//			List<Vector2> bottomUVs = new List<Vector2>();
//			List<int> bottomTris = new List<int>();

//			var splitVerts = tempMesh.vertices;
//			var splitNorms = tempMesh.normals;
//			var splitUVs = tempMesh.uv;
//			var splitTris = tempMesh.triangles;

//			// Track vertices on the plane to detect potential orphaned geometry
//			HashSet<int> planeVertices = new HashSet<int>();
//			for (int i = 0; i < splitVerts.Length; i++)
//			{
//				float dist = Mathf.Abs(Vector3.Dot(splitVerts[i], divisionAxis) - offset);
//				if (dist < 1e-4f)
//				{
//					planeVertices.Add(i);
//				}
//			}

//			for (int i = 0; i < splitTris.Length; i += 3)
//			{
//				Vector3 v0 = splitVerts[splitTris[i]];
//				float d0 = Vector3.Dot(v0, divisionAxis) - offset;
//				bool isTop = d0 >= -1e-4f; // Slightly relaxed threshold to include plane vertices

//				List<Vector3> targetVerts = isTop ? topVerts : bottomVerts;
//				List<Vector3> targetNorms = isTop ? topNorms : bottomNorms;
//				List<Vector2> targetUVs = isTop ? topUVs : bottomUVs;
//				List<int> targetTris = isTop ? topTris : bottomTris;

//				int baseIdx = targetVerts.Count;
//				bool onPlane = false;
//				for (int j = 0; j < 3; j++)
//				{
//					int idx = splitTris[i + j];
//					targetVerts.Add(splitVerts[idx]);
//					targetNorms.Add(splitNorms[idx]);
//					targetUVs.Add(splitUVs[idx]);
//					targetTris.Add(baseIdx + j);
//					if (planeVertices.Contains(idx))
//					{
//						onPlane = true;
//					}
//				}

//				if (Debug.isDebugBuild && onPlane)
//				{
//					Debug.Log($"Triangle {i / 3} {(isTop ? "top" : "bottom")}: vertices [{splitTris[i]},{splitTris[i + 1]},{splitTris[i + 2]}], on plane: {onPlane}");
//				}
//			}

//			// Build top and bottom meshes
//			Mesh topMesh = new Mesh();
//			topMesh.SetVertices(topVerts);
//			topMesh.SetNormals(topNorms);
//			topMesh.SetUVs(0, topUVs);
//			topMesh.SetTriangles(topTris, 0);
//			topMesh.RecalculateBounds();

//			Mesh bottomMesh = new Mesh();
//			bottomMesh.SetVertices(bottomVerts);
//			bottomMesh.SetNormals(bottomNorms);
//			bottomMesh.SetUVs(0, bottomUVs);
//			bottomMesh.SetTriangles(bottomTris, 0);
//			bottomMesh.RecalculateBounds();

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Split at depth {depth}, offset {offset:F3}: Top mesh {topMesh.vertexCount} verts, {topMesh.triangles.Length / 3} tris; Bottom mesh {bottomMesh.vertexCount} verts, {bottomMesh.triangles.Length / 3} tris");
//			}

//			// Recurse on both sides
//			if (topMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(topMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//			}
//			if (bottomMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(bottomMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//			}

//			// Clean up temporary meshes
//			Object.Destroy(tempMesh);
//			Object.Destroy(topMesh);
//			Object.Destroy(bottomMesh);
//		}

//		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
//		{
//			var uniqueVerts = new List<VertexData>();
//			var vertexMap = new Dictionary<VertexData, int>(new VertexEqualityComparer(1e-6f, 1e-6f));
//			var newTris = new List<int>();

//			int[] indexMap = new int[verts.Count];
//			for (int i = 0; i < verts.Count; i++)
//			{
//				if (!vertexMap.TryGetValue(verts[i], out int idx))
//				{
//					idx = uniqueVerts.Count;
//					uniqueVerts.Add(verts[i]);
//					vertexMap[verts[i]] = idx;
//				}
//				indexMap[i] = idx;
//			}

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

//		private class VertexEqualityComparer : IEqualityComparer<VertexData>
//		{
//			private readonly float _posTol;
//			private readonly float _normTol;

//			public VertexEqualityComparer(float posTolerance = 1e-6f, float normTolerance = 1e-6f)
//			{
//				_posTol = posTolerance;
//				_normTol = normTolerance;
//			}

//			public bool Equals(VertexData a, VertexData b)
//			{
//				return Vector3.SqrMagnitude(a.pos - b.pos) < _posTol * _posTol &&
//					   Vector3.SqrMagnitude(a.normal - b.normal) < _normTol * _normTol;
//			}

//			public int GetHashCode(VertexData v)
//			{
//				int hx = (int)(Mathf.Round(v.pos.x / _posTol) * 73856093);
//				int hy = (int)(Mathf.Round(v.pos.y / _posTol) * 19349663);
//				int hz = (int)(Mathf.Round(v.pos.z / _posTol) * 83492791);

//				int nx = (int)(Mathf.Round(v.normal.x / _normTol) * 73856093);
//				int ny = (int)(Mathf.Round(v.normal.y / _normTol) * 19349663);
//				int nz = (int)(Mathf.Round(v.normal.z / _normTol) * 83492791);

//				return hx ^ hy ^ hz ^ nx ^ ny ^ nz;
//			}
//		}
//	}
//}

//using System.Collections.Generic;
//using System.Linq;
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

//		public static Mesh SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
//		{
//			divisionAxis = divisionAxis.normalized;

//			// Compute bounds along axis
//			float minProj = float.MaxValue;
//			float maxProj = float.MinValue;
//			foreach (Vector3 v in inputMesh.vertices)
//			{
//				float proj = Vector3.Dot(v - meshCenter, divisionAxis);
//				minProj = Mathf.Min(minProj, proj);
//				maxProj = Mathf.Max(maxProj, proj);
//			}
//			float length = maxProj - minProj;
//			int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"SubdivideMeshLongEdges: Length = {length:F3}, maxSegmentLength = {maxSegmentLength:F3}, maxDepth = {maxDepth}");
//			}

//			// Accumulate all vertices and triangles
//			List<VertexData> allVerts = new List<VertexData>();
//			List<int> allTris = new List<int>();

//			// Start BSP recursion
//			SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, allVerts, allTris);

//			if (allVerts.Count == 0 || allTris.Count == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.LogWarning("SubdivideMeshLongEdges: No valid geometry after subdivision. Returning empty mesh.");
//				}
//				return new Mesh();
//			}

//			// Deduplicate vertices using the same robust method as MeshUtilTest
//			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

//			// Build and return the unified mesh
//			Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
//			}

//			return resultMesh;
//		}

//		private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<VertexData> allVerts, List<int> allTris)
//		{
//			float length = maxProj - minProj;
//			if (length <= maxSegmentLength || depth >= maxDepth)
//			{
//				if (mesh.vertexCount > 0)
//				{
//					// Add this mesh's geometry to the shared lists
//					var verts = mesh.vertices;
//					var tris = mesh.triangles;
//					var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//					var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//					int baseIndex = allVerts.Count;
//					for (int i = 0; i < verts.Length; i++)
//					{
//						Vector3 p = verts[i];
//						Vector3 n = norms != null ? norms[i] : Vector3.up;
//						Vector2 uv = uvs != null ? uvs[i] : Vector2.zero;
//						allVerts.Add(new VertexData(p, n, uv));
//					}
//					allTris.AddRange(tris.Select(t => t + baseIndex));

//					if (Debug.isDebugBuild)
//					{
//						Debug.Log($"Leaf at depth {depth}: {verts.Length} vertices, {tris.Length / 3} triangles added to unified mesh");
//					}
//				}
//				return;
//			}

//			// Split at midpoint
//			float midProj = (minProj + maxProj) / 2f;
//			float offset = Vector3.Dot(meshCenter + divisionAxis * midProj, divisionAxis);

//			// Use SplitMeshAlongPlane to get a single mesh
//			Mesh tempMesh = MeshUtilTest.SplitMeshAlongPlane(mesh, divisionAxis, offset);

//			if (tempMesh.vertexCount == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.Log($"Split at depth {depth}, offset {offset:F3}: No vertices in split mesh. Skipping.");
//				}
//				Object.Destroy(tempMesh);
//				return;
//			}

//			// Create separate meshes for top and bottom
//			List<Vector3> topVerts = new List<Vector3>();
//			List<Vector3> topNorms = new List<Vector3>();
//			List<Vector2> topUVs = new List<Vector2>();
//			List<int> topTris = new List<int>();
//			List<Vector3> bottomVerts = new List<Vector3>();
//			List<Vector3> bottomNorms = new List<Vector3>();
//			List<Vector2> bottomUVs = new List<Vector2>();
//			List<int> bottomTris = new List<int>();

//			// Use distinct names to avoid conflicts
//			var splitVerts = tempMesh.vertices;
//			var splitNorms = tempMesh.normals;
//			var splitUVs = tempMesh.uv;
//			var splitTris = tempMesh.triangles;

//			for (int i = 0; i < splitTris.Length; i += 3)
//			{
//				Vector3 v0 = splitVerts[splitTris[i]];
//				float d0 = Vector3.Dot(v0, divisionAxis) - offset;
//				bool isTop = d0 >= 0;

//				List<Vector3> targetVerts = isTop ? topVerts : bottomVerts;
//				List<Vector3> targetNorms = isTop ? topNorms : bottomNorms;
//				List<Vector2> targetUVs = isTop ? topUVs : bottomUVs;
//				List<int> targetTris = isTop ? topTris : bottomTris;

//				int baseIdx = targetVerts.Count;
//				for (int j = 0; j < 3; j++)
//				{
//					int idx = splitTris[i + j];
//					targetVerts.Add(splitVerts[idx]);
//					targetNorms.Add(splitNorms[idx]);
//					targetUVs.Add(splitUVs[idx]);
//					targetTris.Add(baseIdx + j);
//				}
//			}

//			// Build top and bottom meshes
//			Mesh topMesh = new Mesh();
//			topMesh.SetVertices(topVerts);
//			topMesh.SetNormals(topNorms);
//			topMesh.SetUVs(0, topUVs);
//			topMesh.SetTriangles(topTris, 0);
//			topMesh.RecalculateBounds();

//			Mesh bottomMesh = new Mesh();
//			bottomMesh.SetVertices(bottomVerts);
//			bottomMesh.SetNormals(bottomNorms);
//			bottomMesh.SetUVs(0, bottomUVs);
//			bottomMesh.SetTriangles(bottomTris, 0);
//			bottomMesh.RecalculateBounds();

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Split at depth {depth}, offset {offset:F3}: Top mesh {topMesh.vertexCount} verts, {topMesh.triangles.Length / 3} tris; Bottom mesh {bottomMesh.vertexCount} verts, {bottomMesh.triangles.Length / 3} tris");
//			}

//			// Recurse on both sides
//			if (topMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(topMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//			}
//			if (bottomMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(bottomMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//			}

//			// Clean up temporary meshes
//			Object.Destroy(tempMesh);
//			Object.Destroy(topMesh);
//			Object.Destroy(bottomMesh);
//		}

//		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
//		{
//			var uniqueVerts = new List<VertexData>();
//			var vertexMap = new Dictionary<VertexData, int>(new VertexEqualityComparer(1e-6f, 1e-6f));
//			var newTris = new List<int>();

//			int[] indexMap = new int[verts.Count];
//			for (int i = 0; i < verts.Count; i++)
//			{
//				if (!vertexMap.TryGetValue(verts[i], out int idx))
//				{
//					idx = uniqueVerts.Count;
//					uniqueVerts.Add(verts[i]);
//					vertexMap[verts[i]] = idx;
//				}
//				indexMap[i] = idx;
//			}

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

//		private class VertexEqualityComparer : IEqualityComparer<VertexData>
//		{
//			private readonly float _posTol;
//			private readonly float _normTol;

//			public VertexEqualityComparer(float posTolerance = 1e-6f, float normTolerance = 1e-6f)
//			{
//				_posTol = posTolerance;
//				_normTol = normTolerance;
//			}

//			public bool Equals(VertexData a, VertexData b)
//			{
//				return Vector3.SqrMagnitude(a.pos - b.pos) < _posTol * _posTol &&
//					   Vector3.SqrMagnitude(a.normal - b.normal) < _normTol * _normTol;
//			}

//			public int GetHashCode(VertexData v)
//			{
//				int hx = (int)(Mathf.Round(v.pos.x / _posTol) * 73856093);
//				int hy = (int)(Mathf.Round(v.pos.y / _posTol) * 19349663);
//				int hz = (int)(Mathf.Round(v.pos.z / _posTol) * 83492791);

//				int nx = (int)(Mathf.Round(v.normal.x / _normTol) * 73856093);
//				int ny = (int)(Mathf.Round(v.normal.y / _normTol) * 19349663);
//				int nz = (int)(Mathf.Round(v.normal.z / _normTol) * 83492791);

//				return hx ^ hy ^ hz ^ nx ^ ny ^ nz;
//			}
//		}
//	}
//}

//using System.Collections.Generic;
//using System.Linq;
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

//		public static Mesh SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
//		{
//			divisionAxis = divisionAxis.normalized;

//			// Compute bounds along axis
//			float minProj = float.MaxValue;
//			float maxProj = float.MinValue;
//			foreach (Vector3 v in inputMesh.vertices)
//			{
//				float proj = Vector3.Dot(v - meshCenter, divisionAxis);
//				minProj = Mathf.Min(minProj, proj);
//				maxProj = Mathf.Max(maxProj, proj);
//			}
//			float length = maxProj - minProj;
//			int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"SubdivideMeshLongEdges: Length = {length:F3}, maxSegmentLength = {maxSegmentLength:F3}, maxDepth = {maxDepth}");
//			}

//			// Accumulate all vertices and triangles
//			List<VertexData> allVerts = new List<VertexData>();
//			List<int> allTris = new List<int>();

//			// Start BSP recursion
//			SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, allVerts, allTris);

//			if (allVerts.Count == 0 || allTris.Count == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.LogWarning("SubdivideMeshLongEdges: No valid geometry after subdivision. Returning empty mesh.");
//				}
//				return new Mesh();
//			}

//			// Deduplicate vertices across all subdivisions
//			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

//			// Build and return the unified mesh
//			Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
//			}

//			return resultMesh;
//		}

//		private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<VertexData> allVerts, List<int> allTris)
//		{
//			float length = maxProj - minProj;
//			if (length <= maxSegmentLength || depth >= maxDepth)
//			{
//				if (mesh.vertexCount > 0)
//				{
//					// Add this mesh's geometry to the shared lists
//					var verts = mesh.vertices;
//					var tris = mesh.triangles;
//					var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//					var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//					int baseIndex = allVerts.Count;
//					for (int i = 0; i < verts.Length; i++)
//					{
//						Vector3 p = verts[i];
//						Vector3 n = norms != null ? norms[i] : Vector3.up;
//						Vector2 uv = uvs != null ? uvs[i] : Vector2.zero;
//						allVerts.Add(new VertexData(p, n, uv));
//					}
//					allTris.AddRange(tris.Select(t => t + baseIndex));

//					if (Debug.isDebugBuild)
//					{
//						Debug.Log($"Leaf at depth {depth}: {verts.Length} vertices, {tris.Length / 3} triangles added to unified mesh");
//					}
//				}
//				return;
//			}

//			// Split at midpoint
//			float midProj = (minProj + maxProj) / 2f;
//			float offset = Vector3.Dot(meshCenter + divisionAxis * midProj, divisionAxis);

//			// Use SplitMeshAlongPlane to get a single mesh (top and bottom combined)
//			Mesh splitMesh = MeshUtilTest.SplitMeshAlongPlane(mesh, divisionAxis, offset);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Split at depth {depth}, offset {offset:F3}: split mesh {splitMesh.vertexCount} vertices, {splitMesh.triangles.Length / 3} triangles");
//			}

//			// Recurse on the split mesh
//			if (splitMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(splitMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//				SubdivideMeshBSP(splitMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//				Object.Destroy(splitMesh); // Clean up temporary mesh
//			}
//		}

//		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
//		{
//			var uniqueVerts = new List<VertexData>();
//			var vertexMap = new Dictionary<Vector3, int>(new VertexPositionEqualityComparer(1e-6f));
//			var newTris = new List<int>();

//			int[] indexMap = new int[verts.Count];
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
//				return ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
//					   ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
//					   ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
//			}
//		}
//	}
//}



//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//namespace ClassicTilestorm
//{
//    public static class MeshUtils
//    {
//        private struct VertexData
//        {
//            public Vector3 pos;
//            public Vector3 normal;
//            public Vector2 uv;

//            public VertexData(Vector3 p, Vector3 n, Vector2 u)
//            {
//                pos = p;
//                normal = n;
//                uv = u;
//            }
//        }

//        //public static Mesh SplitMeshAlongPlane(Mesh mesh, Vector3 planeNormal, float offset)
//        //{
//        //    planeNormal.Normalize();

//        //    var verts = mesh.vertices;
//        //    var tris = mesh.triangles;
//        //    var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//        //    var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//        //    List<VertexData> allVerts = new List<VertexData>();
//        //    List<int> allTris = new List<int>();
//        //    List<VertexData> tri = new List<VertexData>(3);

//        //    for (int i = 0; i < tris.Length; i += 3)
//        //    {
//        //        tri.Clear();
//        //        for (int j = 0; j < 3; j++)
//        //        {
//        //            int idx = tris[i + j];
//        //            Vector3 p = verts[idx];
//        //            Vector3 n = norms != null ? norms[idx] : Vector3.up;
//        //            Vector2 uv = uvs != null ? uvs[idx] : Vector2.zero;
//        //            tri.Add(new VertexData(p, n, uv));
//        //        }

//        //        ClipTriangle(tri, planeNormal, offset, allVerts, allTris);
//        //    }

//        //    var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);
//        //    return BuildMesh(uniqueVerts, uniqueTris);
//        //}

//        public static Mesh SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
//        {
//            divisionAxis = divisionAxis.normalized;

//            // Compute bounds along axis
//            float minProj = float.MaxValue;
//            float maxProj = float.MinValue;
//            foreach (Vector3 v in inputMesh.vertices)
//            {
//                float proj = Vector3.Dot(v - meshCenter, divisionAxis);
//                minProj = Mathf.Min(minProj, proj);
//                maxProj = Mathf.Max(maxProj, proj);
//            }
//            float length = maxProj - minProj;
//            int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));

//            if (Debug.isDebugBuild)
//            {
//                Debug.Log($"SubdivideMeshLongEdges: Length = {length:F3}, maxSegmentLength = {maxSegmentLength:F3}, maxDepth = {maxDepth}");
//            }

//            // Accumulate all vertices and triangles
//            List<VertexData> allVerts = new List<VertexData>();
//            List<int> allTris = new List<int>();

//            // Start BSP recursion
//            SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, allVerts, allTris);

//            if (allVerts.Count == 0 || allTris.Count == 0)
//            {
//                if (Debug.isDebugBuild)
//                {
//                    Debug.LogWarning("SubdivideMeshLongEdges: No valid geometry after subdivision. Returning empty mesh.");
//                }
//                return new Mesh();
//            }

//            // Deduplicate vertices across all subdivisions
//            var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

//            // Build and return the unified mesh
//            Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

//            if (Debug.isDebugBuild)
//            {
//                Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
//            }

//            return resultMesh;
//        }

//        private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<VertexData> allVerts, List<int> allTris)
//        {
//            float length = maxProj - minProj;
//            if (length <= maxSegmentLength || depth >= maxDepth)
//            {
//                if (mesh.vertexCount > 0)
//                {
//                    // Add this mesh's geometry to the shared lists
//                    var verts = mesh.vertices;
//                    var tris = mesh.triangles;
//                    var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//                    var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//                    int baseIndex = allVerts.Count;
//                    for (int i = 0; i < verts.Length; i++)
//                    {
//                        Vector3 p = verts[i];
//                        Vector3 n = norms != null ? norms[i] : Vector3.up;
//                        Vector2 uv = uvs != null ? uvs[i] : Vector2.zero;
//                        allVerts.Add(new VertexData(p, n, uv));
//                    }
//                    allTris.AddRange(tris.Select(t => t + baseIndex));

//                    if (Debug.isDebugBuild)
//                    {
//                        Debug.Log($"Leaf at depth {depth}: {verts.Length} vertices, {tris.Length / 3} triangles added to unified mesh");
//                    }
//                }
//                return;
//            }

//            // Split at midpoint
//            float midProj = (minProj + maxProj) / 2f;
//            float offset = Vector3.Dot(meshCenter + divisionAxis * midProj, divisionAxis);

//            // Use SplitMeshAlongPlane to get a single mesh (top and bottom combined)
//            Mesh splitMesh = MeshUtilTest.SplitMeshAlongPlane(mesh, divisionAxis, offset);

//            if (Debug.isDebugBuild)
//            {
//                Debug.Log($"Split at depth {depth}, offset {offset:F3}: split mesh {splitMesh.vertexCount} vertices, {splitMesh.triangles.Length / 3} triangles");
//            }

//            // Recurse on the split mesh
//            if (splitMesh.vertexCount > 0)
//            {
//                SubdivideMeshBSP(splitMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//                SubdivideMeshBSP(splitMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//                Object.Destroy(splitMesh); // Clean up temporary mesh
//            }
//        }

//        private static void ClipTriangle(List<VertexData> tri,
//            Vector3 planeNormal, float offset,
//            List<VertexData> allVerts, List<int> allTris)
//        {
//            List<VertexData> topPoly, bottomPoly;
//            ClipPolygonAgainstPlane(tri, planeNormal, offset, out topPoly, out bottomPoly);
//            AddPolygon(topPoly, allVerts, allTris);
//            AddPolygon(bottomPoly, allVerts, allTris);
//        }

//        private static void ClipPolygonAgainstPlane(
//            List<VertexData> poly,
//            Vector3 planeNormal, float offset,
//            out List<VertexData> topPoly, out List<VertexData> bottomPoly)
//        {
//            topPoly = new List<VertexData>();
//            bottomPoly = new List<VertexData>();

//            int count = poly.Count;
//            for (int i = 0; i < count; i++)
//            {
//                VertexData curr = poly[i];
//                VertexData next = poly[(i + 1) % count];

//                float d1 = Vector3.Dot(curr.pos, planeNormal) - offset;
//                float d2 = Vector3.Dot(next.pos, planeNormal) - offset;

//                bool currAbove = d1 >= 0;
//                bool nextAbove = d2 >= 0;

//                if (currAbove) topPoly.Add(curr);
//                else bottomPoly.Add(curr);

//                if (currAbove != nextAbove)
//                {
//                    float t = d1 / (d1 - d2);
//                    VertexData interp = Lerp(curr, next, t);
//                    topPoly.Add(interp);
//                    bottomPoly.Add(interp);
//                }
//            }

//            // Remove coplanar quads in top and bottom poly
//            topPoly = MeshSimplify(topPoly);
//            bottomPoly = MeshSimplify(bottomPoly);
//        }

//        private static List<VertexData> MeshSimplify(List<VertexData> poly)
//        {
//            if (poly.Count < 3) return poly;

//            // Triangulate the polygon to identify triangles
//            List<int> tris = new List<int>();
//            int baseIndex = 0; // Simulate vertex list starting at 0
//            for (int i = 1; i < poly.Count - 1; i++)
//            {
//                tris.Add(baseIndex);
//                tris.Add(baseIndex + i);
//                tris.Add(baseIndex + i + 1);
//            }

//            // Find coplanar triangle pairs and merge them
//            List<int> newTris = new List<int>();
//            HashSet<int> processedTris = new HashSet<int>(); // Track processed triangle indices
//            List<VertexData> mergedPoly = new List<VertexData>(poly); // Start with original vertices

//            for (int i = 0; i < tris.Count; i += 3)
//            {
//                if (processedTris.Contains(i)) continue;

//                // Get first triangle
//                int t0 = tris[i];
//                int t1 = tris[i + 1];
//                int t2 = tris[i + 2];
//                Vector3 v0 = poly[t0].pos;
//                Vector3 v1 = poly[t1].pos;
//                Vector3 v2 = poly[t2].pos;
//                Vector3 normal1 = Vector3.Cross(v1 - v0, v2 - v0).normalized;

//                // Look for a coplanar triangle sharing an edge
//                for (int j = 0; j < tris.Count; j += 3)
//                {
//                    if (i == j || processedTris.Contains(j)) continue;

//                    int t3 = tris[j];
//                    int t4 = tris[j + 1];
//                    int t5 = tris[j + 2];
//                    Vector3 v3 = poly[t3].pos;
//                    Vector3 v4 = poly[t4].pos;
//                    Vector3 v5 = poly[t5].pos;
//                    Vector3 normal2 = Vector3.Cross(v4 - v3, v5 - v3).normalized;

//                    // Check if triangles are coplanar (normals are parallel within tolerance)
//                    const float normalTolerance = 0.001f;
//                    if (Vector3.Dot(normal1, normal2) < 1f - normalTolerance) continue;

//                    // Check if triangles share an edge
//                    int[] tri1 = { t0, t1, t2 };
//                    int[] tri2 = { t3, t4, t5 };
//                    List<int> sharedVerts = new List<int>();
//                    foreach (int idx1 in tri1)
//                    {
//                        foreach (int idx2 in tri2)
//                        {
//                            if (Vector3.SqrMagnitude(poly[idx1].pos - poly[idx2].pos) < 1e-6f)
//                            {
//                                sharedVerts.Add(idx1);
//                            }
//                        }
//                    }

//                    // If exactly two vertices are shared, the triangles share an edge
//                    if (sharedVerts.Count == 2)
//                    {
//                        // Mark triangles as processed
//                        processedTris.Add(i);
//                        processedTris.Add(j);

//                        // Collect unique vertices to form a quad
//                        HashSet<int> quadVerts = new HashSet<int> { t0, t1, t2, t3, t4, t5 };
//                        List<VertexData> quad = quadVerts.Select(idx => poly[idx]).ToList();

//                        // Project vertices to 2D plane for convex polygon triangulation
//                        List<Vector2> projectedVerts = ProjectTo2D(quad, normal1);
//                        List<int> quadTris = TriangulateConvexPolygon(projectedVerts);

//                        // Add new triangles with adjusted indices
//                        int newBaseIndex = mergedPoly.Count;
//                        mergedPoly.AddRange(quad);
//                        foreach (int triIdx in quadTris)
//                        {
//                            newTris.Add(newBaseIndex + triIdx);
//                        }

//                        // Debug log
//                        if (Debug.isDebugBuild)
//                        {
//                            Debug.Log($"Merged coplanar triangles into quad: {quad.Count} vertices, {quadTris.Count / 3} triangles");
//                        }
//                    }
//                }

//                // If triangle wasn't merged, keep it
//                if (!processedTris.Contains(i))
//                {
//                    newTris.Add(t0);
//                    newTris.Add(t1);
//                    newTris.Add(t2);
//                }
//            }

//            // If no simplification occurred, return original polygon
//            if (newTris.Count == tris.Count)
//            {
//                return poly;
//            }

//            // Deduplicate vertices in the merged polygon
//            var (uniqueVerts, uniqueTris) = DeduplicateVertices(mergedPoly, newTris);
//            return uniqueVerts;
//        }

//        private static List<Vector2> ProjectTo2D(List<VertexData> vertices, Vector3 planeNormal)
//        {
//            List<Vector2> projected = new List<Vector2>();
//            Vector3 u = Vector3.Cross(planeNormal, Mathf.Abs(Vector3.Dot(planeNormal, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up).normalized;
//            Vector3 v = Vector3.Cross(planeNormal, u).normalized;

//            foreach (var vertex in vertices)
//            {
//                float x = Vector3.Dot(vertex.pos, u);
//                float y = Vector3.Dot(vertex.pos, v);
//                projected.Add(new Vector2(x, y));
//            }

//            return projected;
//        }

//        private static List<int> TriangulateConvexPolygon(List<Vector2> vertices)
//        {
//            List<int> triangles = new List<int>();
//            for (int i = 1; i < vertices.Count - 1; i++)
//            {
//                triangles.Add(0);
//                triangles.Add(i);
//                triangles.Add(i + 1);
//            }
//            return triangles;
//        }

//        private static void AddPolygon(List<VertexData> poly,
//            List<VertexData> verts, List<int> tris)
//        {
//            if (poly.Count < 3) return;

//            int baseIndex = verts.Count;
//            verts.AddRange(poly);

//            for (int i = 1; i < poly.Count - 1; i++)
//            {
//                tris.Add(baseIndex);
//                tris.Add(baseIndex + i);
//                tris.Add(baseIndex + i + 1);
//            }
//        }

//        private static VertexData Lerp(VertexData a, VertexData b, float t)
//        {
//            return new VertexData(
//                Vector3.Lerp(a.pos, b.pos, t),
//                Vector3.Normalize(Vector3.Lerp(a.normal, b.normal, t)),
//                Vector2.Lerp(a.uv, b.uv, t)
//            );
//        }

//        private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int> tris)
//        {
//            var uniqueVerts = new List<VertexData>();
//            var vertexMap = new Dictionary<Vector3, int>(new VertexPositionEqualityComparer(1e-6f));
//            var newTris = new List<int>();

//            int[] indexMap = new int[verts.Count];
//            for (int i = 0; i < verts.Count; i++)
//            {
//                Vector3 pos = verts[i].pos;
//                if (!vertexMap.TryGetValue(pos, out int index))
//                {
//                    index = uniqueVerts.Count;
//                    uniqueVerts.Add(verts[i]);
//                    vertexMap[pos] = index;
//                }
//                indexMap[i] = index;
//            }

//            foreach (int oldIndex in tris)
//            {
//                if (oldIndex >= 0 && oldIndex < indexMap.Length)
//                {
//                    newTris.Add(indexMap[oldIndex]);
//                }
//                else
//                {
//                    Debug.LogWarning($"Invalid triangle index {oldIndex} encountered. Skipping.");
//                }
//            }

//            return (uniqueVerts, newTris);
//        }

//        private static Mesh BuildMesh(List<VertexData> verts, List<int> tris)
//        {
//            Mesh mesh = new Mesh();
//            var positions = new Vector3[verts.Count];
//            var normals = new Vector3[verts.Count];
//            var uvs = new Vector2[verts.Count];

//            for (int i = 0; i < verts.Count; i++)
//            {
//                positions[i] = verts[i].pos;
//                normals[i] = verts[i].normal;
//                uvs[i] = verts[i].uv;
//            }

//            mesh.SetVertices(positions);
//            mesh.SetNormals(normals);
//            mesh.SetUVs(0, uvs);
//            mesh.SetTriangles(tris, 0);

//            mesh.RecalculateBounds();
//            return mesh;
//        }

//        private class VertexPositionEqualityComparer : IEqualityComparer<Vector3>
//        {
//            private readonly float _tolerance;

//            public VertexPositionEqualityComparer(float tolerance)
//            {
//                _tolerance = tolerance;
//            }

//            public bool Equals(Vector3 a, Vector3 b)
//            {
//                return Vector3.SqrMagnitude(a - b) < _tolerance * _tolerance;
//            }

//            public int GetHashCode(Vector3 obj)
//            {
//                return ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
//                       ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
//                       ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
//            }
//        }
//    }
//}


//using System.Collections.Generic;
//using System.Linq;
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

//			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);
//			return BuildMesh(uniqueVerts, uniqueTris);
//		}

//		// Updated to return a single Mesh
//		public static Mesh SubdivideMeshLongEdges(Mesh inputMesh, Vector3 divisionAxis, float maxSegmentLength, Vector3 meshCenter)
//		{
//			divisionAxis = divisionAxis.normalized;

//			// Compute bounds along axis
//			float minProj = float.MaxValue;
//			float maxProj = float.MinValue;
//			foreach (Vector3 v in inputMesh.vertices)
//			{
//				float proj = Vector3.Dot(v - meshCenter, divisionAxis);
//				minProj = Mathf.Min(minProj, proj);
//				maxProj = Mathf.Max(maxProj, proj);
//			}
//			float length = maxProj - minProj;
//			int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"SubdivideMeshLongEdges: Length = {length:F3}, maxSegmentLength = {maxSegmentLength:F3}, maxDepth = {maxDepth}");
//			}

//			// Accumulate all vertices and triangles
//			List<VertexData> allVerts = new List<VertexData>();
//			List<int> allTris = new List<int>();

//			// Start BSP recursion
//			SubdivideMeshBSP(inputMesh, divisionAxis, minProj, maxProj, maxSegmentLength, meshCenter, 0, maxDepth, allVerts, allTris);

//			if (allVerts.Count == 0 || allTris.Count == 0)
//			{
//				if (Debug.isDebugBuild)
//				{
//					Debug.LogWarning("SubdivideMeshLongEdges: No valid geometry after subdivision. Returning empty mesh.");
//				}
//				return new Mesh();
//			}

//			// Deduplicate vertices across all subdivisions
//			var (uniqueVerts, uniqueTris) = DeduplicateVertices(allVerts, allTris);

//			// Build and return the unified mesh
//			Mesh resultMesh = BuildMesh(uniqueVerts, uniqueTris);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Final unified mesh: {resultMesh.vertexCount} vertices, {resultMesh.triangles.Length / 3} triangles");
//			}

//			return resultMesh;
//		}

//		// Updated to accumulate vertices and triangles
//		private static void SubdivideMeshBSP(Mesh mesh, Vector3 divisionAxis, float minProj, float maxProj, float maxSegmentLength, Vector3 meshCenter, int depth, int maxDepth, List<VertexData> allVerts, List<int> allTris)
//		{
//			float length = maxProj - minProj;
//			if (length <= maxSegmentLength || depth >= maxDepth)
//			{
//				if (mesh.vertexCount > 0)
//				{
//					// Add this mesh's geometry to the shared lists
//					var verts = mesh.vertices;
//					var tris = mesh.triangles;
//					var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
//					var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

//					int baseIndex = allVerts.Count;
//					for (int i = 0; i < verts.Length; i++)
//					{
//						Vector3 p = verts[i];
//						Vector3 n = norms != null ? norms[i] : Vector3.up;
//						Vector2 uv = uvs != null ? uvs[i] : Vector2.zero;
//						allVerts.Add(new VertexData(p, n, uv));
//					}
//					allTris.AddRange(tris.Select(t => t + baseIndex));

//					if (Debug.isDebugBuild)
//					{
//						Debug.Log($"Leaf at depth {depth}: {verts.Length} vertices, {tris.Length / 3} triangles added to unified mesh");
//					}
//				}
//				return;
//			}

//			// Split at midpoint
//			float midProj = (minProj + maxProj) / 2f;
//			float offset = Vector3.Dot(meshCenter + divisionAxis * midProj, divisionAxis);

//			// Use SplitMeshAlongPlane to get a single mesh (top and bottom combined)
//			Mesh splitMesh = SplitMeshAlongPlane(mesh, divisionAxis, offset);

//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Split at depth {depth}, offset {offset:F3}: split mesh {splitMesh.vertexCount} vertices, {splitMesh.triangles.Length / 3} triangles");
//			}

//			// Recurse on the split mesh
//			if (splitMesh.vertexCount > 0)
//			{
//				SubdivideMeshBSP(splitMesh, divisionAxis, midProj, maxProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//				SubdivideMeshBSP(splitMesh, divisionAxis, minProj, midProj, maxSegmentLength, meshCenter, depth + 1, maxDepth, allVerts, allTris);
//				Object.Destroy(splitMesh); // Clean up temporary mesh
//			}
//		}

//		private static void ClipTriangle(List<VertexData> tri,
//			Vector3 planeNormal, float offset,
//			List<VertexData> allVerts, List<int> allTris)
//		{
//			List<VertexData> topPoly, bottomPoly;
//			ClipPolygonAgainstPlane(tri, planeNormal, offset, out topPoly, out bottomPoly);
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
//			//remove coplanar quads in top and bottom poly
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

//			int[] indexMap = new int[verts.Count];
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
//				return ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
//					   ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
//					   ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
//			}
//		}
//	}
//}


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


