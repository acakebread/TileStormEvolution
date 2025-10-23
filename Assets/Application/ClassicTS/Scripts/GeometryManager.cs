using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public class RTTI : MonoBehaviour { public DatabaseSerializer.TileDef tileDef; }

	public static class GeometryManager
	{
		private static readonly Dictionary<string, GameObject> prefabCache = new();

		// Loads a prefab from Resources, caching it for performance
		private static GameObject GetPrefab(string geomName)
		{
			if (string.IsNullOrEmpty(geomName))
			{
				Debug.LogWarning("GeometryManager: Empty geometry name provided.");
				return null;
			}

			if (prefabCache.TryGetValue(geomName, out var prefab))
				return prefab;

			var path = $"{PreviewSettings.GeometryPath}{geomName}".Replace(".x", "");
			prefab = Resources.Load<GameObject>(path);
			if (prefab == null)
			{
				Debug.LogWarning($"GeometryManager: Prefab not found at {path}");
				return null;
			}

			prefabCache[geomName] = prefab;
			return prefab;
		}

		// Workaround for the fact that TileDefs are really prefab definitions
		public static GameObject InstantiatePrefab(DatabaseSerializer.TileDef tileDef, Transform parent, Vector3 position) => InstantiateTile(tileDef, parent, position);

		// Instantiates a GameObject based on TileDef, with optional texture animation and collider
		public static GameObject InstantiateTile(DatabaseSerializer.TileDef tileDef, Transform parent, Vector3 position, bool interactive = false)
		{
			if (null == tileDef || string.IsNullOrEmpty(tileDef.szGeom))
			{
				if (tileDef.szType == "tile_invisible")
				{
					if (PreviewSettings.ShowHiddenTiles)
					{
						var debug_tile = CreateDebugTile(parent, position);
#if DEBUG
						debug_tile.AddComponent<RTTI>().tileDef = tileDef; // This is for debug in editor only - do not use RTTI
#endif
						return debug_tile;
					}
					return null;
				}

				Debug.LogWarning("GeometryManager: Invalid TileDef or geometry name.");
				var result = CreateFallbackTile(parent, position);
#if DEBUG
				result.AddComponent<RTTI>().tileDef = tileDef; // This is for debug in editor only - do not use RTTI
#endif
				return result;
			}

			var prefab = GetPrefab(tileDef.szGeom);
			if (null == prefab)
			{
				Debug.LogWarning($"GeometryManager: Prefab {tileDef.szGeom} not found for TileDef {tileDef.szType}.");
				return CreateFallbackTile(parent, position);
			}

			var gameObject = Object.Instantiate(prefab, position, Quaternion.identity, parent);
			gameObject.name = tileDef.szGeom.Replace(".x", "");

			// Apply texture animation
			var theme = DatabaseSerializer.Themes.FirstOrDefault(t => t.name == tileDef.szTheme);
			if (null != theme)
			{
				var animator = gameObject.AddComponent<TextureSetAnimator>();
				animator.Initialize(TextureSetManager.GetTextureFrames(tileDef.szTheme));

				if ("Caustic" == tileDef.szTheme)
				{
					var pointLight = gameObject.AddComponent<Light>();
					pointLight.type = LightType.Point;
					pointLight.color = Color.green;
					pointLight.intensity = 1f;
					pointLight.range = 1f;
					pointLight.shadows = LightShadows.None;

					var targetRenderer = animator.GetComponentInChildren<MeshRenderer>(true);
					if (targetRenderer != null)
					{
						// Load the preallocated material
						Material emissiveMaterial = MaterialManager.Get("toxic");
						if (emissiveMaterial == null)
						{
							Debug.LogWarning("Preallocated material 'toxic' not found. Creating fallback.");
							emissiveMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
							emissiveMaterial.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f)); // Black with full alpha
							emissiveMaterial.EnableKeyword("_EMISSION");
							emissiveMaterial.SetColor("_EmissionColor", new Color(0f, 1f, 0f) * 2.0f); // Green emission
																									   // ... other initial setup as needed ...
						}

						// Apply the preallocated material to the target renderer
						targetRenderer.material = emissiveMaterial;// new Material(emissiveMaterial); // Use a new instance to avoid shared material issues
						Material activeMaterial = targetRenderer.material;

						//// Configure for transparent rendering (override if not set in 'toxic')
						//activeMaterial.SetFloat("_Surface", 1); // Transparent
						//activeMaterial.SetFloat("_Blend", 0); // Alpha Blend
						//activeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
						//activeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
						//activeMaterial.SetInt("_ZWrite", 0);
						//activeMaterial.SetFloat("_SpecularHighlights", 0);
						//activeMaterial.SetFloat("_EnvironmentReflections", 0);

						// Sync with TextureSetAnimator
						var textureAnimator = gameObject.GetComponent<TextureSetAnimator>();
						if (textureAnimator != null)
						{
							textureAnimator.ApplyTexture(0); // Initial sync
							textureAnimator.OnTextureChanged += (newTexture) =>
							{
								if (targetRenderer != null && targetRenderer.material != null)
								{
									targetRenderer.material.mainTexture = null;
									Material mat = targetRenderer.material;
									mat.SetTexture("_EmissionMap", newTexture); // Update emission map
									//Debug.Log("Emission Map updated: " + newTexture.name + ", Material Instance: " + mat.GetInstanceID());
								}
							};
						}
					}
				}
			}
			else
			{
				Debug.LogWarning($"GeometryManager: No theme found for {tileDef.szTheme}, type {tileDef.szType}.");
			}

			// Add collider for interactive tiles
			if (interactive)
			{
				var collider = gameObject.AddComponent<BoxCollider>();
				collider.size = new Vector3(1f, 0.1f, 1f);
				collider.center = new Vector3(0f, -0.05f, 0f);
			}
