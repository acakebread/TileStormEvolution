using System;
using UnityEngine;
using MassiveHadronLtd;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm.Assets
{
	/// <summary>
	/// Typed access to model geometry assets (FBX/OBJ/etc placed in Resources)
	/// </summary>
	public static class ModelAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<GameObject>.RegisterModelRoot(root);
		public static void ClearCache() => AssetRegistry<GameObject>.ClearModelCache();
		public static GameObject Find(string modelName) => AssetRegistry<GameObject>.FindModel(modelName);

		public static GameObject Instantiate(string modelName, Transform parent = null)
		{
			var asset = Find(modelName);
			if (asset == null) return null;

			var instance = UnityEngine.Object.Instantiate(asset, parent);
			instance.name = System.IO.Path.GetFileNameWithoutExtension(modelName);
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
		public static void RegisterRoot(string root) => AssetRegistry<Material>.RegisterMaterialRoot(root);
		public static void ClearCache() => AssetRegistry<Material>.ClearMaterialCache();
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
	public static partial class ProjectAssets
	{
		// ──────────────────────────────────────────────────────────────
		//  Cached name listings for all asset types (loaded only once)
		// ──────────────────────────────────────────────────────────────

		private static readonly Dictionary<Type, (List<string> Names, bool Cached)> NameCaches
			= new Dictionary<Type, (List<string>, bool)>();

		private static void EnsureCached<T>(Func<IEnumerable<string>> loadFunc) where T : UnityEngine.Object
		{
			var t = typeof(T);
			if (!NameCaches.TryGetValue(t, out var entry) || !entry.Cached)
			{
				var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var name in loadFunc())
				{
					if (!string.IsNullOrEmpty(name))
						names.Add(name);
				}

				NameCaches[t] = (
					names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
					true
				);
			}
		}

		private static IReadOnlyList<string> GetNames<T>(Func<IEnumerable<string>> loadFunc, bool forceRefresh = false)
			where T : UnityEngine.Object
		{
			if (forceRefresh)
			{
				NameCaches[typeof(T)] = (null, false);
			}

			EnsureCached<T>(loadFunc);
			return NameCaches[typeof(T)].Names ?? new List<string>();
		}

		// ── Public API for each major type ────────────────────────────────

		public static IReadOnlyList<string> GetModelNames(bool forceRefresh = false)
			=> GetNames<GameObject>(() => GetAssetNamesFromRoots<GameObject>(new[]
			{
				AssetPath.GeometryPath?.Trim('/') ?? "",
				"Levels",
				"Levels/Med"
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
			}), forceRefresh);

		public static IReadOnlyList<string> GetSkycubeNames(bool forceRefresh = false)
			=> GetNames<Material>(() => GetAssetNamesFromRoots<Material>(new[]
			{
				AssetPath.SkycubesPath?.Trim('/') ?? ""
			}), forceRefresh);

		// Add more when needed, e.g.:
		// public static IReadOnlyList<string> GetSoundNames(bool forceRefresh = false) { ... }

		// ── Core loading helper ─────────────────────────────────────────────

		private static IEnumerable<string> GetAssetNamesFromRoots<T>(string[] roots)
			where T : UnityEngine.Object
		{
			foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r)))
			{
				var assets = Resources.LoadAll<T>(root);
				foreach (var asset in assets)
				{
					if (!string.IsNullOrEmpty(asset.name))
						yield return asset.name;
				}
			}
		}

		/// <summary>
		/// Force refresh all name caches (call after significant asset changes)
		/// </summary>
		public static void RefreshAllNameCaches()
		{
			NameCaches.Clear();
		}
	}
}