// File: EditorTransformUtil.cs
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class EditorTransformUtil
	{
		private static GameObject root;
		private static GameObject positionHandle;
		private static GameObject rotationOrbiter;
		private static View activeView;
		private static IMapManager mapManager;
		private const float TARGET_SCREEN_SIZE = 90f; // pixels - tweak this to change gizmo size

		// Drag state
		private static bool draggingPosition;
		private static Plane dragPlane;
		private static Vector3 dragStartPoint;
		private static Vector3 lockedAxis = Vector3.zero;
		private static bool draggingRotation;
		private static Transform draggedRing;
		private static Vector3 rotationAxis;
		private static Quaternion startRotation;
		private static Vector3 startMouseWorld;

		private class RingTag : MonoBehaviour
		{
			public Vector3 axis;
			public Color originalColor;
			public Material renderMaterial;
		}

		private class FaceTag : MonoBehaviour
		{
			public Vector3 normal;
			public Color originalColor;
			public Material renderMaterial;
		}

		private class GizmoRenderData : MonoBehaviour
		{
			public Mesh mesh;
			public Material material;
			public Matrix4x4 matrix;

			public void Init(Mesh m, Material mat)
			{
				mesh = m;
				material = mat;
				matrix = transform.localToWorldMatrix;
			}

			void OnDestroy()
			{
				if (material) Object.DestroyImmediate(material);
			}
		}

		// ===================================================================
		// PUBLIC API
		// ===================================================================
		public static void ShowTransformGizmo(View view, IMapManager mgr, Camera cam)
		{
			HideTransformGizmo();
			if (view == null || mgr == null || cam == null) return;

			activeView = view;
			mapManager = mgr;
			root = new GameObject("GIZMO_ROOT") { layer = LayerMask.NameToLayer("Editor") };

			positionHandle = CreatePositionHandle(root.transform);
			rotationOrbiter = CreateRotationOrbiter(root.transform);

			var providerGo = cam.gameObject;
			var provider = providerGo.GetComponent<EditorGizmoCommandProvider>()
				?? providerGo.AddComponent<EditorGizmoCommandProvider>();
			provider.RegisterGizmoRoot(root);

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

			// Compute world-space size of one screen pixel at the gizmo distance and scale the gizmo
			// so it occupies TARGET_SCREEN_SIZE pixels on screen.
			float dist = Vector3.Distance(cam.transform.position, root.transform.position);

			// half-height at distance for perspective camera
			float orthoHalfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
			// full world-space height of the frustum slice at that distance
			float frustumHeightAtDist = orthoHalfHeight * 2f;

			// world units per screen pixel at that distance
			float worldPerPixel = frustumHeightAtDist / (float)Screen.height;

			// final uniform scale to make the gizmo be TARGET_SCREEN_SIZE pixels tall
			float finalScale = worldPerPixel * TARGET_SCREEN_SIZE;

			root.transform.localScale = Vector3.one * finalScale;
		}

		private static Ray GetGizmoOrthoRay(Camera cam)
		{
			// Build the same orthographic extents the renderer uses and create a ray
			// from the screen pixel in that ortho space.

			Vector3 worldPos = root.transform.position;
			float dist = Vector3.Distance(cam.transform.position, worldPos);

			float orthoHalfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
			float orthoHalfWidth = orthoHalfHeight * cam.aspect;

			// Screen -> viewport coords [0..1]
			Vector3 viewport = new Vector3(
				Input.mousePosition.x / (float)Screen.width,
				Input.mousePosition.y / (float)Screen.height,
				0f);

			// Convert viewport -> camera-space X/Y for orthographic camera
			float camX = (viewport.x - 0.5f) * 2f * orthoHalfWidth;
			float camY = (viewport.y - 0.5f) * 2f * orthoHalfHeight;

			// Preserve the same camera-space Z for the gizmo origin
			Matrix4x4 origView = cam.worldToCameraMatrix;
			float camZ = origView.MultiplyPoint(worldPos).z;

			Vector3 camSpacePoint = new Vector3(camX, camY, camZ);

			// Transform back to world space to get ray origin
			Vector3 worldStart = cam.cameraToWorldMatrix.MultiplyPoint(camSpacePoint);

			// For orthographic, ray direction is camera forward
			Vector3 dir = cam.transform.forward;

			return new Ray(worldStart, dir);
		}


		public static bool HandleTransformGizmoInput(Camera cam)
		{
			if (activeView == null || cam == null || root == null) return false;

			UpdateTransformGizmoVisuals(cam);
			Ray ray = GetGizmoOrthoRay(cam);
			bool handled = false;

			DoHover(ray);

			if (Input.GetMouseButtonDown(0))
			{
				if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Editor")))
				{
					if (hit.transform.IsChildOf(positionHandle.transform))
					{
						StartPositionDrag(ray, cam, hit);
						handled = true;
					}
					else if (TryStartRotationDrag(ray))
					{
						handled = true;
					}
				}
			}

			if (Input.GetMouseButton(0))
			{
				if (draggingPosition) ContinuePositionDrag(cam);
				if (draggingRotation) ContinueRotationDrag(cam);
				handled = true;
			}

			if (Input.GetMouseButtonUp(0))
			{
				draggingPosition = draggingRotation = false;
				lockedAxis = Vector3.zero;
			}

			return handled;
		}

		// ===================================================================
		// POSITION HANDLE (6 faces)
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

			var shader = Shader.Find("Hidden/URPGizmoSolid");

			foreach (var f in faces)
			{
				GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
				quad.layer = LayerMask.NameToLayer("Editor");
				quad.transform.SetParent(go.transform, false);
				quad.transform.localScale = Vector3.one * 0.5f;
				quad.transform.localPosition = -f.Item1 * 0.25f;
				quad.transform.rotation = Quaternion.LookRotation(f.Item1);

				// Use box collider for picking
				var bc = quad.AddComponent<BoxCollider>();
				bc.size = new Vector3(1f, 1f, 0.02f);

				var mesh = quad.GetComponent<MeshFilter>().sharedMesh;

				Object.Destroy(quad.GetComponent<MeshRenderer>());
				Object.Destroy(quad.GetComponent<MeshFilter>());
				Object.Destroy(quad);

				GameObject faceGO = new GameObject("Face");
				faceGO.layer = LayerMask.NameToLayer("Editor");
				faceGO.transform.SetParent(go.transform, false);
				faceGO.transform.localPosition = -f.Item1 * 0.25f;
				faceGO.transform.rotation = Quaternion.LookRotation(f.Item1);
				faceGO.transform.localScale = Vector3.one * 0.5f;

				faceGO.AddComponent<BoxCollider>().size = new Vector3(1f, 1f, 0.02f);

				var mat = new Material(shader)
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				mat.SetColor("_BaseColor", f.Item2);

				var renderData = faceGO.AddComponent<GizmoRenderData>();
				renderData.Init(mesh, mat);

				var tag = faceGO.AddComponent<FaceTag>();
				tag.normal = f.Item1;
				tag.originalColor = f.Item2;
				tag.renderMaterial = mat;
			}

			return go;
		}

		// ===================================================================
		// ROTATION RINGS
		// ===================================================================
		private static GameObject CreateRotationOrbiter(Transform parent)
		{
			GameObject orb = new GameObject("RotationOrbiter");
			orb.layer = LayerMask.NameToLayer("Editor");
			orb.transform.SetParent(parent, false);

			Color[] colors = { new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.3f), new Color(0.3f, 0.6f, 1f) };
			Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

			for (int i = 0; i < 3; i++)
			{
				GameObject ring = CreateTorusRing(colors[i], axes[i]);
				ring.name = "Ring_" + "XYZ"[i];
				ring.transform.SetParent(orb.transform, false);
				ring.transform.rotation = Quaternion.FromToRotation(
					new Vector3(axes[i].y, axes[i].z, axes[i].x), axes[i]);

				var tag = ring.GetComponent<RingTag>();
				tag.axis = axes[i];
				tag.originalColor = colors[i];
			}

			return orb;
		}

		private static GameObject CreateTorusRing(Color color, Vector3 axis)
		{
			GameObject go = new GameObject("TorusRing");
			go.layer = LayerMask.NameToLayer("Editor");

			var visualMesh = GenerateTorusMesh(tube: 0.02f, segments: 48, sides: 28);
			var colliderMesh = GenerateTorusMesh(tube: 0.075f, segments: 28, sides: 16);

			var mf = go.AddComponent<MeshFilter>();
			mf.mesh = visualMesh;

			var mr = go.AddComponent<MeshRenderer>();
			mr.enabled = false;

			var mc = go.AddComponent<MeshCollider>();
			mc.sharedMesh = colliderMesh;
			mc.convex = false;

			var mat = new Material(Shader.Find("Hidden/URPGizmoSolid"))
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.55f));

			var renderData = go.AddComponent<GizmoRenderData>();
			renderData.Init(visualMesh, mat);

			var tag = go.AddComponent<RingTag>();
			tag.renderMaterial = mat;

			return go;
		}

		// ===================================================================
		// INPUT & HOVER
		// ===================================================================
		private static void StartPositionDrag(Ray ray, Camera cam, RaycastHit hit)
		{
			var tag = hit.transform.GetComponent<FaceTag>();
			if (tag != null)
			{
				lockedAxis = tag.normal.normalized;
				dragPlane = new Plane(lockedAxis, root.transform.position);
			}
			else
			{
				lockedAxis = Vector3.zero;
				dragPlane = new Plane(-cam.transform.forward, root.transform.position);
			}

			if (dragPlane.Raycast(ray, out float enter))
				dragStartPoint = ray.GetPoint(enter);

			draggingPosition = true;
		}

		private static bool TryStartRotationDrag(Ray ray)
		{
			foreach (Transform ring in rotationOrbiter.transform)
			{
				var c = ring.GetComponent<Collider>();
				if (c && c.Raycast(ray, out _, Mathf.Infinity))
				{
					draggedRing = ring;
					rotationAxis = ring.up;
					startRotation = activeView.Rotation;

					var p = new Plane(rotationAxis, root.transform.position);
					p.Raycast(ray, out float enter);
					startMouseWorld = ray.GetPoint(enter);

					draggingRotation = true;
					return true;
				}
			}
			return false;
		}

		private static void ContinuePositionDrag(Camera cam)
		{
			Ray ray = GetGizmoOrthoRay(cam);
			if (!dragPlane.Raycast(ray, out float enter)) return;

			Vector3 cur = ray.GetPoint(enter);
			Vector3 delta = cur - dragStartPoint;
			if (lockedAxis != Vector3.zero)
				delta -= Vector3.Project(delta, lockedAxis);

			Vector3 newPos = root.transform.position + delta;
			activeView.Position = newPos - mapManager.TileWorldPosition(activeView.tile);
			UpdateTransformGizmoVisuals(cam);
			EditorUtil.UpdateViewFrustumMarker(activeView, mapManager);
			dragStartPoint = cur;
		}

		private static void ContinueRotationDrag(Camera cam)
		{
			var plane = new Plane(rotationAxis, root.transform.position);
			Ray ray = GetGizmoOrthoRay(cam);
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

		private static void DoHover(Ray ray)
		{
			if (draggingPosition || draggingRotation) return;

			if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, 1 << LayerMask.NameToLayer("Editor")))
			{
				ResetHover();
				return;
			}

			var faceTag = hit.transform.GetComponent<FaceTag>();
			if (faceTag != null && hit.transform.IsChildOf(positionHandle.transform))
			{
				foreach (var f in positionHandle.GetComponentsInChildren<FaceTag>())
				{
					bool same = Mathf.Abs(Vector3.Dot(f.normal, faceTag.normal)) > 0.9f;
					Color c = same ? Color.yellow : new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f);
					if (f.renderMaterial) f.renderMaterial.SetColor("_BaseColor", c);
				}

				foreach (var r in rotationOrbiter.GetComponentsInChildren<RingTag>())
				{
					if (r.renderMaterial)
						r.renderMaterial.SetColor("_BaseColor", new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f));
				}
			}
			else
			{
				var ringTag = hit.transform.GetComponent<RingTag>();
				if (ringTag != null)
				{
					foreach (var r in rotationOrbiter.GetComponentsInChildren<RingTag>())
					{
						bool same = Mathf.Abs(Vector3.Dot(r.axis, ringTag.axis)) > 0.9f;
						Color c = same ? Color.yellow : new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f);
						if (r.renderMaterial) r.renderMaterial.SetColor("_BaseColor", c);
					}

					foreach (var f in positionHandle.GetComponentsInChildren<FaceTag>())
					{
						if (f.renderMaterial)
							f.renderMaterial.SetColor("_BaseColor", new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f));
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
				foreach (var f in positionHandle.GetComponentsInChildren<FaceTag>())
					if (f.renderMaterial)
						f.renderMaterial.SetColor("_BaseColor", new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f));
			}

			if (rotationOrbiter != null)
			{
				foreach (var r in rotationOrbiter.GetComponentsInChildren<RingTag>())
					if (r.renderMaterial)
						r.renderMaterial.SetColor("_BaseColor", new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f));
			}
		}

		// ===================================================================
		// TORUS MESH
		// ===================================================================
		private static Mesh GenerateTorusMesh(int segments = 48, int sides = 32, float radius = 1f, float tube = 0.02f)
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
			return mesh;
		}

		// ===================================================================
		// COMMAND BUFFER PROVIDER
		// ===================================================================
		private class EditorGizmoCommandProvider : MonoBehaviour
		{
			private readonly List<GameObject> gizmoRoots = new();

			public void RegisterGizmoRoot(GameObject root)
			{
				if (!gizmoRoots.Contains(root))
					gizmoRoots.Add(root);
			}

			private void OnEnable()
			{
				RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
			}

			private void OnDisable()
			{
				RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
			}

			private void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
			{
				if (gizmoRoots.Count == 0) return;

				var cmd = CommandBufferPool.Get("Editor Gizmo Draw");

				// Save original matrices once
				Matrix4x4 originalView = cam.worldToCameraMatrix;
				Matrix4x4 originalProj = cam.projectionMatrix;

				foreach (var root in gizmoRoots.ToArray())
				{
					if (root == null) continue;

					Vector3 worldPos = root.transform.position;

					// Where the origin lands in viewport (0..1)
					Vector3 viewport = cam.WorldToViewportPoint(worldPos);

					// Camera-space Z for the root
					Vector3 viewSpacePos = cam.worldToCameraMatrix.MultiplyPoint(worldPos);
					float camZ = viewSpacePos.z;

					// Ortho half extents matched to perspective frustum at that distance
					float dist = Vector3.Distance(cam.transform.position, worldPos);
					float orthoHalfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
					float orthoHalfWidth = orthoHalfHeight * cam.aspect;

					// Build ortho projection: centered on camera
					Matrix4x4 orthoProj = Matrix4x4.Ortho(
						-orthoHalfWidth, orthoHalfWidth,
						-orthoHalfHeight, orthoHalfHeight,
						cam.nearClipPlane, cam.farClipPlane);

					// Compute camera-space target that corresponds to the perspective viewport coords
					Vector3 camSpaceTarget = new Vector3(
						(viewport.x - 0.5f) * 2f * orthoHalfWidth,
						(viewport.y - 0.5f) * 2f * orthoHalfHeight,
						camZ);

					// Convert to world to find the world location that should appear at that camera-space point
					Vector3 worldTarget = cam.cameraToWorldMatrix.MultiplyPoint(camSpaceTarget);

					// Offset to apply to the view so the gizmo origin appears at the same screen point
					Vector3 worldOffset = worldTarget - worldPos;

					// Shift view matrix by negative offset (move camera by -offset)
					Matrix4x4 offsetView = originalView * Matrix4x4.Translate(-worldOffset);

					// Set ortho view/proj and draw the whole gizmo as a rigid object
					cmd.SetViewProjectionMatrices(offsetView, orthoProj);

					// Draw
					foreach (var data in root.GetComponentsInChildren<GizmoRenderData>(true))
					{
						if (data.mesh && data.material)
						{
							// localToWorldMatrix includes the scale we set in UpdateTransformGizmoVisuals
							data.matrix = data.transform.localToWorldMatrix;
							cmd.DrawMesh(data.mesh, data.matrix, data.material, 0, 0);
						}
					}

					// Restore original view/proj for safety (next root or other rendering)
					cmd.SetViewProjectionMatrices(originalView, originalProj);
				}

				ctx.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}
		}
	}
}





