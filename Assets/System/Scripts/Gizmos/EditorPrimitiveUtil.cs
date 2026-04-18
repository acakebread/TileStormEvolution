using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class EditorPrimitiveUtil
	{
		private static GameObject coneMarker;
		private static Mesh cachedConeMesh;
		private static float cachedLength = -1f;
		private static float cachedApexAngle = -1f;  // Now cache apex angle instead of radius

		// ===================================================================
		// INITIAL SHOW
		// ===================================================================
		public static void ShowConeAt(Vector3 worldPosition, Quaternion worldRotation, float length, float apexAngle)
		{
			UpdateCone(worldPosition, worldRotation, length, apexAngle);
		}

		// ===================================================================
		// MAIN UPDATE — uses apex angle (in degrees)
		// ===================================================================
		public static bool UpdateCone(Vector3 worldPosition, Quaternion worldRotation, float length, float apexAngle)
		{
			if (length < 0.02f || apexAngle <= 0f || apexAngle >= 180f)
			{
				Hide();
				return false;
			}

			float halfAngleRad = apexAngle * 0.5f * Mathf.Deg2Rad;
			float baseRadius = length * Mathf.Tan(halfAngleRad);

			// Create once
			if (coneMarker == null)
			{
				coneMarker = new GameObject("GIZMO_CONE");
				coneMarker.layer = LayerMask.NameToLayer("Editor");

				var mf = coneMarker.AddComponent<MeshFilter>();
				var mr = coneMarker.AddComponent<MeshRenderer>();

				var transparentShader = Shader.Find("Hidden/URPGizmoTransparent");
				var additiveShader = Shader.Find("Hidden/URPGizmoAdditive");

				var materials = new Material[4]
				{
					new Material(additiveShader),
					new Material(transparentShader),
					new Material(additiveShader),
					new Material(transparentShader)
				};

				materials[0].SetColor("_BaseColor", new Color(0.05f, 0.30f, 0.40f, 1f));
				materials[1].SetColor("_BaseColor", new Color(0.3f, 0.6f, 0.9f, 0.20f));
				materials[2].SetColor("_BaseColor", new Color(0.03f, 0.22f, 0.30f, 1f));
				materials[3].SetColor("_BaseColor", new Color(0.1f, 0.5f, 0.8f, 0.20f));

				foreach (var m in materials)
					m.hideFlags = HideFlags.HideAndDontSave;

				mr.materials = materials;
			}

			// Regenerate mesh only if length or apex angle changed
			bool needsNewMesh = !Mathf.Approximately(cachedLength, length) ||
								!Mathf.Approximately(cachedApexAngle, apexAngle);

			if (needsNewMesh)
			{
				if (cachedConeMesh != null)
					Object.DestroyImmediate(cachedConeMesh);

				cachedConeMesh = CreateConeMesh(length, baseRadius);
				cachedLength = length;
				cachedApexAngle = apexAngle;

				var mf = coneMarker.GetComponent<MeshFilter>();
				mf.sharedMesh = cachedConeMesh;
			}

			coneMarker.transform.position = worldPosition;
			coneMarker.transform.rotation = worldRotation;
			coneMarker.SetActive(true);
			return true;
		}

		public static void Hide()
		{
			if (coneMarker != null)
			{
				if (Application.isPlaying)
					Object.Destroy(coneMarker);
				else
					Object.DestroyImmediate(coneMarker);
				coneMarker = null;
			}

			if (cachedConeMesh != null)
			{
				Object.DestroyImmediate(cachedConeMesh);
				cachedConeMesh = null;
			}

			cachedLength = -1f;
			cachedApexAngle = -1f;
		}

		// ===================================================================
		// PROCEDURAL CONE MESH - now supports arbitrary normal direction
		// ===================================================================
		private static Mesh CreateConeMesh(float length, float baseRadius, Vector3 normal = default, int segments = 32)
		{
			if (normal == default) normal = Vector3.up;
			normal = normal.normalized;

			var mesh = new Mesh { name = "GizmoCone" };

			var vertices = new List<Vector3>();
			var trianglesOuter = new List<int>();
			var trianglesInner = new List<int>();
			var trianglesCapOuter = new List<int>();
			var trianglesCapInner = new List<int>();

			// Tip vertex at origin (local space)
			vertices.Add(Vector3.zero); // index 0

			// Base circle vertices perpendicular to the normal
			int baseStart = vertices.Count;

			// Create an orthonormal basis for the base plane
			Vector3 axis1 = Vector3.Cross(normal, Vector3.up);
			if (axis1.sqrMagnitude < 0.0001f) // normal is nearly parallel to up
				axis1 = Vector3.Cross(normal, Vector3.right);
			axis1 = axis1.normalized;

			Vector3 axis2 = Vector3.Cross(normal, axis1).normalized;

			for (int i = 0; i < segments; i++)
			{
				float angle = i * Mathf.PI * 2f / segments;
				float x = Mathf.Cos(angle) * baseRadius;
				float y = Mathf.Sin(angle) * baseRadius;

				// Build base vertex in local plane, then align to normal
				Vector3 localBase = axis1 * x + axis2 * y + normal * length;
				vertices.Add(localBase);
			}

			// Base center (for cap)
			int baseCenter = vertices.Count;
			vertices.Add(normal * length);

			// Side triangles (outer)
			for (int i = 0; i < segments; i++)
			{
				int next = (i + 1) % segments;
				int v1 = baseStart + i;
				int v2 = baseStart + next;

				trianglesOuter.Add(0);   // tip
				trianglesOuter.Add(v2);
				trianglesOuter.Add(v1);
			}

			// Side triangles (inner — reverse winding for backface)
			for (int i = 0; i < segments; i++)
			{
				int next = (i + 1) % segments;
				int v1 = baseStart + i;
				int v2 = baseStart + next;

				trianglesInner.Add(0);
				trianglesInner.Add(v1);
				trianglesInner.Add(v2);
			}

			// Base cap (outer)
			for (int i = 0; i < segments; i++)
			{
				int next = (i + 1) % segments;
				int v1 = baseStart + i;
				int v2 = baseStart + next;

				trianglesCapOuter.Add(baseCenter);
				trianglesCapOuter.Add(v2);
				trianglesCapOuter.Add(v1);
			}

			// Base cap (inner — reverse winding)
			for (int i = 0; i < segments; i++)
			{
				int next = (i + 1) % segments;
				int v1 = baseStart + i;
				int v2 = baseStart + next;

				trianglesCapInner.Add(baseCenter);
				trianglesCapInner.Add(v1);
				trianglesCapInner.Add(v2);
			}

			mesh.SetVertices(vertices);
			mesh.subMeshCount = 4;
			mesh.SetTriangles(trianglesOuter, 0);
			mesh.SetTriangles(trianglesInner, 1);
			mesh.SetTriangles(trianglesCapOuter, 2);
			mesh.SetTriangles(trianglesCapInner, 3);

			mesh.RecalculateNormals();
			mesh.RecalculateBounds();

			return mesh;
		}
	}
}