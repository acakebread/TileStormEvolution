// EditorFrustumUtil.cs
using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class EditorFrustumUtil
	{
		private static GameObject viewFrustumMarker;
		private static Mesh cachedFrustumMesh;
		private static float cachedFrustumDistance = -1f;

		// ===================================================================
		// NEW PURE WORLD-SPACE API
		// ===================================================================
		public static void ShowAt(Vector3 worldPosition, Quaternion worldRotation, float distance)
		{
			if (distance < 0.02f)
			{
				Hide();
				return;
			}

			// Create once
			if (viewFrustumMarker == null)
			{
				viewFrustumMarker = new GameObject("GIZMO_VIEWFRUSTUM");
				viewFrustumMarker.layer = LayerMask.NameToLayer("Editor");

				var mf = viewFrustumMarker.AddComponent<MeshFilter>();
				var mr = viewFrustumMarker.AddComponent<MeshRenderer>();

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

			// Regenerate mesh only if distance changed
			if (!Mathf.Approximately(cachedFrustumDistance, distance))
			{
				if (cachedFrustumMesh != null)
					Object.DestroyImmediate(cachedFrustumMesh);

				cachedFrustumMesh = CreateViewFrustumMesh(distance);
				cachedFrustumDistance = distance;

				var mf = viewFrustumMarker.GetComponent<MeshFilter>();
				mf.sharedMesh = cachedFrustumMesh;
			}

			// Fast update
			viewFrustumMarker.transform.position = worldPosition;
			viewFrustumMarker.transform.rotation = worldRotation;
			viewFrustumMarker.SetActive(true);
		}

		public static void Hide()
		{
			if (viewFrustumMarker != null)
			{
				if (Application.isPlaying)
					Object.Destroy(viewFrustumMarker);
				else
					Object.DestroyImmediate(viewFrustumMarker);
				viewFrustumMarker = null;
			}

			if (cachedFrustumMesh != null)
			{
				Object.DestroyImmediate(cachedFrustumMesh);
				cachedFrustumMesh = null;
			}
			cachedFrustumDistance = -1f;
		}

		// ===================================================================
		// CORRECT FRUSTUM MESH (from your reliable version)
		// ===================================================================
		private static Mesh CreateViewFrustumMesh(float distance)
		{
			const float GameFOV = 20f;
			const float Near = 0.25f;
			float Far = Mathf.Max(distance, Near + 0.1f);

			float aspect = 16f / 9f;
			float halfFov = GameFOV * 0.5f * Mathf.Deg2Rad;
			float t = Mathf.Tan(halfFov);

			float nh = Near * t * 2f;
			float nw = nh * aspect;
			float fh = Far * t * 2f;
			float fw = fh * aspect;

			Vector3[] near = {
				new(-nw/2, -nh/2, Near),
				new( nw/2, -nh/2, Near),
				new( nw/2,  nh/2, Near),
				new(-nw/2,  nh/2, Near)
			};

			Vector3[] far = {
				new(-fw/2, -fh/2, Far),
				new( fw/2, -fh/2, Far),
				new( fw/2,  fh/2, Far),
				new(-fw/2,  fh/2, Far)
			};

			var mesh = new Mesh { name = "ViewFrustum_DoubleSided_4Mat" };

			var verts = new List<Vector3>();
			verts.AddRange(near);
			verts.AddRange(far);
			mesh.SetVertices(verts);

			var sideOutside = new List<int>();
			var sideInside = new List<int>();
			var capOutside = new List<int>();
			var capInside = new List<int>();

			// LEFT FACE
			sideOutside.AddRange(new[] { 0, 4, 7 }); sideOutside.AddRange(new[] { 0, 7, 3 });
			sideInside.AddRange(new[] { 0, 7, 4 }); sideInside.AddRange(new[] { 0, 3, 7 });

			// RIGHT FACE
			sideOutside.AddRange(new[] { 1, 2, 6 }); sideOutside.AddRange(new[] { 1, 6, 5 });
			sideInside.AddRange(new[] { 1, 6, 2 }); sideInside.AddRange(new[] { 1, 5, 6 });

			// TOP SLOPE
			capOutside.AddRange(new[] { 3, 7, 6 }); capOutside.AddRange(new[] { 3, 6, 2 });
			capInside.AddRange(new[] { 3, 6, 7 }); capInside.AddRange(new[] { 3, 2, 6 });

			// BOTTOM SLOPE
			capOutside.AddRange(new[] { 0, 1, 5 }); capOutside.AddRange(new[] { 0, 5, 4 });
			capInside.AddRange(new[] { 0, 5, 1 }); capInside.AddRange(new[] { 0, 4, 5 });

			mesh.subMeshCount = 4;
			mesh.SetTriangles(sideOutside, 0);
			mesh.SetTriangles(sideInside, 1);
			mesh.SetTriangles(capOutside, 2);
			mesh.SetTriangles(capInside, 3);

			mesh.RecalculateBounds();
			return mesh;
		}
	}
}