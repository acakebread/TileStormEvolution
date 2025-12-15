using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class DefinitionFactory
    {
		private static string geometryPath = null;//"ClassicTS/Geometry/"
		private static string GeometryPath { get => geometryPath ?? PreviewSettings.GeometryPath; set => geometryPath = value; }

		private static string texturePath = null;//"ClassicTS/Textures/"
		private static string TexturePath { get => texturePath ?? PreviewSettings.TexturePath; set => texturePath = value; }

		private static string materialPath = null;//"ClassicTS/Materials/"
		private static string MaterialPath { get => materialPath ?? PreviewSettings.MaterialPath; set => materialPath = value; }

		public static GameObject Instantiate(
			Definition definition,
			Vector3? position = null,
			Quaternion? rotation = null,
			Transform parent = null)
		{
			if (definition == null || string.IsNullOrEmpty(definition.model))
				return null; // or handle fallback as needed

			string prefabPath = $"{GeometryPath}{definition.model}";

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
			{
				Debug.Log(definition.model);
				definition.bSway = true;
			}

			//var meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);//Workaround until the definition editor is implemented
			//if (null != meshRenderer)
			//{
			//	var filter = meshRenderer.GetComponent<MeshFilter>();
			//	definition.bSway = filter != null && filter.IsRuntimeWritable();
			//}

			if ("Caustic" == definition.texture)
				definition.material = "toxic";
			//temporary special placeholder flag setting for special properties in absence of definition editor 

			// Apply texture animation
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			textureAnimator.Initialize(TextureSetManager.GetTextureSequence(definition.texture, TexturePath));

			// Add collider for interactive tiles
			if (definition.bDrag)
			{
				var collider = gameObject.AddComponent<BoxCollider>();
				collider.size = new Vector3(1f, 0.1f, 1f);
				collider.center = new Vector3(0f, -0.05f, 0f);
			}

			if (definition.bSway)//ToDo add flag for sway to definition
			{
				var morphGeomSway = gameObject.AddComponent<MorphGeomSway>();
				morphGeomSway.SetCustomInfluenceVolume(Vector3.up, 0.2f);
				morphGeomSway.swayInfluencePower = 0.5f; // More top sway
				morphGeomSway.ConfigureSubdivision(true, 0.3f); // Enable stratification with maxSegmentLength for influence volume
			}

			if ("toxic" == definition.material)
			{
				var pointLight = gameObject.AddComponent<Light>();
				pointLight.type = LightType.Point;
				pointLight.color = Color.green;
				pointLight.intensity = 1f;
				pointLight.range = 1f;
				pointLight.shadows = LightShadows.None;

				var targetRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
				if (null != targetRenderer)
				{
					// Load the preallocated material
					var materialPath = $"{MaterialPath}{definition.material}";
					Material material = MaterialCache.Get(materialPath);
					if (material == null)
					{
						Debug.LogWarning("Preallocated material 'toxic' not found. Creating fallback.");
						material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
						material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f)); // Black with full alpha
						material.EnableKeyword("_EMISSION");
						material.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 2.0f); // Green emission
					}
					// Apply the preallocated material to the target renderer
					targetRenderer.material = material;

					// Sync with TextureSetAnimator
					textureAnimator.ApplyFrame(0); // Initial sync - calls ApplyFrame(0) which sets mainTexture
					textureAnimator.OnTextureChanged += (newTexture) =>
					{
						if (targetRenderer != null && targetRenderer.material != null)
						{
							Material mat = targetRenderer.material;
							mat.mainTexture = null; // Clear main texture (base color stays black)
							mat.SetTexture("_EmissionMap", newTexture); // Update emission map with animated texture
						}
					};
				}
			}

#if DEBUG
			gameObject.AddComponent<RTTI>().definition = definition; // This is for debug in editor only - do not use RTTI
#endif

			return gameObject;
		}

		//ToDo implement instead of legacy pass through
		public static GameObject InstantiateTile(Definition definition, Transform parent, Vector3 position)
		{
			if (null == definition || string.IsNullOrEmpty(definition.model))
			{
				if (definition?.id == "tile_invisible")
					return PreviewSettings.ShowHiddenTiles ? GeometryFactory.CreateDebugTile(parent, position) : null;

				Debug.LogWarning("GeometryManager: Invalid Definition or geometry name." + definition.id);
				return GeometryFactory.CreateFallbackTile(parent, position);
			}

			return Instantiate(definition, position, Quaternion.identity, parent);
		}

		public static string GetGeometryPrefabPath(string modelName)
		{
			if (string.IsNullOrEmpty(modelName))
				return null;

			return $"{GeometryPath}{modelName}";
		}
	}

#if DEBUG
	public class RTTI : MonoBehaviour { public Definition definition; }//debug class so Definition data can be seen in the inspector
#endif
}