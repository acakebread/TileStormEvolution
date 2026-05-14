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
		public enum ModelSourceKind
		{
			Internal,
			Imported
		}

		public readonly struct ModelEntry
		{
			public readonly string DisplayName;
			public readonly string HashId;
			public readonly ModelSourceKind Source;
			public readonly string ResourcePath;
			public readonly string FilePath;

			public ModelEntry(string displayName, string hashId, ModelSourceKind source, string resourcePath = null, string filePath = null)
			{
				DisplayName = displayName;
				HashId = hashId;
				Source = source;
				ResourcePath = resourcePath;
				FilePath = filePath;
			}
		}

		private static readonly Dictionary<string, string> DisplayNameToHash = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, ModelEntry> HashToEntry = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, GameObject> ReferenceCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, GameObject> ImportedInstanceCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly List<ModelEntry> CachedEntries = new();
		private static bool registryReady;

		public static void RegisterRoot(string root) => AssetRegistry<GameObject>.RegisterModelRoot(root);
		public static void ClearCache()
		{
			AssetRegistry<GameObject>.ClearModelCache();
			DisplayNameToHash.Clear();
			HashToEntry.Clear();
			ReferenceCache.Clear();
			foreach (var go in ImportedInstanceCache.Values)
				DestroyLoadedObject(go);
			ImportedInstanceCache.Clear();
			CachedEntries.Clear();
			registryReady = false;
		}

		public static void RefreshRegistry(bool forceRefresh = false)
		{
			if (registryReady && !forceRefresh && CachedEntries.Count > 0)
				return;

			DisplayNameToHash.Clear();
			HashToEntry.Clear();
			ReferenceCache.Clear();
			CachedEntries.Clear();

			var importedEntries = GetImportedModelEntries().ToList();
			var usedHashes = new HashSet<string>(
				importedEntries.Select(e => e.HashId),
				StringComparer.OrdinalIgnoreCase);

			var internalEntries = ProjectAssets.GetModelNames(forceRefresh: true)
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.Select(n => n.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(name =>
				{
					string hash = CreateStableHashForName(name, usedHashes);
					usedHashes.Add(hash);
					return new ModelEntry(name, hash, ModelSourceKind.Internal, resourcePath: name);
				});

			foreach (var entry in internalEntries.Concat(importedEntries).OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
			{
				DisplayNameToHash[entry.DisplayName] = entry.HashId;
				HashToEntry[entry.HashId] = entry;
				CachedEntries.Add(entry);
			}

			registryReady = true;
		}

		public static IReadOnlyList<ModelEntry> GetModelEntries(bool forceRefresh = false)
		{
			RefreshRegistry(forceRefresh);
			return CachedEntries;
		}

		public static IReadOnlyList<string> GetModelDisplayNames(bool forceRefresh = false)
			=> GetModelEntries(forceRefresh).Select(e => e.DisplayName).ToArray();

		public static bool TryGetHashForModelName(string modelName, out string hash)
		{
			if (string.IsNullOrWhiteSpace(modelName))
			{
				hash = null;
				return false;
			}

			RefreshRegistry(false);
			string key = modelName.Trim();
			if (DisplayNameToHash.TryGetValue(key, out hash))
				return true;

			RefreshRegistry(true);
			return DisplayNameToHash.TryGetValue(key, out hash);
		}

		public static string ResolveDisplayName(string modelReference)
		{
			if (string.IsNullOrWhiteSpace(modelReference))
				return null;

			RefreshRegistry(false);

			string key = modelReference.Trim();
			if (HashToEntry.TryGetValue(key, out var entry))
				return entry.DisplayName;

			if (DisplayNameToHash.ContainsKey(key))
				return key;

			RefreshRegistry(true);
			if (HashToEntry.TryGetValue(key, out entry))
				return entry.DisplayName;

			if (DisplayNameToHash.ContainsKey(key))
				return key;

			return key;
		}

		public static string NormalizeStoredReference(string modelReference)
		{
			if (string.IsNullOrWhiteSpace(modelReference))
				return null;

			RefreshRegistry(false);

			string key = modelReference.Trim();

			if (DisplayNameToHash.TryGetValue(key, out var hash))
				return hash;

			if (HashToEntry.ContainsKey(key))
				return key;

			RefreshRegistry(true);

			if (DisplayNameToHash.TryGetValue(key, out hash))
				return hash;

			if (HashToEntry.ContainsKey(key))
				return key;

			if (LooksLikeCanonicalHash(key))
				return key;

			return HTB50Settings.ToString(RadixHash.GetStableHash32(key));
		}

		public static GameObject Find(string modelName)
		{
			if (string.IsNullOrWhiteSpace(modelName))
				return null;

			RefreshRegistry(false);

			string key = modelName.Trim();
			if (ReferenceCache.TryGetValue(key, out var cached))
				return cached;

			var asset = TryLoadExactReference(key);

			if (asset == null)
			{
				RefreshRegistry(true);
				asset = TryLoadExactReference(key);
			}

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
			instance.name = ResolveDisplayName(modelName) ?? System.IO.Path.GetFileNameWithoutExtension(modelName);
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

		private static string CreateStableHashForName(string modelName, HashSet<string> usedHashes)
		{
			for (int attempt = 0; attempt < 2048; attempt++)
			{
				string seed = attempt == 0 ? modelName : $"{modelName}#{attempt}";
				string hash = HTB50Settings.ToString(RadixHash.GetStableHash32(seed));

				if (!usedHashes.Contains(hash))
					return hash;
			}

			return HTB50Settings.ToString(RadixHash.GetStableHash32($"{modelName}#{Guid.NewGuid():N}"));
		}

		private static IEnumerable<ModelEntry> GetImportedModelEntries()
		{
			string importedRoot = Path.Combine(Application.persistentDataPath, "Imported");
			if (!Directory.Exists(importedRoot))
				yield break;

			foreach (var objPath in Directory.EnumerateFiles(importedRoot, "*.obj", SearchOption.AllDirectories))
			{
				string folderName = Path.GetFileName(Path.GetDirectoryName(objPath));
				if (string.IsNullOrWhiteSpace(folderName))
					continue;

				string displayBase = Path.GetFileNameWithoutExtension(objPath);
				if (string.IsNullOrWhiteSpace(displayBase))
					displayBase = folderName;

				yield return new ModelEntry(
					displayName: $"{displayBase} [{folderName}]",
					hashId: folderName,
					source: ModelSourceKind.Imported,
					filePath: objPath);
			}
		}

		private static GameObject TryLoadExactReference(string key)
		{
			if (HashToEntry.TryGetValue(key, out var entry))
				return LoadEntry(entry);

			if (DisplayNameToHash.TryGetValue(key, out var hash) && HashToEntry.TryGetValue(hash, out entry))
				return LoadEntry(entry);

			if (LooksLikeCanonicalHash(key) && HashToEntry.TryGetValue(key, out entry))
				return LoadEntry(entry);

			return AssetRegistry<GameObject>.FindModel(key);
		}

		private static GameObject LoadEntry(ModelEntry entry)
		{
			if (entry.Source == ModelSourceKind.Internal)
				return AssetRegistry<GameObject>.FindModel(entry.ResourcePath ?? entry.DisplayName);

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

		private static bool LooksLikeCanonicalHash(string value)
		{
			try
			{
				return string.Equals(
					HTB50Settings.ToString(HTB50.Decode(value)),
					value,
					StringComparison.OrdinalIgnoreCase);
			}
			catch
			{
				return false;
			}
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
	/// Typed access to texture2Ds
	/// </summary>
	public static class Texture2DAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Texture2D>.RegisterTexture2DRoot(root);
		public static void ClearCache() => AssetRegistry<Texture2D>.ClearTexture2DCache();
		public static Texture2D Find(string textureName) => AssetRegistry<Texture2D>.FindTexture2D(textureName);
	}

	/// <summary>
	/// Typed access to materials
	/// </summary>
	public static class MaterialAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Material>.RegisterMaterialRoot(root);
		public static void ClearCache() => AssetRegistry<Material>.ClearMaterialCache();
		public static Material Find(string materialName) => AssetRegistry<Material>.FindMaterial(materialName);
	}

	/// <summary>
	/// Typed access to skybox assets (materials)
	/// </summary>
	public static class SkyboxAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Material>.RegisterSkyboxRoot(root);
		public static void ClearCache() => AssetRegistry<Material>.ClearSkyboxCache();
		public static Material Find(string skyboxName) => AssetRegistry<Material>.FindSkybox(skyboxName);
	}

	/// <summary>
	/// Typed access to sound assets (AudioClips)
	/// </summary>
	public static class SoundAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<AudioClip>.RegisterSoundRoot(root);
		public static void ClearCache() => AssetRegistry<AudioClip>.ClearSoundCache();
		public static AudioClip Find(string clipName) => AssetRegistry<AudioClip>.FindSound(clipName);
	}

	/// <summary>
	/// Typed access to music assets (AudioClips)
	/// </summary>
	public static class MusicAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<AudioClip>.RegisterMusicRoot(root);
		public static void ClearCache() => AssetRegistry<AudioClip>.ClearMusicCache();
		public static AudioClip Find(string clipName) => AssetRegistry<AudioClip>.FindMusic(clipName);
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
			=> GetNames<GameObject>(() => GetAssetNamesFromRoots<GameObject>(new[]
			{
				AssetPath.GeometryPath?.Trim('/') ?? "",
			}), forceRefresh);

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

		public static IReadOnlyList<string> GetTexture2DNames(bool forceRefresh = false)
			=> GetNames<Texture2D>(() => GetAssetNamesFromRoots<Texture2D>(new[]
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
