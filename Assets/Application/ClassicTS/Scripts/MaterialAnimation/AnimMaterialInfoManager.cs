using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using ClassicTilestorm.Assets;

namespace ClassicTilestorm
{
	public static class AnimMaterialInfoManager
	{
		private static readonly Dictionary<string, AnimMaterial> Cache = new(StringComparer.OrdinalIgnoreCase);

		public static AnimMaterial GetAnimMaterial(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;

			if (Cache.TryGetValue(id, out var cached))
				return cached;

			var definition = Load(id);
			Cache[id] = definition;
			return definition;
		}

		public static Texture2D GetFrameZero(string id)
		{
			var definition = GetAnimMaterial(id);
			if (definition == null || definition.ResolvedFrames == null || definition.ResolvedFrames.Length == 0)
				return null;

			return definition.FirstTexture;
		}

		public static AnimMaterial Get(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;

			return GetAnimMaterial(id);
		}

		private static AnimMaterial Load(string id)
		{
			try
			{
				var jsonAsset = LoadJsonAsset(id);
				if (jsonAsset == null || string.IsNullOrWhiteSpace(jsonAsset.text))
					return null;

				var definition = JsonConvert.DeserializeObject<AnimMaterial>(jsonAsset.text);
				if (definition == null)
					return null;

				if (string.IsNullOrWhiteSpace(definition.id))
					definition.id = id;

				ResolveTextures(definition);
				return definition;
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

		private static void ResolveTextures(AnimMaterial definition)
		{
			var frames = definition.ResolvedFrames;
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
				definition.SetResolvedFrames(frames);
		}

		public static void ClearCache()
		{
			Cache.Clear();
		}
	}
}
