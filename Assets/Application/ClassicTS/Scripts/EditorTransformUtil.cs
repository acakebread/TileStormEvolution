// EditorTransformUtil.cs
using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;

namespace ClassicTilestorm
{
	public static class EditorTransformUtil
	{
		private static GameObject root;
		private static GameObject positionHandle;
		private static GameObject rotationOrbiter;

		private const float TARGET_SCREEN_SIZE = 60f;

		// Drag state
		private static bool draggingPosition;
		private static Plane dragPlane;
		private static Vector3 dragStartPoint;
		private static Vector3 lockedAxis = Vector3.zero;

		private static bool draggingRotation;
		private static Vector3 rotationAxis;
		private static Quaternion startRotation;
		private static Vector3 startMouseWorld;

		private static bool wasActive = false;

		private class RingTag : MonoBehaviour
		{
			public Vector3 axis;
			public Color originalColor;
		}

		private class FaceTag : MonoBehaviour
		{
			public Vector3 normal;
			public Color originalColor;
		}

		// ===================================================================
		// NEW PURE WORLD-SPACE API
		// ===================================================================

		public static void ShowAt(Vector3 worldPosition, Quaternion worldRotation, Camera editorCamera, bool worldSpace = false)
		{
			Hide();

			root = new GameObject("GIZMO_ROOT");
			root.layer = LayerMask.NameToLayer("Editor");

			positionHandle = CreatePositionHandle(root.transform);
			rotationOrbiter = CreateRotationOrbiter(root.transform);

			root.transform.position = worldPosition;
			root.transform.rotation = worldSpace ? quaternion.identity : worldRotation;

			UpdateVisuals(editorCamera);
		}

		public static void UpdateTransform(Vector3 worldPosition, Quaternion worldRotation, Camera editorCamera, bool worldSpace = false)
		{
			bool needsCreation = (root == null);

			if (needsCreation)
			{
				// First time: create it properly
				ShowAt(worldPosition, worldRotation, editorCamera, worldSpace);
				return;
			}

			// Update existing gizmo without recreating
			root.transform.position = worldPosition;
			root.transform.rotation = worldSpace ? Quaternion.identity : worldRotation;

			// Still update scale to keep it screen-size consistent
			UpdateVisuals(editorCamera);
		}

		public static bool HandleInput(Camera editorCamera, out Vector3 newWorldPosition, out Quaternion newWorldRotation, bool worldSpace = false)
		{
			newWorldPosition = root ? root.transform.position : Vector3.zero;
			newWorldRotation = rotationOrbiter ? rotationOrbiter.transform.rotation : Quaternion.identity;

			if (root == null || editorCamera == null) return false;

			UpdateVisuals(editorCamera);

			Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
			bool handled = false;
			bool _wasActive = wasActive;

			DoHover(ray);

			if (Input.GetMouseButtonDown(0))
			{
				var hits = Physics.RaycastAll(ray, Mathf.Infinity, 1 << LayerMask.NameToLayer("Editor"));
				System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

				RaycastHit? posHit = null;
				RaycastHit? ringHit = null;

				foreach (var h in hits)
				{
					if (h.transform.IsChildOf(positionHandle.transform))
					{
						posHit = h;
						break;
					}
					if (ringHit == null && h.transform.GetComponent<RingTag>() != null)
					{
						ringHit = h;
					}
				}

				if (posHit.HasValue)
				{
					StartPositionDrag(ray, editorCamera, posHit.Value, worldSpace);
					handled = true;
				}
				else if (ringHit.HasValue)
				{
					if (TryStartRotationDrag(ray))
						handled = true;
				}

				wasActive = handled;
			}

			if (Input.GetMouseButton(0))
			{
				if (draggingPosition) ContinuePositionDrag(editorCamera);
				if (draggingRotation) ContinueRotationDrag(editorCamera);
				handled = draggingPosition || draggingRotation;
				wasActive |= handled;
			}

			if (Input.GetMouseButtonUp(0))
			{
				draggingPosition = draggingRotation = false;
				lockedAxis = Vector3.zero;
				wasActive = false;
			}

			if (handled || _wasActive)
			{
				newWorldPosition = root.transform.position;
				newWorldRotation = rotationOrbiter.transform.rotation;
			}

			return handled || _wasActive;
		}

