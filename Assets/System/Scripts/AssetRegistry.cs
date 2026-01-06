using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class AssetRegistry<T> where T : UnityEngine.Object
	{
		// Separate root lists
		private static readonly HashSet<string> ModelRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> PrefabRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> TextureRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> MaterialRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> Texture2DRoots = new(StringComparer.OrdinalIgnoreCase);

		private static readonly HashSet<string> SoundRoots = new(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> MusicRoots = new(StringComparer.OrdinalIgnoreCase);

		// Separate caches
		private static readonly Dictionary<string, T> ModelCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, T> PrefabCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, T> TextureCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, T> MaterialCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, T> Texture2DCache = new(StringComparer.OrdinalIgnoreCase);

		private static readonly Dictionary<string, AudioClip> SoundCache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, AudioClip> MusicCache = new(StringComparer.OrdinalIgnoreCase);

		public static Func<string, string> NameRemapper { get; set; }

		// Register methods
		public static void RegisterModelRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				ModelRoots.Add(root.Trim('/'));
		}

		public static void RegisterPrefabRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				PrefabRoots.Add(root.Trim('/'));
		}

		public static void RegisterTextureRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				TextureRoots.Add(root.Trim('/'));
		}

		public static void RegisterTexture2DRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				Texture2DRoots.Add(root.Trim('/'));
		}

		public static void RegisterMaterialRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				MaterialRoots.Add(root.Trim('/'));
		}

		public static void RegisterSoundRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				SoundRoots.Add(root.Trim('/'));
		}

		public static void RegisterMusicRoot(string root)
		{
			if (!string.IsNullOrWhiteSpace(root))
				MusicRoots.Add(root.Trim('/'));
		}

		// Clear caches
		public static void ClearModelCache() => ModelCache.Clear();
		public static void ClearPrefabCache() => PrefabCache.Clear();
		public static void ClearTextureCache() => TextureCache.Clear();
		public static void ClearMaterialCache() => MaterialCache.Clear();
		public static void ClearTexture2DCache() => Texture2DCache.Clear();
		public static void ClearSoundCache() => SoundCache.Clear();
		public static void ClearMusicCache() => MusicCache.Clear();

		// Generic Find methods for main types
		public static T FindModel(string assetName) => Find(assetName, ModelCache, ModelRoots);
		public static T FindPrefab(string assetName) => Find(assetName, PrefabCache, PrefabRoots);
		public static T FindTexture(string assetName) => Find(assetName, TextureCache, TextureRoots);
		public static T FindTexture2D(string assetName) => Find(assetName, Texture2DCache, Texture2DRoots);
		public static T FindMaterial(string assetName) => Find(assetName, MaterialCache, MaterialRoots);
		public static T FindSkybox(string assetName) => Find(assetName, MaterialCache, MaterialRoots);

		// Non-generic Find methods for AudioClip
		public static AudioClip FindSound(string clipName) => FindAudioClip(clipName, SoundCache, SoundRoots);
		public static AudioClip FindMusic(string clipName) => FindAudioClip(clipName, MusicCache, MusicRoots);

		private static T Find(string assetName, Dictionary<string, T> cache, HashSet<string> roots)
		{
			if (string.IsNullOrEmpty(assetName)) return null;

			string key = assetName.Trim();

			if (cache.TryGetValue(key, out var cached)) return cached;

			T asset = null;

			if (NameRemapper != null)
			{
				string remapped = NameRemapper(key);
				if (remapped != key)
				{
					asset = TryLoad<T>(remapped, roots);
					if (asset != null)
					{
						cache[key] = asset;
						cache[remapped] = asset;
						return asset;
					}
				}
			}

			asset = TryLoad<T>(key, roots);

			if (asset != null)
				cache[key] = asset;

			return asset;
		}

		private static AudioClip FindAudioClip(string assetName, Dictionary<string, AudioClip> cache, HashSet<string> roots)
		{
			if (string.IsNullOrEmpty(assetName)) return null;

			string key = assetName.Trim();

			if (cache.TryGetValue(key, out var cached)) return cached;

			AudioClip asset = null;

			if (NameRemapper != null)
			{
				string remapped = NameRemapper(key);
				if (remapped != key)
				{
					asset = TryLoad<AudioClip>(remapped, roots);
					if (asset != null)
					{
						cache[key] = asset;
						cache[remapped] = asset;
						return asset;
					}
				}
			}

			asset = TryLoad<AudioClip>(key, roots);

			if (asset != null)
				cache[key] = asset;

			return asset;
		}

		private static U TryLoad<U>(string name, HashSet<string> roots) where U : UnityEngine.Object
		{
			foreach (var root in roots)
			{
				string path = string.IsNullOrEmpty(root) ? name : $"{root}/{name}";
				var loaded = Resources.Load<U>(path);
				if (loaded != null) return loaded;
			}
			return null;
		}
	}
}

