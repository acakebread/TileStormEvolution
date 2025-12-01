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
			if (null == ghostMaterial)
			{
				ghostMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(1f, 1f, 1f, 0.5f));
				if (null == ghostMaterial)
					Debug.LogError("GeometryUtil: Failed to create transparent unlit material.");
			}
		}

		// Update or create the ghost tile at the mouse position
		public static void UpdateGhostTile(Camera camera, IMapManager mapManager, Definition definition)
		{
			if (mapManager == null || definition == null) return;
			//if (null != ghostTile) Object.Destroy(ghostTile);

			// Create or update ghost tile
			if (null == ghostTile)// || ghostTile.GetComponent<RTTI>()?.definition != definition)// do not use RTTI - it's debug only || ghostTile.GetComponent<RTTI>()?.definition != definition)
			{
				ghostTile = GeometryManager.InstantiateTile(definition, mapManager.CurrentTransform, Vector3.zero, false);
				if (null != ghostTile)
				{
					// Remove TextureSetAnimator
					foreach (var iter in ghostTile.GetComponentsInChildren<TextureSetAnimator>())
						iter.enabled = false;

					// Apply ghost material to all renderers
					foreach (var renderer in ghostTile.GetComponentsInChildren<MeshRenderer>())
						renderer.material = ghostMaterial;

					// Remove colliders to prevent raycast interference
					foreach (var collider in ghostTile.GetComponentsInChildren<Collider>())
						Object.Destroy(collider);

					// Remove MorphGeomSway
					foreach (var iter in ghostTile.GetComponentsInChildren<MorphGeomSway>())
						Object.Destroy(iter);

					ghostTile.name = "GhostTile";
				}
			}

			if (null == camera) return;
			if (null == ghostTile) return;

			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			var mapIndex = mapManager.WorldToMapIndex(worldPos);

			// Update position and visibility
			ghostTile.SetActive(-1 != mapIndex);
			if (-1 == mapIndex) return;
			ghostTile.transform.position = mapManager.TileWorldPosition(mapIndex);//snappedPos
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
	}
}