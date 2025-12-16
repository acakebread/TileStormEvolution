using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class TextureSetManager
	{
		private static readonly Dictionary<string, TextureSequence> _cache = new();

		public static TextureSequence GetTextureSequence(string id, string path)
		{
			//var _id = System.IO.Path.GetFileNameWithoutExtension(path);

			if (string.IsNullOrEmpty(id)) return null;

			if (_cache.TryGetValue(id, out var cached))
				return cached;

			var sequence = ResourceManager.TextureSets.FirstOrDefault(ts => ts.id == id);
			if (sequence == null) return null;

			// Work on a copy so we can mutate safely
			var frames = sequence.ResolvedFrames;
			var framesArray = frames.ToArray(); // copy for mutation
			bool modified = false;

			for (int i = 0; i < framesArray.Length; i++)
			{
				var frame = framesArray[i];
				if (frame.texture == null && !string.IsNullOrEmpty(frame.textureName))
				{
					var texturePath = $"{path}{frame.textureName}";
					frame.texture = TextureCache.Get(texturePath);
					framesArray[i] = frame;
					modified = true;
				}
			}

			if (modified)
			{
				sequence.SetResolvedFrames(framesArray); // write back loaded textures
			}

			_cache[id] = sequence;
			return sequence;
		}

		public static void ClearCache() => _cache.Clear();

		// ============================
		// NEW HIGH-LEVEL HELPER
		// ============================
		/// <summary>
		/// Applies a material and optional animated texture sequence to a GameObject.
		/// Handles instancing, emissive vs standard, animated vs static cases automatically.
		/// </summary>
		/// <param name="gameObject">The target GameObject</param>
		/// <param name="textureId">The texture sequence ID (can be null/empty for no texture override)</param>
		/// <param name="material">The base material to apply (required)</param>
		/// <param name="texturePath">Base path for loading textures (usually AssetPath.TexturePath)</param>
		/// <returns>The TextureSetAnimator if one was added, null otherwise</returns>
		public static TextureSetAnimator ApplyMaterialAndTexture( GameObject gameObject, string textureId, string materialName, string texturePath, string materialPath)
		{
			if (gameObject == null || string.IsNullOrEmpty(materialName)) return null;

			var renderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
			if (renderer == null) return null;

			var fullMaterialPath = $"{materialPath}{materialName}";
			var baseMaterial = MaterialCache.Get(fullMaterialPath);
			if (baseMaterial == null)
			{
				Debug.LogWarning($"Material not found: {fullMaterialPath}");
				return null;
			}

			var sequence = string.IsNullOrEmpty(textureId) ? null : GetTextureSequence(textureId, texturePath);

			return TextureSetAnimator.SetupAnimation(gameObject, sequence, baseMaterial);
		}
	}
}