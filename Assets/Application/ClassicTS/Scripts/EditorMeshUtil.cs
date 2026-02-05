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
		private static Vector3 lastPosition;
		private static float lastAngle;
		private static bool lastOutOfBounds;

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

			if (null == currentDefinition || currentDefinition.HashID != definition.HashID)
			{
				CreateMesh();
				return;
			}

			// Nothing changed → early out
			if (lastPosition == position &&
				lastOutOfBounds == outOfBounds &&
				Mathf.Approximately(lastAngle, angle))
			{
				return;
			}

			// Update tracked values
			lastPosition = position;
			lastAngle = angle;
			lastOutOfBounds = outOfBounds;

			UpdateMesh();


			// ── Local helpers ────────────────────────────────────────────────────────

			void CreateMesh()
			{
				if (null != ghostMesh)
				{
					Object.DestroyImmediate(ghostMesh);
					ghostMesh = null;
				}

				ghostMesh = Assets.ModelAssets.Instantiate(
					definition.model,
					position,
					Quaternion.Euler(0f, angle, 0f),
					parent: MainController.MapRoot);

				if (null == ghostMesh) return;

				ghostMesh.name = "GhostMesh";

				// Remove any colliders baked into the prefab
				foreach (var collider in ghostMesh.GetComponentsInChildren<Collider>())
					Object.DestroyImmediate(collider);

				currentDefinition = definition;

				UpdateMaterial();
				ghostMesh.SetActive(true);
			}

			void UpdateMesh()
			{
				if (null == ghostMesh) return;

				UpdateMaterial();

				ghostMesh.transform.position = position;
				ghostMesh.transform.rotation = Quaternion.Euler(0f, angle, 0f);
				ghostMesh.SetActive(true);
			}

			void UpdateMaterial()
			{
				if (null == ghostMesh) return;

				var targetMaterial = outOfBounds ? ghostMaterialInvalid : ghostMaterial;

				foreach (var renderer in ghostMesh.GetComponentsInChildren<MeshRenderer>())
					renderer.material = targetMaterial;
			}
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