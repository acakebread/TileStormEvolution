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


		//waypoint visualisation system
		private static System.Collections.Generic.List<GameObject> waypointMarkers = new();
		private static GameObject waypointCursor;
		private static Material waypointCursorMaterial;

		public static void InitializeWaypointMaterials()
		{
			if (waypointCursorMaterial == null)
				waypointCursorMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(1f, 1f, 0f, 0.8f)); // Yellow cursor
		}

		public static void HideWaypointCursor()
		{
			if (waypointCursor != null)
				waypointCursor.SetActive(false);
		}

		public static void UpdateWaypointCursor(Camera cam, IMapManager mapManager, Vector3 mouseWorldPos)
		{
			InitializeWaypointMaterials();

			if (waypointCursor == null)
			{
				waypointCursor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				Object.DestroyImmediate(waypointCursor.GetComponent<Collider>());
				waypointCursor.GetComponent<MeshRenderer>().material = waypointCursorMaterial;
				waypointCursor.transform.localScale = new Vector3(0.9f, 0.015f, 0.9f);
				waypointCursor.name = "WP_Cursor";
			}

			var snapped = mapManager.SnappedMapPosition(mouseWorldPos);
			int tile = mapManager.WorldToMapIndex(snapped);

			if (tile >= 0)
			{
				waypointCursor.transform.position = snapped + Vector3.up * 0.01f;
				waypointCursor.SetActive(true);

				// PULSING — restored and improved
				float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.2f;
				waypointCursor.transform.localScale = new Vector3(0.9f, 0.015f, 0.9f) * pulse;
			}
			else
			{
				waypointCursor.SetActive(false);
			}
		}

		public static void UpdateWaypointMarkers(IMapManager mapManager, Waypoint[] waypoints, int selectedIndex = -1)
		{
			// Destroy old
			foreach (var m in waypointMarkers)
				if (m) Object.DestroyImmediate(m);
			waypointMarkers.Clear();

			if (waypoints == null || waypoints.Length == 0) return;

			for (int i = 0; i < waypoints.Length; i++)
			{
				var wp = waypoints[i];
				if (wp.tile < 0 || wp.tile >= mapManager.Count) continue;

				var pos = mapManager.TileWorldPosition(wp.tile) + Vector3.up * 0.02f;

				var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
				go.name = $"WP{i}"; // Short name: WP0, WP1, etc. — perfect for hierarchy

				// Keep collider for clicking, make it trigger so it doesn't block anything
				var col = go.GetComponent<Collider>();
				if (col) col.isTrigger = true;

				go.transform.position = pos;
				go.transform.localScale = new Vector3(0.8f, 0.01f, 0.8f);

				var mr = go.GetComponent<MeshRenderer>();

				if (i == selectedIndex)
				{
					// Selected = bright green + pulsing
					mr.material = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 1f, 0f, 0.7f));
					var pulse = go.AddComponent<WaypointPulse>();
					pulse.intensity = 2.1f;
					pulse.speed = 3.2f;
				}
				else if (wp.IsCamera())
				{
					mr.material = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 1f, 1f, 0.5f));
				}
				else
				{
					mr.material = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0f, 0.7f, 1f, 0.7f));
				}

				waypointMarkers.Add(go);
			}
		}

		public static void DestroyWaypointVisuals()
		{
			if (waypointCursor) Object.DestroyImmediate(waypointCursor);
			waypointCursor = null;

			foreach (var m in waypointMarkers)
				if (m) Object.DestroyImmediate(m);
			waypointMarkers.Clear();
		}
	}

	public class WaypointPulse : MonoBehaviour
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
			float pulse = 1f + (Mathf.Sin(Time.time * speed) * 0.5f + 0.5f) * (intensity - 1f);
			transform.localScale = baseScale * pulse;
		}
	}
}