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

		public static GameObject Instantiate(Definition definition, Transform parent = null)
		{
			var gameObject = PrefabFactory.InstantiatePrefab($"{GeometryPath}{definition.model}", parent);
			AppyDefinitionProperties(gameObject, definition);
			return gameObject;
		}

		public static GameObject Instantiate(Definition definition, Vector3 position, Transform parent = null)
		{
			var gameObject = PrefabFactory.InstantiatePrefab($"{GeometryPath}{definition.model}", position, parent);
			AppyDefinitionProperties(gameObject, definition);
			return gameObject;
		}

		public static GameObject Instantiate(Definition definition, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			var gameObject = PrefabFactory.InstantiatePrefab($"{GeometryPath}{definition.model}", position, rotation, parent);
			AppyDefinitionProperties(gameObject, definition);
			return gameObject;
		}

		private static void AppyDefinitionProperties(GameObject gameObject, Definition definition)
		{
			if (null == gameObject) return;

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

			var meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);//Workaround until the definition editor is implemented
			if (null != meshRenderer)
			{
				var filter = meshRenderer.GetComponent<MeshFilter>();
				definition.bSway = filter != null && filter.IsRuntimeWritable();
			}

			if (definition.bSway)//ToDo add flag for sway to definition
			{
				var morphGeomSway = gameObject.AddComponent<MorphGeomSway>();
				morphGeomSway.SetCustomInfluenceVolume(Vector3.up, 0.2f);
				morphGeomSway.swayInfluencePower = 0.5f; // More top sway
				morphGeomSway.ConfigureSubdivision(true, 0.3f); // Enable stratification with maxSegmentLength for influence volume
			}

			//if (definition.material)//ToDo implement material override
			{
				if ("Caustic" == definition.texture)
				{
					var pointLight = gameObject.AddComponent<Light>();
					pointLight.type = LightType.Point;
					pointLight.color = Color.green;
					pointLight.intensity = 1f;
					pointLight.range = 1f;
					pointLight.shadows = LightShadows.None;

					var targetRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
					if (targetRenderer != null)
					{
						// Load the preallocated material
						//Material emissiveMaterial = MaterialCache.Get("toxic");
						var materialPath = $"{MaterialPath}toxic";
						Material emissiveMaterial = MaterialCache.Get(materialPath);
						if (emissiveMaterial == null)
						{
							Debug.LogWarning("Preallocated material 'toxic' not found. Creating fallback.");
							emissiveMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
							emissiveMaterial.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f)); // Black with full alpha
							emissiveMaterial.EnableKeyword("_EMISSION");
							emissiveMaterial.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 2.0f); // Green emission
						}
						// Apply the preallocated material to the target renderer
						targetRenderer.material = emissiveMaterial; // new Material(emissiveMaterial); // Use a new instance to avoid shared material issues

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
			}

#if DEBUG
			gameObject.AddComponent<RTTI>().definition = definition; // This is for debug in editor only - do not use RTTI
#endif
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
	}

#if DEBUG
	public class RTTI : MonoBehaviour { public Definition definition; }//debug class so Definition data can be seen in the inspector
#endif
}