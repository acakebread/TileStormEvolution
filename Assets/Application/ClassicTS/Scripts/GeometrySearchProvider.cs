using ClassicTilestorm;
using MassiveHadronLtd;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class GeometrySearchProvider
{
	private static bool hasRegistered = false;
	private static string[] searchRoots;

	// Per-root cache: root → dictionary of name → GameObject
	private static readonly Dictionary<string, Dictionary<string, GameObject>> rootCaches = new();

	public static void Register()
	{
		if (hasRegistered) return;
		hasRegistered = true;

		var geometryPath = AssetPath.GeometryPath;
		var configuredRoot = string.IsNullOrWhiteSpace(geometryPath)
			? null
			: geometryPath.Replace("\\", "/").Trim('/');

		var roots = new List<string>();
		if (!string.IsNullOrEmpty(configuredRoot))
			roots.Add(configuredRoot);
		roots.Add("Levels");

		searchRoots = roots.ToArray();

		Debug.Log($"[GeometrySearchProvider] Registered. Roots: {string.Join(", ", searchRoots)}");

		PrefabFactory.CustomSearchProvider = FindModelByName;
	}

	private static GameObject FindModelByName(string modelName)
	{
		if (string.IsNullOrEmpty(modelName)) return null;

		var cleanName = Path.GetFileNameWithoutExtension(modelName).Trim();

		string targetName = cleanName;

		// Only apply remapping if enabled
		if (UseRemapping)
		{
			var preferredName = ClassicTileStormAssetRemapHelper.RemapName(cleanName);
			if (preferredName != cleanName)
			{
				// PHASE 1: Try preferred (remapped) name
				var preferred = FindInAnyRoot(preferredName, isPreferred: true, originalName: cleanName);
				if (preferred != null) return preferred;

				// PHASE 2: Fall back to original
				targetName = cleanName;
			}
		}
		// If remapping is disabled, or remap returned same name, just use original
		var result = FindInAnyRoot(targetName, isPreferred: false, originalName: cleanName);
		if (result != null) return result;

		Debug.LogWarning($"[GeometrySearchProvider] Model '{modelName}' ('{cleanName}') not found.");
		return null;
	}

	private static GameObject FindInAnyRoot(string candidateName, bool isPreferred, string originalName)
	{
		foreach (string root in searchRoots)
		{
			var cacheKey = string.IsNullOrEmpty(root) ? "" : root;

			if (!rootCaches.TryGetValue(cacheKey, out var nameDict))
			{
				nameDict = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

				GameObject[] allInRoot = Resources.LoadAll<GameObject>(root);

				foreach (var go1 in allInRoot)
				{
					if (go1 != null && !nameDict.ContainsKey(go1.name))
					{
						nameDict[go1.name] = go1;
					}
				}

				rootCaches[cacheKey] = nameDict;

				Debug.Log($"[GeometrySearchProvider] Scanned root '{root}' → {allInRoot.Length} assets cached.");
			}

			if (nameDict.TryGetValue(candidateName, out GameObject go))
			{
				// Only log "Upgraded/Remapped" if we actually used a different name
				if (isPreferred && candidateName != originalName)
				{
					Debug.Log($"[GeometrySearchProvider] Upgraded/Remapped: '{originalName}' → '{candidateName}' found in root '{root}'");
				}
				// Optional: quiet success for normal case
				// else Debug.Log($"[GeometrySearchProvider] Found standard model '{originalName}' in root '{root}'");

				return go;
			}
		}
		return null;
	}

	// NEW: Global toggle for remapping behavior
	private static bool useRemapping = true;
	public static bool UseRemapping
	{
		get => useRemapping;
		set
		{
			if (useRemapping != value)
			{
				useRemapping = value;
				// When toggled, clear the root caches so next lookup rescans with new logic
				rootCaches.Clear();
				Debug.Log($"[GeometrySearchProvider] Geometry remapping {(value ? "enabled" : "disabled")}. Caches cleared.");
			}
		}
	}
}


//private static GameObject FindModelByName(string modelName)
//{
//	if (string.IsNullOrEmpty(modelName)) return null;

//	string cleanName = Path.GetFileNameWithoutExtension(modelName).Trim();
//	string hdName = cleanName + "_hd";

//	// PHASE 1: Try HD version across all roots
//	GameObject hd = FindInAnyRoot(hdName, isHd: true);
//	if (hd != null) return hd;

//	// PHASE 2: Fall back to legacy
//	GameObject legacy = FindInAnyRoot(cleanName, isHd: false);
//	if (legacy != null) return legacy;

//	Debug.LogWarning($"[GeometrySearchProvider] Model '{modelName}' not found (neither '{hdName}' nor '{cleanName}') in any root.");
//	return null;
//}

//private static GameObject FindInAnyRoot(string candidateName, bool isHd)
//{
//	foreach (string root in searchRoots)
//	{
//		// Normalize root for cache key
//		string cacheKey = string.IsNullOrEmpty(root) ? "" : root;

//		// Build cache on-demand for this root
//		if (!rootCaches.TryGetValue(cacheKey, out var nameDict))
//		{
//			nameDict = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

