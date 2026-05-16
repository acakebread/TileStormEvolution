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
				appliedVisuals = MaterialUtils.ApplyMaterialOverride(gameObject, replacementMaterial);
			}
			else
			{
				// Fall back to animated material data on the original prefab/material name.
				var animMaterialName = MaterialUtils.GetPrimaryTextureName(gameObject);
				var sequence = AnimMaterialInfoManager.GetAnimMaterial(animMaterialName);
				appliedVisuals = sequence != null && AnimMaterialManager.Apply(gameObject, sequence);
			}

			if (appliedVisuals)
			{
				Color? emissiveColor = MaterialUtils.GetEmissiveColor(gameObject);
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

		public static Material ResolveMaterialOverride(Definition definition)
		{
			if (definition == null || string.IsNullOrWhiteSpace(definition.material))
				return null;

			return MaterialAssets.Find(definition.material);
		}
	}
}
