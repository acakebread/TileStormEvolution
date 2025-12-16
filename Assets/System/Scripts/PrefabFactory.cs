using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace MassiveHadronLtd
{
	// Optional delegate for projects that want filename-based search
	public delegate GameObject PrefabSearchProvider(string modelName);

	public static class PrefabFactory
	{
		private static readonly Dictionary<string, GameObject> prefabCache = new();

		// This is the ONLY project-specific hook — optional!
		public static PrefabSearchProvider CustomSearchProvider { get; set; }

		private static readonly string[] ModelExtensions = { ".x", ".fbx", ".obj", ".blend", ".prefab" };

		private static GameObject GetPrefab(string pathOrName)
		{
			if (string.IsNullOrEmpty(pathOrName))
			{
				Debug.LogWarning("PrefabFactory: Empty path/name provided.");
				return null;
			}

			string loadKey = StripExtensions(pathOrName);

			// 1. Fast path: already cached
			if (prefabCache.TryGetValue(loadKey, out var cached))
				return cached;

			// 2. Try direct Resources.Load (original just-in-time behavior)
			var direct = Resources.Load<GameObject>(loadKey);
			if (direct != null)
			{
				prefabCache[loadKey] = direct;
				return direct;
			}

			// 3. Optional: Ask the project if it has a custom way to find by name
			if (CustomSearchProvider != null)
			{
				string cleanName = Path.GetFileNameWithoutExtension(pathOrName);
				var fromProvider = CustomSearchProvider(cleanName);
				if (fromProvider != null)
				{
					prefabCache[loadKey] = fromProvider;
					prefabCache[cleanName] = fromProvider; // cache under name too
					return fromProvider;
				}
			}

			Debug.LogWarning($"PrefabFactory: Could not find prefab '{pathOrName}' (tried direct load and custom search).");
			return null;
		}

		// All Instantiate methods unchanged — just use GetPrefab(modelName)
		public static GameObject Instantiate(string modelName, Transform parent = null)
		{
			var prefab = GetPrefab(modelName);
			if (prefab == null) return null;

			var go = parent ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
			go.name = Path.GetFileNameWithoutExtension(modelName);
			return go;
		}

		public static GameObject Instantiate(string modelName, Vector3 position, Transform parent = null)
		{
			var go = Instantiate(modelName, parent);
			if (go) go.transform.position = position;
			return go;
		}

		public static GameObject Instantiate(string modelName, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			var go = Instantiate(modelName, parent);
			if (go)
			{
				go.transform.position = position;
				go.transform.rotation = rotation;
			}
			return go;
		}

		public static void ClearCache()
		{
			prefabCache.Clear();
		}

		private static string StripExtensions(string path) =>
			ResourcePathUtils.NormalizeForResourcesLoad(path, ModelExtensions);
	}
}

//using UnityEngine;
//using System.Collections.Generic;
//using System.IO;

//namespace MassiveHadronLtd
//{
//	public static class PrefabFactory
//	{
//		private static readonly Dictionary<string, GameObject> prefabCache = new();

//		private static readonly string[] ModelExtensions = { ".x", ".fbx", ".obj", ".blend" }; // add more if needed

//		private static GameObject GetPrefab(string fullPath)
//		{
//			if (string.IsNullOrEmpty(fullPath))
//			{
//				Debug.LogWarning("PrefabManager: Empty prefab path provided.");
//				return null;
//			}

//			string loadPath = StripExtensions(fullPath);

//			if (prefabCache.TryGetValue(loadPath, out var prefab))
//				return prefab;

//			prefab = Resources.Load<GameObject>(loadPath);
//			if (prefab == null)
//			{
//				Debug.LogWarning($"PrefabManager: Prefab not found at {loadPath} (original: {fullPath})");
//				return null;
//			}

//			prefabCache[loadPath] = prefab;
//			return prefab;

//			static string StripExtensions(string path) => ResourcePathUtils.NormalizeForResourcesLoad(path, ModelExtensions);
//		}

//		public static GameObject Instantiate(string fullPath, Transform parent = null)
//		{
//			var prefab = GetPrefab(fullPath);
//			if (prefab == null) return null;

//			var go = parent != null ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
//			go.name = Path.GetFileNameWithoutExtension(fullPath); // clean name without extension
//			return go;
//		}

//		public static GameObject Instantiate(string fullPath, Vector3 position, Transform parent = null)
//		{
//			var go = Instantiate(fullPath, parent);
//			if (go != null)
//			{
//				go.transform.position = position;
//				go.transform.rotation = Quaternion.identity;
//			}
//			return go;
//		}

//		public static GameObject Instantiate(string fullPath, Vector3 position, Quaternion rotation, Transform parent = null)
//		{
//			var go = Instantiate(fullPath, parent);
//			if (go != null)
//			{
//				go.transform.position = position;
//				go.transform.rotation = rotation;
//			}
//			return go;
//		}

//		public static void ClearCache() => prefabCache.Clear();
//	}
//}