using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class EditorSelectionUtil
	{
		private static readonly Color HighlightTint = new Color(1.4f, 1.25f, 0.85f, 0.6f);

		private static void DestroyRuntimeMaterials(Renderer renderer)
		{
			if (renderer == null) return;

			var runtimeMaterials = renderer.materials;
			if (runtimeMaterials == null) return;

			for (int i = 0; i < runtimeMaterials.Length; i++)
			{
				if (runtimeMaterials[i] == null) continue;

				if (runtimeMaterials[i].mainTexture != null &&
					(runtimeMaterials[i].mainTexture.hideFlags & HideFlags.HideAndDontSave) != 0)
					Object.Destroy(runtimeMaterials[i].mainTexture);

				if ((runtimeMaterials[i].hideFlags & HideFlags.HideAndDontSave) != 0)
					Object.Destroy(runtimeMaterials[i]);
			}
		}

		private static void ApplyClonedTintedMaterials(GameObject highlightMesh, Color tintColor, float brightnessMultiplier = 1f)
		{
			var renderers = highlightMesh.GetComponentsInChildren<Renderer>(true);
			for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
			{
				var renderer = renderers[rendererIndex];
				if (renderer == null) continue;

				var sourceMaterials = renderer.sharedMaterials;
				if (sourceMaterials == null || sourceMaterials.Length == 0) continue;

				var replacementArray = new Material[sourceMaterials.Length];
				for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
				{
					var sourceMaterial = sourceMaterials[materialIndex];
					if (sourceMaterial == null) continue;

					var copy = new Material(sourceMaterial);
					copy.hideFlags = HideFlags.HideAndDontSave;
					copy.mainTexture = sourceMaterial.mainTexture.CloneMonochrome(brightnessMultiplier);
					copy.color = new Color(
						tintColor.r,
						tintColor.g,
						tintColor.b,
						tintColor.a * sourceMaterial.color.a);
					copy.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

					if (copy.HasProperty("_Surface"))
						copy.SetFloat("_Surface", 1f);
					if (copy.HasProperty("_SrcBlend"))
						copy.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
					if (copy.HasProperty("_DstBlend"))
						copy.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					if (copy.HasProperty("_ZWrite"))
						copy.SetFloat("_ZWrite", 0f);

					copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
					copy.SetOverrideTag("RenderType", "Transparent");
					replacementArray[materialIndex] = copy;
				}

				renderer.materials = replacementArray;
			}
		}

		/// <summary>
		/// Creates a new highlight ghost mesh from a Variant.
		/// Returns the GameObject or null on failure.
		/// </summary>
		public static GameObject Create(Variant variant, Vector3 renderPosition)
		{
			if (variant.hash == 0) return null;

			var definition = ResourceManager.GetDefinition(variant.hash);
			if (definition == null) return null;

			var go = DefinitionFactory.InstantiateSimplified(
				definition,
				renderPosition,
				Quaternion.Euler(0f, variant.angle, 0f),
				MainController.MapRoot);

			if (go == null) return null;

			go.name = "HighlightMesh";
			ApplyClonedTintedMaterials(go, HighlightTint);
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

			highlightMesh.transform.SetPositionAndRotation(renderPosition, Quaternion.Euler(0f, variant.angle, 0f));
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
				var renderers = highlightMesh.GetComponentsInChildren<Renderer>(true);
				for (int i = 0; i < renderers.Length; i++)
					DestroyRuntimeMaterials(renderers[i]);

				Object.Destroy(highlightMesh);
			}
		}
	}
}