#if DEBUG
			gameObject.AddComponent<RTTI>().tileDef = tileDef; // This is for debug in editor only - do not use RTTI
#endif

			var meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
			if (meshRenderer != null)
			{
				var filter = meshRenderer.GetComponent<MeshFilter>();
				if (filter != null && filter.IsRuntimeWritable())
				{
					var morphGeomSway = gameObject.AddComponent<MorphGeomSway>();
					morphGeomSway.SetCustomInfluenceVolume(Vector3.up, 0.2f);
					morphGeomSway.swayInfluencePower = 0.5f; // More top sway
					morphGeomSway.ConfigureSubdivision(true, 0.3f); // Enable stratification with maxSegmentLength for influence volume
				}
			}

			return gameObject;

			// Fallback tile for missing prefabs
			static GameObject CreateFallbackTile(Transform parent, Vector3 position)
			{
				var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
				urpMaterial.color = new Color(0.25f, 0.25f, 0.35f, 1f); // Set desired color

				var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
				gameObject.GetComponent<Renderer>().material = urpMaterial;
				gameObject.transform.SetParent(parent, false);
				gameObject.transform.position = position + new Vector3(0f, -0.1f, 0f);
				gameObject.transform.localScale = new Vector3(1f, 0.1f, 1f);
				gameObject.name = "Fallback_Cube";
				return gameObject;
			}
		}

		// Creates a debug tile (e.g., for tile_invisible or spare tiles)
		private static GameObject CreateDebugTile(Transform parent, Vector3 position, bool isSpareTile = false)
		{
			var gameObject = new GameObject(isSpareTile ? "spare_tile" : "debug_Tile");
			gameObject.transform.SetParent(parent, false);
			gameObject.transform.position = position;

			var meshFilter = gameObject.AddComponent<MeshFilter>();
			var meshRenderer = gameObject.AddComponent<MeshRenderer>();

			// Create a flattened cube mesh
			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var originalMesh = cube.GetComponent<MeshFilter>().sharedMesh;
			var newMesh = Object.Instantiate(originalMesh);
			var vertices = newMesh.vertices;
			for (var i = 0; i < vertices.Length; ++i)
			{
				vertices[i].y *= 0.05f;
				vertices[i].y -= 0.05f;
			}
			newMesh.vertices = vertices;
			newMesh.RecalculateBounds();
			newMesh.RecalculateNormals();
			meshFilter.mesh = newMesh;
			Object.Destroy(cube);

			// Apply debug material
			meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
			return gameObject;
		}

		// Creates a spare tile by copying mesh and material from a source tile
		public static GameObject CreateSpareTile(GameObject sourceTile, Transform parent, Vector3 position)
		{
			if (null == sourceTile || null == parent) return null;
			var spareTile = new GameObject("SpareTile");
			spareTile.transform.SetParent(parent, false);
			spareTile.transform.position = position;

			var sourceRenderer = sourceTile.GetComponentInChildren<MeshRenderer>();
			var sourceFilter = sourceTile.GetComponentInChildren<MeshFilter>();
			var spareRenderer = spareTile.AddComponent<MeshRenderer>();
			var spareFilter = spareTile.AddComponent<MeshFilter>();

			if (null != sourceRenderer && null != sourceFilter)
			{
				spareFilter.sharedMesh = sourceFilter.sharedMesh;
				spareRenderer.material = sourceRenderer.material;
				spareRenderer.transform.rotation = sourceRenderer.transform.rotation;
				spareRenderer.transform.localScale = sourceRenderer.transform.localScale;
			}
			else
			{
				Debug.LogWarning("GeometryManager: Source tile lacks MeshRenderer or MeshFilter.");
				Object.Destroy(spareTile);
				return CreateDebugTile(parent, position, true);
			}

			return spareTile;
		}

		// Clears cache (optional, for resource management)
		public static void ClearCache() => prefabCache.Clear();
	}
}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using MassiveHadronLtd;

