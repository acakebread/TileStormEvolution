using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorSelectionUtil
	{
		private static Material s_validMaterial;
		private static Material s_invalidMaterial;
		private static Material s_selectedMaterial;

		private static void EnsureMaterials()
		{
			if (s_validMaterial != null) return;

			s_validMaterial = MaterialUtils.CreateAlwaysOnTopUnlitMaterial(new Color(1f, 1f, 1f, 0.5f));
			s_invalidMaterial = MaterialUtils.CreateAlwaysOnTopUnlitMaterial(new Color(1f, 0f, 0f, 0.5f));
			s_selectedMaterial = MaterialUtils.CreateAlwaysOnTopUnlitMaterial(new Color(1.4f, 1.25f, 0.85f, 0.6f));

			if (s_validMaterial == null || s_invalidMaterial == null || s_selectedMaterial == null)
				Debug.LogError("EditorSelectionUtil: Failed to create ghost materials.");
		}

		/// <summary>
		/// Creates a new highlight ghost mesh from a Variant.
		/// Returns the GameObject or null on failure.
		/// </summary>
		public static GameObject Create(Variant variant, Vector3 renderPosition)
		{
			if (variant.hash == 0) return null;
			EnsureMaterials();

			var definition = ResourceManager.GetDefinition(variant.hash);
			if (definition == null) return null;

			var go = Assets.ModelAssets.Instantiate(
				definition.model,
				renderPosition,
				Quaternion.Euler(0f, variant.angle, 0f),
				parent: MainController.MapRoot);

			if (go == null) return null;

			go.name = "HighlightMesh";
			return go;
		}

		public static void SyncPickCollider(IMapEdit map, GameObject highlightMesh, int logicalIndex)
		{
			if (highlightMesh == null || logicalIndex < 0) return;

			var concreteMap = map as Map;
			if (concreteMap == null) return;

			Map.AttachPickColliders(highlightMesh, concreteMap, logicalIndexOverride: logicalIndex);
		}

		/// <summary>
		/// Updates position, rotation, and material of an existing highlight mesh.
		/// Does NOT recreate if definition changes (assumes variant.hash stays consistent during drag).
		/// </summary>
		public static void Update(
			GameObject highlightMesh,
			Variant variant,
			Vector3 renderPosition,
			bool outOfBounds,
			bool isSelectedOrDragging = true)
		{
			if (highlightMesh == null) return;
			EnsureMaterials();

			highlightMesh.transform.SetPositionAndRotation(renderPosition, Quaternion.Euler(0f, variant.angle, 0f));

			Material mat = outOfBounds
				? s_invalidMaterial
				: (isSelectedOrDragging ? s_selectedMaterial : s_validMaterial);

			highlightMesh.SetAllMaterials(mat);
			highlightMesh.SetActive(true);
		}

		/// <summary>
		/// Convenience: full update using IMapEdit + world position
		/// </summary>
		public static void Update(
			IMapEdit map,
			GameObject highlightMesh,
			Vector3 worldPos,
			Variant variant,
			bool isSelectedOrDragging = true)
		{
			int mapIndex = map.VectorToIndex(worldPos);
			bool outOfBounds = mapIndex == -1;
			Vector3 renderPos = Map.WorldToRender(worldPos);

			Update(highlightMesh, variant, renderPos, outOfBounds, isSelectedOrDragging);
			SyncPickCollider(map, highlightMesh, mapIndex);
		}

		public static void Destroy(GameObject highlightMesh)
		{
			if (highlightMesh != null)
			{
				Object.Destroy(highlightMesh);
			}
		}
	}
}
//using UnityEngine;
//using MassiveHadronLtd;

//namespace ClassicTilestorm
//{
//	public static class EditorSelectionUtil
//	{
//		private static (Renderer renderer, Material[] originalMaterials)?[] originalRenderersState;

//		public static bool HighlightTile(IMapEdit map, Vector3 worldPos)
//		{
//			var variant = map.GetVariantAt(worldPos);
//			if (variant.IsDefaultEquivalent)
//				return false;

//			return HighlightGameObject(map.GetTile(worldPos).gameObject);
//		}

//		public static void UnhighlightTile(IMapEdit map, Vector3 worldPos)
//		{
//			UnhighlightGameObject(map.GetTile(worldPos).gameObject);
//			originalRenderersState = null;
//		}

//		public static bool HighlightGameObject(GameObject gameObject)
//		{
//			if (gameObject == null) return false;

//			Color SELECT_TINT = new(1.4f, 1.25f, 0.85f, 1f);
//			const float SELECT_TINT_BRIGHTNESS = 1.35f;

//			originalRenderersState = gameObject.ApplySelectionHighlight(
//				SELECT_TINT,
//				SELECT_TINT_BRIGHTNESS,
//				includeInactive: true);

//			return true;
//		}

//		public static void UnhighlightGameObject(GameObject gameObject)
//		{
//			if (null != gameObject)
//				gameObject.RestoreSelectionHighlight(originalRenderersState);

//			originalRenderersState = null;
//		}
//	}
//}
