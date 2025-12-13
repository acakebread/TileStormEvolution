using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MassiveHadronLtd
{
	public static class PrefabFactory
	{
		private static readonly Dictionary<string, GameObject> prefabCache = new();

		private static readonly string[] ModelExtensions = { ".x", ".fbx", ".obj", ".blend" }; // add more if needed

		private static GameObject GetPrefab(string fullPath)
		{
			if (string.IsNullOrEmpty(fullPath))
			{
				Debug.LogWarning("PrefabManager: Empty prefab path provided.");
				return null;
			}

			string loadPath = StripExtensions(fullPath);

			if (prefabCache.TryGetValue(loadPath, out var prefab))
				return prefab;

			prefab = Resources.Load<GameObject>(loadPath);
			if (prefab == null)
			{
				Debug.LogWarning($"PrefabManager: Prefab not found at {loadPath} (original: {fullPath})");
				return null;
			}

			prefabCache[loadPath] = prefab;
			return prefab;
		}

		private static string StripExtensions(string path)
		{
			string normalized = path.Replace('\\', '/');

			string directory = Path.GetDirectoryName(normalized).Replace('\\', '/');
			string fileName = Path.GetFileNameWithoutExtension(normalized);

			while (ModelExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
			{
				fileName = Path.GetFileNameWithoutExtension(fileName);
			}

			if (string.IsNullOrEmpty(directory))
				return fileName;

			return $"{directory}/{fileName}";
		}

		public static GameObject InstantiatePrefab(string fullPath, Transform parent = null)
		{
			var prefab = GetPrefab(fullPath);
			if (prefab == null) return null;

			var go = parent != null ? Object.Instantiate(prefab, parent) : Object.Instantiate(prefab);
			go.name = Path.GetFileNameWithoutExtension(fullPath); // clean name without extension
			return go;
		}

		public static GameObject InstantiatePrefab(string fullPath, Vector3 position, Transform parent = null)
		{
			var go = InstantiatePrefab(fullPath, parent);
			if (go != null)
			{
				go.transform.position = position;
				go.transform.rotation = Quaternion.identity;
			}
			return go;
		}

		public static GameObject InstantiatePrefab(string fullPath, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			var go = InstantiatePrefab(fullPath, parent);
			if (go != null)
			{
				go.transform.position = position;
				go.transform.rotation = rotation;
			}
			return go;
		}

		public static void ClearCache() => prefabCache.Clear();
	}
}