// File: EditorTransformUtil.cs
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using MassiveHadronLtd; // for MaterialUtils
using UnityEngine.Rendering.Universal;

namespace ClassicTilestorm
{
	public static class EditorTransformUtil
	{
		// --- public-ish state kept as before
		private static GameObject root;
		private static GameObject positionHandle;
		private static GameObject rotationOrbiter;

		// fixed-size target (not used to scale - reference only)
		private const float TARGET_SCREEN_SIZE = 60f;

		// Drag state (kept from your working script)
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

		// rotation drag helpers (from working script)
		private static Vector2 startMouseScreen;
		private static Vector2 startTangentScreen;
		private static float ringWorldRadius;
		private static Vector3 ringStartPointWorld;

		// --- URP / Command-buffer overlay pieces (new)
		private static Camera orthoCam = null; // private static ortho camera (just-in-time)
		private static CommandBuffer cmd = null;
		private static bool commandBufferRegistered = false;

		// Keep a lightweight registry of drawable pieces so we don't rely on MeshRenderers.
		private class DrawItem
		{
			public Mesh mesh;
			public Material material;
			public Matrix4x4 localToWorld;
			// Transform reference for convenience (may be null)
			public Transform transformRef;
		}
		private static readonly List<DrawItem> drawItems = new List<DrawItem>();

