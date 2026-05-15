using System;
using UnityEngine;
using MassiveHadronLtd;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace ClassicTilestorm.Assets
{
	/// <summary>
	/// Typed access to model geometry assets (FBX/OBJ/etc placed in Resources)
	/// </summary>
	public static class ModelAssets
	{
		public readonly struct ModelEntry
		{
			public readonly string DisplayName;
			public readonly string HashId;
			public readonly string ResourcePath;
			public readonly string FilePath;

			public ModelEntry(string displayName, string hashId, string resourcePath = null, string filePath = null)
			{
				DisplayName = displayName;
				HashId = hashId;
				ResourcePath = resourcePath;
				FilePath = filePath;
			}
		}

		private static readonly Dictionary<string, GameObject> ReferenceCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, GameObject> ImportedInstanceCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly List<ModelEntry> CachedEntries = new();

		public static void RegisterRoot(string root) => AssetRegistry<GameObject>.RegisterModelRoot(root);
		public static void ClearCache()
		{
			AssetRegistry<GameObject>.ClearModelCache();
			ReferenceCache.Clear();
			foreach (var go in ImportedInstanceCache.Values)
				DestroyLoadedObject(go);
			ImportedInstanceCache.Clear();
			CachedEntries.Clear();
			ModelResourceTable.ClearRuntimeCache();
		}

		public static void RefreshRegistry(bool forceRefresh = false)
		{
			if (!forceRefresh && CachedEntries.Count > 0)
				return;

			ReferenceCache.Clear();
			CachedEntries.Clear();
			ModelResourceTable.Refresh(forceRefresh);

			foreach (var entry in ModelResourceTable.GetEntries(false)
					 .Select(e => new ModelEntry(
						 displayName: e.DisplayName,
						 hashId: e.HashId,
						 resourcePath: e.ResourcePath,
						 filePath: e.FilePath))
					 .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
			{
				CachedEntries.Add(entry);
			}
		}

		public static IReadOnlyList<ModelEntry> GetModelEntries(bool forceRefresh = false)
		{
			RefreshRegistry(forceRefresh);
			return CachedEntries;
		}

		public static IReadOnlyList<string> GetModelDisplayNames(bool forceRefresh = false)
			=> GetModelEntries(forceRefresh).Select(e => e.DisplayName).ToArray();

		public static GameObject Find(string modelName)
		{
			if (string.IsNullOrWhiteSpace(modelName))
				return null;

			RefreshRegistry(false);

			string key = modelName.Trim();
			if (ReferenceCache.TryGetValue(key, out var cached))
				return cached;

			var asset = LoadByKey(key);

			if (asset != null)
			{
				ReferenceCache[key] = asset;
			}

			return asset;
		}

		public static GameObject Instantiate(string modelName, Transform parent = null)
		{
			var asset = Find(modelName);
			if (asset == null) return null;

			var instance = UnityEngine.Object.Instantiate(asset, parent);
			PrepareInstanceForScene(instance, asset);
			instance.name = GetDisplayNameForHash(modelName) ?? System.IO.Path.GetFileNameWithoutExtension(modelName);
			return instance;
		}

		public static GameObject Instantiate(string modelName, Vector3 position, Transform parent = null)
		{
			var go = Instantiate(modelName, parent);
			if (go) go.transform.position = position;
			return go;
		}

		public static GameObject Instantiate(string modelName, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			var instance = Instantiate(modelName, parent);
			if (instance != null)
			{
				instance.transform.position = position;
				instance.transform.rotation = rotation;
			}
			return instance;
		}

		// THIS IS ALL YOU NEED — direct access, used for both init and toggle
		public static Func<string, string> NameRemapper
		{
			get => AssetRegistry<GameObject>.NameRemapper;
			set => AssetRegistry<GameObject>.NameRemapper = value;
		}

		private static GameObject LoadByKey(string key)
		{
			if (ModelResourceTable.TryGetEntry(key, out var entry))
				return LoadEntry(ToModelEntry(entry));

			string hash = GetHashForDisplayName(key);
			return !string.IsNullOrWhiteSpace(hash) && ModelResourceTable.TryGetEntry(hash, out entry)
				? LoadEntry(ToModelEntry(entry))
				: AssetRegistry<GameObject>.FindModel(key);
		}

		private static ModelEntry ToModelEntry(ModelResourceTable.Entry entry)
		{
			return new ModelEntry(
				displayName: entry.DisplayName,
				hashId: entry.HashId,
				resourcePath: entry.ResourcePath,
				filePath: entry.FilePath);
		}

		private static GameObject LoadEntry(ModelEntry entry)
		{
			if (!string.IsNullOrWhiteSpace(entry.ResourcePath))
				return AssetRegistry<GameObject>.FindModel(entry.ResourcePath);

			if (ImportedInstanceCache.TryGetValue(entry.HashId, out var cached) && cached != null)
				return cached;

			if (string.IsNullOrWhiteSpace(entry.FilePath) || !File.Exists(entry.FilePath))
				return null;

			var go = WavefrontUtility.Load(
				entry.FilePath,
				Path.GetFileNameWithoutExtension(entry.FilePath),
				asTemplate: true);
			if (go != null)
				ImportedInstanceCache[entry.HashId] = go;
			return go;
		}

		public static string GetDisplayNameForHash(string hash)
		{
			return ModelResourceTable.GetDisplayName(hash);
		}

		public static string GetHashForDisplayName(string displayName)
		{
			return ModelResourceTable.GetHashForDisplayName(displayName);
		}

		private static void PrepareInstanceForScene(GameObject instance, GameObject source)
		{
			if (instance == null) return;

			bool needsSanitizing = source != null &&
				((source.hideFlags & HideFlags.HideAndDontSave) != 0 || !source.activeSelf);

			if (!needsSanitizing)
				return;

			instance.hideFlags = HideFlags.None;
			instance.SetActive(true);

			foreach (var child in instance.GetComponentsInChildren<Transform>(true))
				child.gameObject.hideFlags = HideFlags.None;
		}

		private static void DestroyLoadedObject(UnityEngine.Object obj)
		{
			if (obj == null) return;

			if (Application.isPlaying)
				UnityEngine.Object.Destroy(obj);
			else
				UnityEngine.Object.DestroyImmediate(obj);
		}
	}

	/// <summary>
	/// Typed access to runtime prefabs (e.g. flame, spark, pickups)
	/// </summary>
	public static class PrefabAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<GameObject>.RegisterPrefabRoot(root);
		public static void ClearCache() => AssetRegistry<GameObject>.ClearPrefabCache();
		public static GameObject Find(string prefabName) => AssetRegistry<GameObject>.FindPrefab(prefabName);

		// === DUPLICATED FROM ModelAssets — safe and clear ===
		public static GameObject Instantiate(string prefabName, Transform parent = null)
		{
			var asset = Find(prefabName);
			if (asset == null) return null;

			var instance = UnityEngine.Object.Instantiate(asset, parent);
			instance.name = System.IO.Path.GetFileNameWithoutExtension(prefabName);
			return instance;
		}

		public static GameObject Instantiate(string prefabName, Vector3 position, Transform parent = null)
		{
			var go = Instantiate(prefabName, parent);
			if (go) go.transform.position = position;
			return go;
		}

		public static GameObject Instantiate(string prefabName, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			var instance = Instantiate(prefabName, parent);
			if (instance != null)
			{
				instance.transform.position = position;
				instance.transform.rotation = rotation;
			}
			return instance;
		}
	}

	/// <summary>
	/// Typed access to textures
	/// </summary>
	public static class TextureAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Texture>.RegisterTextureRoot(root);
		public static void ClearCache() => AssetRegistry<Texture>.ClearTextureCache();
		public static Texture Find(string textureName) => AssetRegistry<Texture>.FindTexture(textureName);
	}

	/// <summary>
	/// Typed access to materials
	/// </summary>
	public static class MaterialAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Material>.RegisterMaterialRoot(root);
		public static void ClearCache() => AssetRegistry<Material>.ClearMaterialCache();
		public static Material Find(string materialName)
		{
			if (string.IsNullOrWhiteSpace(materialName))
				return null;

			if (MaterialResourceTable.TryResolveResourceKey(materialName, out var resourceKey))
				return AssetRegistry<Material>.FindMaterial(resourceKey);

			return AssetRegistry<Material>.FindMaterial(materialName);
		}
	}

	/// <summary>
	/// Typed access to skybox assets (materials)
	/// </summary>
	public static class SkyboxAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Material>.RegisterSkyboxRoot(root);
		public static void ClearCache() => AssetRegistry<Material>.ClearSkyboxCache();
		public static Material Find(string skyboxName)
		{
			if (string.IsNullOrWhiteSpace(skyboxName))
				return null;

			if (Assets.SkycubeResourceTable.TryResolveResourceKey(skyboxName, out var resourceKey))
				return AssetRegistry<Material>.FindSkybox(resourceKey);

			return AssetRegistry<Material>.FindSkybox(skyboxName);
		}
	}

	/// <summary>
	/// Typed access to sound assets (AudioClips)
	/// </summary>
	public static class SoundAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<AudioClip>.RegisterSoundRoot(root);
		public static void ClearCache() => AssetRegistry<AudioClip>.ClearSoundCache();
		public static AudioClip Find(string clipName)
		{
			if (string.IsNullOrWhiteSpace(clipName))
				return null;

			if (SoundResourceTable.TryResolveResourceKey(clipName, out var resourceKey))
				return AssetRegistry<AudioClip>.FindSound(resourceKey);

			return AssetRegistry<AudioClip>.FindSound(clipName);
		}
	}

	/// <summary>
	/// Typed access to music assets (AudioClips)
	/// </summary>
	public static class MusicAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<AudioClip>.RegisterMusicRoot(root);
		public static void ClearCache() => AssetRegistry<AudioClip>.ClearMusicCache();
		public static AudioClip Find(string clipName)
		{
			if (string.IsNullOrWhiteSpace(clipName))
				return null;

			if (Assets.MusicResourceTable.TryResolveResourceKey(clipName, out var resourceKey))
				return AssetRegistry<AudioClip>.FindMusic(resourceKey);

			return AssetRegistry<AudioClip>.FindMusic(clipName);
		}
	}
}

