using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class GhostMeshUtil
	{
		//ghost mesh system
		private static GameObject ghostMesh;
		private static Material ghostMaterial;          // normal/valid (white 0.5 alpha)
		private static Material ghostMaterialInvalid;   // invalid (red 0.5 alpha)
		private static Material ghostMaterialSelected;  // ← NEW: yellow tint for selected/dragging

		private static Variant currentVariant;
		private static Definition currentDefinition;
		private static Vector3 lastPosition;
		private static float lastAngle;
		private static bool lastOutOfBounds;

		// Initialize the ghost materials
		public static void InitializeGhostMaterial()
		{
			if (ghostMaterial == null)
			{
				ghostMaterial = MaterialUtils.CreateAlwaysOnTopUnlitMaterial(new Color(1f, 1f, 1f, 0.5f));
				if (ghostMaterial == null)
					Debug.LogError("GeometryUtil: Failed to create transparent unlit material.");
			}

			if (ghostMaterialInvalid == null)
			{
				ghostMaterialInvalid = MaterialUtils.CreateAlwaysOnTopUnlitMaterial(new Color(1f, 0f, 0f, 0.5f));
				if (ghostMaterialInvalid == null)
					Debug.LogError("GeometryUtil: Failed to create invalid ghost material.");
			}

			if (ghostMaterialSelected == null)
			{
				// Using the same color as the old highlight system (but with ghost-appropriate alpha)
				ghostMaterialSelected = MaterialUtils.CreateAlwaysOnTopUnlitMaterial(new Color(1.4f, 1.25f, 0.85f, 0.6f));
				if (ghostMaterialSelected == null)
					Debug.LogError("GeometryUtil: Failed to create selected ghost material.");
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
				if (null != ghostMesh)
					ghostMesh.SetAllMaterials(outOfBounds ? ghostMaterialInvalid : ghostMaterial);
			}
		}

		// New overload: takes Variant directly + selected/dragging flag
		private static void UpdateGhostMesh(Variant variant, Vector3 position, bool outOfBounds, bool isSelectedOrDragging = false)
		{
			if (variant.hash == 0) return;
			InitializeGhostMaterial();

			// Early out if nothing changed
			if (null != ghostMesh &&
				currentVariant.hash == variant.hash &&
				Mathf.Approximately(currentVariant.angle, variant.angle) &&
				Vector3LexComparer.ApproximatelyEqual(currentVariant.delta, variant.delta) &&
				lastPosition == position &&
				lastOutOfBounds == outOfBounds)
			{
				if (!ghostMesh.activeInHierarchy)
					ghostMesh.SetActive(true);
				return;
			}

			// Update tracked values
			currentVariant = variant;
			lastPosition = position;
			lastAngle = variant.angle;
			lastOutOfBounds = outOfBounds;

			var definition = ResourceManager.GetDefinition(variant.hash);
			if (null == definition) return;

			if (null == ghostMesh || null == currentDefinition || currentDefinition.HashID != variant.hash)
			{
				CreateMesh();
				return;
			}

			UpdateMesh();

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
					Quaternion.Euler(0f, variant.angle, 0f),
					parent: MainController.MapRoot);

				if (null == ghostMesh) return;

				ghostMesh.name = "GhostMesh";

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
				ghostMesh.transform.rotation = Quaternion.Euler(0f, variant.angle, 0f);
				ghostMesh.SetActive(true);
			}

			void UpdateMaterial()
			{
				if (null != ghostMesh)
				{
					Material matToUse;

					if (isSelectedOrDragging)
						matToUse = outOfBounds ? ghostMaterialInvalid : ghostMaterialSelected;
					else
						matToUse = outOfBounds ? ghostMaterialInvalid : ghostMaterial;

					ghostMesh.SetAllMaterials(matToUse);
				}
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

		//// Public wrapper - normal placing mode (no selected flag)
		//public static void UpdateGhostMesh(IMapEdit map, Vector3 worldPos, Variant variant)
		//{
		//	var mapIndex = map.VectorToIndex(worldPos);
		//	var renderPos = Map.WorldToRender(worldPos);
		//	UpdateGhostMesh(variant, renderPos, mapIndex == -1, false);
		//}

		// NEW public overload - for selected / dragging (pass true)
		public static void UpdateGhostMesh(IMapEdit map, Vector3 worldPos, Variant variant, bool isSelectedOrDragging)
		{
			var mapIndex = map.VectorToIndex(worldPos);
			var renderPos = Map.WorldToRender(worldPos);
			UpdateGhostMesh(variant, renderPos, mapIndex == -1, isSelectedOrDragging);
		}

		// Public wrapper - normal placing mode (no selected flag)
		public static void UpdateGhostMesh(Variant variant)
		{
			UpdateGhostMesh(variant, lastPosition, false, true);
		}
	}
}