		// materials cache
		private static Material faceMaterialTemplate;
		private static Material ringMaterialTemplate;

		// ===================================================================
		// PUBLIC API (based on your working script)
		// ===================================================================
		public static void ShowAt(Vector3 worldPosition, Quaternion worldRotation, Camera editorCamera, bool worldSpace = false)
		{
			Hide();

			// Create root and gizmo as per working script
			root = new GameObject("GIZMO_ROOT");
			root.layer = LayerMask.NameToLayer("Editor");
			positionHandle = CreatePositionHandle(root.transform);
			rotationOrbiter = CreateRotationOrbiter(root.transform);

			root.transform.position = worldPosition;
			root.transform.rotation = worldSpace ? Quaternion.identity : worldRotation;

			// Create ortho camera + command buffer (just-in-time)
			EnsureOrthoRendering(editorCamera);

			// Build draw item registry from created GameObjects and disable MeshRenderer components
			BuildDrawRegistry();

			// Update visuals if needed (kept from working script)
			UpdateVisuals(editorCamera);
		}

		public static void UpdateTransform(Vector3 worldPosition, Quaternion worldRotation, Camera editorCamera, bool worldSpace = false)
		{
			bool needsCreation = (root == null);

			if (needsCreation)
			{
				ShowAt(worldPosition, worldRotation, editorCamera, worldSpace);
				return;
			}

			root.transform.position = worldPosition;
			root.transform.rotation = worldSpace ? Quaternion.identity : worldRotation;

			UpdateVisuals(editorCamera);
		}