		public static void UpdateVisuals(Camera editorCamera)
		{
			if (root == null || editorCamera == null) return;

			float dist = Vector3.Distance(editorCamera.transform.position, root.transform.position);
			float scale = dist * Mathf.Tan(editorCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;
			scale = TARGET_SCREEN_SIZE * scale / Screen.height;
			root.transform.localScale = Vector3.one * Mathf.Max(scale, 0.01f);
		}

		public static void Hide()
		{
			if (root != null)
				Object.DestroyImmediate(root);

			root = positionHandle = rotationOrbiter = null;
			draggingPosition = draggingRotation = false;
			lockedAxis = Vector3.zero;
			wasActive = false;
		}

		// ===================================================================
		// BACKWARD-COMPATIBLE WRAPPERS (so your existing code compiles unchanged)
		// ===================================================================

		public static void ShowTransformGizmo(View view, IMapManager mgr, Camera cam)
		{
			if (view == null || mgr == null || cam == null) return;

			Vector3 worldPos = mgr.TileWorldPosition(view.tile) + view.Position;
			ShowAt(worldPos, view.Rotation, cam);
		}

		public static void HideTransformGizmo()
		{
			Hide();
		}

		public static void UpdateTransformGizmoVisuals(Camera cam)
		{
			UpdateVisuals(cam);
		}

		public static bool HandleTransformGizmoInput(Camera cam)
		{
			return HandleInput(cam, out _, out _);
		}

		// ===================================================================
		// POSITION HANDLE
		// ===================================================================

		private static GameObject CreatePositionHandle(Transform parent)
		{
			GameObject go = new GameObject("PositionHandle");
			go.layer = LayerMask.NameToLayer("Editor");
			go.transform.SetParent(parent, false);

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

				BoxCollider bc = quad.AddComponent<BoxCollider>();
				bc.size = new Vector3(1f, 1f, 0.02f);

				MeshRenderer mr = quad.GetComponent<MeshRenderer>();
				var shader = Shader.Find("Hidden/URPGizmoSolid");
				mr.material = new Material(shader);
				mr.material.SetColor("_BaseColor", f.Item2);

				Object.DestroyImmediate(quad.GetComponent<Collider>());

				FaceTag tag = quad.AddComponent<FaceTag>();
				tag.normal = f.Item1;
				tag.originalColor = f.Item2;
			}

			return go;
		}

		private static void StartPositionDrag(Ray ray, Camera cam, RaycastHit hit, bool worldSpace = false)
		{
			FaceTag tag = hit.transform.GetComponent<FaceTag>();

			if (tag != null)
			{
				lockedAxis = tag.normal.normalized;
				dragPlane = new Plane(lockedAxis, root.transform.position);
			}
			else
			{
				lockedAxis = Vector3.zero;
				dragPlane = new Plane(worldSpace ? - cam.transform.forward : dragPlane.normal, root.transform.position);
			}

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
				delta -= Vector3.Project(delta, lockedAxis);

			root.transform.position += delta;
			dragStartPoint = cur;
		}

		// ===================================================================
		// ROTATION ORBITER
		// ===================================================================

		private static Vector2 startMouseScreen;
		private static Vector2 startTangentScreen;
		private static float ringWorldRadius;
		private static Vector3 ringStartPointWorld;

		private static GameObject CreateRotationOrbiter(Transform parent)
		{
			GameObject orb = new GameObject("RotationOrbiter");
			orb.layer = LayerMask.NameToLayer("Editor");
			orb.transform.SetParent(parent, false);

			Color[] colors = { new Color(0.3f, 1f, 0.3f), new Color(1f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 1f) };
			Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

			for (int i = 0; i < 3; i++)
			{
				GameObject ring = CreateTorus();
				ring.name = "Ring_" + "XYZ"[i];
				ring.transform.SetParent(orb.transform, false);
				ring.transform.rotation = Quaternion.FromToRotation(new Vector3(axes[i].y, axes[i].z, axes[i].x), axes[i]);

				MeshRenderer mr = ring.GetComponent<MeshRenderer>();
				var shader = Shader.Find("Hidden/URPGizmoSolid");
				mr.material = new Material(shader);
				mr.material.SetColor("_BaseColor", colors[i]);

				ring.AddComponent<MeshCollider>();

				RingTag tag = ring.AddComponent<RingTag>();
				tag.axis = axes[i];
				tag.originalColor = colors[i];
			}

			return orb;
		}

