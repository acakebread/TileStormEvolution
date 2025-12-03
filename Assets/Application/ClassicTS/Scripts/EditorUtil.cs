using UnityEngine;
using MassiveHadronLtd;

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


		//waypoint visualisation system
		private static GameObject waypointGhost;
		private static readonly System.Collections.Generic.List<GameObject> waypointMarkers = new();
		private static Material waypointMaterialNormal;
		private static Material waypointMaterialSelected;
		private static Material waypointMaterialCamera;

		public static void UpdateWaypointGhost(Camera cam, IMapManager mapManager, Vector3 mouseWorldPos)
		{
			InitializeWaypointMaterials();

			if (waypointGhost == null)
			{
				waypointGhost = CreateWaypointMarker(Vector3.zero, waypointMaterialNormal);
				waypointGhost.name = "WaypointGhost";
			}

			var snapped = mapManager.SnappedMapPosition(mouseWorldPos);
			waypointGhost.transform.position = snapped + new Vector3(0, 0.01f, 0); // Slightly above ground
			waypointGhost.SetActive(true);
		}

		private static Material waypointMaterialInvalid;

		public static void InitializeWaypointMaterials()
		{
			if (waypointMaterialNormal == null)
				waypointMaterialNormal = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 1f, 0f, 0.3f));
			if (waypointMaterialSelected == null)
				waypointMaterialSelected = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 0.7f, 1f, 0.6f));
			if (waypointMaterialCamera == null)
				waypointMaterialCamera = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 1f, 1f, 0.4f));
			if (waypointMaterialInvalid == null)
				waypointMaterialInvalid = MaterialUtils.CreateTransparentUnlitMaterial(new Color(1f, 0.5f, 0f, 0.5f)); // Orange
		}

		public static void UpdateWaypointMarkers(IMapManager mapManager, Waypoint[] waypoints, int selectedIndex = -1)
		{
			InitializeWaypointMaterials();

			// Destroy old
			foreach (var m in waypointMarkers) if (m) Object.Destroy(m);
			waypointMarkers.Clear();

			if (waypoints == null || waypoints.Length == 0) return;

			for (int i = 0; i < waypoints.Length; i++)
			{
				var wp = waypoints[i];
				if (wp.tile < 0 || wp.tile >= mapManager.Count) continue;

				var pos = mapManager.TileWorldPosition(wp.tile) + new Vector3(0, 0.02f, 0);

				Material mat;
				if (i == selectedIndex)
					mat = waypointMaterialSelected;                    // Blue = selected
				else if (wp.tile < 0)
					mat = waypointMaterialInvalid ?? waypointMaterialNormal; // Optional: red/orange for unplaced
				else if (wp.IsCamera())
					mat = waypointMaterialCamera;                      // Cyan = camera
				else
					mat = waypointMaterialNormal;                      // Green = normal

				var marker = CreateWaypointMarker(pos, mat);
				marker.name = $"WP_{i}_{(i == selectedIndex ? "SELECTED" : wp.IsCamera() ? "CAM" : "NORMAL")}";
				waypointMarkers.Add(marker);
			}
		}

		private static GameObject CreateWaypointMarker(Vector3 pos, Material mat)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			go.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);
			go.transform.position = pos;
			Object.Destroy(go.GetComponent<Collider>());

			var mr = go.GetComponent<MeshRenderer>();
			mr.material = mat;
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			mr.receiveShadows = false;

			return go;
		}

		public static void HideWaypointGhost()
		{
			if (waypointGhost != null) waypointGhost.SetActive(false);
		}

		public static void DestroyWaypointVisuals()
		{
			if (waypointGhost) Object.Destroy(waypointGhost);
			foreach (var m in waypointMarkers) if (m) Object.Destroy(m);
			waypointMarkers.Clear();
			waypointGhost = null;
		}
	}
}