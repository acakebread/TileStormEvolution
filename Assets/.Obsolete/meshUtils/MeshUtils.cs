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
