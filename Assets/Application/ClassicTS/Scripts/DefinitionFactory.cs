using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class DefinitionFactory
    {
		public static GameObject Instantiate(Definition definition, Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
		{
			if (null == definition || string.IsNullOrEmpty(definition.model))
				return null; // or handle fallback as needed

			var prefabPath = $"{AssetPath.GeometryPath}{definition.model}";

			// Decide which PrefabFactory overload to use based on what's provided
			GameObject gameObject;
			if (position.HasValue && rotation.HasValue)
				gameObject = PrefabFactory.Instantiate(prefabPath, position.Value, rotation.Value, parent);
			else if (position.HasValue)
				gameObject = PrefabFactory.Instantiate(prefabPath, position.Value, parent);
			else
				gameObject = PrefabFactory.Instantiate(prefabPath, parent);

			if (null == gameObject)
				return null;

			//temporary special placeholder flag setting for special properties in absence of definition editor 
			if (definition.model.Contains("tree"))
				definition.bSway = true;//ToDo implement sway in definition editor - hard set to trees for now

			//temporary special placeholder material override for special properties in absence of definition editor 
			if ("Caustic" == definition.texture)
				definition.material = "toxic";


			//Apply Definition Properties

			// Apply texture animation
			Material replacement = null;
			if (!string.IsNullOrEmpty(definition.material))
			{
				var materialPath = $"{AssetPath.MaterialPath}{definition.material}";
				replacement = MaterialCache.Get(materialPath);
			}

			var sequence = TextureSetManager.GetTextureSequence(definition.texture, AssetPath.TexturePath);

			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			textureAnimator.Initialize(sequence, replacement);

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