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

			//Apply Definition Properties
			var replacement = MaterialAssets.Find(definition.material);

			// Apply texture animation and / or material replacement
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			if (!IsHD(gameObject))
			{
				var sequence = TextureSequenceManager.GetTextureSequence(definition.texture);
				textureAnimator.Initialize(sequence, replacement);
			}

			// Point light only if emissive and we have an animator (meaning texture was applied) - placeholder only
			if (null != textureAnimator && textureAnimator.IsEmissive)
				LightFactory.AddPointLight(gameObject, replacement.GetColor("_EmissionColor"));

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

			ApplySimplifiedVisuals(gameObject, definition);
			return gameObject;
		}
#if DEBUG
		private class RTTI : MonoBehaviour { public Definition definition; }//debug class so Definition data can be seen in the inspector
#endif

		//temporary provision to suppress texture replacement on loaded HD models
		public static bool IsHD(GameObject gameObject)
		{
			var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
			return null != meshRenderers && ((meshRenderers.Length == 1 && meshRenderers[0].sharedMaterials.Length >= 2) || meshRenderers.Length >= 2);
		}

		private static void ApplySimplifiedVisuals(GameObject gameObject, Definition definition)
		{
			if (gameObject == null || definition == null || IsHD(gameObject)) return;

			var texture = TextureSequenceManager.GetFrameZero(definition.texture);
			var material = MaterialAssets.Find(definition.material);
			if (!MaterialUtils.IsEmissive(material)) material = null;
			var emissive = MaterialUtils.EmissiiveColour(material, Color.white * 1.2f);

			var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
			for (int i = 0; i < meshRenderers.Length; i++)
				ReplaceRendererMaterials(meshRenderers[i], texture, material, emissive);

			var skinnedRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			for (int i = 0; i < skinnedRenderers.Length; i++)
				ReplaceRendererMaterials(skinnedRenderers[i], texture, material, emissive);
		}

		private static void ReplaceRendererMaterials(Renderer renderer, Texture2D texture, Material replacementMaterial, Color emissive)
		{
			if (renderer == null) return;

			var sharedMaterials = renderer.sharedMaterials;
			if (sharedMaterials == null || sharedMaterials.Length == 0) return;

			var replacementArray = new Material[sharedMaterials.Length];
			for (int i = 0; i < sharedMaterials.Length; i++)
			{
				var source = sharedMaterials[i];
				if (source == null) continue;

				var copy = new Material(source);
				if (texture) copy.mainTexture = texture;
				if (replacementMaterial) copy.color = emissive;
				replacementArray[i] = copy;
			}

			renderer.materials = replacementArray;
		}
	}
}
