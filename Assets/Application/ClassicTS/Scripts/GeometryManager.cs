//using UnityEngine;
//public static class GeometryManager { public static GameObject Get(string name) => Resources.Load<GameObject>($"{PreviewSettings.GeometryPath}{name}".Replace(".x", "")); }

using UnityEngine;
using System.Collections.Generic;

public static class GeometryManager
{
	private static Dictionary<string, GameObject> prefabs = new();

	public static GameObject Get(string name)
	{
		if (true == prefabs.ContainsKey(name)) return prefabs[name];
		var geomPath = $"{PreviewSettings.GeometryPath}{name}".Replace(".x", "");
		prefabs[name] = Resources.Load<GameObject>(geomPath);//loads as a prefab
		return prefabs[name];
	}
}

// Gork's failed attempt to stremaling it

//using UnityEngine;
//using System.Collections.Generic;

//public static class GeometryManager
//{
//	private static Dictionary<string, Mesh> geometryCache = new();

//	public static GameObject Get(string name)
//	{
//		// Normalize the name by removing ".x" if present
//		string normalizedName = name.Replace(".x", "");

//		// Check if the mesh is already cached
//		if (!geometryCache.ContainsKey(normalizedName))
//		{
//			// Load the GameObject from Resources
//			string geomPath = $"{PreviewSettings.GeometryPath}{normalizedName}";
//			GameObject loadedObj = Resources.Load<GameObject>(geomPath);

//			if (loadedObj == null)
//			{
//				Debug.LogError($"Failed to load geometry at path: {geomPath}");
//				return null;
//			}

//			// Extract the Mesh from the MeshFilter component
//			MeshFilter meshFilter = loadedObj.GetComponentInChildren<MeshFilter>();
//			if (meshFilter == null || meshFilter.sharedMesh == null)
//			{
//				Debug.LogError($"No MeshFilter or Mesh found in geometry: {normalizedName}");
//				return null;
//			}

//			// Cache the Mesh (sharedMesh ensures we get the actual mesh asset)
//			geometryCache[normalizedName] = meshFilter.sharedMesh;
//		}

//		// Create a new GameObject with the cached Mesh
//		GameObject newObj = new GameObject(normalizedName);
//		MeshFilter newMeshFilter = newObj.AddComponent<MeshFilter>();
//		newMeshFilter.sharedMesh = geometryCache[normalizedName];

//		// Add a MeshRenderer (required for the mesh to be visible)
//		MeshRenderer renderer = newObj.AddComponent<MeshRenderer>();

//		// Optionally, assign a default material (you can customize this)
//		renderer.material = new Material(Shader.Find("Standard"));

//		return newObj;
//	}
//}
