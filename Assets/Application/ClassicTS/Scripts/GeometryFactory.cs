using UnityEngine;

namespace ClassicTilestorm
{
	public static class GeometryFactory
	{
		// Fallback tile for missing prefabs
		public static GameObject CreateFallbackTile(Transform parent, Vector3 position)
		{
			var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
			urpMaterial.color = new Color(0.25f, 0.25f, 0.35f, 1f); // Set desired color

			var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject.GetComponent<Renderer>().material = urpMaterial;
			gameObject.transform.SetParent(parent, false);
			gameObject.transform.position = position;// + new Vector3(0f, -0.1f, 0f);//we can't do this because it gets overwritten when the level is loaded!! TileWorldPosition resets vertical offset
			gameObject.transform.localScale = new Vector3(1f, 0.1f, 1f);
			gameObject.name = "Fallback_Cube";
			return gameObject;
		}

		// Creates a debug tile (e.g., for tile_invisible or spare tiles)
		public static GameObject CreateDebugTile(Transform parent, Vector3 position, bool isSpareTile = false)
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
	}
}
