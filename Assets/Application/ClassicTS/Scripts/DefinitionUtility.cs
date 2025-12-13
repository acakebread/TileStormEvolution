using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    public static class DefinitionUtility
    {
		private static string geometryPath = null;//"ClassicTS/Geometry/"
		public static string GeometryPath { get => geometryPath ?? PreviewSettings.GeometryPath; set => geometryPath = value; }

		private static string texturePath = null;//"ClassicTS/Textures/"
		public static string TexturePath { get => texturePath ?? PreviewSettings.TexturePath; set => texturePath = value; }

		private static string materialPath = null;//"ClassicTS/Materials/"
		public static string MaterialPath { get => materialPath ?? PreviewSettings.MaterialPath; set => materialPath = value; }

		public static GameObject Instantiate(Definition definition, Transform parent = null)
		{
			var gameObject = PrefabFactory.InstantiatePrefab($"{GeometryPath}{definition.model}", parent);
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			textureAnimator.Initialize(TextureSetManager.GetTextureSequence(definition.texture, TexturePath));
			return gameObject;
		}

		public static GameObject Instantiate(Definition definition, Vector3 position, Transform parent = null)
		{
			var gameObject = PrefabFactory.InstantiatePrefab($"{GeometryPath}{definition.model}", position, parent);
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			textureAnimator.Initialize(TextureSetManager.GetTextureSequence(definition.texture, TexturePath));
			return gameObject;
		}

		public static GameObject Instantiate(Definition definition, Vector3 position, Quaternion rotation, Transform parent = null)
		{
			var gameObject = PrefabFactory.InstantiatePrefab($"{GeometryPath}{definition.model}", position, rotation, parent);
			var textureAnimator = gameObject.AddComponent<TextureSetAnimator>();
			textureAnimator.Initialize(TextureSetManager.GetTextureSequence(definition.texture, TexturePath));
			return gameObject;
		}

		//ToDo implement instead of legacy pass through
		public static GameObject InstantiateTile(Definition definition, Transform parent, Vector3 position) => GeometryManager.InstantiateTileWithAllProperties(definition, parent, position);
	}
}