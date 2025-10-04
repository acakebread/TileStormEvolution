using UnityEngine;

namespace ClassicTilestorm
{
	public class MeshDivisionTest : MonoBehaviour
	{
		[SerializeField] private bool enableSubdivision = true;
		[SerializeField] private float maxSegmentLength = 0.3f;
		[SerializeField] private Vector3 divisionAxis = Vector3.up;
		[SerializeField] private float offset = 0f;

		private MeshFilter meshFilter;
		private MeshRenderer meshRenderer;
		private Mesh subdividedMesh;
		private GameObject meshObject;

		private void Awake()
		{
			meshFilter = GetComponent<MeshFilter>();
			meshRenderer = GetComponent<MeshRenderer>();
			if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
			{
				Debug.LogError("MeshDivisionTest: No MeshFilter or MeshRenderer found. Disabling.");
				enabled = false;
				return;
			}

			if (Debug.isDebugBuild)
			{
				Debug.Log($"Original mesh: {meshFilter.sharedMesh.vertexCount} vertices, {meshFilter.sharedMesh.triangles.Length / 3} triangles");
			}

			ApplySubdivision();
		}

		private void ApplySubdivision()
		{
			if (subdividedMesh != null) Object.Destroy(subdividedMesh);
			if (meshObject != null && meshObject != gameObject) Object.Destroy(meshObject);

			if (enableSubdivision)
			{
				Vector3 localAxis = transform.InverseTransformDirection(divisionAxis.normalized);
				Bounds bounds = meshFilter.sharedMesh.bounds;
				Vector3 localCenter = bounds.center;

				Mesh inputMesh = Object.Instantiate(meshFilter.sharedMesh);
				// Call SubdivideMeshLongEdges (corrected from SplitMeshAlongPlane)
				subdividedMesh = MeshUtils.SubdivideMeshLongEdges(inputMesh, localAxis, maxSegmentLength, localCenter + localAxis * offset * 0.5f);
				Object.Destroy(inputMesh);

				if (subdividedMesh != null && subdividedMesh.vertexCount > 0)
				{
					meshObject = new GameObject($"{name}_Subdivided");
					meshObject.transform.SetParent(transform, false);
					var filter = meshObject.AddComponent<MeshFilter>();
					var renderer = meshObject.AddComponent<MeshRenderer>();
					filter.mesh = subdividedMesh;
					renderer.material = meshRenderer.material;
				}
				else
				{
					Debug.LogWarning("Subdivided mesh is empty or null. No GameObject created.");
				}

				meshRenderer.enabled = false;
			}
			else
			{
				subdividedMesh = Object.Instantiate(meshFilter.sharedMesh);
				meshObject = gameObject;
				meshFilter.mesh = subdividedMesh;
				meshRenderer.enabled = true;
			}
		}

		private void OnDestroy()
		{
			if (subdividedMesh != null) Object.Destroy(subdividedMesh);
			if (meshObject != null && meshObject != gameObject) Object.Destroy(meshObject);
		}

		private void OnDrawGizmosSelected()
		{
			if (Debug.isDebugBuild && meshFilter != null && meshFilter.sharedMesh != null)
			{
				Bounds bounds = meshFilter.sharedMesh.bounds;
				Vector3 localCenter = bounds.center;
				Vector3 localAxis = transform.InverseTransformDirection(divisionAxis.normalized);
				float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * transform.localScale.magnitude;

				// Draw splitting plane
				Vector3 planePoint = transform.TransformPoint(localCenter + localAxis * offset * 0.5f);
				Vector3 planeNormal = transform.TransformDirection(localAxis);
				Vector3 right = Vector3.Cross(planeNormal, Vector3.up).normalized;
				if (right.sqrMagnitude < 0.01f) right = Vector3.Cross(planeNormal, Vector3.forward).normalized;
				Vector3 up = Vector3.Cross(right, planeNormal).normalized;
				Vector3 p1 = planePoint + (right + up) * size;
				Vector3 p2 = planePoint + (right - up) * size;
				Vector3 p3 = planePoint + (-right - up) * size;
				Vector3 p4 = planePoint + (-right + up) * size;
				Gizmos.color = Color.yellow;
				Gizmos.DrawLine(p1, p2);
				Gizmos.DrawLine(p2, p3);
				Gizmos.DrawLine(p3, p4);
				Gizmos.DrawLine(p4, p1);

				if (meshObject != null)
				{
					var filter = meshObject.GetComponent<MeshFilter>();
					if (filter != null && filter.mesh != null)
					{
						Gizmos.color = Color.red;
						int[] tris = filter.mesh.triangles;
						Vector3[] verts = filter.mesh.vertices;
						for (int j = 0; j < tris.Length; j += 3)
						{
							Vector3 v0 = meshObject.transform.TransformPoint(verts[tris[j]]);
							Vector3 v1 = meshObject.transform.TransformPoint(verts[tris[j + 1]]);
							Vector3 v2 = meshObject.transform.TransformPoint(verts[tris[j + 2]]);
							Gizmos.DrawLine(v0, v1);
							Gizmos.DrawLine(v1, v2);
							Gizmos.DrawLine(v2, v0);
						}
					}
				}
			}
		}
	}
}


