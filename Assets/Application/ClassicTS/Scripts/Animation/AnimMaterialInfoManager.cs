using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public static class AnimMaterialInfoManager
	{
		private static readonly Dictionary<string, TextureSequence> Cache = new(StringComparer.OrdinalIgnoreCase);

		public static TextureSequence GetTextureSequence(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;

			if (Cache.TryGetValue(id, out var cached))
				return cached;

			var sequence = Load(id);
			Cache[id] = sequence;
			return sequence;
		}

		public static Texture2D GetFrameZero(string id)
		{
			var sequence = GetTextureSequence(id);
			if (sequence == null || sequence.ResolvedFrames == null || sequence.ResolvedFrames.Length == 0)
				return null;

			return sequence.FirstTexture;
		}

		public static TextureSequence Get(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;

			return GetTextureSequence(id);
		}

		private static TextureSequence Load(string id)
		{
			try
			{
				var jsonAsset = LoadJsonAsset(id);
				if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
					return null;

				var sequence = JsonConvert.DeserializeObject<TextureSequence>(jsonAsset.text);
				if (sequence == null)
					return null;

				if (string.IsNullOrWhiteSpace(sequence.id))
					sequence.id = id;

				ResolveTextures(sequence);
				return sequence;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"AnimMaterialInfoManager: Failed to load anim material json for '{id}': {ex.Message}");
				return null;
			}
		}

		private static TextAsset LoadJsonAsset(string id)
		{
			var root = AssetPath.GeometryPath?.Trim('/')?.Trim();
			if (string.IsNullOrEmpty(root))
				return null;

			var basePath = $"{root}/Materials/{id}";

			var asset = Resources.Load<TextAsset>(basePath);
			if (asset != null) return asset;

			return Resources.Load<TextAsset>($"{basePath}.json");
		}

		private static void ResolveTextures(TextureSequence sequence)
		{
			var frames = sequence.ResolvedFrames;
			if (frames == null || frames.Length == 0) return;

			var modified = false;
			for (var i = 0; i < frames.Length; i++)
			{
				var frame = frames[i];
				if (frame.texture == null && !string.IsNullOrWhiteSpace(frame.textureName))
				{
					frame.texture = Texture2DAssets.Find(frame.textureName);
					frames[i] = frame;
					modified = true;
				}
			}

			if (modified)
				sequence.SetResolvedFrames(frames);
		}

		public static void ClearCache()
		{
			Cache.Clear();
		}
	}
}
