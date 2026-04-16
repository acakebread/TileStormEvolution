using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class GeometryFactory
	{
		// Fallback tile for missing prefabs
		public static GameObject CreateFallbackTile(Transform parent, Vector3 position, Quaternion rotation = default)
		{
			if (rotation == default) rotation = Quaternion.identity;

			var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
			urpMaterial.color = new Color(0.25f, 0.25f, 0.35f, 1f);

			var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
			gameObject.GetComponent<Renderer>().material = urpMaterial;
			gameObject.transform.SetParent(parent, false);
			gameObject.transform.position = position;
			gameObject.transform.rotation = rotation;
			gameObject.transform.localScale = new Vector3(1f, 0.1f, 1f);
			gameObject.name = "Fallback_Cube";

			LeakDetector.TrackCreation(urpMaterial, "Material", "GeometryFactory.Fallback");
			LeakDetector.TrackCreation(gameObject, "GameObject", "GeometryFactory.Fallback");

			return gameObject;
		}

		// Creates a debug tile
		public static GameObject CreateDebugTile(Transform parent, Vector3 position, Quaternion rotation = default, bool isSpareTile = false)
		{
			if (rotation == default) rotation = Quaternion.identity;

			var gameObject = new GameObject(isSpareTile ? "spare_tile" : "debug_Tile");
			gameObject.transform.SetParent(parent, false);
			gameObject.transform.position = position;
			gameObject.transform.rotation = rotation;

			var meshFilter = gameObject.AddComponent<MeshFilter>();
			var meshRenderer = gameObject.AddComponent<MeshRenderer>();

			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var originalMesh = cube.GetComponent<MeshFilter>().sharedMesh;
			var newMesh = Object.Instantiate(originalMesh);
			newMesh.name = "DebugMesh";

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

			var debugMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
			debugMat.name = "DebugMaterial";
			meshRenderer.material = debugMat;

			LeakDetector.TrackCreation(newMesh, "Mesh", "GeometryFactory.DebugTile");
			LeakDetector.TrackCreation(debugMat, "Material", "GeometryFactory.DebugTile");
			LeakDetector.TrackCreation(gameObject, "GameObject", "GeometryFactory.DebugTile");

			return gameObject;
		}

		// Spare tile (no new heavy objects)
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
				Object.Destroy(spareTile);
				return CreateDebugTile(parent, position, isSpareTile: true);
			}

			LeakDetector.TrackCreation(spareTile, "GameObject", "GeometryFactory.SpareTile");

			return spareTile;
		}
	}
}

//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public static class GeometryFactory
//	{
//		// Fallback tile for missing prefabs
//		public static GameObject CreateFallbackTile(Transform parent, Vector3 position, Quaternion rotation = default)
//		{
//			if (rotation == default) rotation = Quaternion.identity;

//			var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
//			urpMaterial.color = new Color(0.25f, 0.25f, 0.35f, 1f);

//			var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
//			gameObject.GetComponent<Renderer>().material = urpMaterial;
//			gameObject.transform.SetParent(parent, false);
//			gameObject.transform.position = position;// + new Vector3(0f, -0.1f, 0f);//we can't do this because it gets overwritten when the level is loaded!! TileWorldPosition resets vertical offset
//			gameObject.transform.rotation = rotation;
//			gameObject.transform.localScale = new Vector3(1f, 0.1f, 1f);
//			gameObject.name = "Fallback_Cube";
//			return gameObject;
//		}

//		// Creates a debug tile (e.g., for tile_invisible or spare tiles)
//		public static GameObject CreateDebugTile(Transform parent, Vector3 position, Quaternion rotation = default, bool isSpareTile = false)
//		{
//			if (rotation == default) rotation = Quaternion.identity;

//			var gameObject = new GameObject(isSpareTile ? "spare_tile" : "debug_Tile");
//			gameObject.transform.SetParent(parent, false);
//			gameObject.transform.position = position;
//			gameObject.transform.rotation = rotation;

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
//				return CreateDebugTile(parent, position, isSpareTile: true);
//			}

