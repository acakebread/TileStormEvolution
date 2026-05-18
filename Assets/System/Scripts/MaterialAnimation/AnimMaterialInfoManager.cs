using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicTilestorm;
using Newtonsoft.Json;
using MassiveHadronLtd;
using UnityEngine;

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
				if (!TryLoadJsonAsset(id, out var json, out var baseDirectory))
					return null;

				var definition = JsonConvert.DeserializeObject<AnimMaterial>(json);
				if (definition == null)
					return null;

				if (string.IsNullOrWhiteSpace(definition.id))
					definition.id = id;

				ResolveTextures(definition, baseDirectory);
				return definition;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"AnimMaterialInfoManager: Failed to load anim material json for '{id}': {ex.Message}");
				return null;
			}
		}

		private static bool TryLoadJsonAsset(string id, out string json, out string baseDirectory)
		{
			json = null;
			baseDirectory = null;

			var roots = ApplicationSettings.GetGeometryMaterialPaths().ToArray();
			foreach (var root in roots)
			{
				var basePath = $"{root}/{id}";

				var asset = Resources.Load<TextAsset>(basePath);
				if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
				{
					json = asset.text;
					return true;
				}

				asset = Resources.Load<TextAsset>($"{basePath}.json");
				if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
				{
					json = asset.text;
					return true;
				}
			}

			var importedPath = FindImportedJson(id);
			if (string.IsNullOrWhiteSpace(importedPath) || !File.Exists(importedPath))
				return false;

			json = File.ReadAllText(importedPath);
			baseDirectory = Path.GetDirectoryName(importedPath);
			return !string.IsNullOrWhiteSpace(json);
		}

		private static string FindImportedJson(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return null;

			try
			{
				foreach (var importedRoot in new[] { ApplicationSettings.SystemModelsFolder, Path.Combine(Application.persistentDataPath, "Imported") })
				{
					if (!Directory.Exists(importedRoot))
						continue;

					var match = Directory.EnumerateFiles(importedRoot, $"{id}.json", SearchOption.AllDirectories)
						.FirstOrDefault();

					if (!string.IsNullOrWhiteSpace(match))
						return match;
				}

				return null;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"AnimMaterialInfoManager: failed to scan imported animations: {ex.Message}");
				return null;
			}
		}

		private static void ResolveTextures(AnimMaterial definition, string baseDirectory)
		{
			var frames = definition.ResolvedFrames;
			if (frames == null || frames.Length == 0) return;

			var modified = false;
			for (var i = 0; i < frames.Length; i++)
			{
				var frame = frames[i];
				if (frame.texture == null && !string.IsNullOrWhiteSpace(frame.textureName))
				{
					frame.texture = LoadAnimationTexture(frame.textureName, baseDirectory);
					frames[i] = frame;
					modified = true;
				}
			}

			if (modified)
				definition.SetResolvedFrames(frames);
		}

		private static Texture2D LoadAnimationTexture(string textureName, string baseDirectory = null)
		{
			if (string.IsNullOrEmpty(textureName))
				return null;

			if (!string.IsNullOrWhiteSpace(baseDirectory))
			{
				var imported = LoadImportedTexture(baseDirectory, textureName);
				if (imported != null)
					return imported;
			}

			foreach (var root in ApplicationSettings.GetGeometryMaterialPaths())
			{
				var texture = Resources.Load<Texture2D>($"{root}/{textureName}");
				if (texture != null)
					return texture;
			}

			return null;
		}

		private static Texture2D LoadImportedTexture(string baseDirectory, string textureName)
		{
			var candidates = new[]
			{
				Path.Combine(baseDirectory, textureName),
				Path.Combine(baseDirectory, textureName + ".png"),
				Path.Combine(baseDirectory, textureName + ".jpg"),
				Path.Combine(baseDirectory, textureName + ".jpeg"),
				Path.Combine(baseDirectory, textureName + ".tga"),
				Path.Combine(baseDirectory, textureName + ".bmp")
			};

			foreach (var candidate in candidates)
			{
				if (!ImportedResourceLoader.TryLoadTexture(candidate, out var texture))
					continue;

				if (texture is Texture2D tex2d)
					return tex2d;
			}

			return null;
		}

		public static void ClearCache()
		{
			Cache.Clear();
		}
	}
}