//namespace ClassicTilestorm
//{
//	public class RTTI : MonoBehaviour { public DatabaseSerializer.TileDef tileDef; }

//	public static class GeometryManager
//	{
//		private static readonly Dictionary<string, GameObject> prefabCache = new();

//		// Loads a prefab from Resources, caching it for performance
//		private static GameObject GetPrefab(string geomName)
//		{
//			if (string.IsNullOrEmpty(geomName))
//			{
//				Debug.LogWarning("GeometryManager: Empty geometry name provided.");
//				return null;
//			}

//			if (prefabCache.TryGetValue(geomName, out var prefab))
//				return prefab;

//			var path = $"{PreviewSettings.GeometryPath}{geomName}".Replace(".x", "");
//			prefab = Resources.Load<GameObject>(path);
//			if (prefab == null)
//			{
//				Debug.LogWarning($"GeometryManager: Prefab not found at {path}");
//				return null;
//			}

//			prefabCache[geomName] = prefab;
//			return prefab;
//		}

//		//workaround for the fact that TileDefs are really prefab definitions
//		public static GameObject InstantiatePrefab(DatabaseSerializer.TileDef tileDef, Transform parent, Vector3 position) => InstantiateTile(tileDef, parent, position);

//		// Instantiates a GameObject based on TileDef, with optional texture animation and collider
//		public static GameObject InstantiateTile(DatabaseSerializer.TileDef tileDef, Transform parent, Vector3 position, bool interactive = false)
//		{
//			if (null == tileDef || string.IsNullOrEmpty(tileDef.szGeom))
//			{
//				if (tileDef.szType == "tile_invisible")
//				{
//					if (PreviewSettings.ShowHiddenTiles)
//					{
//						var debug_tile = CreateDebugTile(parent, position);
//#if DEBUG
//						debug_tile.AddComponent<RTTI>().tileDef = tileDef;//this is for debug in editor only - do not use RTTI
//#endif
//						return debug_tile;
//					}
//					return null;
//				}

//				Debug.LogWarning("GeometryManager: Invalid TileDef or geometry name.");
//				var result = CreateFallbackTile(parent, position);
//#if DEBUG
//				result.AddComponent<RTTI>().tileDef = tileDef;//this is for debug in editor only - do not use RTTI
//#endif
//				return result;
//			}

//			var prefab = GetPrefab(tileDef.szGeom);
//			if (null == prefab)
//			{
//				Debug.LogWarning($"GeometryManager: Prefab {tileDef.szGeom} not found for TileDef {tileDef.szType}.");
//				return CreateFallbackTile(parent, position);
//			}

//			var gameObject = Object.Instantiate(prefab, position, Quaternion.identity, parent);
//			gameObject.name = tileDef.szGeom.Replace(".x", "");

//			// Apply texture animation
//			var theme = DatabaseSerializer.Themes.FirstOrDefault(t => t.name == tileDef.szTheme);
//			if (null != theme)
//			{
//				var animator = gameObject.AddComponent<TextureSetAnimator>();
//				animator.Initialize(TextureSetManager.GetTextureFrames(tileDef.szTheme));

//				//special case for caustic for now
//				if ("Caustic" == tileDef.szTheme)
//				{
//					var target = animator.GetComponentInChildren<MeshRenderer>(true);
//					if (null!= target)
//					{
//						target.material.color = Color.green;
//					}
//				}
//			}
//			else
//			{
//				Debug.LogWarning($"GeometryManager: No theme found for {tileDef.szTheme}, type {tileDef.szType}.");
//			}

