using UnityEngine;
using MassiveHadronLtd;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class EditorUtil
	{
		//ghost tile system
		private static GameObject ghostTile;
		private static Material ghostMaterial;        // default valid color (white 0.5 alpha)
		private static Material ghostMaterialInvalid; // invalid color (red 0.5 alpha)

		// Initialize the ghost materials
		public static void InitializeGhostMaterial()
		{
			if (ghostMaterial == null)
			{
				ghostMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(1f, 1f, 1f, 0.5f));
				if (ghostMaterial == null)
					Debug.LogError("GeometryUtil: Failed to create transparent unlit material.");
			}

			if (ghostMaterialInvalid == null)
			{
				ghostMaterialInvalid = MaterialUtils.CreateTransparentUnlitMaterial(new Color(1f, 0f, 0f, 0.5f));
				if (ghostMaterialInvalid == null)
					Debug.LogError("GeometryUtil: Failed to create invalid ghost material.");
			}
		}

		// Update or create the ghost tile at the mouse position
		public static void UpdateGhostTile(Camera camera, IMapManager mapManager, Definition definition)
		{
			if (mapManager == null || definition == null) return;
			InitializeGhostMaterial();

			// Create ghost tile if needed (or if definition changed)
			if (ghostTile == null)
			{
				ghostTile = GeometryManager.InstantiateTile(definition, mapManager.CurrentTransform.parent, Vector3.zero, false);
				if (ghostTile != null)
				{
					// Remove TextureSetAnimator
					foreach (var anim in ghostTile.GetComponentsInChildren<TextureSetAnimator>())
						anim.enabled = false;

					// Remove colliders to prevent raycast interference
					foreach (var collider in ghostTile.GetComponentsInChildren<Collider>())
						Object.Destroy(collider);

					// Remove MorphGeomSway
					foreach (var sway in ghostTile.GetComponentsInChildren<MorphGeomSway>())
						Object.Destroy(sway);

					ghostTile.name = "GhostTile";
				}
			}

			if (camera == null || ghostTile == null) return;

			// Update position
			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			ghostTile.transform.position = mapManager.SnappedMapPosition(worldPos);

			// === ToDo IMPLEMENTED ===
			var mapIndex = mapManager.WorldToMapIndex(worldPos);

			bool isValid = mapIndex != -1; // add extra checks here if your game has more rules
										   // e.g. && !mapManager.IsCellOccupied(mapIndex)

			// Switch material based on validity
			Material targetMaterial = isValid ? ghostMaterial : ghostMaterialInvalid;

			foreach (var renderer in ghostTile.GetComponentsInChildren<MeshRenderer>())
			{
				renderer.material = targetMaterial;
			}

			// Make sure ghost is visible
			ghostTile.SetActive(true);
		}

		// Hide the ghost tile
		public static void HideGhostTile()
		{
			if (ghostTile != null)
				ghostTile.SetActive(false);
		}

		// Clean up the ghost tile
		public static void DestroyGhostTile()
		{
			if (ghostTile != null)
			{
				Object.Destroy(ghostTile);
				ghostTile = null;
			}
		}

		// === UNIFIED MARKER SYSTEM ===
		private class MarkerPulse : MonoBehaviour
		{
			public float intensity = 1.5f;
			public float speed = 2.5f;

			private Vector3 baseScale;

			private void Awake()
			{
				baseScale = transform.localScale;
			}

			private void Update()
			{
				float pulse = 0.75f + (Mathf.Sin(Time.time * speed) * 0.25f + 0.25f) * (intensity - 1f);
				transform.localScale = baseScale * pulse;
			}
		}

		private static readonly List<GameObject> mapMarkers = new();
		private static GameObject markerCursor;
		private static Material markerCursorMaterial;

		public enum MarkerType
		{
			Undefined,
			Waypoint,
			Attachment,
			Custom // for future use
		}

		public static void InitializeMarkerMaterials()
		{
			if (markerCursorMaterial == null)
				markerCursorMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(1f, 1f, 0f, 0.8f)); // Yellow pulsing cursor
		}

		public static void UpdateMapMarkers(IMapManager mapManager, int[] tiles, int selectedIndex = -1, MarkerType type = MarkerType.Undefined)
		{
			ClearMapMarkers();

			if (tiles == null || tiles.Length == 0) return;

			for (int i = 0; i < tiles.Length; i++)
			{
				int tile = tiles[i];
				if (tile < 0 || tile >= mapManager.Count) continue;

				Vector3 pos = mapManager.TileWorldPosition(tile) + Vector3.up * 0.02f;

				var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				go.name = $"GIZMO_MARKER_{type}_{tile}";
				go.layer = LayerMask.NameToLayer("Editor");
				go.transform.position = pos;
				go.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

				var mr = go.GetComponent<MeshRenderer>();
				var col = go.GetComponent<Collider>();
				if (col) col.isTrigger = true;

				// Base color logic
				Color baseColor = type switch
				{
					MarkerType.Waypoint => new Color(0f, 0.7f, 1f, 0.7f),     // Blue
					MarkerType.Attachment => new Color(0f, 0.7f, 1f, 0.7f),   // Same blue (or change if desired)
					_ => new Color(0f, 0.7f, 1f, 0.7f)//undefined
				};

				// Special cases
				bool hasView = type == MarkerType.Waypoint && mapManager.GetView(tile) != null;
				if (hasView)
					baseColor = new Color(0f, 1f, 1f, 0.5f); // Cyan for camera waypoints

				if (i == selectedIndex)
				{
					// Selected: bright green + pulsing
					mr.material = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 1f, 0f, 0.7f));
					var pulse = go.AddComponent<MarkerPulse>();
					pulse.intensity = 2.1f;
					pulse.speed = 3.2f;
				}
				else
				{
					mr.material = MaterialUtils.CreateTransparentUnlitMaterial(baseColor);
				}

				mapMarkers.Add(go);
			}
		}

		public static void ClearMapMarkers()
		{
			foreach (var m in mapMarkers)
				if (m) Object.DestroyImmediate(m);
			mapMarkers.Clear();
		}

		public static void DestroyMarkerVisuals()
		{
			ClearMapMarkers();
			if (markerCursor) Object.DestroyImmediate(markerCursor);
			markerCursor = null;
		}

		// === VIEW FRUSTUM MARKER ===
		private static GameObject viewFrustumMarker;

		//private static Mesh CreateViewFrustumMesh(float distance)
		//{
		//	const float GameFOV = 20f;//temporarily fixed to 20 until the property can be stored with the View class
		//	const float Near = 0.5f;
		//	float Far = Mathf.Max(distance, Near + 0.1f);

		//	float aspect = 16f / 9f;
		//	float halfFov = GameFOV * 0.5f * Mathf.Deg2Rad;
		//	float t = Mathf.Tan(halfFov);

		//	float nh = Near * t * 2f;
		//	float nw = nh * aspect;
		//	float fh = Far * t * 2f;
		//	float fw = fh * aspect;

		//	Vector3[] near = {
		//new(-nw/2, -nh/2, Near),  // 0 bottom-left
		//      new( nw/2, -nh/2, Near),  // 1 bottom-right
		//      new( nw/2,  nh/2, Near),  // 2 top-right
		//      new(-nw/2,  nh/2, Near)   // 3 top-left
		//  };

		//	Vector3[] far = {
		//new(-fw/2, -fh/2, Far),   // 4
		//      new( fw/2, -fh/2, Far),   // 5
		//      new( fw/2,  fh/2, Far),   // 6
		//      new(-fw/2,  fh/2, Far)    // 7
		//  };

		//	var mesh = new Mesh();
		//	var verts = new List<Vector3>();
		//	verts.AddRange(near);
		//	verts.AddRange(far);

		//	var sideTris = new List<int>(); // submesh 0 → LEFT + RIGHT only
		//	var capTris = new List<int>(); // submesh 1 → TOP + BOTTOM (slanted + caps)

		//	// ===== LEFT FACE =====
		//	sideTris.AddRange(new[] { 0, 3, 7 });
		//	sideTris.AddRange(new[] { 0, 7, 4 });

		//	// ===== RIGHT FACE =====
		//	sideTris.AddRange(new[] { 1, 5, 6 });
		//	sideTris.AddRange(new[] { 1, 6, 2 });

		//	// ===== TOP SLOPE =====
		//	capTris.AddRange(new[] { 3, 2, 6 });
		//	capTris.AddRange(new[] { 3, 6, 7 });

		//	// ===== BOTTOM SLOPE =====
		//	capTris.AddRange(new[] { 0, 4, 5 });
		//	capTris.AddRange(new[] { 0, 5, 1 });

		//	mesh.subMeshCount = 2;
		//	mesh.SetVertices(verts);
		//	mesh.SetTriangles(sideTris, 0); // sides only
		//	mesh.SetTriangles(capTris, 1);  // top + bottom

		//	mesh.RecalculateBounds();
		//	return mesh;
		//}

		private static Mesh CreateViewFrustumMesh(float distance)
		{
			const float GameFOV = 20f;
			const float Near = 0.5f;
			float Far = Mathf.Max(distance, Near + 0.1f);

			float aspect = 16f / 9f;
			float halfFov = GameFOV * 0.5f * Mathf.Deg2Rad;
			float t = Mathf.Tan(halfFov);

			float nh = Near * t * 2f;
			float nw = nh * aspect;
			float fh = Far * t * 2f;
			float fw = fh * aspect;

			Vector3[] near = {
		new(-nw/2, -nh/2, Near),  // 0 bottom-left
        new( nw/2, -nh/2, Near),  // 1 bottom-right
        new( nw/2,  nh/2, Near),  // 2 top-right
        new(-nw/2,  nh/2, Near)   // 3 top-left
    };

			Vector3[] far = {
		new(-fw/2, -fh/2, Far),   // 4
        new( fw/2, -fh/2, Far),   // 5
        new( fw/2,  fh/2, Far),   // 6
        new(-fw/2,  fh/2, Far)    // 7
    };

			var mesh = new Mesh { name = "ViewFrustum_DoubleSided_4Mat" };

			var verts = new List<Vector3>();
			verts.AddRange(near);
			verts.AddRange(far);
			mesh.SetVertices(verts);

			// Submesh 0: Sides  – Outside (correct CCW)
			// Submesh 1: Sides  – Inside  (reversed)
			// Submesh 2: Caps   – Outside (correct CCW)
			// Submesh 3: Caps   – Inside  (reversed)

			var sideOutside = new List<int>();
			var sideInside = new List<int>();
			var capOutside = new List<int>();
			var capInside = new List<int>();

			// LEFT FACE
			sideOutside.AddRange(new[] { 0, 4, 7 });   // outside
			sideOutside.AddRange(new[] { 0, 7, 3 });
			sideInside.AddRange(new[] { 0, 7, 4 });   // reversed
			sideInside.AddRange(new[] { 0, 3, 7 });

			// RIGHT FACE
			sideOutside.AddRange(new[] { 1, 2, 6 });   // outside
			sideOutside.AddRange(new[] { 1, 6, 5 });
			sideInside.AddRange(new[] { 1, 6, 2 });   // reversed
			sideInside.AddRange(new[] { 1, 5, 6 });

			// TOP SLOPE
			capOutside.AddRange(new[] { 3, 7, 6 });    // outside
			capOutside.AddRange(new[] { 3, 6, 2 });
			capInside.AddRange(new[] { 3, 6, 7 });    // reversed
			capInside.AddRange(new[] { 3, 2, 6 });

			// BOTTOM SLOPE
			capOutside.AddRange(new[] { 0, 1, 5 });    // outside
			capOutside.AddRange(new[] { 0, 5, 4 });
			capInside.AddRange(new[] { 0, 5, 1 });    // reversed
			capInside.AddRange(new[] { 0, 4, 5 });

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
			//go.hideFlags = HideFlags.HideAndDontSave;
			go.layer = LayerMask.NameToLayer("Editor");

			var mf = go.AddComponent<MeshFilter>();
			var mr = go.AddComponent<MeshRenderer>();

			mf.mesh = CreateViewFrustumMesh(view.Distance);

			//// Create two materials
			//var shader = Shader.Find("Hidden/URPGizmoAdditive");
			//var materials = new[]
			//{
			//	new Material(shader) { hideFlags = HideFlags.HideAndDontSave },
			//	new Material(shader) { hideFlags = HideFlags.HideAndDontSave }
			//};

			//// Submesh 0: Sides — cyan
			//materials[0].SetColor("_BaseColor", new Color(0.05f, 0.15f, 0.2f, 1f));

			//// Submesh 1: Top & Bottom — different cyan
			//materials[1].SetColor("_BaseColor", new Color(0.03f, 0.1f, 0.15f, 1f));

			//mr.materials = materials;

			//// Inside UpdateViewFrustumMarker, replace the shader line with:
			//string shaderName = useAdditiveGizmos
			//	? "Hidden/URPGizmoAdditive"
			//	: "Hidden/URPGizmoTransparent";

			//var shader = Shader.Find(shaderName);

			//var materials = new Material[4]
			//{
			//	new Material(shader), // 0: Sides – Outside
			//	new Material(shader), // 1: Sides – Inside (darker)
			//	new Material(shader), // 2: Top/Bottom – Outside
			//	new Material(shader), // 3: Top/Bottom – Inside (darker)
			//};

			//// Bright cyan-ish for sides
			//materials[0].SetColor("_BaseColor", new Color(0.05f, 0.30f, 0.40f, 0.95f));
			//materials[1].SetColor("_BaseColor", new Color(0.01f, 0.06f, 0.10f, 0.70f));

			//// Slightly different tone for top/bottom caps
			//materials[2].SetColor("_BaseColor", new Color(0.03f, 0.22f, 0.30f, 0.85f));
			//materials[3].SetColor("_BaseColor", new Color(0.008f, 0.04f, 0.07f, 0.60f));

			var transparentShader = Shader.Find("Hidden/URPGizmoTransparent");
			var additiveShader = Shader.Find("Hidden/URPGizmoAdditive");

			var materials = new Material[4]
			{
				new Material(additiveShader), // 0: Sides – Outside
				new Material(transparentShader), // 1: Sides – Inside (darker)
				new Material(additiveShader), // 2: Top/Bottom – Outside
				new Material(transparentShader), // 3: Top/Bottom – Inside (darker)
			};

			// Bright cyan-ish for sides
			materials[0].SetColor("_BaseColor", new Color(0.05f, 0.30f, 0.40f, 1f));//additive
			materials[1].SetColor("_BaseColor", new Color(0.3f, 0.6f, 0.9f, 0.20f));//transparent

			// Slightly different tone for top/bottom caps
			materials[2].SetColor("_BaseColor", new Color(0.03f, 0.22f, 0.30f, 1f));//additive
			materials[3].SetColor("_BaseColor", new Color(0.1f, 0.5f, 0.8f, 0.20f));//transparent

			foreach (var m in materials)
				m.hideFlags = HideFlags.HideAndDontSave;

			mr.materials = materials;

			// Position & rotation
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