namespace ClassicTilestorm.Assets
{
	public static class ProjectAssets
	{
		// ──────────────────────────────────────────────────────────────
		//  Cached name listings for all asset types (loaded only once)
		// ──────────────────────────────────────────────────────────────

		// CHANGED: now keyed by (Type + Category) so Material vs Skybox don't collide
		private static readonly Dictionary<(Type Type, string Category), (List<string> Names, bool Cached)> NameCaches
			= new Dictionary<(Type, string), (List<string>, bool)>();

		private static (Type Type, string Category) GetCacheKey<T>(string category = null) where T : UnityEngine.Object
			=> (typeof(T), category ?? string.Empty);

		private static void EnsureCached<T>(Func<IEnumerable<string>> loadFunc, string category = null) where T : UnityEngine.Object
		{
			var key = GetCacheKey<T>(category);

			if (!NameCaches.TryGetValue(key, out var entry) || !entry.Cached)
			{
				var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var name in loadFunc())
				{
					if (!string.IsNullOrEmpty(name))
						names.Add(name);
				}

				NameCaches[key] = (
					names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
					true
				);
			}
		}

		private static IReadOnlyList<string> GetNames<T>(Func<IEnumerable<string>> loadFunc, bool forceRefresh = false, string category = null)
			where T : UnityEngine.Object
		{
			var key = GetCacheKey<T>(category);

			if (forceRefresh)
			{
				NameCaches.Remove(key);   // safe even if the key didn't exist
			}

			EnsureCached<T>(loadFunc, category);
			return NameCaches.TryGetValue(key, out var entry) ? entry.Names ?? new List<string>() : new List<string>();
		}