//using UnityEngine;
//using System.Collections.Generic;

//namespace ClassicTilestorm
//{
//	public class MeshDivisionTest : MonoBehaviour
//	{
//		[SerializeField] private bool enableSubdivision = true;
//		[SerializeField] private float maxSegmentLength = 0.3f;
//		[SerializeField] private Vector3 divisionAxis = Vector3.up;
//		[SerializeField] private float offset = 0f;//changing this appears to make no difference

//		private MeshFilter meshFilter;
//		private MeshRenderer meshRenderer;
//		private Mesh subdividedMesh;
//		private GameObject meshObject;

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
//			// Clean up previous mesh and object
//			if (subdividedMesh != null) Object.Destroy(subdividedMesh);
//			if (meshObject != null && meshObject != gameObject) Object.Destroy(meshObject);

//			if (enableSubdivision)
//			{
//				Vector3 localAxis = transform.InverseTransformDirection(divisionAxis.normalized);
//				Bounds bounds = meshFilter.sharedMesh.bounds;
//				Vector3 localCenter = bounds.center;

//				// Instantiate a copy to avoid modifying the original
//				Mesh inputMesh = Object.Instantiate(meshFilter.sharedMesh);
//				subdividedMesh = MeshUtils.SubdivideMeshLongEdges(inputMesh, localAxis, maxSegmentLength, localCenter + localAxis * offset * 0.5f);
//				Object.Destroy(inputMesh); // Clean up the copy

//				// Create GameObject for the unified mesh
//				if (subdividedMesh != null && subdividedMesh.vertexCount > 0)
//				{
//					meshObject = new GameObject($"{name}_Subdivided");
//					meshObject.transform.SetParent(transform, false);
//					var filter = meshObject.AddComponent<MeshFilter>();
//					var renderer = meshObject.AddComponent<MeshRenderer>();
//					filter.mesh = subdividedMesh;
//					renderer.material = meshRenderer.material;
//				}
//				else
//				{
//					Debug.LogWarning("Subdivided mesh is empty or null. No GameObject created.");
//				}

//				// Disable original mesh
//				meshRenderer.enabled = false;
//			}
//			else
//			{
//				subdividedMesh = Object.Instantiate(meshFilter.sharedMesh);
//				meshObject = gameObject;
//				meshFilter.mesh = subdividedMesh;
//				meshRenderer.enabled = true;
//			}
//		}

//		private void OnDestroy()
//		{
//			if (subdividedMesh != null) Object.Destroy(subdividedMesh);
//			if (meshObject != null && meshObject != gameObject) Object.Destroy(meshObject);
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

//				// Draw the subdivided mesh
//				if (meshObject != null)
//				{
//					var filter = meshObject.GetComponent<MeshFilter>();
//					if (filter != null && filter.mesh != null)
//					{
//						Gizmos.color = Color.red;
//						int[] tris = filter.mesh.triangles;
//						Vector3[] verts = filter.mesh.vertices;
//						for (int j = 0; j < tris.Length; j += 3)
//						{
//							Vector3 v0 = meshObject.transform.TransformPoint(verts[tris[j]]);
//							Vector3 v1 = meshObject.transform.TransformPoint(verts[tris[j + 1]]);
//							Vector3 v2 = meshObject.transform.TransformPoint(verts[tris[j + 2]]);
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