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
		public static void RegisterRoot(string root) => AssetRegistry<GameObject>.RegisterRoot(root);
		public static void ClearCache() => AssetRegistry<GameObject>.ClearCache();

		public static GameObject Find(string modelName) => AssetRegistry<GameObject>.Find(modelName);

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
		public static void RegisterRoot(string root) => AssetRegistry<GameObject>.RegisterRoot(root);
		public static void ClearCache() => AssetRegistry<GameObject>.ClearCache();
		public static GameObject Find(string prefabName) => AssetRegistry<GameObject>.Find(prefabName);

		// Reuse ModelAssets instantiation logic (identical behavior)
		public static GameObject Instantiate(string prefabName, Transform parent = null) =>
			ModelAssets.Instantiate(prefabName, parent);

		public static GameObject Instantiate(string prefabName, Vector3 pos, Quaternion rot, Transform parent = null) =>
			ModelAssets.Instantiate(prefabName, pos, rot, parent);
	}

	/// <summary>
	/// Typed access to materials (including skyboxes)
	/// </summary>
	public static class MaterialAssets
	{
		public static void RegisterRoot(string root) => AssetRegistry<Material>.RegisterRoot(root);
		public static void ClearCache() => AssetRegistry<Material>.ClearCache();
		public static Material Find(string materialName) => AssetRegistry<Material>.Find(materialName);
	}
}