using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public static class MeshUtilTest
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

		private struct Edge
		{
			public int a, b;

			public Edge(int a, int b)
			{
				this.a = Mathf.Min(a, b);
				this.b = Mathf.Max(a, b);
			}

			public override int GetHashCode() => a ^ b * 397;
			public override bool Equals(object obj) => obj is Edge e && e.a == a && e.b == b;
		}

		public static Mesh SplitMeshAlongPlane(Mesh mesh, Vector3 planeNormal, float offset)
		{
			planeNormal = planeNormal.normalized;

			var verts = mesh.vertices;
			var tris = mesh.triangles;
			var norms = mesh.normals.Length == verts.Length ? mesh.normals : null;
			var uvs = mesh.uv.Length == verts.Length ? mesh.uv : null;

			List<VertexData> allVerts = new List<VertexData>();
			List<int[]> triIndices = new List<int[]>();

			// Build initial VertexData and triangle indices
			for (int i = 0; i < tris.Length; i += 3)
			{
				int[] triIdx = new int[3];
				for (int j = 0; j < 3; j++)
				{
					int idx = tris[i + j];
					triIdx[j] = allVerts.Count;
					allVerts.Add(new VertexData(
						verts[idx],
						norms != null ? norms[idx] : Vector3.up,
						uvs != null ? uvs[idx] : Vector2.zero
					));
				}
				triIndices.Add(triIdx);
			}

			// Identify coplanar triangle pairs
			HashSet<int> processedTriangles = new HashSet<int>();
			List<int[]> newTris = new List<int[]>();
			Dictionary<Edge, int> edgeSplitVertex = new Dictionary<Edge, int>();

			int pairCount = 0;
			for (int i = 0; i < triIndices.Count; i++)
			{
				for (int j = i + 1; j < triIndices.Count; j++)
				{
					if (processedTriangles.Contains(i) || processedTriangles.Contains(j))
						continue;

					int[] tri1 = triIndices[i];
					int[] tri2 = triIndices[j];
					VertexData[] v1 = new VertexData[] { allVerts[tri1[0]], allVerts[tri1[1]], allVerts[tri1[2]] };
					VertexData[] v2 = new VertexData[] { allVerts[tri2[0]], allVerts[tri2[1]], allVerts[tri2[2]] };

					bool shareEdge = AreTrianglesSharingEdge(v1, v2);
					bool coplanar = AreTrianglesCoplanar(v1, v2);

					if (shareEdge && coplanar)
					{
						Debug.Log($"Coplanar pair {pairCount++}: Tri {i} ({tri1[0]},{tri1[1]},{tri1[2]}) and Tri {j} ({tri2[0]},{tri2[1]},{tri2[2]})");
						SplitCoplanarPair(tri1, tri2, v1, v2, planeNormal, offset, edgeSplitVertex, allVerts, newTris);
						processedTriangles.Add(i);
						processedTriangles.Add(j);
					}
					else
					{
						Debug.Log($"Pair {i}-{j} rejected: Shares edge={shareEdge}, Coplanar={coplanar}");
					}
				}
			}
			Debug.Log($"Detected {pairCount} coplanar pairs");

			// Process remaining triangles
			for (int i = 0; i < triIndices.Count; i++)
			{
				if (processedTriangles.Contains(i))
					continue;

				Debug.Log($"Processing single triangle {i}");
				int[] tri = triIndices[i];
				VertexData[] v = new VertexData[] { allVerts[tri[0]], allVerts[tri[1]], allVerts[tri[2]] };

				float d0 = Vector3.Dot(v[0].pos, planeNormal) - offset;
				float d1 = Vector3.Dot(v[1].pos, planeNormal) - offset;
				float d2 = Vector3.Dot(v[2].pos, planeNormal) - offset;

				bool above0 = d0 >= 0;
				bool above1 = d1 >= 0;
				bool above2 = d2 >= 0;

				int aboveCount = (above0 ? 1 : 0) + (above1 ? 1 : 0) + (above2 ? 1 : 0);

				if (aboveCount == 0 || aboveCount == 3)
				{
					newTris.Add(tri);
					Debug.Log($"Triangle {i} entirely on one side: aboveCount={aboveCount}");
					continue;
				}

				List<VertexData> top = new List<VertexData>();
				List<VertexData> bottom = new List<VertexData>();

				SplitTrianglePlane(v, tri, planeNormal, offset, edgeSplitVertex, allVerts, top, bottom);

				AddPolygonToTris(top, newTris, allVerts);
				AddPolygonToTris(bottom, newTris, allVerts);
			}

			var (uniqueVerts, finalTris) = DeduplicateVertices(allVerts, newTris);

			// Log final vertices
			for (int i = 0; i < uniqueVerts.Count; i++)
			{
				Debug.Log($"Final vertex {i}: pos={uniqueVerts[i].pos}, normal={uniqueVerts[i].normal}, uv={uniqueVerts[i].uv}");
			}

			Debug.Log($"Final mesh: {uniqueVerts.Count} vertices, {finalTris.Count / 3} triangles");

			return BuildMesh(uniqueVerts, finalTris);
		}

		private static bool AreTrianglesSharingEdge(VertexData[] tri1, VertexData[] tri2)
		{
			int sharedCount = 0;
			for (int a = 0; a < 3; a++)
			{
				for (int b = 0; b < 3; b++)
				{
					if (Vector3.SqrMagnitude(tri1[a].pos - tri2[b].pos) < 1e-6f)
					{
						sharedCount++;
						break;
					}
				}
			}
			return sharedCount == 2;
		}

		private static bool AreTrianglesCoplanar(VertexData[] tri1, VertexData[] tri2)
		{
			Vector3 n1 = Vector3.Cross(tri1[1].pos - tri1[0].pos, tri1[2].pos - tri1[0].pos).normalized;
			Vector3 n2 = Vector3.Cross(tri2[1].pos - tri2[0].pos, tri2[2].pos - tri2[0].pos).normalized;

			float dot = Mathf.Abs(Vector3.Dot(n1, n2));
			if (dot < 0.9999f)
			{
				Debug.Log($"Non-coplanar normals: n1={n1}, n2={n2}, abs(dot)={dot}");
				return false;
			}

			float d1 = Vector3.Dot(n1, tri1[0].pos);
			for (int i = 0; i < 3; i++)
			{
				float dist = Mathf.Abs(Vector3.Dot(n1, tri2[i].pos) - d1);
				if (dist > 1e-4f)
				{
					Debug.Log($"Non-coplanar plane: tri2[{i}] pos={tri2[i].pos}, dist={dist}");
					return false;
				}
			}

			Vector3 refNormal = tri1[0].normal;
			for (int i = 0; i < 3; i++)
			{
				float dot1 = Mathf.Abs(Vector3.Dot(tri1[i].normal, refNormal));
				float dot2 = Mathf.Abs(Vector3.Dot(tri2[i].normal, refNormal));
				if (dot1 < 0.9999f || dot2 < 0.9999f)
				{
					Debug.Log($"Non-matching normal at tri1[{i}]={tri1[i].normal} or tri2[{i}]={tri2[i].normal}, ref={refNormal}, dots={dot1},{dot2}");
					return false;
				}
			}

			return true;
		}

		private static void SplitTrianglePlane(VertexData[] v, int[] tri, Vector3 planeNormal, float offset,
			Dictionary<Edge, int> edgeSplitVertex, List<VertexData> allVerts, List<VertexData> top, List<VertexData> bottom)
		{
			Vector3 avgNormal = (v[0].normal + v[1].normal + v[2].normal).normalized;

			for (int i = 0; i < 3; i++)
			{
				int i0 = i;
				int i1 = (i + 1) % 3;

				VertexData a = v[i0];
				VertexData b = v[i1];
				int aIdx = tri[i0];
				int bIdx = tri[i1];

				float da = Vector3.Dot(a.pos, planeNormal) - offset;
				float db = Vector3.Dot(b.pos, planeNormal) - offset;

				bool aboveA = da >= 0;
				bool aboveB = db >= 0;

				if (aboveA) top.Add(a); else bottom.Add(a);

				if (aboveA != aboveB)
				{
					Edge e = new Edge(aIdx, bIdx);
					if (!edgeSplitVertex.TryGetValue(e, out int newIndex))
					{
						float t = da / (da - db);
						if (t < 0 || t > 1)
						{
							Debug.LogWarning($"Invalid interpolation t={t} for edge {aIdx}-{bIdx}, da={da}, db={db}");
							t = Mathf.Clamp01(t);
						}
						VertexData interp = new VertexData(
							Vector3.Lerp(a.pos, b.pos, t),
							avgNormal,
							Vector2.Lerp(a.uv, b.uv, t)
						);
						newIndex = allVerts.Count;
						allVerts.Add(interp);
						edgeSplitVertex[e] = newIndex;
						Debug.Log($"Split edge {aIdx}-{bIdx}: pos={interp.pos}, normal={interp.normal}, uv={interp.uv}, t={t}");
					}
					top.Add(allVerts[newIndex]);
					bottom.Add(allVerts[newIndex]);
				}
			}
		}

		private static void SplitCoplanarPair(int[] tri1, int[] tri2, VertexData[] v1, VertexData[] v2,
			Vector3 planeNormal, float offset, Dictionary<Edge, int> edgeSplitVertex, List<VertexData> allVerts, List<int[]> newTris)
		{
			Dictionary<Vector3, List<int>> posToIndices = new Dictionary<Vector3, List<int>>(new VertexPositionEqualityComparer(1e-6f));
			foreach (int idx in tri1.Concat(tri2))
			{
				Vector3 pos = allVerts[idx].pos;
				if (!posToIndices.TryGetValue(pos, out var list))
				{
					list = new List<int>();
					posToIndices[pos] = list;
				}
				list.Add(idx);
			}

			List<int> sharedVerts = new List<int>();
			List<int> uniqueVerts1 = new List<int>();
			List<int> uniqueVerts2 = new List<int>();

			foreach (var kvp in posToIndices)
			{
				if (kvp.Value.Count > 1)
				{
					sharedVerts.Add(kvp.Value[0]);
				}
				else
				{
					if (tri1.Contains(kvp.Value[0]))
					{
						uniqueVerts1.Add(kvp.Value[0]);
					}
					else
					{
						uniqueVerts2.Add(kvp.Value[0]);
					}
				}
			}

			if (sharedVerts.Count != 2 || uniqueVerts1.Count != 1 || uniqueVerts2.Count != 1)
			{
				Debug.LogWarning($"Invalid coplanar pair: shared={sharedVerts.Count}, unique1={uniqueVerts1.Count}, unique2={uniqueVerts2.Count}");
				newTris.Add(tri1);
				newTris.Add(tri2);
				return;
			}

			int v1Idx = uniqueVerts1[0];
			int v2Idx = uniqueVerts2[0];
			int s1Idx = sharedVerts[0];
			int s2Idx = sharedVerts[1];

			VertexData v1Unique = allVerts[v1Idx];
			VertexData v2Unique = allVerts[v2Idx];
			VertexData s1 = allVerts[s1Idx];
			VertexData s2 = allVerts[s2Idx];

			Vector3 avgNormal = (v1Unique.normal + v2Unique.normal + s1.normal + s2.normal).normalized;

			float d1 = Vector3.Dot(v1Unique.pos, planeNormal) - offset;
			float d2 = Vector3.Dot(v2Unique.pos, planeNormal) - offset;
			float ds1 = Vector3.Dot(s1.pos, planeNormal) - offset;
			float ds2 = Vector3.Dot(s2.pos, planeNormal) - offset;

			bool above1 = d1 >= 0;
			bool above2 = d2 >= 0;
			bool aboveS1 = ds1 >= 0;
			bool aboveS2 = ds2 >= 0;

			Debug.Log($"Pair vertices: v1={v1Unique.pos}, v2={v2Unique.pos}, s1={s1.pos}, s2={s2.pos}");
			Debug.Log($"Plane distances: v1={d1}, v2={d2}, s1={ds1}, s2={ds2}");
			Debug.Log($"Average face normal: {avgNormal}");

			if ((above1 && above2 && aboveS1 && aboveS2) || (!above1 && !above2 && !aboveS1 && !aboveS2))
			{
				newTris.Add(tri1);
				newTris.Add(tri2);
				Debug.Log("Pair entirely on one side, keeping original triangles");
				return;
			}

			int p1Idx = -1;
			Edge e1 = new Edge(v1Idx, s1Idx);
			if (above1 != aboveS1)
			{
				if (!edgeSplitVertex.TryGetValue(e1, out p1Idx))
				{
					float t = d1 / (d1 - ds1);
					if (t < 0 || t > 1)
					{
						Debug.LogWarning($"Invalid interpolation t={t} for edge v1-s1, d1={d1}, ds1={ds1}");
						t = Mathf.Clamp01(t);
					}
					VertexData interp = new VertexData(
						Vector3.Lerp(v1Unique.pos, s1.pos, t),
						avgNormal,
						Vector2.Lerp(v1Unique.uv, s1.uv, t)
					);
					p1Idx = allVerts.Count;
					allVerts.Add(interp);
					edgeSplitVertex[e1] = p1Idx;
					Debug.Log($"Split edge v1-s1: pos={interp.pos}, normal={interp.normal}, uv={interp.uv}, t={t}");
				}
			}

			int p2Idx = -1;
			Edge e2 = new Edge(v2Idx, s2Idx);
			if (above2 != aboveS2)
			{
				if (!edgeSplitVertex.TryGetValue(e2, out p2Idx))
				{
					float t = d2 / (d2 - ds2);
					if (t < 0 || t > 1)
					{
						Debug.LogWarning($"Invalid interpolation t={t} for edge v2-s2, d2={d2}, ds2={ds2}");
						t = Mathf.Clamp01(t);
					}
					VertexData interp = new VertexData(
						Vector3.Lerp(v2Unique.pos, s2.pos, t),
						avgNormal,
						Vector2.Lerp(v2Unique.uv, s2.uv, t)
					);
					p2Idx = allVerts.Count;
					allVerts.Add(interp);
					edgeSplitVertex[e2] = p2Idx;
					Debug.Log($"Split edge v2-s2: pos={interp.pos}, normal={interp.normal}, uv={interp.uv}, t={t}");
				}
			}

			if (p1Idx != -1 && p2Idx != -1)
			{
				Debug.Log($"Forming quads: s1={s1Idx}, s2={s2Idx}, v1={v1Idx}, v2={v2Idx}, p1={p1Idx}, p2={p2Idx}");

				Vector3 faceNormal = Vector3.Cross(v1Unique.pos - s1.pos, s2.pos - s1.pos).normalized;
				if (Vector3.Dot(faceNormal, avgNormal) < 0)
				{
					faceNormal = -faceNormal;
				}

				// Above quad
				if (aboveS1 && aboveS2)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, s1Idx, s2Idx, p2Idx, faceNormal, "Above s1-s2-p2");
					AddTriangleWithNormalCheck(newTris, allVerts, s1Idx, p2Idx, p1Idx, faceNormal, "Above s1-p2-p1");
				}
				else if (above1 && above2)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, v2Idx, p2Idx, faceNormal, "Above v1-v2-p2");
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, p2Idx, p1Idx, faceNormal, "Above v1-p2-p1");
				}
				else if (above1)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, p2Idx, p1Idx, faceNormal, "Above v1-p2-p1");
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, s2Idx, p2Idx, faceNormal, "Above v1-s2-p2");
				}
				else if (above2)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, v2Idx, p1Idx, p2Idx, faceNormal, "Above v2-p1-p2");
					AddTriangleWithNormalCheck(newTris, allVerts, v2Idx, s1Idx, p1Idx, faceNormal, "Above v2-s1-p1");
				}

				// Below quad
				if (!aboveS1 && !aboveS2)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, s1Idx, p2Idx, s2Idx, faceNormal, "Below s1-p2-s2");
					AddTriangleWithNormalCheck(newTris, allVerts, s1Idx, p1Idx, p2Idx, faceNormal, "Below s1-p1-p2");
				}
				else if (!above1 && !above2)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, p1Idx, v2Idx, faceNormal, "Below v1-p1-v2");
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, v2Idx, p2Idx, faceNormal, "Below v1-v2-p2");
				}
				else if (!above1)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, p1Idx, p2Idx, faceNormal, "Below v1-p1-p2");
					AddTriangleWithNormalCheck(newTris, allVerts, v1Idx, p2Idx, s2Idx, faceNormal, "Below v1-p2-s2");
				}
				else if (!above2)
				{
					AddTriangleWithNormalCheck(newTris, allVerts, v2Idx, p2Idx, p1Idx, faceNormal, "Below v2-p2-p1");
					AddTriangleWithNormalCheck(newTris, allVerts, v2Idx, p1Idx, s1Idx, faceNormal, "Below v2-p1-s1");
				}

				Debug.Log($"Added 4 triangles for coplanar pair");
			}
			else
			{
				newTris.Add(tri1);
				newTris.Add(tri2);
				Debug.LogWarning("Coplanar pair not split: insufficient edge intersections");
			}
		}

		private static void AddTriangleWithNormalCheck(List<int[]> tris, List<VertexData> allVerts, int i0, int i1, int i2, Vector3 refNormal, string context)
		{
			Vector3 p0 = allVerts[i0].pos;
			Vector3 p1 = allVerts[i1].pos;
			Vector3 p2 = allVerts[i2].pos;
			Vector3 triNormal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
			float area = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;

			if (area < 1e-6f)
			{
				Debug.LogWarning($"Degenerate triangle in {context}: {i0},{i1},{i2}, area={area}");
				return;
			}

			if (Vector3.Dot(triNormal, refNormal) < 0)
			{
				tris.Add(new int[] { i0, i2, i1 });
				Debug.Log($"Reversed triangle in {context}: {i0},{i2},{i1}, normal={triNormal}, refNormal={refNormal}");
			}
			else
			{
				tris.Add(new int[] { i0, i1, i2 });
				Debug.Log($"Added triangle in {context}: {i0},{i1},{i2}, normal={triNormal}, refNormal={refNormal}");
			}
		}

		private static void AddPolygonToTris(List<VertexData> poly, List<int[]> tris, List<VertexData> allVerts)
		{
			if (poly.Count < 3) return;

			int baseIndex = allVerts.Count;
			allVerts.AddRange(poly);

			Vector3 refNormal = Vector3.Cross(poly[1].pos - poly[0].pos, poly[2].pos - poly[0].pos).normalized;
			if (Vector3.Dot(refNormal, poly[0].normal) < 0)
			{
				refNormal = -refNormal;
			}

			for (int i = 1; i < poly.Count - 1; i++)
			{
				AddTriangleWithNormalCheck(tris, allVerts, baseIndex, baseIndex + i, baseIndex + i + 1, refNormal, $"Polygon tri {i}");
			}
		}

		private static (List<VertexData>, List<int>) DeduplicateVertices(List<VertexData> verts, List<int[]> tris)
		{
			var uniqueVerts = new List<VertexData>();
			var vertexMap = new Dictionary<(Vector3 pos, Vector3 normal), int>(new VertexPositionNormalEqualityComparer(1e-6f, 1e-4f));
			var finalTris = new List<int>();

			int[] indexMap = new int[verts.Count];
			for (int i = 0; i < verts.Count; i++)
			{
				var key = (verts[i].pos, verts[i].normal);
				if (!vertexMap.TryGetValue(key, out int idx))
				{
					idx = uniqueVerts.Count;
					uniqueVerts.Add(verts[i]);
					vertexMap[key] = idx;
					Debug.Log($"Vertex {i} mapped to {idx}: pos={verts[i].pos}, normal={verts[i].normal}, uv={verts[i].uv}");
				}
				else
				{
					Debug.Log($"Vertex {i} deduplicated to {idx}: pos={verts[i].pos}, normal={verts[i].normal}, uv={verts[i].uv}");
				}
				indexMap[i] = idx;
			}

			foreach (var tri in tris)
			{
				if (indexMap[tri[0]] == indexMap[tri[1]] || indexMap[tri[1]] == indexMap[tri[2]] || indexMap[tri[0]] == indexMap[tri[2]])
				{
					Debug.LogWarning($"Skipping degenerate triangle: {tri[0]},{tri[1]},{tri[2]}");
					continue;
				}
				finalTris.Add(indexMap[tri[0]]);
				finalTris.Add(indexMap[tri[1]]);
				finalTris.Add(indexMap[tri[2]]);
			}

			return (uniqueVerts, finalTris);
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
			public VertexPositionEqualityComparer(float tolerance) { _tolerance = tolerance; }
			public bool Equals(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < _tolerance * _tolerance;
			public int GetHashCode(Vector3 obj) => ((int)(Mathf.Round(obj.x / _tolerance) * 73856093)) ^
												   ((int)(Mathf.Round(obj.y / _tolerance) * 19349663)) ^
												   ((int)(Mathf.Round(obj.z / _tolerance) * 83492791));
		}

		private class VertexPositionNormalEqualityComparer : IEqualityComparer<(Vector3 pos, Vector3 normal)>
		{
			private readonly float _posTolerance;
			private readonly float _normTolerance;

			public VertexPositionNormalEqualityComparer(float posTolerance, float normTolerance)
			{
				_posTolerance = posTolerance;
				_normTolerance = normTolerance;
			}

			public bool Equals((Vector3 pos, Vector3 normal) a, (Vector3 pos, Vector3 normal) b)
			{
				return Vector3.SqrMagnitude(a.pos - b.pos) < _posTolerance * _posTolerance &&
					   Vector3.SqrMagnitude(a.normal - b.normal) < _normTolerance * _normTolerance;
			}

			public int GetHashCode((Vector3 pos, Vector3 normal) obj)
			{
				int posHash = ((int)(Mathf.Round(obj.pos.x / _posTolerance) * 73856093)) ^
							  ((int)(Mathf.Round(obj.pos.y / _posTolerance) * 19349663)) ^
							  ((int)(Mathf.Round(obj.pos.z / _posTolerance) * 83492791));
				int normHash = ((int)(Mathf.Round(obj.normal.x / _normTolerance) * 73856093)) ^
							   ((int)(Mathf.Round(obj.normal.y / _normTolerance) * 19349663)) ^
							   ((int)(Mathf.Round(obj.normal.z / _normTolerance) * 83492791));
				return posHash ^ normHash;
			}
		}
	}
}