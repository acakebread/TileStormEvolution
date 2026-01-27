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

			// Collect both types
			var meshRenderers = prefabRoot.GetComponentsInChildren<MeshRenderer>(true);
			var skinnedRenderers = prefabRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

			if (meshRenderers.Length == 0 && skinnedRenderers.Length == 0)
			{
				Debug.LogWarning($"No renderers found on prefab: {prefabRoot.name}");
				return;
			}

			Material replacement = null;
			if (!string.IsNullOrEmpty(def.material))
				replacement = MaterialAssets.Find(def.material);

			TextureSequence sequence = null;
			if (!string.IsNullOrEmpty(def.texture))
				sequence = TextureSequenceManager.GetTextureSequence(def.texture);

			int added = 0;

			// Process MeshRenderers
			foreach (var renderer in meshRenderers)
			{
				var filter = renderer.GetComponent<MeshFilter>();
				if (filter == null || filter.sharedMesh == null) continue;

				var worldMatrix = rootMatrix * filter.transform.localToWorldMatrix;
				Material[] mats = renderer.sharedMaterials;

				// Apply sequence override (your code unchanged)
				if (sequence != null && sequence.ResolvedFrames.Length > 0)
				{
					var firstTex = sequence.ResolvedFrames[0].texture;
					if (firstTex != null)
					{
						var overrideMats = new Material[mats.Length];
						for (int m = 0; m < mats.Length; m++)
						{
							if (mats[m] == null) { overrideMats[m] = null; continue; }
							var copy = new Material(mats[m]);
							copy.mainTexture = firstTex;
							if (replacement != null && MaterialUtils.IsEmissive(replacement))
								copy.color = GetEmissionLikeColor(replacement, Color.white * 1.2f);
							overrideMats[m] = copy;
						}
						mats = overrideMats;
					}
				}

				target.AddMeshInstance(filter.sharedMesh, mats, worldMatrix);
				added++;
			}

			// Process SkinnedMeshRenderers
			foreach (var skinned in skinnedRenderers)
			{
				if (skinned.sharedMesh == null) continue;

				var worldMatrix = rootMatrix * skinned.transform.localToWorldMatrix;
				Material[] mats = skinned.sharedMaterials;

				// Apply same sequence override (duplicate or extract to method)
				if (sequence != null && sequence.ResolvedFrames.Length > 0)
				{
					var firstTex = sequence.ResolvedFrames[0].texture;
					if (firstTex != null)
					{
						var overrideMats = new Material[mats.Length];
						for (int m = 0; m < mats.Length; m++)
						{
							if (mats[m] == null) { overrideMats[m] = null; continue; }
							var copy = new Material(mats[m]);
							copy.mainTexture = firstTex;
							if (replacement != null && MaterialUtils.IsEmissive(replacement))
								copy.color = GetEmissionLikeColor(replacement, Color.white * 1.2f);
							overrideMats[m] = copy;
						}
						mats = overrideMats;
					}
				}

				target.AddMeshInstance(skinned.sharedMesh, mats, worldMatrix);
				added++;
			}

			Debug.Log($"Added {added} instances from {prefabRoot.name}");
		}

		private static Color GetEmissionLikeColor(Material mat, Color fallback = default)
		{
			if (mat == null) return fallback;

			// Most common emission color names in order of probability
			string[] candidates = { "_EmissionColor", "_EmissiveColor", "_TintColor", "_Color" };

			foreach (var prop in candidates)
			{
				if (mat.HasProperty(prop))
				{
					var c = mat.GetColor(prop);
					// Very small values usually aren't intended to glow
					if (c.maxColorComponent > 0.02f)
						return c;
				}
			}

			return fallback;
		}
	}
}