		// ── Public API for each major type ────────────────────────────────
		// (only the two Material-based ones changed – all others are identical to before)

		public static IReadOnlyList<string> GetModelNames(bool forceRefresh = false)
			=> GetNames<GameObject>(() =>
			{
				var roots = AssetRegistry<GameObject>.GetRegisteredModelRoots()
					.Where(r => !string.IsNullOrWhiteSpace(r))
					.Select(r => r.Trim('/').Trim())
					.Where(r => !string.IsNullOrWhiteSpace(r))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToArray();

				if (roots.Length == 0)
					roots = new[] { AssetPath.GeometryPath?.Trim('/')?.Trim() ?? "" };

				return GetAssetNamesFromRoots<GameObject>(roots);
			}, forceRefresh);

		public static IReadOnlyList<string> GetPrefabNames(bool forceRefresh = false)
			=> GetNames<GameObject>(() => GetAssetNamesFromRoots<GameObject>(new[]
			{
				AssetPath.PrefabPath?.Trim('/') ?? ""
			}), forceRefresh);

		public static IReadOnlyList<string> GetTextureNames(bool forceRefresh = false)
			=> GetNames<Texture>(() => GetAssetNamesFromRoots<Texture>(new[]
			{
				AssetPath.TexturePath?.Trim('/') ?? ""
			}), forceRefresh);

		public static IReadOnlyList<string> GetMaterialNames(bool forceRefresh = false)
			=> GetNames<Material>(() => GetAssetNamesFromRoots<Material>(new[]
			{
				AssetPath.MaterialPath?.Trim('/') ?? ""
			}), forceRefresh, "Material");

		public static IReadOnlyList<string> GetSkycubeNames(bool forceRefresh = false)
			=> GetNames<Material>(() => GetAssetNamesFromRoots<Material>(new[]
			{
				AssetPath.SkycubesPath?.Trim('/') ?? ""
			}), forceRefresh, "Skycube");

		public static IReadOnlyList<string> GetMusicNames(bool forceRefresh = false)
			=> GetNames<AudioClip>(() => GetAssetNamesFromRoots<AudioClip>(new[]
			{
				AssetPath.MusicPath?.Trim('/') ?? ""
			}), forceRefresh);

		public static IReadOnlyList<string> GetSoundNames(bool forceRefresh = false)
			=> GetNames<AudioClip>(() => GetAssetNamesFromRoots<AudioClip>(new[]
			{
				AssetPath.SoundPath?.Trim('/') ?? ""
			}), forceRefresh, "Sound");

		/// <summary>
		/// Force refresh all name caches (call after significant asset changes)
		/// </summary>
		public static void RefreshAllNameCaches() => NameCaches.Clear();

		// ── Core loading helper ─────────────────────────────────────────────
		private static IEnumerable<string> GetAssetNamesFromRoots<T>(string[] roots) where T : UnityEngine.Object
		{
			string manifestName = AssetManifestConfig.GetManifestName<T>(roots);
			return ResourceUtils.GetAssetNamesFromResources<T>(roots, manifestName);
		}
	}
}
