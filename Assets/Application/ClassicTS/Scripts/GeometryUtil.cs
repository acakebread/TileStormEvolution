using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class GeometryUtil
	{
		private static GameObject ghostTile;
		private static Material ghostMaterial;

		// Initialize the ghost material
		public static void InitializeGhostMaterial()
		{
			if (ghostMaterial == null)
			{
				ghostMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(1f, 1f, 1f, 0.5f));
				if (ghostMaterial == null)
				{
					Debug.LogError("GeometryUtil: Failed to create transparent unlit material.");
				}
			}
		}

		// Update or create the ghost tile at the mouse position
		public static void UpdateGhostTile(Camera camera, MapManager mapManager, TileDef tileDef)
		{
			if (camera == null || mapManager == null || tileDef == null) return;

			// Get mouse world position on the XZ plane
			Ray ray = camera.ScreenPointToRay(Input.mousePosition);
			Plane plane = new Plane(Vector3.up, Vector3.zero); // Assume y=0 for map
			if (plane.Raycast(ray, out float enter))
			{
				Vector3 worldPos = ray.GetPoint(enter);
				int mapIndex = mapManager.WorldToMapIndex(worldPos);
				if (mapIndex < 0 || mapIndex >= mapManager.Count)
				{
					if (ghostTile != null)
						ghostTile.SetActive(false);
					return;
				}
				Vector3 snappedPos = mapManager.TileWorldPosition(mapIndex);

				// Create or update ghost tile
				if (ghostTile == null)// || ghostTile.GetComponent<RTTI>()?.tileDef != tileDef)// do not use RTTI - it's debug only || ghostTile.GetComponent<RTTI>()?.tileDef != tileDef)
				{
					//if (ghostTile != null)
					//	Object.Destroy(ghostTile);

					ghostTile = GeometryManager.InstantiateTile(tileDef, mapManager.transform, snappedPos, false);
					if (ghostTile != null)
					{
						// Remove TextureSetAnimator
						foreach (var iter in ghostTile.GetComponentsInChildren<TextureSetAnimator>())
						{
							iter.enabled = false;
							//Object.Destroy(iter);
						}                       
						// Apply ghost material to all renderers
						foreach (var renderer in ghostTile.GetComponentsInChildren<MeshRenderer>())
						{
							renderer.material = ghostMaterial;
						}
						// Remove colliders to prevent raycast interference
						foreach (var collider in ghostTile.GetComponentsInChildren<Collider>())
						{
							Object.Destroy(collider);
						}
						// Remove MorphGeomSway
						foreach (var iter in ghostTile.GetComponentsInChildren<MorphGeomSway>())
						{
							Object.Destroy(iter);
						}

						ghostTile.name = "GhostTile";
					}
				}

				// Update position and visibility
				if (ghostTile != null)
				{
					ghostTile.transform.position = snappedPos;
					ghostTile.SetActive(true);
				}
			}
			else
			{
				if (ghostTile != null)
					ghostTile.SetActive(false);
			}
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

		// For debugging
		public static bool IsGhostTileActive() => ghostTile != null && ghostTile.activeSelf;
		public static Vector3 GetGhostTilePosition() => ghostTile != null ? ghostTile.transform.position : Vector3.zero;
	}
}