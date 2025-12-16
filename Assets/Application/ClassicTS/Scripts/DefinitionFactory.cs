using UnityEngine;
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
				gameObject = PrefabFactory.Instantiate(definition.model, position.Value, rotation.Value, parent);
			else if (position.HasValue)
				gameObject = PrefabFactory.Instantiate(definition.model, position.Value, parent);
			else
				gameObject = PrefabFactory.Instantiate(definition.model, parent);

			if (gameObject == null)
				return null;

			//temporary special placeholder flag setting for special properties in absence of definition editor 
			if (definition.model.Contains("tree"))
				definition.bSway = true;//ToDo implement sway in definition editor - hard set to trees for now

			//temporary special placeholder material override for special properties in absence of definition editor 
			if ("Caustic" == definition.texture)
				definition.material = "toxic";

			//temporary provision to suppress texture replacement on loaded HD models
			var renderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
			var isHD = renderer && null == renderer.material.mainTexture;

			//Apply Definition Properties
			var materialPath = $"{AssetPath.MaterialPath}{definition.material}";
			var replacement = string.IsNullOrEmpty(definition.material) ? null : MaterialCache.Get(materialPath);

			// Apply texture animation and / or material replacement
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			if (isHD || null != replacement)
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