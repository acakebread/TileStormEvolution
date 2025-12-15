using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class DefinitionFactory
    {
		public static GameObject Instantiate(
			Definition definition,
			Vector3? position = null,
			Quaternion? rotation = null,
			Transform parent = null)
		{
			if (definition == null || string.IsNullOrEmpty(definition.model))
				return null; // or handle fallback as needed

			string prefabPath = $"{AssetPath.GeometryPath}{definition.model}";

			GameObject gameObject;

			// Decide which PrefabFactory overload to use based on what's provided
			if (position.HasValue && rotation.HasValue)
			{
				gameObject = PrefabFactory.Instantiate(prefabPath, position.Value, rotation.Value, parent);
			}
			else if (position.HasValue)
			{
				gameObject = PrefabFactory.Instantiate(prefabPath, position.Value, parent);
			}
			else
			{
				gameObject = PrefabFactory.Instantiate(prefabPath, parent);
			}

			if (null == gameObject) return null;
			//Apply Definition Properties

			//temporary special placeholder flag setting for special properties in absence of definition editor 
			if (definition.model.Contains("tree"))
				definition.bSway = true;//ToDo implement sway in definition editor - hard set to trees for now

			if ("Caustic" == definition.texture)
				definition.material = "toxic";
			//temporary special placeholder flag setting for special properties in absence of definition editor 

			// Apply texture animation
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			textureAnimator.Initialize(TextureSetManager.GetTextureSequence(definition.texture, AssetPath.TexturePath));

			// Add collider for interactive tiles
			if (definition.bDrag)
			{
				var collider = gameObject.AddComponent<BoxCollider>();
				collider.size = new Vector3(1f, 0.1f, 1f);
				collider.center = new Vector3(0f, -0.05f, 0f);
			}

			if (definition.bSway)
			{
				var meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);//Workaround until the definition editor is implemented
				if (null != meshRenderer)
				{
					var filter = meshRenderer.GetComponent<MeshFilter>();
					if (filter != null && filter.IsRuntimeWritable())
					{
						var morphGeomSway = gameObject.AddComponent<MorphGeomSway>();
						morphGeomSway.SetCustomInfluenceVolume(Vector3.up, 0.2f);
						morphGeomSway.swayInfluencePower = 0.5f; // More top sway
						morphGeomSway.ConfigureSubdivision(true, 0.3f); // Enable stratification with maxSegmentLength for influence volume
					}
					else Debug.LogError($"geometry not writable in: {definition.model}");
				}
			}

			if (!string.IsNullOrEmpty(definition.material))
			{ 
				var targetRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
				if (targetRenderer != null)
				{
					// Always load the material defined in the definition
					var materialPath = $"{AssetPath.MaterialPath}{definition.material}";
					Material material = MaterialCache.Get(materialPath);

					if (null != material)
					{
						// Apply the material
						targetRenderer.material = material;

						// Check if this material is intended to be emissive
						bool isEmissive = MaterialUtils.isEmissive(material);

						if (isEmissive && null != textureAnimator)
						{
							// Initial sync
							textureAnimator.ApplyFrame(0);

							// Subscribe to texture changes to drive the emission map
							textureAnimator.OnTextureChanged += (newTexture) =>
							{
								if (null != targetRenderer && null != targetRenderer.material)
								{
									Material mat = targetRenderer.material;

									// Restore the original base texture (important for correct albedo)
									mat.mainTexture = material.mainTexture;

									// Optionally restore main texture offset/scale if your original material uses tiling
									mat.mainTextureOffset = material.mainTextureOffset;
									mat.mainTextureScale = material.mainTextureScale;

									// Drive the emission map with the animated texture
									mat.SetTexture("_EmissionMap", newTexture);

									// Ensure emission is enabled in case it was disabled
									mat.EnableKeyword("_EMISSION");
								}
							};
						}

						// Optional: Add a point light for extra glow effect when emissive
						if (isEmissive)
						{
							// Avoid adding multiple lights if this runs more than once
							var existingLight = gameObject.GetComponent<Light>();
							if (existingLight == null)
							{
								var pointLight = gameObject.AddComponent<Light>();
								pointLight.type = LightType.Point;
								pointLight.color = material.GetColor("_EmissionColor"); // Use actual emission color if available
								pointLight.intensity = 2f; // Adjust based on desired brightness
								pointLight.range = 3f;     // Adjust based on object size
								pointLight.shadows = LightShadows.None;
							}
						}
					}
				}
				else
				{
					Debug.LogWarning($"Material not found: {definition.material}");//suppress for now
				}
			}

#if DEBUG
			gameObject.AddComponent<RTTI>().definition = definition; // This is for debug in editor only - do not use RTTI
#endif

			return gameObject;
		}
#if DEBUG
		private class RTTI : MonoBehaviour { public Definition definition; }//debug class so Definition data can be seen in the inspector
#endif
	}
}