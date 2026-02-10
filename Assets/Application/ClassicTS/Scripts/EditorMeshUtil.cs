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
		private static Variant currentVariant;         // ← added for the new overload
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
				if (null != ghostMesh)
					ghostMesh.SetAllMaterials(outOfBounds ? ghostMaterialInvalid : ghostMaterial);
			}
		}

		// New overload: takes Variant directly (uses hash, angle, delta from variant)
		public static void UpdateGhostMesh(Variant variant, Vector3 position, bool outOfBounds)
		{
			if (variant.hash == 0) return;  // invalid variant
			InitializeGhostMaterial();

			// Early out if nothing changed (compare full variant + position + validity)
			if (currentVariant.hash == variant.hash &&
				Mathf.Approximately(currentVariant.angle, variant.angle) &&
				Vector3LexComparer.ApproximatelyEqual(currentVariant.delta, variant.delta) &&
				lastPosition == position &&
				lastOutOfBounds == outOfBounds)
			{
				return;
			}

			// Update tracked values
			currentVariant = variant;
			lastPosition = position;
			lastAngle = variant.angle;
			lastOutOfBounds = outOfBounds;

			// Get definition from hash (needed for model instantiation)
			var definition = ResourceManager.GetDefinition(variant.hash);
			if (null == definition) return;

			// Reuse same helpers, but pass variant.angle and add delta to y
			if (null == ghostMesh || null == currentDefinition || currentDefinition.HashID != variant.hash)
			{
				CreateMesh();
				return;
			}

			UpdateMesh();

			// ── Local helpers (same as above, but using variant properties) ───────

			void CreateMesh()
			{
				if (null != ghostMesh)
				{
					Object.DestroyImmediate(ghostMesh);
					ghostMesh = null;
				}

				ghostMesh = Assets.ModelAssets.Instantiate(
					definition.model,
					position + variant.delta,
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

				ghostMesh.transform.position = position + variant.delta;
				ghostMesh.transform.rotation = Quaternion.Euler(0f, variant.angle, 0f);
				ghostMesh.SetActive(true);
			}

			void UpdateMaterial()
			{
				if (null != ghostMesh)
					ghostMesh.SetAllMaterials(outOfBounds ? ghostMaterialInvalid : ghostMaterial);
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