using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public static class RenderModelFactory
	{
		public static RenderModelData Create(Definition definition, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default)
		{
			var data = new RenderModelData();

			if (definition == null || string.IsNullOrEmpty(definition.model))
				return data;

			scale = scale == default ? Vector3.one : scale;

			// Get the base model (same logic as DefinitionFactory)
			var modelPrefab = ModelAssets.Find(definition.model);
			if (modelPrefab == null)
			{
				Debug.LogWarning($"RenderModelFactory: Model not found: {definition.model}");
				return data;
			}

			// We need the mesh(es) and materials from the prefab
			// This is the tricky part — we have to traverse the prefab hierarchy without instantiating
			CollectMeshRenderers(modelPrefab, definition, position, rotation, scale, data);

			return data;
		}

		private static void CollectMeshRenderers(GameObject prefabRoot, Definition def,
			Vector3 rootPos, Quaternion rootRot, Vector3 rootScale, RenderModelData target)
		{
			Matrix4x4 rootMatrix = Matrix4x4.TRS(rootPos, rootRot, rootScale);

			var filters = prefabRoot.GetComponentsInChildren<MeshFilter>(true);
			var renderers = prefabRoot.GetComponentsInChildren<MeshRenderer>(true);

			if (filters.Length == 0) return;

			// Temporary HD check logic (same as DefinitionFactory)
			bool isHD = (renderers.Length == 1 && renderers[0].sharedMaterials.Length >= 2) || renderers.Length >= 2;

			// Definition material override
			Material replacement = null;
			if (!string.IsNullOrEmpty(def.material))
				replacement = MaterialAssets.Find(def.material);

			// Texture sequence
			TextureSequence sequence = null;
			if (!isHD && !string.IsNullOrEmpty(def.texture))
				sequence = TextureSetManager.GetTextureSequence(def.texture);

			// Loop through filters/renderers (naive index matching — works for most prefabs)
			for (int i = 0; i < Mathf.Min(filters.Length, renderers.Length); i++)
			{
				var filter = filters[i];
				var renderer = renderers[i];

				if (filter.sharedMesh == null) continue;

				var localToRoot = filter.transform.localToWorldMatrix;
				var worldMatrix = rootMatrix * localToRoot;

				// Start with prefab materials
				Material[] mats = renderer.sharedMaterials;

				// Apply texture animation override (same as DefinitionFactory)
				if (sequence != null && sequence.ResolvedFrames.Length > 0)
				{
					// For non-HD: override base texture with first frame (animation happens later if needed)
					var firstTex = sequence.ResolvedFrames[0].texture;
					if (firstTex != null)
					{
						foreach (var mat in mats)
						{
							if (mat != null)
							{
								mat.mainTexture = firstTex;
								if (MaterialUtils.isEmissive(replacement))
									mat.color = replacement.GetColor("_EmissionColor"); // forget the emission just use the color
							}
						}
					}
				}

				target.AddMeshInstance(filter.sharedMesh, mats, worldMatrix);
			}
		}
	}
}