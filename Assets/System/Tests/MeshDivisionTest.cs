//using UnityEngine;
//using System.Collections.Generic;

//namespace ClassicTilestorm
//{
//	public class MeshDivisionTest : MonoBehaviour
//	{
//		[SerializeField] private bool enableSubdivision = true;
//		[SerializeField] private float maxSegmentLength = 0.3f;
//		[SerializeField] private Vector3 divisionAxis = Vector3.up;
//		[SerializeField] private bool useDoubleSidedShader = true;

//		private MeshFilter meshFilter;
//		private MeshRenderer meshRenderer;
//		private List<Mesh> subdividedMeshes = new List<Mesh>();
//		private List<GameObject> meshObjects = new List<GameObject>();

//		private void Awake()
//		{
//			meshFilter = GetComponent<MeshFilter>();
//			meshRenderer = GetComponent<MeshRenderer>();
//			if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
//			{
//				Debug.LogError("MeshDivisionTest: No MeshFilter or MeshRenderer found. Disabling.");
//				enabled = false;
//				return;
//			}

//			// Log original mesh stats
//			if (Debug.isDebugBuild)
//			{
//				Debug.Log($"Original mesh: {meshFilter.sharedMesh.vertexCount} vertices, {meshFilter.sharedMesh.triangles.Length / 3} triangles");
//			}

//			ApplySubdivision();
//		}

//		private void ApplySubdivision()
//		{
//			// Clean up previous meshes and objects
//			foreach (Mesh mesh in subdividedMeshes)
//			{
//				if (mesh != null) Object.Destroy(mesh);
//			}
//			foreach (GameObject obj in meshObjects)
//			{
//				if (obj != null && obj != gameObject) Object.Destroy(obj);
//			}
//			subdividedMeshes.Clear();
//			meshObjects.Clear();

//			if (enableSubdivision)
//			{
//				Vector3 localAxis = transform.InverseTransformDirection(divisionAxis.normalized);
//				Bounds bounds = meshFilter.sharedMesh.bounds;
//				Vector3 localCenter = bounds.center;

//				// Instantiate a copy to avoid modifying the original
//				Mesh inputMesh = Object.Instantiate(meshFilter.sharedMesh);
//				subdividedMeshes = MeshUtils.SubdivideMeshLongEdges(inputMesh, localAxis, maxSegmentLength, localCenter);
//				Object.Destroy(inputMesh); // Clean up the copy

//				// Create GameObjects for each mesh
//				for (int i = 0; i < subdividedMeshes.Count; i++)
//				{
//					GameObject obj = new GameObject($"{name}_Slice_{i}");
//					obj.transform.SetParent(transform, false);
//					var filter = obj.AddComponent<MeshFilter>();
//					var renderer = obj.AddComponent<MeshRenderer>();
//					filter.mesh = subdividedMeshes[i];
//					renderer.material = meshRenderer.material;

//					if (useDoubleSidedShader && Debug.isDebugBuild)
//					{
//						Shader doubleSided = Shader.Find("Custom/DoubleSided");
//						if (doubleSided != null)
//						{
//							renderer.material = new Material(doubleSided);
//						}
//						else
//						{
//							Debug.LogWarning("Double-sided shader not found. Create 'Custom/DoubleSided' for debugging.");
//						}
//					}

//					meshObjects.Add(obj);
//					Debug.Log($"Created slice {i}: {subdividedMeshes[i].vertexCount} vertices, {subdividedMeshes[i].triangles.Length / 3} triangles");
//				}

//				// Disable original mesh
//				meshRenderer.enabled = false;
//			}
//			else
//			{
//				Mesh originalCopy = Object.Instantiate(meshFilter.sharedMesh);
//				subdividedMeshes.Add(originalCopy);
//				meshObjects.Add(gameObject);
//				meshFilter.mesh = originalCopy;
//				meshRenderer.enabled = true;
//			}
//		}