//			return spareTile;
//		}
//	}
//}




//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public static class GeometryFactory
//	{
//		private static int _createdFallbacks = 0;
//		private static int _createdDebugs = 0;
//		private static int _createdSpares = 0;

//		// Fallback tile for missing prefabs
//		public static GameObject CreateFallbackTile(Transform parent, Vector3 position, Quaternion rotation = default)
//		{
//			if (rotation == default) rotation = Quaternion.identity;

//			var urpMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
//			urpMaterial.color = new Color(0.25f, 0.25f, 0.35f, 1f);
//			urpMaterial.name = "FallbackMaterial_" + _createdFallbacks;

//			var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
//			gameObject.GetComponent<Renderer>().material = urpMaterial;
//			gameObject.transform.SetParent(parent, false);
//			gameObject.transform.position = position;
//			gameObject.transform.rotation = rotation;
//			gameObject.transform.localScale = new Vector3(1f, 0.1f, 1f);
//			gameObject.name = "Fallback_Cube_" + _createdFallbacks;

//			Debug.Log($"[GeometryFactory] Created FallbackTile #{++_createdFallbacks} | Material: {urpMaterial.GetInstanceID()} | GO: {gameObject.GetInstanceID()}");

//			return gameObject;
//		}

//		// Creates a debug tile (e.g., for tile_invisible or spare tiles)
//		public static GameObject CreateDebugTile(Transform parent, Vector3 position, Quaternion rotation = default, bool isSpareTile = false)
//		{
//			if (rotation == default) rotation = Quaternion.identity;

//			var gameObject = new GameObject(isSpareTile ? "spare_tile" : "debug_Tile");
//			gameObject.transform.SetParent(parent, false);
//			gameObject.transform.position = position;
//			gameObject.transform.rotation = rotation;

//			var meshFilter = gameObject.AddComponent<MeshFilter>();
//			var meshRenderer = gameObject.AddComponent<MeshRenderer>();

//			// Create a flattened cube mesh
//			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//			var originalMesh = cube.GetComponent<MeshFilter>().sharedMesh;
//			var newMesh = Object.Instantiate(originalMesh);
//			newMesh.name = "DebugMesh_" + _createdDebugs;

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

//			Object.Destroy(cube);   // clean primitive

//			// Apply debug material
//			var debugMat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
//			debugMat.name = "DebugMaterial_" + _createdDebugs;
//			meshRenderer.material = debugMat;

//			Debug.Log($"[GeometryFactory] Created DebugTile #{++_createdDebugs} (spare={isSpareTile}) | Mesh: {newMesh.GetInstanceID()} | Material: {debugMat.GetInstanceID()} | GO: {gameObject.GetInstanceID()}");

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
//				spareFilter.sharedMesh = sourceFilter.sharedMesh;   // shared = safe, no new mesh
//				spareRenderer.material = sourceRenderer.material;   // shared material = safe
//				spareRenderer.transform.rotation = sourceRenderer.transform.rotation;
//				spareRenderer.transform.localScale = sourceRenderer.transform.localScale;
//			}
//			else
//			{
//				Debug.LogWarning("GeometryFactory: Source tile lacks MeshRenderer or MeshFilter.");
//				Object.Destroy(spareTile);
//				return CreateDebugTile(parent, position, isSpareTile: true);
//			}

//			Debug.Log($"[GeometryFactory] Created SpareTile #{++_createdSpares} from source {sourceTile.name} | GO: {spareTile.GetInstanceID()}");

//			return spareTile;
//		}

//		// Optional: call this from MainController when you know a map is fully unloaded (for debugging)
//		public static void ResetCounters()
//		{
//			Debug.Log($"[GeometryFactory] Reset counters - Fallbacks: {_createdFallbacks}, Debugs: {_createdDebugs}, Spares: {_createdSpares}");
//			_createdFallbacks = _createdDebugs = _createdSpares = 0;
//		}
//	}
//}