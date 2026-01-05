using System;
using UnityEngine;
using MassiveHadronLtd;

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
	/// Typed access to materials (including skyboxes)
	/// </summary>
	public static class MaterialAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Material>.RegisterMaterialRoot(root);
		public static void ClearCache() => AssetRegistry<Material>.ClearMaterialCache();
		public static Material Find(string materialName) => AssetRegistry<Material>.FindMaterial(materialName);
	}

	public static class SoundAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<AudioClip>.RegisterSoundRoot(root);
		public static void ClearCache() => AssetRegistry<AudioClip>.ClearSoundCache();
		public static AudioClip Find(string clipName) => AssetRegistry<AudioClip>.FindSound(clipName);
	}

	public static class MusicAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<AudioClip>.RegisterMusicRoot(root);
		public static void ClearCache() => AssetRegistry<AudioClip>.ClearMusicCache();
		public static AudioClip Find(string clipName) => AssetRegistry<AudioClip>.FindMusic(clipName);
	}
}