//using System;
//using UnityEngine;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public static class AssetRegistry<T> where T : UnityEngine.Object
//	{
//		// Separate root lists for models and prefabs (since both are GameObject)
//		private static readonly HashSet<string> ModelRoots = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly HashSet<string> PrefabRoots = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly HashSet<string> TextureRoots = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly HashSet<string> MaterialRoots = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly HashSet<string> Texture2DRoots = new(StringComparer.OrdinalIgnoreCase);

//		private static readonly Dictionary<string, T> ModelCache = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly Dictionary<string, T> PrefabCache = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly Dictionary<string, T> TextureCache = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly Dictionary<string, T> MaterialCache = new(StringComparer.OrdinalIgnoreCase);
//		private static readonly Dictionary<string, T> Texture2DCache = new(StringComparer.OrdinalIgnoreCase);

//		public static Func<string, string> NameRemapper { get; set; }

//		public static void RegisterModelRoot(string root)
//		{
//			if (!string.IsNullOrWhiteSpace(root))
//				ModelRoots.Add(root.Trim('/'));
//		}

//		public static void RegisterPrefabRoot(string root)
//		{
//			if (!string.IsNullOrWhiteSpace(root))
//				PrefabRoots.Add(root.Trim('/'));
//		}

//		public static void RegisterTextureRoot(string root)
//		{
//			if (!string.IsNullOrWhiteSpace(root))
//				TextureRoots.Add(root.Trim('/'));
//		}

//		public static void RegisterTexture2DRoot(string root)
//		{
//			if (!string.IsNullOrWhiteSpace(root))
//				Texture2DRoots.Add(root.Trim('/'));
//		}

//		public static void RegisterMaterialRoot(string root)
//		{
//			if (!string.IsNullOrWhiteSpace(root))
//				MaterialRoots.Add(root.Trim('/'));
//		}

//		public static void ClearModelCache() => ModelCache.Clear();
//		public static void ClearPrefabCache() => PrefabCache.Clear();
//		public static void ClearTextureCache() => TextureCache.Clear();
//		public static void ClearMaterialCache() => MaterialCache.Clear();
//		public static void ClearTexture2DCache() => Texture2DCache.Clear();

//		public static T FindModel(string assetName) => Find(assetName, ModelCache, ModelRoots);

//		public static T FindPrefab(string assetName) => Find(assetName, PrefabCache, PrefabRoots);

//		public static T FindTexture(string assetName) => Find(assetName, TextureCache, TextureRoots);

//		public static T FindTexture2D(string assetName) => Find(assetName, Texture2DCache, Texture2DRoots);

//		public static T FindMaterial(string assetName) => Find(assetName, MaterialCache, MaterialRoots);

//		private static T Find(string assetName, Dictionary<string, T> cache, HashSet<string> roots)
//		{
//			if (string.IsNullOrEmpty(assetName)) return null;

//			string key = assetName.Trim();

//			if (cache.TryGetValue(key, out var cached)) return cached;

//			T asset = null;

//			if (null != NameRemapper)
//			{
//				string remapped = NameRemapper(key);
//				if (remapped != key)
//				{
//					asset = TryLoad(remapped, roots);
//					if (asset != null)
//					{
//						cache[key] = asset;
//						cache[remapped] = asset;
//						return asset;
//					}
//				}
//			}

//			asset = TryLoad(key, roots);

//			if (null != asset)
//				cache[key] = asset;

//			return asset;
//		}

//		private static T TryLoad(string name, HashSet<string> roots)
//		{
//			foreach (var root in roots)
//			{
//				string path = string.IsNullOrEmpty(root) ? name : $"{root}/{name}";
//				var loaded = Resources.Load<T>(path);
//				if (null != loaded) return loaded;
//			}
//			return null;
//		}
//	}
//}