//			// Add collider for interactive tiles
//			if (interactive)
//			{
//				var collider = gameObject.AddComponent<BoxCollider>();
//				collider.size = new Vector3(1f, 0.1f, 1f);
//				collider.center = new Vector3(0f, -0.05f, 0f);
//			}
//#if DEBUG
//			gameObject.AddComponent<RTTI>().tileDef = tileDef;//this is for debug in editor only - do not use RTTI
//#endif

//			var meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
//			if (meshRenderer != null)
//			{
//				var filter = meshRenderer.GetComponent<MeshFilter>();
//				if (filter != null && filter.IsRuntimeWritable())// || tiles[n].GameObject.name.Contains("door")
//				{
//					var morphGeomSway = gameObject.AddComponent<MorphGeomSway>();
//					morphGeomSway.SetCustomInfluenceVolume(Vector3.up, 0.2f);
//					morphGeomSway.swayInfluencePower = 0.5f; // More top sway
//					morphGeomSway.ConfigureSubdivision(true, 0.3f); // Enable stratification with maxSegmentLength for influence volume
//				}
//			}

//			return gameObject;

//			// Fallback tile for missing prefabs
//			static GameObject CreateFallbackTile(Transform parent, Vector3 position)
//			{
//				var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
//				urpMaterial.color = new Color(0.25f, 0.25f, 0.35f, 1f); // Set desired color

//				var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
//				gameObject.GetComponent<Renderer>().material = urpMaterial;
//				gameObject.transform.SetParent(parent, false);
//				gameObject.transform.position = position + new Vector3(0f, -0.1f, 0f);
//				gameObject.transform.localScale = new Vector3(1f, 0.1f, 1f);
//				gameObject.name = "Fallback_Cube";
//				return gameObject;
//			}
//		}

//		// Creates a debug tile (e.g., for tile_invisible or spare tiles)
//		private static GameObject CreateDebugTile(Transform parent, Vector3 position, bool isSpareTile = false)
//		{
//			var gameObject = new GameObject(isSpareTile ? "spare_tile" : "debug_Tile");
//			gameObject.transform.SetParent(parent, false);
//			gameObject.transform.position = position;

//			var meshFilter = gameObject.AddComponent<MeshFilter>();
//			var meshRenderer = gameObject.AddComponent<MeshRenderer>();

//			// Create a flattened cube mesh
//			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//			var originalMesh = cube.GetComponent<MeshFilter>().sharedMesh;
//			var newMesh = Object.Instantiate(originalMesh);
//			var vertices = newMesh.vertices;
//			for (var i = 0; i < vertices.Length; ++i)
//			{
//				vertices[i].y *= 0.05f;
//				vertices[i].y -= 0.05f;
//			}
//			newMesh.vertices = vertices;
//			newMesh.RecalculateBounds();
//			newMesh.RecalculateNormals();
//			meshFilter.mesh = newMesh;
//			Object.Destroy(cube);

//			// Apply debug material
//			meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
//			return gameObject;
//		}

//		// Creates a spare tile by copying mesh and material from a source tile
//		public static GameObject CreateSpareTile(GameObject sourceTile, Transform parent, Vector3 position)
//		{
//			if (null == sourceTile || null == parent) return null;
//			var spareTile = new GameObject("SpareTile");
//			spareTile.transform.SetParent(parent, false);
//			spareTile.transform.position = position;

//			var sourceRenderer = sourceTile.GetComponentInChildren<MeshRenderer>();
//			var sourceFilter = sourceTile.GetComponentInChildren<MeshFilter>();
//			var spareRenderer = spareTile.AddComponent<MeshRenderer>();
//			var spareFilter = spareTile.AddComponent<MeshFilter>();

//			if (null != sourceRenderer && null != sourceFilter)
//			{
//				spareFilter.sharedMesh = sourceFilter.sharedMesh;
//				spareRenderer.material = sourceRenderer.material;
//				spareRenderer.transform.rotation = sourceRenderer.transform.rotation;
//				spareRenderer.transform.localScale = sourceRenderer.transform.localScale;
//			}
//			else
//			{
//				Debug.LogWarning("GeometryManager: Source tile lacks MeshRenderer or MeshFilter.");
//				Object.Destroy(spareTile);
//				return CreateDebugTile(parent, position, true);
//			}

//			return spareTile;
//		}

//		// Clears cache (optional, for resource management)
//		public static void ClearCache() => prefabCache.Clear();
//	}
//}
