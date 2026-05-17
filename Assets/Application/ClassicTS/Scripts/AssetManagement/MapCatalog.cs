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
			public enum MapStorageLocation
			{
				Internal = 0,
				External = 1
			}

			public readonly struct MapEntry
			{
				public HashId HashId { get; }
				public Map Map { get; }
				public MapStorageLocation StorageLocation { get; }
				public string ResourceName { get; }
				public string FilePath { get; }

				public MapEntry(HashId hashId, Map map, MapStorageLocation storageLocation, string resourceName = null, string filePath = null)
				{
					HashId = hashId;
					Map = map;
					StorageLocation = storageLocation;
					ResourceName = string.IsNullOrWhiteSpace(resourceName) ? null : resourceName;
					FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
				}

				public string DisplayName => Map != null && !string.IsNullOrWhiteSpace(Map.name)
					? Map.name
					: !string.IsNullOrWhiteSpace(ResourceName)
						? Path.GetFileNameWithoutExtension(ResourceName)
						: !string.IsNullOrWhiteSpace(FilePath)
							? Path.GetFileNameWithoutExtension(FilePath)
							: null;

				public bool IsInternal => StorageLocation == MapStorageLocation.Internal;
				public bool IsExternal => StorageLocation == MapStorageLocation.External;
			}

			private readonly struct InternalManifestEntry
			{
				public readonly HashId HashId;
				public readonly string ResourceName;

				public InternalManifestEntry(HashId hashId, string resourceName)
				{
					HashId = hashId;
					ResourceName = resourceName;
				}
			}

			private static string InternalResourcesRoot => $"{ApplicationSettings.JsonDataResourcePath}/Maps";
			private const string InternalManifestPath = "AssetManifests/Maps";
			private const string FileHashSeparator = "__";
			private const string PersistentMapsFolderName = "Maps";

			private static readonly Dictionary<HashId, Map> CachedMaps = new();
			private static Dictionary<HashId, string> InternalResourceIndex;
			private static List<InternalManifestEntry> InternalManifestEntries;
			private static bool InternalResourceIndexLoaded;
			private static readonly JsonSerializerSettings MapSerializerSettings = new()
			{
				Converters = { new DatabaseMapConverter() },
				NullValueHandling = NullValueHandling.Ignore
		};

		public static string PersistentMapsFolder => Path.Combine(Application.persistentDataPath, PersistentMapsFolderName);

		public static string InternalMapsFolder => Path.Combine(ApplicationSettings.JsonDataProjectPath, "Maps");

			public static void ClearCache()
			{
				CachedMaps.Clear();
				InternalResourceIndex = null;
				InternalManifestEntries = null;
				InternalResourceIndexLoaded = false;
			}

		public static bool IsInternalMap(HashId hash)
		{
			EnsureInternalIndex();
			return hash != 0 && InternalResourceIndex != null && InternalResourceIndex.ContainsKey(hash);
		}

		public static MapStorageLocation GetStorageLocation(HashId hash)
		{
			if (hash == 0)
				return MapStorageLocation.External;

			if (LoadInternalMap(hash) != null)
				return MapStorageLocation.Internal;

			if (LoadCommunityMap(hash) != null)
				return MapStorageLocation.External;

			return IsInternalMap(hash)
				? MapStorageLocation.Internal
				: MapStorageLocation.External;
		}

		public static bool TryGetInternalResourceName(HashId hash, out string resourceName)
		{
			EnsureInternalIndex();
			resourceName = null;
			return InternalResourceIndex != null && InternalResourceIndex.TryGetValue(hash, out resourceName) && !string.IsNullOrWhiteSpace(resourceName);
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

			public static IReadOnlyList<MapEntry> GetAvailableMaps(bool forceRefresh = false)
			{
				var entries = new List<MapEntry>();
				entries.AddRange(GetInternalMaps(forceRefresh));
				entries.AddRange(GetExternalMaps(forceRefresh));
				return entries;
			}

			public static IReadOnlyList<MapEntry> GetInternalMaps(bool forceRefresh = false)
			{
				EnsureInternalIndex(forceRefresh);
				if (InternalManifestEntries == null || InternalManifestEntries.Count == 0)
					return Array.Empty<MapEntry>();

				var list = new List<MapEntry>(InternalManifestEntries.Count);
				foreach (var entry in InternalManifestEntries)
				{
					if (entry.HashId == 0)
						continue;

					var map = LoadInternalMap(entry.HashId);
					if (map == null)
						continue;

					list.Add(new MapEntry(entry.HashId, map, MapStorageLocation.Internal, entry.ResourceName));
				}

				return list;
			}

			public static IReadOnlyList<MapEntry> GetExternalMaps(bool forceRefresh = false)
			{
				if (!Directory.Exists(PersistentMapsFolder))
					return Array.Empty<MapEntry>();

				var list = new List<MapEntry>();
				foreach (var file in Directory.EnumerateFiles(PersistentMapsFolder, "*.json", SearchOption.TopDirectoryOnly))
				{
					if (!TryGetMapHashFromFileName(file, out var hash))
						continue;

					var map = LoadMapFromFile(file);
					if (map == null)
						continue;

					map.EnsureHashID();
					list.Add(new MapEntry(map.HashID, map, MapStorageLocation.External, filePath: file));
					CachedMaps[map.HashID] = map;
				}

				return list;
			}

		public static Map LoadMap(HashId hash)
		{
			if (hash == 0)
				return null;

			if (CachedMaps.TryGetValue(hash, out var cached) && cached != null)
				return cached;

			var map = IsInternalMap(hash)
				? LoadInternalMap(hash) ?? LoadCommunityMap(hash)
				: LoadCommunityMap(hash) ?? LoadInternalMap(hash);
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

			foreach (var existing in Directory.EnumerateFiles(PersistentMapsFolder, "*.json", SearchOption.TopDirectoryOnly).ToArray())
			{
				if (TryGetMapHashFromFileName(existing, out var existingHash) &&
					existingHash == map.HashID &&
					!string.Equals(existing, path, StringComparison.OrdinalIgnoreCase) &&
					File.Exists(existing))
					File.Delete(existing);
			}

			var saveMap = map.Clone();
			saveMap.Optimise();
			var json = JsonConvert.SerializeObject(saveMap, Formatting.Indented, MapSerializerSettings);
			WriteJsonIfChanged(path, json);

			CachedMaps[map.HashID] = map;
			return true;
		}

		public static bool SaveInternalMap(Map map)
		{
			if (map == null)
				return false;

			map.EnsureHashID();
			if (map.HashID == 0)
				return false;

			Directory.CreateDirectory(InternalMapsFolder);

			string fileName = BuildFileName(map);
			string path = Path.Combine(InternalMapsFolder, fileName);
			foreach (var existing in Directory.EnumerateFiles(InternalMapsFolder, "*.json", SearchOption.TopDirectoryOnly).ToArray())
			{
				if (TryGetMapHashFromFileName(existing, out var existingHash) &&
					existingHash == map.HashID &&
					!string.Equals(existing, path, StringComparison.OrdinalIgnoreCase) &&
					File.Exists(existing))
					File.Delete(existing);
			}

			var saveMap = map.Clone();
			saveMap.Optimise();
			var json = JsonConvert.SerializeObject(saveMap, Formatting.Indented, MapSerializerSettings);
			WriteJsonIfChanged(path, json);

			var resourceName = Path.GetFileNameWithoutExtension(fileName);
			InternalResourceIndex ??= new Dictionary<HashId, string>();
			InternalResourceIndex[map.HashID] = resourceName;

			DeleteCommunityMap(map.HashID);
			CachedMaps[map.HashID] = map;
			return true;
		}

		public static bool SaveMap(Map map)
		{
			return GetStorageLocation(map?.HashID ?? 0) == MapStorageLocation.Internal
				? SaveInternalMap(map)
				: SaveCommunityMap(map);
		}

		public static bool DeleteCommunityMap(HashId hash)
		{
			if (hash == 0)
				return false;

			if (!Directory.Exists(PersistentMapsFolder))
				return false;

			var file = Directory.EnumerateFiles(PersistentMapsFolder, "*.json", SearchOption.TopDirectoryOnly)
				.FirstOrDefault(f => TryGetMapHashFromFileName(f, out var fileHash) && fileHash == hash);

			if (file == null)
				return false;

			File.Delete(file);
			CachedMaps.Remove(hash);
			return true;
		}

		public static bool DeleteInternalMap(HashId hash)
		{
			if (hash == 0)
				return false;

			if (!Directory.Exists(InternalMapsFolder))
				return false;

			var file = Directory.EnumerateFiles(InternalMapsFolder, "*.json", SearchOption.TopDirectoryOnly)
				.FirstOrDefault(f => TryGetMapHashFromFileName(f, out var fileHash) && fileHash == hash);

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
			return $"{safeName}{FileHashSeparator}{HTB50Settings.ToString(map.HashID)}.json";
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

			var file = Directory.EnumerateFiles(PersistentMapsFolder, "*.json", SearchOption.TopDirectoryOnly)
				.FirstOrDefault(f => TryGetMapHashFromFileName(f, out var fileHash) && fileHash == hash);

			return file != null ? LoadMapFromFile(file) : null;
		}

			private static Map LoadInternalMap(HashId hash)
			{
				EnsureInternalIndex();
			if (TryGetInternalResourceName(hash, out var resourceName))
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

			foreach (var asset in assets)
			{
				if (asset == null || string.IsNullOrWhiteSpace(asset.name))
					continue;

				if (!TryGetMapHashFromFileStem(asset.name, out var fileHash) || fileHash != hash)
					continue;

				var map = LoadMapFromJson(asset.text);
				if (map != null && map.HashID == hash)
					return map;
			}

			return null;
		}

			private static void EnsureInternalIndex()
				=> EnsureInternalIndex(false);

			private static void EnsureInternalIndex(bool forceRefresh)
			{
				if (InternalResourceIndexLoaded && !forceRefresh)
					return;

				InternalResourceIndexLoaded = true;
				InternalResourceIndex = new Dictionary<HashId, string>();
				InternalManifestEntries = new List<InternalManifestEntry>();

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
						if (!ClassicTilestorm.Assets.ResourceIdUtil.TryParseCanonicalHash(parts[0].Trim(), out var hash))
							continue;
						var resourceName = parts[parts.Length - 1].Trim();
						if (hash != 0 && !string.IsNullOrWhiteSpace(resourceName))
						{
							InternalResourceIndex[hash] = resourceName;
							InternalManifestEntries.Add(new InternalManifestEntry(hash, resourceName));
						}
					}
					catch
					{
						// Ignore malformed manifest rows and fall back to direct scanning.
					}
				}
			}
		}

		public static bool TryGetMapHashFromFileName(string filePathOrName, out HashId hash)
		{
			hash = 0;

			if (string.IsNullOrWhiteSpace(filePathOrName))
				return false;

			var stem = Path.GetFileNameWithoutExtension(filePathOrName);
			return TryGetMapHashFromFileStem(stem, out hash);
		}

		public static bool TryGetMapHashFromFileStem(string fileStem, out HashId hash)
		{
			hash = 0;

			if (string.IsNullOrWhiteSpace(fileStem))
				return false;

			string candidate = null;
			int suffixIndex = fileStem.LastIndexOf(FileHashSeparator, StringComparison.Ordinal);
			if (suffixIndex >= 0 && suffixIndex + FileHashSeparator.Length < fileStem.Length)
			{
				candidate = fileStem.Substring(suffixIndex + FileHashSeparator.Length);
			}
			else
			{
				int prefixIndex = fileStem.IndexOf('_');
				if (prefixIndex > 0)
					candidate = fileStem.Substring(0, prefixIndex);
				else
					candidate = fileStem;
			}

			return ClassicTilestorm.Assets.ResourceIdUtil.TryParseCanonicalHash(candidate, out hash);
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

		private static void WriteJsonIfChanged(string path, string json)
		{
			if (string.IsNullOrWhiteSpace(path))
				return;

			var normalized = EnsureTrailingNewline(json ?? string.Empty);
			if (File.Exists(path))
			{
				try
				{
					var current = File.ReadAllText(path);
					if (string.Equals(current, normalized, StringComparison.Ordinal))
						return;
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"MapCatalog: failed to read existing file before save '{path}': {ex.Message}");
				}
			}

			File.WriteAllText(path, normalized);
		}

		private static string EnsureTrailingNewline(string value)
		{
			if (string.IsNullOrEmpty(value))
				return string.Empty;

			return value.EndsWith(Environment.NewLine, StringComparison.Ordinal)
				? value
				: value + Environment.NewLine;
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