//		private void OnDestroy()
//		{
//			foreach (Mesh mesh in subdividedMeshes)
//			{
//				if (mesh != null) Object.Destroy(mesh);
//			}
//			foreach (GameObject obj in meshObjects)
//			{
//				if (obj != null && obj != gameObject) Object.Destroy(obj);
//			}
//		}

//		private void OnDrawGizmosSelected()
//		{
//			if (Debug.isDebugBuild && meshFilter != null && meshFilter.sharedMesh != null)
//			{
//				// Draw splitting planes
//				Bounds bounds = meshFilter.sharedMesh.bounds;
//				Vector3 localCenter = bounds.center;
//				Vector3 localAxis = transform.InverseTransformDirection(divisionAxis.normalized);
//				float minProj = float.MaxValue;
//				float maxProj = float.MinValue;
//				foreach (Vector3 v in meshFilter.sharedMesh.vertices)
//				{
//					float proj = Vector3.Dot(v - localCenter, localAxis);
//					minProj = Mathf.Min(minProj, proj);
//					maxProj = Mathf.Max(maxProj, proj);
//				}
//				float length = maxProj - minProj;
//				int maxDepth = Mathf.CeilToInt(Mathf.Log(length / maxSegmentLength, 2f));
//				float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * transform.localScale.magnitude;

//				// Draw planes at each depth
//				DrawBSPPlanes(localAxis, minProj, maxProj, localCenter, 0, maxDepth, size);

//				// Draw meshes
//				for (int i = 0; i < meshObjects.Count; i++)
//				{
//					var filter = meshObjects[i].GetComponent<MeshFilter>();
//					if (filter != null && filter.mesh != null)
//					{
//						Gizmos.color = i % 2 == 0 ? Color.red : Color.blue;
//						int[] tris = filter.mesh.triangles;
//						Vector3[] verts = filter.mesh.vertices;
//						for (int j = 0; j < tris.Length; j += 3)
//						{
//							Vector3 v0 = meshObjects[i].transform.TransformPoint(verts[tris[j]]);
//							Vector3 v1 = meshObjects[i].transform.TransformPoint(verts[tris[j + 1]]);
//							Vector3 v2 = meshObjects[i].transform.TransformPoint(verts[tris[j + 2]]);
//							Gizmos.DrawLine(v0, v1);
//							Gizmos.DrawLine(v1, v2);
//							Gizmos.DrawLine(v2, v0);
//						}
//					}
//				}
//			}
//		}

//		private void DrawBSPPlanes(Vector3 localAxis, float minProj, float maxProj, Vector3 localCenter, int depth, int maxDepth, float size)
//		{
//			if (depth >= maxDepth) return;

//			float midProj = (minProj + maxProj) / 2f;
//			Vector3 planePoint = transform.TransformPoint(localCenter + localAxis * midProj);
//			Vector3 planeNormal = transform.TransformDirection(localAxis);
//			Vector3 right = Vector3.Cross(planeNormal, Vector3.up).normalized;
//			if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(planeNormal, Vector3.forward).normalized;
//			Vector3 up = Vector3.Cross(right, planeNormal).normalized;
//			Vector3 p1 = planePoint + (right + up) * size;
//			Vector3 p2 = planePoint + (right - up) * size;
//			Vector3 p3 = planePoint + (-right - up) * size;
//			Vector3 p4 = planePoint + (-right + up) * size;
//			Gizmos.color = Color.yellow;
//			Gizmos.DrawLine(p1, p2);
//			Gizmos.DrawLine(p2, p3);
//			Gizmos.DrawLine(p3, p4);
//			Gizmos.DrawLine(p4, p1);

//			// Recurse
//			DrawBSPPlanes(localAxis, midProj, maxProj, localCenter, depth + 1, maxDepth, size);
//			DrawBSPPlanes(localAxis, minProj, midProj, localCenter, depth + 1, maxDepth, size);
//		}
//	}
//}