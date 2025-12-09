// File: EditorTransformUtil.cs
using UnityEngine;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorTransformUtil
	{
		private static GameObject root;
		private static GameObject positionHandle;
		private static GameObject rotationOrbiter;
		private static View activeView;
		private static IMapManager mapManager;

		private const float TARGET_SCREEN_SIZE = 60f;

		// drag state
		private static bool draggingPosition;
		private static Plane dragPlane;
		private static Vector3 dragStartPoint;
		private static Vector3 lockedAxis = Vector3.zero;

		private static bool draggingRotation;
		private static Transform draggedRing;
		private static Vector3 rotationAxis;
		private static Quaternion startRotation;
		private static Vector3 startMouseWorld;

		// ===================================================================
		// PUBLIC API – EXACTLY what you already call
		// ===================================================================
		public static void ShowTransformGizmo(View view, IMapManager mgr, Camera cam)
		{
			HideTransformGizmo();
			if (view == null || mgr == null || cam == null) return;

			activeView = view;
			mapManager = mgr;

			root = new GameObject("GIZMO_ROOT");
			root.layer = LayerMask.NameToLayer("Editor");

			positionHandle = CreatePositionHandle(root.transform);
			rotationOrbiter = CreateRotationOrbiter(root.transform);

			UpdateTransformGizmoVisuals(cam);
		}

		public static void HideTransformGizmo()
		{
			if (root != null) Object.Destroy(root);

			root = positionHandle = rotationOrbiter = null;
			activeView = null;
			mapManager = null;

			draggingPosition = draggingRotation = false;
			lockedAxis = Vector3.zero;
		}

		public static void UpdateTransformGizmoVisuals(Camera cam)
		{
			if (activeView == null || root == null || mapManager == null || cam == null) return;

			Vector3 worldPos = mapManager.TileWorldPosition(activeView.tile) + activeView.Position;
			root.transform.position = worldPos;

			if (rotationOrbiter != null)
				rotationOrbiter.transform.rotation = activeView.Rotation;

			float dist = Vector3.Distance(cam.transform.position, root.transform.position);
			float scale = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;
			scale = TARGET_SCREEN_SIZE * scale / Screen.height;
			root.transform.localScale = Vector3.one * scale;
		}

		public static bool HandleTransformGizmoInput(Camera cam)
		{
			if (activeView == null || cam == null || root == null) return false;

			UpdateTransformGizmoVisuals(cam);

			Ray ray = cam.ScreenPointToRay(Input.mousePosition);
			bool handled = false;

			// Hover
			DoHover(ray);

			// Click
			if (Input.GetMouseButtonDown(0))
			{
				Collider col = positionHandle.GetComponent<Collider>();
				if (col && col.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
				{
					StartPositionDrag(ray, cam, hit);
					handled = true;
				}
				else if (TryStartRotationDrag(ray))
				{
					handled = true;
				}
			}

			// Drag
			if (Input.GetMouseButton(0))
			{
				if (draggingPosition) ContinuePositionDrag(cam);
				if (draggingRotation) ContinueRotationDrag(cam);
				handled = true;
			}

			// Release
			if (Input.GetMouseButtonUp(0))
			{
				draggingPosition = draggingRotation = false;
				lockedAxis = Vector3.zero;
			}

			return handled;
		}

		// ===================================================================
		// POSITION HANDLE – works with your original picking code
		// ===================================================================
		private static GameObject CreatePositionHandle(Transform parent)
		{
			GameObject go = new GameObject("PositionHandle");
			go.layer = LayerMask.NameToLayer("Editor");
			go.transform.SetParent(parent, false);

			// THIS IS THE IMPORTANT PART – collider on the root so your old code works
			SphereCollider sc = go.AddComponent<SphereCollider>();
			sc.radius = 0.3f;

			var faces = new[]
			{
				(Vector3.right,   new Color(0.95f, 0.25f, 0.25f, 0.55f)),
				(-Vector3.right,  new Color(0.95f, 0.25f, 0.25f, 0.55f)),
				(Vector3.up,      new Color(0.25f, 0.95f, 0.25f, 0.55f)),
				(-Vector3.up,     new Color(0.25f, 0.95f, 0.25f, 0.55f)),
				(Vector3.forward, new Color(0.25f, 0.45f, 0.95f, 0.55f)),
				(-Vector3.forward,new Color(0.25f, 0.45f, 0.95f, 0.55f))
			};

			foreach (var f in faces)
			{
				GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
				quad.layer = LayerMask.NameToLayer("Editor");
				quad.transform.SetParent(go.transform, false);
				quad.transform.localScale = Vector3.one * 0.5f;
				quad.transform.localPosition = -f.Item1 * 0.25f;
				quad.transform.rotation = Quaternion.LookRotation(f.Item1);

				MeshRenderer mr = quad.GetComponent<MeshRenderer>();
				mr.material = MaterialUtils.CreateTransparentUnlitMaterial(f.Item2);
				Object.Destroy(quad.GetComponent<Collider>());

				FaceTag tag = quad.AddComponent<FaceTag>();
				tag.normal = f.Item1;
			}

			return go;
		}

		private class FaceTag : MonoBehaviour
		{
			public Vector3 normal;
		}

		private static void StartPositionDrag(Ray ray, Camera cam, RaycastHit hit)
		{
			lockedAxis = Vector3.zero;
			FaceTag tag = hit.transform.GetComponent<FaceTag>();
			if (tag != null) lockedAxis = tag.normal;

			dragPlane = new Plane(-cam.transform.forward, root.transform.position);
			if (dragPlane.Raycast(ray, out float enter))
				dragStartPoint = ray.GetPoint(enter);

			draggingPosition = true;
		}

		private static void ContinuePositionDrag(Camera cam)
		{
			Ray ray = cam.ScreenPointToRay(Input.mousePosition);
			if (!dragPlane.Raycast(ray, out float enter)) return;

			Vector3 cur = ray.GetPoint(enter);
			Vector3 delta = cur - dragStartPoint;

			if (lockedAxis != Vector3.zero)
				delta = Vector3.ProjectOnPlane(delta, lockedAxis);

			Vector3 newPos = root.transform.position + delta;
			activeView.Position = newPos - mapManager.TileWorldPosition(activeView.tile);

			UpdateTransformGizmoVisuals(cam);
			EditorUtil.UpdateViewFrustumMarker(activeView, mapManager);

			dragStartPoint = cur;
		}

		// ===================================================================
		// ROTATION – your exact working code
		// ===================================================================
		private static GameObject CreateRotationOrbiter(Transform parent)
		{
			GameObject orb = new GameObject("RotationOrbiter");
			orb.transform.SetParent(parent, false);

			Color[] colors = { new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.3f), new Color(0.3f, 0.6f, 1f) };
			Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

			for (int i = 0; i < 3; i++)
			{
				GameObject ring = CreateTorus();
				ring.name = "Ring_" + "XYZ"[i];
				ring.transform.SetParent(orb.transform, false);
//				ring.transform.localRotation = Quaternion.FromToRotation(new Vector3(axes[i].y, axes[i].z, axes[i].x), axes[i]);
				ring.transform.rotation = Quaternion.FromToRotation(new Vector3(axes[i].y, axes[i].z, axes[i].x), axes[i]);//move to screen space
				//ring.transform.localScale = new Vector3(1f, 0.001f, 1f);
				ring.transform.localScale = new Vector3(1f, 1f, 1f);

				MeshRenderer mr = ring.GetComponent<MeshRenderer>();
				mr.material = MaterialUtils.CreateTransparentUnlitMaterial(colors[i]);
				mr.material.SetFloat("_Alpha", 0.7f);

				ring.AddComponent<MeshCollider>();
			}
			return orb;
		}

		private static bool TryStartRotationDrag(Ray ray)
		{
			foreach (Transform ring in rotationOrbiter.transform)
			{
				Collider c = ring.GetComponent<Collider>();
				if (c && c.Raycast(ray, out _, Mathf.Infinity))
				{
					draggedRing = ring;
					rotationAxis = ring.up;
					startRotation = activeView.Rotation;

					Plane p = new Plane(rotationAxis, root.transform.position);
					p.Raycast(ray, out float enter);
					startMouseWorld = ray.GetPoint(enter);

					draggingRotation = true;
					return true;
				}
			}
			return false;
		}

		private static void ContinueRotationDrag(Camera cam)
		{
			Plane plane = new Plane(rotationAxis, root.transform.position);
			Ray ray = cam.ScreenPointToRay(Input.mousePosition);
			if (!plane.Raycast(ray, out float enter)) return;

			Vector3 cur = ray.GetPoint(enter);
			Vector3 delta = cur - startMouseWorld;
			if (delta.sqrMagnitude < 0.0001f) return;

			Vector3 camDir = (root.transform.position - cam.transform.position).normalized;
			Vector3 tangent = Vector3.Cross(rotationAxis, camDir);
			if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(rotationAxis, Vector3.up);

			float angle = Vector3.Dot(delta, tangent.normalized) * 40f;
			Quaternion deltaRot = Quaternion.AngleAxis(angle, rotationAxis);
			startRotation = deltaRot * activeView.Rotation;
			activeView.Rotation = startRotation;

			UpdateTransformGizmoVisuals(cam);
			EditorUtil.UpdateViewFrustumMarker(activeView, mapManager);
			startMouseWorld = cur;
		}

		// ===================================================================
		// HOVER (yellow highlight)
		// ===================================================================
		private static void DoHover(Ray ray)
		{
			if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Editor")))
			{
				ResetHover();
				return;
			}

			FaceTag tag = hit.transform.GetComponent<FaceTag>();
			if (tag == null || !hit.collider.transform.IsChildOf(positionHandle.transform))
			{
				ResetHover();
				return;
			}

			foreach (FaceTag f in positionHandle.GetComponentsInChildren<FaceTag>())
			{
				bool sameAxis = Mathf.Abs(Vector3.Dot(f.normal, tag.normal)) > 0.9f;
				Color c = sameAxis ? Color.yellow : new Color(1f, 1f, 1f, 0.55f);
				f.GetComponent<MeshRenderer>().material.color = c;
			}
		}

		private static void ResetHover()
		{
			if (positionHandle == null) return;
			foreach (MeshRenderer mr in positionHandle.GetComponentsInChildren<MeshRenderer>())
			{
				Color baseCol = mr.material.color;
				mr.material.color = new Color(baseCol.r, baseCol.g, baseCol.b, 0.55f);
			}
		}

		// ===================================================================
		// TORUS MESH (your exact code)
		// ===================================================================
		private static GameObject CreateTorus()
		{
			GameObject go = new GameObject();
			go.layer = LayerMask.NameToLayer("Editor");
			MeshFilter mf = go.AddComponent<MeshFilter>();
			go.AddComponent<MeshRenderer>();
			mf.mesh = GenerateTorusMesh();
			return go;
		}

		private static Mesh GenerateTorusMesh(int segments = 48, int sides = 16, float radius = 1f, float tube = 0.1f)
		{
			var mesh = new Mesh { name = "GizmoTorus" };

			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var triangles = new List<int>();

			for (int seg = 0; seg <= segments; seg++)
			{
				float u = seg * Mathf.PI * 2f / segments;
				float cu = Mathf.Cos(u), su = Mathf.Sin(u);

				for (int side = 0; side <= sides; side++)
				{
					float v = side * Mathf.PI * 2f / sides;
					float cv = Mathf.Cos(v), sv = Mathf.Sin(v);

					float r = radius + tube * cv;
					vertices.Add(new Vector3(r * cu, tube * sv, r * su));
					normals.Add(new Vector3(cv * cu, sv, cv * su));
				}
			}

			for (int seg = 0; seg < segments; seg++)
			{
				int base1 = seg * (sides + 1);
				int base2 = (seg + 1) * (sides + 1);

				for (int side = 0; side < sides; side++)
				{
					int a = base1 + side;
					int b = base1 + side + 1;
					int c = base2 + side + 1;
					int d = base2 + side;

					triangles.Add(a); triangles.Add(b); triangles.Add(c);
					triangles.Add(a); triangles.Add(c); triangles.Add(d);
				}
			}

			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}