		private static bool TryStartRotationDrag(Ray ray)
		{
			foreach (Transform ring in rotationOrbiter.transform)
			{
				Collider c = ring.GetComponent<Collider>();
				if (c && c.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
				{
					// rotationAxis in world space (ring.up is world-up for the ring)
					rotationAxis = ring.up.normalized;
					startRotation = rotationOrbiter.transform.rotation;

					// Compute plane of the ring (world space)
					Plane ringPlane = new Plane(rotationAxis, root.transform.position);

					// Ray -> plane intersection => an initial mouse world point (on plane)
					if (!ringPlane.Raycast(ray, out float enter)) return false;
					Vector3 mousePlanePoint = ray.GetPoint(enter);

					// Compute vector from center to that clicked point, project to plane to ensure orthogonality
					Vector3 center = root.transform.position;
					Vector3 vecFromCenter = mousePlanePoint - center;
					vecFromCenter -= Vector3.Project(vecFromCenter, rotationAxis); // lie fully in ring plane

					// If click is almost exactly on axis (very small) then fallback to cross with camera up
					if (vecFromCenter.sqrMagnitude < 0.0001f)
					{
						// pick a fallback direction in plane
						vecFromCenter = Vector3.Cross(rotationAxis, (Camera.main ? Camera.main.transform.forward : Vector3.forward));
						vecFromCenter -= Vector3.Project(vecFromCenter, rotationAxis);
						if (vecFromCenter.sqrMagnitude < 0.0001f)
							vecFromCenter = Vector3.right; // last resort
					}

					// Determine ring radius in world space. The torus was generated with radius=1f (local),
					// so world radius is root's lossy scale (assuming uniform scale).
					ringWorldRadius = root.transform.lossyScale.x * 1f;
					// Determine exact point on ring circle nearest to click
					Vector3 ringPoint = center + vecFromCenter.normalized * ringWorldRadius;
					ringStartPointWorld = ringPoint;

					// Tangent at that point (world space) — along the ring circle
					Vector3 tangentWorld = Vector3.Cross(rotationAxis, (ringPoint - center)).normalized;

					// Convert the tangent into screen-space vector
					Vector3 screenP = Camera.current != null ? Camera.current.WorldToScreenPoint(ringPoint) : Camera.main.WorldToScreenPoint(ringPoint);
					Vector3 screenT = Camera.current != null ? Camera.current.WorldToScreenPoint(ringPoint + tangentWorld) : Camera.main.WorldToScreenPoint(ringPoint + tangentWorld);

					startTangentScreen = new Vector2(screenT.x - screenP.x, screenT.y - screenP.y);
					if (startTangentScreen.sqrMagnitude < 0.000001f)
					{
						// fallback: use camera-facing tangent (project camera dir into plane)
						Vector3 camDir = (center - (Camera.current ? Camera.current.transform.position : Camera.main.transform.position)).normalized;
						tangentWorld = Vector3.Cross(rotationAxis, camDir).normalized;
						screenT = (Camera.current ? Camera.current.WorldToScreenPoint(ringPoint + tangentWorld) : Camera.main.WorldToScreenPoint(ringPoint + tangentWorld));
						startTangentScreen = new Vector2(screenT.x - screenP.x, screenT.y - screenP.y);
					}

					// record start mouse screen pos
					startMouseScreen = Input.mousePosition;

					draggingRotation = true;
					return true;
				}
			}
			return false;
		}

		private static void ContinueRotationDrag(Camera cam)
		{
			if (!draggingRotation) return;

			// Mouse delta in screen pixels
			Vector2 curMouse = Input.mousePosition;
			Vector2 mouseDelta = curMouse - startMouseScreen;

			if (mouseDelta.sqrMagnitude < 0.0001f) return;

			// Project mouse delta onto the screen-space tangent that we computed at drag start
			Vector2 tangent = startTangentScreen;
			if (tangent.sqrMagnitude < 0.000001f) return;
			Vector2 tangentN = tangent.normalized;

			// scalar movement along tangent (in pixels)
			float moveAlong = Vector2.Dot(mouseDelta, tangentN);

			// Convert pixels -> angle. Sensitivity chosen so ~100px -> ~90 degrees, tweak if needed.
			float sensitivity = 90f / 100f; // degrees per 100 pixels
			float angle = moveAlong * sensitivity;

			// Apply rotation around rotationAxis (world-space)
			Quaternion deltaRot = Quaternion.AngleAxis(angle, rotationAxis);
			rotationOrbiter.transform.rotation = deltaRot * startRotation;

			// Do not incrementally update startRotation here — keep startRotation fixed at initial drag
			// so angle is always relative to startRotation. If you prefer incremental (smoother for some input),
			// you can update startRotation = rotationOrbiter.transform.rotation; startMouseScreen = curMouse;

			// Optionally: update startMouseScreen/startRotation each frame to treat input as incremental:
			// startRotation = rotationOrbiter.transform.rotation;
			// startMouseScreen = curMouse;
		}

		// ===================================================================
		// HOVER
		// ===================================================================

		private static void DoHover(Ray ray)
		{
			if (draggingPosition || draggingRotation) return;

			var hits = Physics.RaycastAll(ray, Mathf.Infinity, 1 << LayerMask.NameToLayer("Editor"));
			if (hits.Length == 0)
			{
				ResetHover();
				return;
			}

			System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

			RaycastHit? faceHit = null;
			RaycastHit? ringHit = null;

			foreach (var h in hits)
			{
				if (h.transform.GetComponent<FaceTag>() != null && h.transform.IsChildOf(positionHandle.transform))
				{
					faceHit = h;
					break;
				}
				if (ringHit == null && h.transform.GetComponent<RingTag>() != null)
				{
					ringHit = h;
				}
			}

			RaycastHit hit;
			if (faceHit.HasValue) hit = faceHit.Value;
			else if (ringHit.HasValue) hit = ringHit.Value;
			else
			{
				ResetHover();
				return;
			}

			FaceTag faceTag = hit.transform.GetComponent<FaceTag>();
			if (faceTag != null && hit.collider.transform.IsChildOf(positionHandle.transform))
			{
				foreach (FaceTag f in positionHandle.GetComponentsInChildren<FaceTag>())
				{
					bool sameAxis = Mathf.Abs(Vector3.Dot(f.normal, faceTag.normal)) > 0.9f;
					Color c = sameAxis ? Color.yellow : new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f);
					f.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", c);
				}

				foreach (RingTag r in rotationOrbiter.GetComponentsInChildren<RingTag>())
				{
					Color c = new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f);
					r.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", c);
				}
			}
			else
			{
				RingTag ringTag = hit.transform.GetComponent<RingTag>();
				if (ringTag != null)
				{
					foreach (RingTag r in rotationOrbiter.GetComponentsInChildren<RingTag>())
					{
						bool sameAxis = Mathf.Abs(Vector3.Dot(r.axis, ringTag.axis)) > 0.9f;
						Color c = sameAxis ? Color.yellow : new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f);
						r.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", c);
					}

					foreach (FaceTag f in positionHandle.GetComponentsInChildren<FaceTag>())
					{
						Color c = new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f);
						f.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", c);
					}
				}
				else
				{
					ResetHover();
				}
			}
		}

		private static void ResetHover()
		{
			if (positionHandle != null)
			{
				foreach (FaceTag f in positionHandle.GetComponentsInChildren<FaceTag>())
				{
					Color c = new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f);
					f.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", c);
				}
			}

			if (rotationOrbiter != null)
			{
				foreach (RingTag r in rotationOrbiter.GetComponentsInChildren<RingTag>())
				{
					Color c = new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f);
					r.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", c);
				}
			}
		}

		// ===================================================================
		// TORUS MESH
		// ===================================================================

		private static GameObject CreateTorus()
		{
			GameObject go = new GameObject();
			go.layer = LayerMask.NameToLayer("Editor");

			MeshFilter mf = go.AddComponent<MeshFilter>();
			MeshRenderer mr = go.AddComponent<MeshRenderer>();

			mf.mesh = GenerateTorusMesh(segments: 48, sides: 32, radius: 1f, tube: 0.02f);

			var shader = Shader.Find("Hidden/URPGizmoSolid");
			mr.material = new Material(shader);

			MeshCollider mc = go.AddComponent<MeshCollider>();
			mc.sharedMesh = GenerateTorusMesh(segments: 32, sides: 16, radius: 1f, tube: 0.05f);

			return go;
		}

		private static Mesh GenerateTorusMesh(int segments = 48, int sides = 32, float radius = 1f, float tube = 0.02f)
		{
			var mesh = new Mesh { name = "GizmoTorus_Visual" };
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var triangles = new List<int>();

			for (int seg = 0; seg <= segments; seg++)
			{
				float u = seg * Mathf.PI * 2f / segments;
				float cu = Mathf.Cos(u);
				float su = Mathf.Sin(u);

				for (int side = 0; side <= sides; side++)
				{
					float v = side * Mathf.PI * 2f / sides;
					float cv = Mathf.Cos(v);
					float sv = Mathf.Sin(v);

					float r = radius + tube * cv;
					vertices.Add(new Vector3(r * cu, tube * sv, r * su));
					normals.Add(new Vector3(cv * cu, sv, cv * su).normalized);
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
			mesh.Optimize();

			return mesh;
		}
	}
}