		/// <summary>
		/// HandleInput: uses manual hit testing (no Physics.Raycast) but otherwise mirrors your working logic.
		/// </summary>
		public static bool HandleInput(Camera editorCamera, out Vector3 newWorldPosition, out Quaternion newWorldRotation, bool worldSpace = false)
		{
			newWorldPosition = root ? root.transform.position : Vector3.zero;
			newWorldRotation = rotationOrbiter ? rotationOrbiter.transform.rotation : Quaternion.identity;

			if (root == null || editorCamera == null) return false;

			UpdateVisuals(editorCamera);

			// Build a perspective ray for input hits (we use perspective ray for picking)
			Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
			bool handled = false;
			bool _wasActive = wasActive;

			// Hover uses manual pick
			DoHover(ray);

			if (Input.GetMouseButtonDown(0))
			{
				// Manual "raycast all" by checking faces first then rings (closest-first)
				RaycastHitCandidate[] hits = CollectManualHits(ray, editorCamera);
				System.Array.Sort(hits, (a, b) => a.dist.CompareTo(b.dist));

				RaycastHitCandidate? posHit = null;
				RaycastHitCandidate? ringHit = null;

				foreach (var h in hits)
				{
					if (h.isFace)
					{
						posHit = h;
						break;
					}
					if (!h.isFace && ringHit == null)
					{
						ringHit = h;
					}
				}

				if (posHit.HasValue)
				{
					// Convert candidate to a RaycastHit-like parameters for StartPositionDrag
					StartPositionDragFromCandidate(posHit.Value, editorCamera, worldSpace);
					handled = true;
				}
				else if (ringHit.HasValue)
				{
					if (TryStartRotationDragFromCandidate(ringHit.Value, editorCamera))
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

			// Per your earlier working script: keep a screen-size consistent scale
			float dist = Vector3.Distance(editorCamera.transform.position, root.transform.position);
			float scale = dist * Mathf.Tan(editorCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;
			scale = TARGET_SCREEN_SIZE * scale / Screen.height;
			root.transform.localScale = Vector3.one * Mathf.Max(scale, 0.01f);

			// Update ortho camera matrices for command buffer draw
			UpdateOrthoCamFor(editorCamera);
		}

		public static void Hide()
		{
			// Destroy root & children
			if (root != null)
				Object.DestroyImmediate(root);
			root = positionHandle = rotationOrbiter = null;

			// Clear drawing registry
			drawItems.Clear();

			// Release materials we created
			if (faceMaterialTemplate != null) Object.DestroyImmediate(faceMaterialTemplate);
			if (ringMaterialTemplate != null) Object.DestroyImmediate(ringMaterialTemplate);
			faceMaterialTemplate = ringMaterialTemplate = null;

			// Unregister command buffer and destroy ortho camera
			TeardownOrthoRendering();

			draggingPosition = draggingRotation = false;
			lockedAxis = Vector3.zero;
			wasActive = false;
		}

		// BACKWARD-COMPATIBILITY WRAPPERS (so existing code compiles unchanged)
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
		// -- Manual hit-test utilities (no Physics) --
		// ===================================================================

		// A lightweight candidate describing a manual hit
		private struct RaycastHitCandidate
		{
			public bool isFace;      // true = face quad, false = ring
			public Transform transform; // transform of the element
			public Vector3 worldPoint; // hit point on element plane
			public float dist;       // distance from ray origin
			public int faceIndex;    // for faces: which face (0..5)
			public int ringIndex;    // for rings: which ring (0..2)
		}

		// Collect manual hits: faces (quads) and rings (torus approximated as circle)
		private static RaycastHitCandidate[] CollectManualHits(Ray ray, Camera cam)
		{
			var list = new List<RaycastHitCandidate>();

			// 1) Faces: each child quad of positionHandle
			if (positionHandle != null)
			{
				int faceIdx = 0;
				foreach (Transform child in positionHandle.transform)
				{
					// each face is a quad placed with local position/rotation/scale
					var t = child;
					// Build plane
					Vector3 normal = t.forward; // quad faces are placed looking at direction
					var plane = new Plane(normal, t.position);
					if (plane.Raycast(ray, out float enter))
					{
						Vector3 p = ray.GetPoint(enter);
						// transform hit into local space of quad to check bounds
						Vector3 local = t.InverseTransformPoint(p);
						// quad local size used earlier: scale (0.5) and quad mesh is 1x1 centered; so extents 0.5
						if (Mathf.Abs(local.x) <= 0.5f + 1e-6f && Mathf.Abs(local.y) <= 0.5f + 1e-6f)
						{
							list.Add(new RaycastHitCandidate
							{
								isFace = true,
								transform = t,
								worldPoint = p,
								dist = Vector3.Distance(ray.origin, p),
								faceIndex = faceIdx
							});
						}
					}
					faceIdx++;
				}
			}

			// 2) Rings: approximate torus by circle center=root.position, radius=ringWorldRadius, detection thickness = ring visual tube*1.2
			if (rotationOrbiter != null)
			{
				int ringIdx = 0;
				foreach (Transform ring in rotationOrbiter.transform)
				{
					Vector3 axis = ring.transform.up.normalized; // ring axis in world space
					var ringPlane = new Plane(axis, root.transform.position);
					// intersect input ray with ring plane (perspective ray is fine)
					if (!ringPlane.Raycast(ray, out float enter)) { ringIdx++; continue; }
					Vector3 planePoint = ray.GetPoint(enter);
					// project vector from center to planePoint into ring-plane
					Vector3 center = root.transform.position;
					Vector3 v = planePoint - center;
					v -= Vector3.Project(v, axis);
					// world radius: torus uses local radius=1 and root.localScale as earlier
					float worldRadius = root.transform.lossyScale.x * 1f;
					float distanceToRing = Mathf.Abs(v.magnitude - worldRadius);

					// pick threshold: use the torus collider tube size used when generating (approx 0.05..0.08)
					// we used tubes like 0.05..0.075 when creating colliders in your original script
					float hitThreshold = 0.08f * root.transform.lossyScale.x; // tweak if needed

					if (distanceToRing <= hitThreshold)
					{
						Vector3 ringPoint = center + (v.normalized * worldRadius);
						list.Add(new RaycastHitCandidate
						{
							isFace = false,
							transform = ring,
							worldPoint = ringPoint,
							dist = Vector3.Distance(ray.origin, ray.GetPoint(enter)),
							ringIndex = ringIdx
						});
					}
					ringIdx++;
				}
			}

			return list.ToArray();
		}

		// Helper to begin position drag from candidate
		private static void StartPositionDragFromCandidate(RaycastHitCandidate candidate, Camera cam, bool worldSpace = false)
		{
			FaceTag tag = candidate.transform.GetComponent<FaceTag>();
			if (tag != null)
			{
				lockedAxis = tag.normal.normalized;
				dragPlane = new Plane(lockedAxis, root.transform.position);
			}
			else
			{
				lockedAxis = Vector3.zero;
				dragPlane = new Plane(-cam.transform.forward, root.transform.position);
			}

			// compute dragStartPoint using the ray-plane intersection (we already have worldPoint)
			dragStartPoint = candidate.worldPoint;
			draggingPosition = true;
		}

		// TryStartRotationDrag from candidate -- compute precise tangent etc.
		private static bool TryStartRotationDragFromCandidate(RaycastHitCandidate candidate, Camera cam)
		{
			if (rotationOrbiter == null) return false;
			Transform ring = candidate.transform;
			if (ring == null) return false;

			// set ring state
			rotationAxis = ring.transform.up.normalized;
			startRotation = rotationOrbiter.transform.rotation;

			// Use candidate.worldPoint as point on ring plane — refine to exact ring circle point
			Vector3 center = root.transform.position;
			Vector3 vecFromCenter = candidate.worldPoint - center;
			vecFromCenter -= Vector3.Project(vecFromCenter, rotationAxis);

			if (vecFromCenter.sqrMagnitude < 1e-6f)
			{
				Vector3 camDir = (center - cam.transform.position).normalized;
				vecFromCenter = Vector3.Cross(rotationAxis, camDir);
				vecFromCenter -= Vector3.Project(vecFromCenter, rotationAxis);
				if (vecFromCenter.sqrMagnitude < 1e-6f) vecFromCenter = Vector3.right;
			}

			ringWorldRadius = root.transform.lossyScale.x * 1f;
			Vector3 ringPoint = center + vecFromCenter.normalized * ringWorldRadius;
			ringStartPointWorld = ringPoint;

			Vector3 tangentWorld = Vector3.Cross(rotationAxis, (ringPoint - center)).normalized;

			// use perspective camera to compute screen-space tangent
			Vector3 screenP = cam.WorldToScreenPoint(ringPoint);
			Vector3 screenT = cam.WorldToScreenPoint(ringPoint + tangentWorld);
			startTangentScreen = new Vector2(screenT.x - screenP.x, screenT.y - screenP.y);
			if (startTangentScreen.sqrMagnitude < 1e-6f)
			{
				Vector3 camDir2 = (center - cam.transform.position).normalized;
				tangentWorld = Vector3.Cross(rotationAxis, camDir2).normalized;
				screenT = cam.WorldToScreenPoint(ringPoint + tangentWorld);
				startTangentScreen = new Vector2(screenT.x - screenP.x, screenT.y - screenP.y);
			}

			startMouseScreen = Input.mousePosition;
			draggingRotation = true;
			return true;
		}

		// ===================================================================
		// POSITION & ROTATION continuation (kept from working script)
		// ===================================================================
		private static void ContinuePositionDrag(Camera cam)
		{
			// Use perspective-screen ray for moving because we use world-space plane intersections
			Ray ray = cam.ScreenPointToRay(Input.mousePosition);
			if (!dragPlane.Raycast(ray, out float enter)) return;

			Vector3 cur = ray.GetPoint(enter);
			Vector3 delta = cur - dragStartPoint;
			if (lockedAxis != Vector3.zero)
				delta -= Vector3.Project(delta, lockedAxis);

			root.transform.position += delta;
			dragStartPoint = cur;
		}

		private static void ContinueRotationDrag(Camera cam)
		{
			if (!draggingRotation) return;

			Vector2 curMouse = Input.mousePosition;
			Vector2 mouseDelta = curMouse - startMouseScreen;
			if (mouseDelta.sqrMagnitude < 1e-6f) return;

			Vector2 tangent = startTangentScreen;
			if (tangent.sqrMagnitude < 1e-6f) return;
			Vector2 tangentN = tangent.normalized;

			float moveAlong = Vector2.Dot(mouseDelta, tangentN);

			// sensitivity (degrees per pixel). Tweak if needed.
			const float ROTATION_DEGREES_PER_PIXEL = 0.5f;
			float angle = moveAlong * ROTATION_DEGREES_PER_PIXEL;

			Quaternion deltaRot = Quaternion.AngleAxis(angle, rotationAxis);
			rotationOrbiter.transform.rotation = deltaRot * startRotation;
		}

		// ===================================================================
		// HOVER (uses manual hit tests)
		// ===================================================================
		private static void DoHover(Ray ray)
		{
			if (draggingPosition || draggingRotation) return;

			var hits = CollectManualHits(ray, Camera.main); // perspective camera used for hover / screen-space mapping
			if (hits.Length == 0)
			{
				ResetHover();
				return;
			}

			System.Array.Sort(hits, (a, b) => a.dist.CompareTo(b.dist));

			RaycastHitCandidate hit = hits[0];

			// If a face
			if (hit.isFace && hit.transform.IsChildOf(positionHandle.transform))
			{
				FaceTag faceTag = hit.transform.GetComponent<FaceTag>();
				if (faceTag != null)
				{
					foreach (FaceTag f in positionHandle.GetComponentsInChildren<FaceTag>())
					{
						bool sameAxis = Mathf.Abs(Vector3.Dot(f.normal, faceTag.normal)) > 0.9f;
						Color c = sameAxis ? Color.yellow : new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f);
						var mr = f.GetComponent<MeshRenderer>();
						if (mr != null) mr.material.SetColor("_BaseColor", c);
					}

					foreach (RingTag r in rotationOrbiter.GetComponentsInChildren<RingTag>())
					{
						Color c = new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f);
						var mr = r.GetComponent<MeshRenderer>();
						if (mr != null) mr.material.SetColor("_BaseColor", c);
					}
					return;
				}
			}

			// Else ring
			{
				RingTag ringTag = hit.transform.GetComponent<RingTag>();
				if (ringTag != null)
				{
					foreach (RingTag r in rotationOrbiter.GetComponentsInChildren<RingTag>())
					{
						bool sameAxis = Mathf.Abs(Vector3.Dot(r.axis, ringTag.axis)) > 0.9f;
						Color c = sameAxis ? Color.yellow : new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f);
						var mr = r.GetComponent<MeshRenderer>();
						if (mr != null) mr.material.SetColor("_BaseColor", c);
					}

					foreach (FaceTag f in positionHandle.GetComponentsInChildren<FaceTag>())
					{
						Color c = new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f);
						var mr = f.GetComponent<MeshRenderer>();
						if (mr != null) mr.material.SetColor("_BaseColor", c);
					}
					return;
				}
			}

