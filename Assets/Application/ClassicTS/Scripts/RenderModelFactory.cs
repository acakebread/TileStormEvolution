using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public static class RenderModelFactory
	{
		public static CommandRenderModelData Create(Definition definition, Vector3 position = default, Quaternion rotation = default, Vector3 scale = default)
		{
			var data = new CommandRenderModelData();

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
			Vector3 rootPos, Quaternion rootRot, Vector3 rootScale, CommandRenderModelData target)
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
				sequence = TextureSequenceManager.GetTextureSequence(def.texture);

			// Loop through matching filter/renderer pairs
			for (int i = 0; i < Mathf.Min(filters.Length, renderers.Length); i++)
			{
				var filter = filters[i];
				var renderer = renderers[i];

				if (filter.sharedMesh == null) continue;

				var localToRoot = filter.transform.localToWorldMatrix;
				var worldMatrix = rootMatrix * localToRoot;

				// Default: use original shared materials
				Material[] materialsToUse = renderer.sharedMaterials;

				// Apply texture sequence override (only for non-HD)
				if (sequence != null && sequence.ResolvedFrames.Length > 0)
				{
					var firstTex = sequence.ResolvedFrames[0].texture;
					if (firstTex != null)
					{
						Material[] overrideMats = new Material[materialsToUse.Length];

						for (int m = 0; m < materialsToUse.Length; m++)
						{
							if (materialsToUse[m] == null)
							{
								overrideMats[m] = null;
								continue;
							}

							// Important: create a copy so we don't modify the asset!
							Material copy = new Material(materialsToUse[m]);
							copy.mainTexture = firstTex;

							// Apply color override from replacement material if it exists and is emissive
							if (replacement != null && MaterialUtils.isEmissive(replacement))
							{
								copy.color = replacement.GetColor("_EmissionColor");
								// If you actually want emission too:
								// copy.SetColor("_EmissionColor", replacement.GetColor("_EmissionColor"));
								// copy.EnableKeyword("_EMISSION"); // ← usually needed
							}

							overrideMats[m] = copy;
						}

						materialsToUse = overrideMats;
					}
				}

				// Finally — add the instance **once** with the correct materials
				target.AddMeshInstance(filter.sharedMesh, materialsToUse, worldMatrix);
			}
		}
	}
}