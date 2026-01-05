using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;

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

			//temporary provision to suppress texture replacement on loaded HD models
			var renderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
			var isHD = (renderers.Length == 1 && renderers[0].sharedMaterials.Length >= 2) || renderers.Length >= 2;//gameObject.CompareTag("Respawn");//HD flag!

			//Apply Definition Properties
			var replacement = MaterialAssets.Find(definition.material);

			// Apply texture animation and / or material replacement
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			if (!isHD)
			{
				var sequence = TextureSetManager.GetTextureSequence(definition.texture, AssetPath.TexturePath);
				textureAnimator.Initialize(sequence, replacement);
			}

			// Point light only if emissive and we have an animator (meaning texture was applied) - placeholder only
			if (null != textureAnimator && textureAnimator.IsEmissive)
				LightFactory.AddPointLight(gameObject, replacement.GetColor("_EmissionColor"));

			// Add collider for interactive tiles
			if (definition.bDrag)
				TileStormGeneric.AddDefaultTileCollider(gameObject);

			// Add sway component for wind controller
			if (definition.bSway)
				MorphGeomSway.AddGeomSway(gameObject);

			// Add wash component for water bobbing
			if (definition.bWash)
				TilePhysicsWash.AddWash(gameObject);

#if DEBUG
			gameObject.AddComponent<RTTI>().definition = definition; // This is for development debug in editor only - do not use RTTI elsewhere
#endif

			return gameObject;
		}
#if DEBUG
		private class RTTI : MonoBehaviour { public Definition definition; }//debug class so Definition data can be seen in the inspector
#endif
	}
}