			ResetHover();
		}

		private static void ResetHover()
		{
			if (positionHandle != null)
			{
				foreach (FaceTag f in positionHandle.GetComponentsInChildren<FaceTag>())
				{
					Color c = new Color(f.originalColor.r, f.originalColor.g, f.originalColor.b, 0.55f);
					var mr = f.GetComponent<MeshRenderer>();
					if (mr != null) mr.material.SetColor("_BaseColor", c);
				}
			}

			if (rotationOrbiter != null)
			{
				foreach (RingTag r in rotationOrbiter.GetComponentsInChildren<RingTag>())
				{
					Color c = new Color(r.originalColor.r, r.originalColor.g, r.originalColor.b, 0.55f);
					var mr = r.GetComponent<MeshRenderer>();
					if (mr != null) mr.material.SetColor("_BaseColor", c);
				}
			}
		}

		// ===================================================================
		// TORUS MESH helpers (from your working script)
		// ===================================================================
		private static GameObject CreateTorus()
		{
			GameObject go = new GameObject();
			go.layer = LayerMask.NameToLayer("Editor");

			MeshFilter mf = go.AddComponent<MeshFilter>();
			MeshRenderer mr = go.AddComponent<MeshRenderer>();

			mf.mesh = GenerateTorusMesh(segments: 48, sides: 32, radius: 1f, tube: 0.02f);

			var shader = Shader.Find("Hidden/URPGizmoSolid");
			// fallback to URP unlit if not found
			if (shader == null) mr.material = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.25f, 0.25f, 0.25f, 0.8f));
			else mr.material = new Material(shader);

			MeshCollider mc = go.AddComponent<MeshCollider>();
			mc.sharedMesh = GenerateTorusMesh(segments: 32, sides: 16, radius: 1f, tube: 0.05f);
			// keep collider but note we're using manual tests instead of physics queries

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
			return mesh;
		}

		// ===================================================================
		// POSITION HANDLE (adapted from working script)
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
				if (shader == null) mr.material = MaterialUtils.CreateTransparentUnlitMaterial(f.Item2);
				else mr.material = new Material(shader);
				mr.material.SetColor("_BaseColor", f.Item2);

				Object.DestroyImmediate(quad.GetComponent<Collider>());

				FaceTag tag = quad.AddComponent<FaceTag>();
				tag.normal = f.Item1;
				tag.originalColor = f.Item2;
			}

			return go;
		}

		// ===================================================================
		// ROTATION ORBITER (adapted)
		// ===================================================================
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
				// use MaterialUtils if available; otherwise the default material was set in CreateTorus
				if (mr != null)
				{
					if (faceMaterialTemplate == null)
						faceMaterialTemplate = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.25f, 0.25f, 0.25f, 0.55f));
					mr.material.SetColor("_BaseColor", colors[i]);
				}

				// keep MeshCollider for convenience but not used for picking
				// ring.AddComponent<MeshCollider>();

				RingTag tag = ring.AddComponent<RingTag>();
				tag.axis = axes[i];
				tag.originalColor = colors[i];
			}

			return orb;
		}

		// ===================================================================
		// --- Draw registry builder (disables MeshRenderers) ---
		// ===================================================================
		private static void BuildDrawRegistry()
		{
			drawItems.Clear();

			// Faces (the quads under positionHandle)
			if (positionHandle != null)
			{
				foreach (Transform face in positionHandle.transform)
				{
					var mf = face.GetComponent<MeshFilter>();
					var mr = face.GetComponent<MeshRenderer>();
					if (mf != null && mf.sharedMesh != null)
					{
						var mat = (mr != null && mr.sharedMaterial != null) ? new Material(mr.sharedMaterial) : MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.8f, 0.8f, 0.8f, 0.6f));
						// ensure hideflags so it's not shown in editor
						mat.hideFlags = HideFlags.HideAndDontSave;
						drawItems.Add(new DrawItem { mesh = mf.sharedMesh, material = mat, transformRef = face });
						// disable mesh renderer so perspective camera doesn't draw it
						if (mr != null) mr.enabled = false;
					}
				}
			}

			// Rings (torus meshes)
			if (rotationOrbiter != null)
			{
				foreach (Transform ring in rotationOrbiter.transform)
				{
					var mf = ring.GetComponent<MeshFilter>();
					var mr = ring.GetComponent<MeshRenderer>();
					if (mf != null && mf.sharedMesh != null)
					{
						Material mat;
						if (mr != null && mr.sharedMaterial != null)
							mat = new Material(mr.sharedMaterial);
						else
							mat = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.5f, 0.5f, 0.9f, 0.6f));
						mat.hideFlags = HideFlags.HideAndDontSave;
						drawItems.Add(new DrawItem { mesh = mf.sharedMesh, material = mat, transformRef = ring });
						if (mr != null) mr.enabled = false;
					}
				}
			}
		}

		// ===================================================================
		// --- Ortho camera & command buffer management (URP drawmesh) ---
		// ===================================================================
		private static void EnsureOrthoRendering(Camera editorCamera)
		{
			// create a hidden camera GameObject to hold a Camera for matrix helper only (not used to render)
			if (orthoCam == null)
			{
				GameObject go = new GameObject("EditorGizmo_OrthoCam");
				go.hideFlags = HideFlags.HideAndDontSave;
				orthoCam = go.AddComponent<Camera>();
				orthoCam.enabled = false;
				orthoCam.cullingMask = 0; // nothing, we use command buffer draw
				orthoCam.clearFlags = CameraClearFlags.Nothing;
				orthoCam.nearClipPlane = editorCamera.nearClipPlane;
				orthoCam.farClipPlane = editorCamera.farClipPlane;
			}

			// create command buffer
			if (cmd == null)
			{
				cmd = new CommandBuffer { name = "Editor Gizmo DrawMesh URP" };
			}

			// register callback if not yet
			if (!commandBufferRegistered)
			{
				RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
				commandBufferRegistered = true;
			}
		}

		private static void TeardownOrthoRendering()
		{
			// unregister
			if (commandBufferRegistered)
			{
				RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
				commandBufferRegistered = false;
			}

			if (cmd != null)
			{
				cmd.Clear();
				cmd.Release();
				cmd = null;
			}

			if (orthoCam != null)
			{
				Object.DestroyImmediate(orthoCam.gameObject);
				orthoCam = null;
			}
		}

		// Compute view & projection matrices matching the aborted orthographic approach and set ortho cam fields
		private static void UpdateOrthoCamFor(Camera cam)
		{
			if (orthoCam == null || root == null || cam == null) return;

			Vector3 worldPos = root.transform.position;
			// distance to camera
			float dist = Vector3.Distance(cam.transform.position, worldPos);

			// compute ortho half sizes matching perspective frustum at that distance
			float orthoHalfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
			float orthoHalfWidth = orthoHalfHeight * cam.aspect;

			// viewport coords of gizmo origin
			Vector3 viewport = cam.WorldToViewportPoint(worldPos);
			// camera-space z for gizmo origin
			float camZ = cam.worldToCameraMatrix.MultiplyPoint(worldPos).z;

			// cam-space target inside orthographic frustum
			Vector3 camSpaceTarget = new Vector3(
				(viewport.x - 0.5f) * 2f * orthoHalfWidth,
				(viewport.y - 0.5f) * 2f * orthoHalfHeight,
				camZ);

			Vector3 worldTarget = cam.cameraToWorldMatrix.MultiplyPoint(camSpaceTarget);
			Vector3 worldOffset = worldTarget - worldPos;

			// Build ortho matrices
			Matrix4x4 originalView = cam.worldToCameraMatrix;
			Matrix4x4 originalProj = cam.projectionMatrix;

			Matrix4x4 orthoProj = Matrix4x4.Ortho(
				-orthoHalfWidth, orthoHalfWidth,
				-orthoHalfHeight, orthoHalfHeight,
				cam.nearClipPlane, cam.farClipPlane);

			// offset view: move camera by -worldOffset
			Matrix4x4 offsetView = originalView * Matrix4x4.Translate(-worldOffset);

			// store into orthoCam for possible debugging, not used for rendering directly
			orthoCam.worldToCameraMatrix = offsetView;
			orthoCam.projectionMatrix = orthoProj;
			// keep orthoCam position/rotation consistent for convenience (not required)
			orthoCam.transform.position = cam.transform.position + -worldOffset; // approximation
			orthoCam.transform.rotation = cam.transform.rotation;
		}

		// The URP render callback. We draw during endCameraRendering so the overlay appears last.
		private static void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
		{
			// only draw for the editor camera that requested it (safe to draw for all; we early out if no root)
			if (root == null || drawItems.Count == 0) return;
			if (cmd == null) cmd = new CommandBuffer { name = "Editor Gizmo DrawMesh URP" };
			else cmd.Clear();

			// Use the same math as UpdateOrthoCamFor to compute ortho proj & offset view
			Vector3 worldPos = root.transform.position;
			float dist = Vector3.Distance(cam.transform.position, worldPos);
			float orthoHalfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
			float orthoHalfWidth = orthoHalfHeight * cam.aspect;
			Vector3 viewport = cam.WorldToViewportPoint(worldPos);
			Vector3 viewSpacePos = cam.worldToCameraMatrix.MultiplyPoint(worldPos);
			float camZ = viewSpacePos.z;

			Vector3 camSpaceTarget = new Vector3(
				(viewport.x - 0.5f) * 2f * orthoHalfWidth,
				(viewport.y - 0.5f) * 2f * orthoHalfHeight,
				camZ);

			Vector3 worldTarget = cam.cameraToWorldMatrix.MultiplyPoint(camSpaceTarget);
			Vector3 worldOffset = worldTarget - worldPos;

			Matrix4x4 originalView = cam.worldToCameraMatrix;
			Matrix4x4 originalProj = cam.projectionMatrix;

			Matrix4x4 orthoProj = Matrix4x4.Ortho(
				-orthoHalfWidth, orthoHalfWidth,
				-orthoHalfHeight, orthoHalfHeight,
				cam.nearClipPlane, cam.farClipPlane);

			Matrix4x4 offsetView = originalView * Matrix4x4.Translate(-worldOffset);

			// set ortho matrices
			cmd.SetViewProjectionMatrices(offsetView, orthoProj);

			// draw all drawItems
			foreach (var di in drawItems)
			{
				if (di.mesh == null || di.material == null) continue;
				// compute world matrix from transformRef (if provided) or from stored localToWorld
				Matrix4x4 m = (di.transformRef != null) ? di.transformRef.localToWorldMatrix : di.localToWorld;
				cmd.DrawMesh(di.mesh, m, di.material, 0, 0);
			}

			// restore
			cmd.SetViewProjectionMatrices(originalView, originalProj);

			ctx.ExecuteCommandBuffer(cmd);
		}
	}
}