//			string loadPath = root; // empty string = all Resources
//			GameObject[] allInRoot = Resources.LoadAll<GameObject>(loadPath);

//			foreach (var go1 in allInRoot)
//			{
//				if (go1 != null && !nameDict.ContainsKey(go1.name))
//				{
//					nameDict[go1.name] = go1;
//				}
//			}

//			rootCaches[cacheKey] = nameDict;

//			Debug.Log($"[GeometrySearchProvider] Scanned root '{loadPath}' → {allInRoot.Length} assets cached.");
//		}

//		// Now lookup by name (fast)
//		if (nameDict.TryGetValue(candidateName, out GameObject go))
//		{
//			if (isHd)
//			{
//				Debug.Log($"[GeometrySearchProvider] Upgraded: '{Path.GetFileNameWithoutExtension(candidateName)}' → HD version found in root '{root}'");
//			}
//			return go;
//		}
//	}

//	return null;
//}

//using UnityEngine;
//using MassiveHadronLtd;
//using System.IO;
//using System.Collections.Generic;

//namespace ClassicTilestorm
//{
//	/// <summary>
//	/// Geometry search provider that uses exactly two roots:
//	/// 1. Whatever is set in PreviewSettings → AssetPath.GeometryPath
//	/// 2. "Levels"
//	/// 
//	/// Call GeometrySearchProvider.Register() from MainController.Awake()
//	/// before any models are loaded.
//	/// </summary>
//	public static class GeometrySearchProvider
//	{
//		private static bool hasRegistered = false;
//		private static string[] searchRoots;

//		/// <summary>
//		/// Call this once from MainController.Awake() (early, before LoadMap)
//		/// </summary>
//		public static void Register()
//		{
//			if (hasRegistered)
//			{
//				Debug.Log("[GeometrySearchProvider] Already registered.");
//				return;
//			}

//			hasRegistered = true;

//			// NOW safe to read — called after PreviewSettings.Awake()
//			string geometryPath = AssetPath.GeometryPath; // or PreviewSettings.GeometryPath — same thing

//			string configuredRoot = string.IsNullOrWhiteSpace(geometryPath)
//				? null
//				: geometryPath.Replace("\\", "/").Trim('/');

//			// Build exactly the two roots you asked for
//			var rootsList = new List<string>();
//			//if (!string.IsNullOrEmpty(configuredRoot))
//			//{
//			//	rootsList.Add(configuredRoot);
//			//}
//			rootsList.Add("Levels/Med");

//			searchRoots = rootsList.ToArray();

//			Debug.Log($"[GeometrySearchProvider] Registered. Search roots: {string.Join(", ", searchRoots)}");

//			PrefabFactory.CustomSearchProvider = FindModelByName;
//		}

//		private static GameObject FindModelByName(string modelName)
//		{
//			if (string.IsNullOrEmpty(modelName)) return null;

//			string cleanName = Path.GetFileNameWithoutExtension(modelName).Trim();

//			// Split into base name and try _hd variant first
//			string hdName = cleanName + "_hd";
//			string[] candidates = { hdName, cleanName }; // HD first, then legacy

//			foreach (string root in searchRoots)
//			{
//				foreach (string candidate in candidates)
//				{
//					string fullPath = string.IsNullOrEmpty(root) ? candidate : $"{root}/{candidate}";

//					GameObject go = Resources.Load<GameObject>(fullPath);
//					if (go != null)
//					{
//						if (candidate == hdName)
//						{
//							Debug.Log($"[GeometrySearchProvider] Upgraded: '{cleanName}' → Found HD version at Resources/{fullPath}");
//						}
//						// else: legacy found — silent is fine
//						return go;
//					}
//				}
//			}

//			// Optional extra layer: common subfolders
//			string[] commonSubfolders = { "Tiles", "Boundaries", "Props", "Geometry", "Models" };
//			foreach (string sub in commonSubfolders)
//			{
//				foreach (string root in searchRoots)
//				{
//					foreach (string candidate in candidates)
//					{
//						string fullPath = string.IsNullOrEmpty(root)
//							? $"{sub}/{candidate}"
//							: $"{root}/{sub}/{candidate}";

//						GameObject go = Resources.Load<GameObject>(fullPath);
//						if (go != null)
//						{
//							if (candidate == hdName)
//							{
//								Debug.Log($"[GeometrySearchProvider] Upgraded: '{cleanName}' → Found HD version at Resources/{fullPath}");
//							}
//							return go;
//						}
//					}
//				}
//			}

//			Debug.LogWarning($"[GeometrySearchProvider] Model '{modelName}' ('{cleanName}') not found — no HD or legacy version in any root.");
//			return null;
//		}

//#if UNITY_EDITOR
//		[UnityEditor.MenuItem("ClassicTilestorm/Reset GeometrySearchProvider Registration")]
//		public static void ResetRegistration()
//		{
//			hasRegistered = false;
//			Debug.Log("[GeometrySearchProvider] Registration reset for next play.");
//		}
//#endif
//	}
//}



