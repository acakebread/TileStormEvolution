using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public static class GeometryManager
	{
		private static Dictionary<string, GameObject> prefabCache = new();

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

			string path = $"{PreviewSettings.GeometryPath}{geomName}".Replace(".x", "");
			prefab = Resources.Load<GameObject>(path);
			if (prefab == null)
			{
				Debug.LogWarning($"GeometryManager: Prefab not found at {path}");
				return null;
			}

			prefabCache[geomName] = prefab;
			return prefab;
		}

		//workaround for the fact that TileDefs are really prefab definitions
		public static GameObject InstantiatePrefab(DatabaseLoader.TileDef tileDef, Transform parent, Vector3 position) => InstantiateTile(tileDef, parent, position);

		// Instantiates a GameObject based on TileDef, with optional texture animation and collider
		public static GameObject InstantiateTile(DatabaseLoader.TileDef tileDef, Transform parent, Vector3 position, bool interactive = false)
		{
			if (tileDef == null || string.IsNullOrEmpty(tileDef.szGeom))
			{
				Debug.LogWarning("GeometryManager: Invalid TileDef or geometry name.");
				return CreateFallbackTile(parent, position);
			}

			var prefab = GetPrefab(tileDef.szGeom);
			if (prefab == null)
			{
				Debug.LogWarning($"GeometryManager: Prefab {tileDef.szGeom} not found for TileDef {tileDef.szType}.");
				return CreateFallbackTile(parent, position);
			}

			var gameObject = Object.Instantiate(prefab, position, Quaternion.identity, parent);
			gameObject.name = tileDef.szGeom;

			// Apply texture animation
			var theme = DatabaseLoader.Themes.FirstOrDefault(t => t.name == tileDef.szTheme);
			if (theme != null)
			{
				var animator = gameObject.AddComponent<TextureSetAnimator>();
				animator.Initialize(TextureSetManager.GetTextureFrames(tileDef.szTheme));
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

			return gameObject;
		}

		// Creates a debug tile (e.g., for tile_invisible or spare tiles)
		public static GameObject CreateDebugTile(Transform parent, Vector3 position, bool isSpareTile = false)
		{
			var gameObject = new GameObject(isSpareTile ? "SpareTile" : "DebugTile");
			gameObject.transform.SetParent(parent, false);
			gameObject.transform.position = position;

			var meshFilter = gameObject.AddComponent<MeshFilter>();
			var meshRenderer = gameObject.AddComponent<MeshRenderer>();

			// Create a flattened cube mesh
			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			Mesh originalMesh = cube.GetComponent<MeshFilter>().sharedMesh;
			Mesh newMesh = Object.Instantiate(originalMesh);
			Vector3[] vertices = newMesh.vertices;
			for (int i = 0; i < vertices.Length; i++)
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
			meshRenderer.material = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.3f, 0.15f, 1f) };

			return gameObject;
		}

		// Creates a spare tile by copying mesh and material from a source tile
		public static GameObject CreateSpareTile(GameObject sourceTile, Transform parent, Vector3 position)
		{
			if (null == sourceTile) return null;
			var spareTile = new GameObject("SpareTile");
			spareTile.transform.SetParent(parent, false);
			spareTile.transform.position = position;

			var sourceRenderer = sourceTile.GetComponentInChildren<MeshRenderer>();
			var sourceFilter = sourceTile.GetComponentInChildren<MeshFilter>();
			var spareRenderer = spareTile.AddComponent<MeshRenderer>();
			var spareFilter = spareTile.AddComponent<MeshFilter>();

			if (sourceRenderer != null && sourceFilter != null)
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

		// Fallback tile for missing prefabs
		private static GameObject CreateFallbackTile(Transform parent, Vector3 position)
		{
			var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject.transform.SetParent(parent, false);
			gameObject.transform.position = position + new Vector3(0f, -0.1f, 0f);
			gameObject.transform.localScale = new Vector3(1f, 0.1f, 1f);
			gameObject.name = "Fallback_Cube";
			return gameObject;
		}

		// Clears cache (optional, for resource management)
		public static void ClearCache()
		{
			prefabCache.Clear();
		}
	}
}
