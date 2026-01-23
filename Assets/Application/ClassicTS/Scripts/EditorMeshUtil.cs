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
		private static Definition currentDefinition;

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
			if (ghostMesh == null || currentDefinition?.model != definition.model)
			{
				if (ghostMesh != null)
					Object.DestroyImmediate(ghostMesh);

				string prefabPath = GetGeometryPath(definition.model);
				if (string.IsNullOrEmpty(prefabPath))
				{
					ghostMesh = null;
					return;
				}

				// Direct, clean, raw instantiation — no runtime junk added
				ghostMesh = Assets.ModelAssets.Instantiate(prefabPath, parent: mapManager.CurrentTransform.parent);

				if (ghostMesh != null)
				{
					ghostMesh.name = "GhostMesh";

					// Optional: strip any colliders that might be baked into prefab
					foreach (var collider in ghostMesh.GetComponentsInChildren<Collider>())
						Object.DestroyImmediate(collider);

					// No need to remove TextureSetAnimator, MorphGeomSway, etc. — they were never added!
				}

				currentDefinition = definition; // track for change detection
			}

			if (camera == null || ghostMesh == null) return;

			// Update position
			var worldPos = Map.ScreenToWorld(camera, Input.mousePosition);
			ghostMesh.transform.position = Map.SnappedMapPosition(worldPos);

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

			static string GetGeometryPath(string modelName) => string.IsNullOrEmpty(modelName) ? null : $"{AssetPath.GeometryPath}{modelName}";
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