//private static GameObject FindModelByName(string modelName)
//{
//	if (string.IsNullOrEmpty(modelName)) return null;

//	string cleanName = Path.GetFileNameWithoutExtension(modelName).Trim();

//	// Try each root
//	foreach (string root in searchRoots)
//	{
//		string fullPath = string.IsNullOrEmpty(root) ? cleanName : $"{root}/{cleanName}";

//		GameObject go = Resources.Load<GameObject>(fullPath);
//		if (go != null)
//		{
//			// Optional success log
//			// Debug.Log($"[GeometrySearchProvider] Found '{cleanName}' at Resources/{fullPath}");
//			return go;
//		}
//	}

//	// Optional extra: try common subfolders under each root
//	string[] commonSubs = { "Tiles", "Boundaries", "Props", "Geometry", "Models" };
//	foreach (string sub in commonSubs)
//	{
//		foreach (string root in searchRoots)
//		{
//			string fullPath = string.IsNullOrEmpty(root)
//				? $"{sub}/{cleanName}"
//				: $"{root}/{sub}/{cleanName}";

//			GameObject go = Resources.Load<GameObject>(fullPath);
//			if (go != null)
//			{
//				// Debug.Log($"[GeometrySearchProvider] Found '{cleanName}' at Resources/{fullPath}");
//				return go;
//			}
//		}
//	}

//	Debug.LogWarning($"[GeometrySearchProvider] Model '{modelName}' ('{cleanName}') not found in roots: {string.Join(", ", searchRoots)}");
//	return null;
//}

//// File: GeometrySearchProvider.cs (in ClassicTilestorm folder)
//using UnityEngine;
//using MassiveHadronLtd;
//using System.Collections.Generic;
//using System.IO;

//namespace ClassicTilestorm
//{
//	public static class GeometrySearchProvider
//	{
//		private static Dictionary<string, GameObject> nameToPrefab;
//		private static bool hasScanned = false;
//		private static bool hasRegistered = false;

//		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//		private static void Initialize()
//		{
//			if (hasRegistered) return;

//			hasRegistered = true;
//			PrefabFactory.CustomSearchProvider = FindPrefabByName;

//			Debug.Log($"[GeometrySearchProvider] Registered search provider. GeometryPath = '{AssetPath.GeometryPath}'");
//		}

//		private static GameObject FindPrefabByName(string modelName)
//		{
//			if (string.IsNullOrEmpty(modelName)) return null;

//			string cleanName = Path.GetFileNameWithoutExtension(modelName);

//			if (!hasScanned)
//			{
//				ScanGeometryFolder();
//			}

//			if (nameToPrefab != null && nameToPrefab.TryGetValue(cleanName, out GameObject prefab))
//			{
//				return prefab;
//			}

//			Debug.LogWarning($"[GeometrySearchProvider] Could not find geometry prefab named '{cleanName}' (from model '{modelName}')");
//			return null;
//		}

//		private static void ScanGeometryFolder()
//		{
//			hasScanned = true;
//			nameToPrefab = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

//			string rootPath = AssetPath.GeometryPath;

//			// Critical: Normalize the path properly
//			rootPath = rootPath.Replace("\\", "/").Trim('/');

//			if (string.IsNullOrEmpty(rootPath))
//			{
//				Debug.LogError("[GeometrySearchProvider] AssetPath.GeometryPath is empty!");
//				return;
//			}

//			Debug.Log($"[GeometrySearchProvider] Scanning Resources folder: '{rootPath}'");

//			GameObject[] prefabs = Resources.LoadAll<GameObject>(rootPath);

//			Debug.Log($"[GeometrySearchProvider] Found {prefabs.Length} prefab(s) under '{rootPath}'");

//			foreach (var prefab in prefabs)
//			{
//				if (prefab != null)
//				{
//					string pname = prefab.name;
//					if (!nameToPrefab.ContainsKey(pname))
//					{
//						nameToPrefab[pname] = prefab;
//					}
//					Debug.Log($"[GeometrySearchProvider] → Registered: '{pname}'");
//				}
//			}

//			if (prefabs.Length == 0)
//			{
//				Debug.LogError($"[GeometrySearchProvider] NO PREFABS FOUND! Check:\n" +
//							   $"• Is GeometryPath set correctly in PreviewSettings? Current: '{AssetPath.GeometryPath}'\n" +
//							   $"• Are your prefabs inside a folder named 'Resources'?\n" +
//							   $"• Example expected path: Assets/Resources/{rootPath}/ss_tile_boundary.prefab\n" +
//							   $"• Try setting GeometryPath to 'ClassicTS/Geometry' (no trailing slash)");
//			}
//		}

//#if UNITY_EDITOR
//		[UnityEditor.MenuItem("ClassicTilestorm/Force Rescan Geometry Prefabs")]
//		public static void ForceRescan()
//		{
//			hasScanned = false;
//			nameToPrefab = null;
//			Debug.Log("[GeometrySearchProvider] Cache cleared — will rescan on next load.");
//		}
//#endif
//	}
//}