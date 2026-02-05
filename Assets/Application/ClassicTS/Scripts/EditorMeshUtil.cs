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
		public static void UpdateGhostMesh(Definition definition, Vector3 position, float angle, bool outOfBounds)
		{
			if (null == definition) return;
			InitializeGhostMaterial();

			bool needsReinstantiation = false;

			// Check if we need to recreate the ghost
			if (ghostMesh == null)
			{
				needsReinstantiation = true;
			}
			else if (currentDefinition == null || currentDefinition.model != definition.model)
			{
				needsReinstantiation = true;
			}
			else if (Vector3.Distance(ghostMesh.transform.position, position) > 0.001f)
			{
				needsReinstantiation = true;
			}
			else if (Mathf.Abs(Mathf.DeltaAngle(ghostMesh.transform.eulerAngles.y, angle)) > 0.1f)
			{
				needsReinstantiation = true;
			}

			if (needsReinstantiation)
			{
				if (null != ghostMesh)
					Object.DestroyImmediate(ghostMesh);

				// raw instantiation
				ghostMesh = Assets.ModelAssets.Instantiate(definition.model, position, Quaternion.Euler(0f, angle, 0f), parent: MainController.MapRoot);

				if (null != ghostMesh)
				{
					ghostMesh.name = "GhostMesh";

					// strip any colliders that might be baked into prefab
					foreach (var collider in ghostMesh.GetComponentsInChildren<Collider>())
						Object.DestroyImmediate(collider);
				}

				currentDefinition = definition; // track for change detection
			}
			else
			{
				// Just update transform (cheaper than destroying + instantiating)
				if (ghostMesh != null)
				{
					ghostMesh.transform.position = position;
					ghostMesh.transform.rotation = Quaternion.Euler(0f, angle, 0f);
				}
			}

			if (null == ghostMesh) return;

			// Switch material based on validity
			Material targetMaterial = outOfBounds ? ghostMaterialInvalid : ghostMaterial;

			foreach (var renderer in ghostMesh.GetComponentsInChildren<MeshRenderer>())
				renderer.material = targetMaterial;

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