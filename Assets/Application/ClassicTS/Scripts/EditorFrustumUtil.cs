// EditorFrustumUtil.cs
using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class EditorFrustumUtil
	{
		private static GameObject viewFrustumMarker;

		public static Mesh CreateViewFrustumMesh(float far)
		{
			// Exact same implementation as before (unchanged)
			var mesh = new Mesh();
			var verts = new List<Vector3>
			{
				new Vector3(-1, -1, 0),
				new Vector3( 1, -1, 0),
				new Vector3( 1,  1, 0),
				new Vector3(-1,  1, 0),
				new Vector3(-2, -2, far),
				new Vector3( 2, -2, far),
				new Vector3( 2,  2, far),
				new Vector3(-2,  2, far)
			};

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

		public static void UpdateViewFrustumMarker(View view, IMapManager mapManager)
		{
			DestroyViewFrustumMarker();

			if (view == null || view.data == null || view.data.Length < 7 || view.Distance < 0.02f)
				return;

			var go = new GameObject("GIZMO_VIEWFRUSTUM");
			go.layer = LayerMask.NameToLayer("Editor");

			var mf = go.AddComponent<MeshFilter>();
			var mr = go.AddComponent<MeshRenderer>();

			mf.mesh = CreateViewFrustumMesh(view.Distance);

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

			Vector3 worldPos = mapManager.TileWorldPosition(view.tile) + view.Position;
			Vector3 forward = (view.LookAt - view.Position).normalized;
			if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

			Quaternion rot = view.Rotation;
			Vector3 up = Vector3.ProjectOnPlane(rot * Vector3.up, forward);
			if (up.sqrMagnitude < 0.01f) up = Vector3.up;
			else up = up.normalized;

			go.transform.position = worldPos;
			go.transform.rotation = Quaternion.LookRotation(forward, up);

			viewFrustumMarker = go;
		}

		public static void DestroyViewFrustumMarker()
		{
			if (viewFrustumMarker != null)
			{
				if (Application.isPlaying)
					Object.Destroy(viewFrustumMarker);
				else
					Object.DestroyImmediate(viewFrustumMarker);
				viewFrustumMarker = null;
			}
		}
	}
}