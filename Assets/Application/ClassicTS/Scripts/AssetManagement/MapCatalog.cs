using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class MapCatalog
	{
		private const string InternalResourcesRoot = "ClassicTS/Maps";
		private const string InternalManifestPath = "AssetManifests/Maps";
		private const string PersistentMapsFolderName = "Maps";

		private static readonly Dictionary<HashId, Map> CachedMaps = new();
		private static Dictionary<HashId, string> InternalResourceIndex;
		private static bool InternalResourceIndexLoaded;
		private static readonly JsonSerializerSettings MapSerializerSettings = new()
		{
			Converters = { new DatabaseMapConverter() },
			NullValueHandling = NullValueHandling.Ignore
		};

		public static string PersistentMapsFolder => Path.Combine(Application.persistentDataPath, PersistentMapsFolderName);

		public static void ClearCache()
		{
			CachedMaps.Clear();
		}

		public static IReadOnlyList<Map> LoadMaps(IEnumerable<string> mapIds)
		{
			if (mapIds == null)
				return Array.Empty<Map>();

			var list = new List<Map>();
			foreach (var id in mapIds)
			{
				if (TryParseHash(id, out var hash))
				{
					var map = LoadMap(hash);
					if (map != null)
						list.Add(map);
				}
			}

			return list;
		}

		public static Map LoadMap(HashId hash)
		{
			if (hash == 0)
				return null;

			if (CachedMaps.TryGetValue(hash, out var cached) && cached != null)
				return cached;

			var map = LoadCommunityMap(hash) ?? LoadInternalMap(hash);
			if (map != null)
			{
				map.EnsureHashID();
				CachedMaps[map.HashID] = map;
			}

			return map;
		}

		public static void Register(Map map)
		{
			if (map == null)
				return;

			map.EnsureHashID();
			if (map.HashID == 0)
				return;

			CachedMaps[map.HashID] = map;
		}

		public static bool SaveCommunityMap(Map map)
		{
			if (map == null)
				return false;

			map.EnsureHashID();
			if (map.HashID == 0)
				return false;

			Directory.CreateDirectory(PersistentMapsFolder);
			string fileName = BuildFileName(map);
			string path = Path.Combine(PersistentMapsFolder, fileName);

			var prefix = HTB50Settings.ToString(map.HashID);
			foreach (var existing in Directory.EnumerateFiles(PersistentMapsFolder, $"{prefix}_*.json", SearchOption.TopDirectoryOnly).ToArray())
			{
				if (!string.Equals(existing, path, StringComparison.OrdinalIgnoreCase) && File.Exists(existing))
					File.Delete(existing);
			}

			var json = JsonConvert.SerializeObject(map.Clone(), Formatting.Indented, MapSerializerSettings);
			File.WriteAllText(path, json);

			CachedMaps[map.HashID] = map;
			return true;
		}

		public static bool DeleteCommunityMap(HashId hash)
		{
			if (hash == 0)
				return false;

			if (!Directory.Exists(PersistentMapsFolder))
				return false;

			var file = Directory.EnumerateFiles(PersistentMapsFolder, $"{HTB50Settings.ToString(hash)}_*.json", SearchOption.TopDirectoryOnly)
				.FirstOrDefault();

			if (file == null)
				return false;

			File.Delete(file);
			CachedMaps.Remove(hash);
			return true;
		}

		public static bool TryGetMap(HashId hash, out Map map)
		{
			map = LoadMap(hash);
			return map != null;
		}

		public static string BuildFileName(Map map)
		{
			var safeName = SanitizeFileNameComponent(string.IsNullOrWhiteSpace(map?.name) ? "Untitled" : map.name);
			return $"{HTB50Settings.ToString(map.HashID)}_{safeName}.json";
		}

		public static string SanitizeFileNameComponent(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return "Untitled";

			var invalid = Path.GetInvalidFileNameChars();
			var chars = value
				.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch)
				.ToArray();
			return new string(chars).Trim('_');
		}

		private static Map LoadCommunityMap(HashId hash)
		{
			if (!Directory.Exists(PersistentMapsFolder))
				return null;

			string prefix = HTB50Settings.ToString(hash);
			var file = Directory.EnumerateFiles(PersistentMapsFolder, $"{prefix}_*.json", SearchOption.TopDirectoryOnly)
				.FirstOrDefault();

			return file != null ? LoadMapFromFile(file) : null;
		}

		private static Map LoadInternalMap(HashId hash)
		{
			EnsureInternalIndex();
			if (InternalResourceIndex != null && InternalResourceIndex.TryGetValue(hash, out var resourceName) && !string.IsNullOrWhiteSpace(resourceName))
			{
				var asset = Resources.Load<TextAsset>($"{InternalResourcesRoot}/{resourceName}");
				if (asset != null)
				{
					var map = LoadMapFromJson(asset.text);
					if (map != null && map.HashID == hash)
						return map;
				}
			}

			var assets = Resources.LoadAll<TextAsset>(InternalResourcesRoot);
			if (assets == null || assets.Length == 0)
				return null;

			string prefix = HTB50Settings.ToString(hash);

			foreach (var asset in assets)
			{
				if (asset == null || string.IsNullOrWhiteSpace(asset.name))
					continue;

				if (!asset.name.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase) &&
					!string.Equals(asset.name, prefix, StringComparison.OrdinalIgnoreCase))
					continue;

				var map = LoadMapFromJson(asset.text);
				if (map != null && map.HashID == hash)
					return map;
			}

			return null;
		}

		private static void EnsureInternalIndex()
		{
			if (InternalResourceIndexLoaded)
				return;

			InternalResourceIndexLoaded = true;
			InternalResourceIndex = new Dictionary<HashId, string>();

			var manifest = Resources.Load<TextAsset>(InternalManifestPath);
			if (manifest != null && !string.IsNullOrWhiteSpace(manifest.text))
			{
				foreach (var line in manifest.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var trimmed = line.Trim();
					if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
						continue;

					var parts = trimmed.Split('\t');
					if (parts.Length < 2)
						continue;

					try
					{
						var hash = HTB50.Decode(parts[0].Trim());
						var resourceName = parts[parts.Length - 1].Trim();
						if (hash != 0 && !string.IsNullOrWhiteSpace(resourceName))
							InternalResourceIndex[hash] = resourceName;
					}
					catch
					{
						// Ignore malformed manifest rows and fall back to direct scanning.
					}
				}
			}
		}

		private static Map LoadMapFromFile(string path)
		{
			try
			{
				return LoadMapFromJson(File.ReadAllText(path));
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to load map file '{path}': {ex.Message}");
				return null;
			}
		}

		private static Map LoadMapFromJson(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
				return null;

			try
			{
				return JsonConvert.DeserializeObject<Map>(json, MapSerializerSettings);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to parse map json: {ex.Message}");
				return null;
			}
		}

		private static bool TryParseHash(string id, out HashId hash)
		{
			hash = 0;

			if (string.IsNullOrWhiteSpace(id))
				return false;

			try
			{
				hash = HTB50.Decode(id.Trim());
				return hash != 0;
			}
			catch
			{
				return false;
			}
		}
	}
}
