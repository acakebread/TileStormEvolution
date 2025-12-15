using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorMeshUtil
	{
		//ghost mesh system
		private static GameObject ghostMesh;
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

		// Update or create the ghost mesh at the mouse position
		public static void UpdateGhostMesh(Camera camera, IMapManager mapManager, Definition definition)
		{
			if (mapManager == null || definition == null) return;
			InitializeGhostMaterial();

			// Create ghost mesh if needed (or if definition changed)
			if (ghostMesh == null)
			{
				ghostMesh = DefinitionFactory.InstantiateTile(definition, mapManager.CurrentTransform.parent, Vector3.zero);
				if (ghostMesh != null)
				{
					// Remove TextureSetAnimator
					foreach (var anim in ghostMesh.GetComponentsInChildren<TextureSetAnimator>())
						anim.enabled = false;

					// Remove colliders to prevent raycast interference
					foreach (var collider in ghostMesh.GetComponentsInChildren<Collider>())
						Object.Destroy(collider);

					// Remove MorphGeomSway
					foreach (var sway in ghostMesh.GetComponentsInChildren<MorphGeomSway>())
						Object.Destroy(sway);

					ghostMesh.name = "GhostMesh";
				}
			}

			if (camera == null || ghostMesh == null) return;

			// Update position
			var worldPos = MapManager.ScreenToWorld(camera, Input.mousePosition);
			ghostMesh.transform.position = mapManager.SnappedMapPosition(worldPos);

			// === ToDo IMPLEMENTED ===
			var mapIndex = mapManager.WorldToMapIndex(worldPos);

			bool isValid = mapIndex != -1; // add extra checks here if your game has more rules

			// Switch material based on validity
			Material targetMaterial = isValid ? ghostMaterial : ghostMaterialInvalid;

			foreach (var renderer in ghostMesh.GetComponentsInChildren<MeshRenderer>())
			{
				renderer.material = targetMaterial;
			}

			// Make sure ghost is visible
			ghostMesh.SetActive(true);
		}

		// Hide the ghost mesh
		public static void HideGhostMesh()
		{
			if (ghostMesh != null)
				ghostMesh.SetActive(false);
		}

		// Clean up the ghost mesh
		public static void DestroyGhostMesh()
		{
			if (ghostMesh != null)
			{
				Object.Destroy(ghostMesh);
				ghostMesh = null;
			}
		}
	}
}