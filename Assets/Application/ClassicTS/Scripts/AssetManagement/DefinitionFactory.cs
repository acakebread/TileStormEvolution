using UnityEngine;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class DefinitionFactory
    {
		public static GameObject Instantiate(Definition definition, Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
		{
			if (definition == null || string.IsNullOrEmpty(definition.model))
				return null;

			// Just pass the model name — search happens automatically
			GameObject gameObject;
			if (position.HasValue && rotation.HasValue)
				gameObject = ModelAssets.Instantiate(definition.model, position.Value, rotation.Value, parent);
			else if (position.HasValue)
				gameObject = ModelAssets.Instantiate(definition.model, position.Value, parent);
			else
				gameObject = ModelAssets.Instantiate(definition.model, parent);

			if (null == gameObject)
				return null;

			var replacementMaterial = ResolveMaterialOverride(definition);
			var appliedVisuals = false;

			if (replacementMaterial != null)
			{
				appliedVisuals = ApplyMaterialOverride(gameObject, replacementMaterial);
			}
			else
			{
				// Fall back to animated material data on the original prefab/material name.
				var animMaterialName = GetPrimaryTextureName(gameObject);
				var sequence = AnimMaterialInfoManager.GetAnimMaterial(animMaterialName);
				appliedVisuals = sequence != null && AnimMaterialManager.Apply(gameObject, sequence);
			}

			if (appliedVisuals)
			{
				Color? emissiveColor = GetEmissiveColor(gameObject);
				if (emissiveColor.HasValue)
					LightFactory.AddPointLight(gameObject, emissiveColor.Value);
			}

			// Add collider for interactive tiles
			if (definition.Drag)
				TileStormGeneric.AddDefaultTileCollider(gameObject);

			// Add sway component for wind controller
			if (definition.Sway)
				MorphGeomSway.AddGeomSway(gameObject);

			// Add wash component for water bobbing
			if (definition.Wash)
				TilePhysicsWash.AddWash(gameObject);

#if DEBUG
			gameObject.AddComponent<RTTI>().definition = definition; // This is for development debug in editor only - do not use RTTI elsewhere
#endif

			return gameObject;
		}

		public static GameObject InstantiateSimplified(Definition definition, Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
		{
			if (definition == null || string.IsNullOrEmpty(definition.model))
				return null;

			GameObject gameObject;
			if (position.HasValue && rotation.HasValue)
				gameObject = ModelAssets.Instantiate(definition.model, position.Value, rotation.Value, parent);
			else if (position.HasValue)
				gameObject = ModelAssets.Instantiate(definition.model, position.Value, parent);
			else
				gameObject = ModelAssets.Instantiate(definition.model, parent);

			if (gameObject == null)
				return null;

			return gameObject;
		}
#if DEBUG
		private class RTTI : MonoBehaviour { public Definition definition; }//debug class so Definition data can be seen in the inspector
#endif

		// Preserve the HD detector for future use, but do not gate runtime replacement logic on it.
		public static bool IsHD(GameObject gameObject)
		{
			var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
			return null != meshRenderers && ((meshRenderers.Length == 1 && meshRenderers[0].sharedMaterials.Length >= 2) || meshRenderers.Length >= 2);
		}

		public static string GetPrimaryTextureName(GameObject gameObject)
		{
			if (gameObject == null)
				return null;

			var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			for (var i = 0; i < renderers.Length; i++)
			{
				var renderer = renderers[i];
				if (renderer == null)
					continue;

				var materials = renderer.sharedMaterials;
				if (materials == null || materials.Length == 0)
					continue;

				for (var j = 0; j < materials.Length; j++)
				{
					var material = materials[j];
					if (material == null || material.mainTexture == null)
						continue;

					var textureName = material.mainTexture.name;
					if (string.IsNullOrWhiteSpace(textureName))
						continue;

					return NormalizeTextureName(textureName);
				}
			}

			return null;
		}

		public static Material ResolveMaterialOverride(Definition definition)
		{
			if (definition == null || string.IsNullOrWhiteSpace(definition.material))
				return null;

			return MaterialAssets.Find(definition.material);
		}

		public static bool ApplyMaterialOverride(GameObject gameObject, Material replacementMaterial)
		{
			if (gameObject == null || replacementMaterial == null)
				return false;

			var applied = false;
			var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			for (var i = 0; i < renderers.Length; i++)
			{
				var renderer = renderers[i];
				if (renderer == null)
					continue;

				var materials = renderer.sharedMaterials;
				if (materials == null || materials.Length == 0)
					continue;

				renderer.sharedMaterials = BuildReplacementMaterials(materials, replacementMaterial);
				applied = true;
			}

			return applied;
		}

		public static Material[] BuildReplacementMaterials(Material[] sourceMaterials, Material replacementMaterial)
		{
			if (sourceMaterials == null || sourceMaterials.Length == 0 || replacementMaterial == null)
				return System.Array.Empty<Material>();

			var result = new Material[sourceMaterials.Length];
			for (var i = 0; i < sourceMaterials.Length; i++)
			{
				var source = sourceMaterials[i];
				if (source == null)
					continue;

				var copy = new Material(replacementMaterial)
				{
					name = $"{replacementMaterial.name} ({source.name})"
				};
				copy.mainTextureOffset = source.mainTextureOffset;
				copy.mainTextureScale = source.mainTextureScale;
				result[i] = copy;
			}

			return result;
		}

		private static string NormalizeTextureName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return name;

			var clean = System.IO.Path.GetFileNameWithoutExtension(name.Trim());
			const string suffix = " (Instance)";
			return clean.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)
				? clean.Substring(0, clean.Length - suffix.Length)
				: clean;
		}

		/// <summary>
		/// Checks whether the final applied materials are emissive and returns the emission color if found.
		/// </summary>
		private static Color? GetEmissiveColor(GameObject gameObject)
		{
			if (gameObject == null) return null;

			var binding = gameObject.GetComponent<AnimMaterialBinding>();
			if (binding != null)
			{
				foreach (var animMat in binding.GetMaterials())
				{
					if (animMat != null && animMat.IsEmissive)
					{
						var emission = GetEmissionColor(animMat.Material);
						if (emission.HasValue)
							return emission;
					}
				}
			}

			var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			foreach (var renderer in renderers)
			{
				foreach (var mat in renderer.sharedMaterials)
				{
					if (mat == null) continue;

					var emission = GetEmissionColor(mat);
					if (emission.HasValue)
					{
						return emission.Value;
					}
				}
			}

			return null;
		}

		private static Color? GetEmissionColor(Material material)
		{
			if (material == null || !material.HasProperty("_EmissionColor"))
				return null;

			var emissionColor = material.GetColor("_EmissionColor");
			return material.IsKeywordEnabled("_EMISSION") || emissionColor.maxColorComponent > 0.01f
				? emissionColor
				: null;
		}
	}
}
