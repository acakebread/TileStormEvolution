using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MassiveHadronLtd;
using ClassicTilestorm.Assets;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

			private static string InternalResourcesRoot => $"{ApplicationSettings.JsonDataResourcePath}/Maps";
			private const bool IncludeLiveContentInExternalMove = false;
			private static string PersistentMapsFolder => ApplicationSettings.SystemMapsFolder;

			private static readonly Dictionary<HashId, Map> CachedMaps = new();
			private static List<HashId> InternalMapIds;
			private static HashSet<HashId> InternalMapIdSet;
			private static bool InternalMapIndexLoaded;
			private static readonly JsonSerializerSettings MapSerializerSettings = new()
			{
				Converters = { new DatabaseMapConverter() },
				NullValueHandling = NullValueHandling.Ignore
		};

		public static string InternalMapsFolder => Path.Combine(ApplicationSettings.JsonDataProjectPath, "Maps");

			public static void ClearCache()
			{
				CachedMaps.Clear();
				InternalMapIds = null;
				InternalMapIdSet = null;
				InternalMapIndexLoaded = false;
			}

		public static bool IsInternalMap(HashId hash)
		{
			EnsureInternalIndex();
			return hash != 0 && InternalMapIdSet != null && InternalMapIdSet.Contains(hash);
		}

		public static MapStorageLocation GetStorageLocation(HashId hash)
		{
			if (hash == 0)
				return MapStorageLocation.External;

			if (HasInternalMapFile(hash))
				return MapStorageLocation.Internal;

			if (LoadInternalMap(hash) != null)
				return MapStorageLocation.Internal;

			if (LoadCommunityMap(hash) != null)
				return MapStorageLocation.External;

			return IsInternalMap(hash)
				? MapStorageLocation.Internal
				: MapStorageLocation.External;
		}

		internal static bool HasInternalMapFile(HashId hash)
		{
			if (hash == 0)
				return false;

			return File.Exists(Path.Combine(InternalMapsFolder, ResourceFileNameBuilder.BuildJsonFileName(hash)));
		}

		internal static bool HasCommunityMapFile(HashId hash)
		{
			if (hash == 0)
				return false;

			return File.Exists(Path.Combine(PersistentMapsFolder, ResourceFileNameBuilder.BuildJsonFileName(hash)));
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
				if (InternalMapIds == null || InternalMapIds.Count == 0)
					return Array.Empty<MapEntry>();

				var list = new List<MapEntry>(InternalMapIds.Count);
				foreach (var hash in InternalMapIds)
				{
					if (hash == 0)
						continue;

					var map = LoadInternalMap(hash);
					if (map == null)
						continue;

					list.Add(new MapEntry(hash, map, MapStorageLocation.Internal, HTB50Settings.ToString(hash)));
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
			WebGLPersistentStorage.Flush();
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

		public static bool MoveMapStorage(Map map, MapStorageLocation targetLocation)
		{
			if (map == null)
				return false;

			map.EnsureHashID();
			if (map.HashID == 0)
				return false;

			var currentLocation = GetStorageLocation(map.HashID);
			if (currentLocation == targetLocation)
				return true;

			var usedDefinitions = ResourceManager.GetUsedDefinitions(map)
				.Where(def => def != null)
				.ToArray();
			var usedModelHashes = ResourceManager.GetUsedModelHashes(map)
				.Where(modelHash => !string.IsNullOrWhiteSpace(modelHash))
				.ToArray();

			if (targetLocation == MapStorageLocation.Internal)
			{
				PromoteDefinitionsToInternal(usedDefinitions);
				PromoteExternalModelsToImmutable(usedModelHashes);
			}
			else
			{
				ExportUniqueModelsToExternal(usedModelHashes);
				ExportUniqueDefinitionsToExternal(map, usedDefinitions);
			}

			var saved = targetLocation == MapStorageLocation.Internal
				? SaveInternalMap(map)
				: SaveCommunityMap(map);

			if (!saved)
				return false;

			if (currentLocation == MapStorageLocation.Internal)
				DeleteInternalMap(map.HashID);
			else
				DeleteCommunityMap(map.HashID);

			RefreshStateAfterStorageMove();

			return true;
		}

		internal static void RefreshStateAfterStorageMove()
		{
#if UNITY_EDITOR
			EditorApplication.ExecuteMenuItem("Tools/Generate Asset Manifests");
			AssetDatabase.Refresh();
#endif
			ClearCache();
			DefinitionCatalog.ClearCache();
			AssetConfiguration.ClearAllCaches();
			AssetConfiguration.Initialize();
		}

		private static void PromoteDefinitionsToInternal(IEnumerable<Definition> usedDefinitions)
		{
			var internalDefinitions = DefinitionCatalog.GetInternalDefinitions(forceRefresh: true)
				.Where(entry => entry.Definition != null)
				.Select(entry => entry.Definition)
				.ToDictionary(def => def.HashID, def => def);

			var definitionsToDeleteExternally = new List<Definition>();
			var importedAny = false;
			foreach (var def in usedDefinitions ?? Array.Empty<Definition>())
			{
				if (def == null || def.HashID == 0)
					continue;

				if (internalDefinitions.ContainsKey(def.HashID))
					continue;

				internalDefinitions[def.HashID] = def;
				definitionsToDeleteExternally.Add(def);
				importedAny = true;
			}

			if (!importedAny)
				return;

			if (!DefinitionCatalog.SaveInternalDefinitions(internalDefinitions.Values.OrderBy(def => def?.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)))
				return;

			foreach (var def in definitionsToDeleteExternally)
				DefinitionCatalog.DeleteExternalDefinition(def.HashID);
		}

		private static void ExportUniqueDefinitionsToExternal(Map map, IEnumerable<Definition> usedDefinitions)
		{
			var internalDefinitions = DefinitionCatalog.GetInternalDefinitions(forceRefresh: true)
				.Where(entry => entry.Definition != null)
				.Select(entry => entry.Definition)
				.ToDictionary(def => def.HashID, def => def);

			var remainingInternalDefinitions = internalDefinitions.Values.ToList();
			var definitionsToExport = new List<Definition>();

			foreach (var def in usedDefinitions ?? Array.Empty<Definition>())
			{
				if (def == null || def.HashID == 0)
					continue;

				if (!DefinitionCatalog.IsInternalDefinition(def.HashID))
					continue;

				if (ResourceManager.DefinitionUsageCount(def.HashID) != 1)
					continue;

				definitionsToExport.Add(def);
			}

			if (definitionsToExport.Count == 0)
				return;

			foreach (var def in definitionsToExport)
			{
				if (!DefinitionCatalog.SaveExternalDefinition(def))
					return;
			}

			foreach (var def in definitionsToExport)
				remainingInternalDefinitions.RemoveAll(existing => existing != null && existing.HashID == def.HashID);

			DefinitionCatalog.SaveInternalDefinitions(remainingInternalDefinitions);
		}

		private static void ExportUniqueModelsToExternal(IEnumerable<string> modelHashes)
		{
			foreach (var modelHash in modelHashes ?? Array.Empty<string>())
			{
				if (string.IsNullOrWhiteSpace(modelHash))
					continue;

				if (ResourceManager.ModelUsageCount(modelHash) != 1)
					continue;

				ResourceDependencyHelpers.TryExportModelToExternal(modelHash, IncludeLiveContentInExternalMove);
			}
		}

		private static void PromoteExternalModelsToImmutable(IEnumerable<string> modelHashes)
		{
			foreach (var modelHash in modelHashes ?? Array.Empty<string>())
			{
				if (string.IsNullOrWhiteSpace(modelHash))
					continue;

				ResourceDependencyHelpers.TryPromoteExternalModelToImmutable(modelHash);
			}
		}

		public static bool DeleteCommunityMap(HashId hash)
		{
			if (hash == 0)
				return false;

			var file = EnumerateCommunityMapFiles()
				.FirstOrDefault(f => TryGetMapHashFromFileName(f, out var fileHash) && fileHash == hash);

			if (file == null)
				return false;

			File.Delete(file);
			CachedMaps.Remove(hash);
			WebGLPersistentStorage.Flush();
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

		public static bool DeleteMap(HashId hash)
		{
			if (hash == 0)
				return false;

			var currentLocation = GetStorageLocation(hash);
			var deleted = currentLocation == MapStorageLocation.Internal
				? DeleteInternalMap(hash) || DeleteCommunityMap(hash)
				: DeleteCommunityMap(hash) || DeleteInternalMap(hash);

			if (deleted)
				ClearCache();

			return deleted;
		}

		public static int CleanupExternalMapsCollidingWithInternal()
		{
			EnsureInternalIndex();

			var internalHashes = GetInternalMaps(forceRefresh: true)
				.Where(entry => entry.HashId != 0)
				.Select(entry => entry.HashId)
				.ToHashSet();

			if (internalHashes.Count == 0)
				return 0;

			var removed = 0;
			foreach (var file in EnumerateCommunityMapFiles().ToArray())
			{
				if (!TryGetMapHashFromFileName(file, out var hash) || !internalHashes.Contains(hash))
					continue;

				try
				{
					File.Delete(file);
					CachedMaps.Remove(hash);
					removed++;
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"MapCatalog: failed to delete colliding external map '{file}': {ex.Message}");
				}
			}

			return removed;
		}

		public static bool TryGetMap(HashId hash, out Map map)
		{
			map = LoadMap(hash);
			return map != null;
		}

		public static string BuildFileName(Map map)
		{
			map?.EnsureHashID();
			return ResourceFileNameBuilder.BuildJsonFileName(map?.HashID ?? 0);
		}

		private static Map LoadCommunityMap(HashId hash)
		{
			var file = EnumerateCommunityMapFiles()
				.FirstOrDefault(f => TryGetMapHashFromFileName(f, out var fileHash) && fileHash == hash);

			return file != null ? LoadMapFromFile(file) : null;
		}

		private static IEnumerable<string> EnumerateCommunityMapFiles()
		{
			if (!Directory.Exists(PersistentMapsFolder))
				yield break;

			foreach (var file in Directory.EnumerateFiles(PersistentMapsFolder, "*.json", SearchOption.TopDirectoryOnly))
				yield return file;
		}

		private static Map LoadInternalMap(HashId hash)
		{
			EnsureInternalIndex();
			if (hash == 0 || !IsInternalMap(hash))
				return null;

			var asset = Resources.Load<TextAsset>($"{InternalResourcesRoot}/{HTB50Settings.ToString(hash)}");
			if (asset != null)
			{
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
				if (InternalMapIndexLoaded && !forceRefresh)
					return;

				InternalMapIndexLoaded = true;
				InternalMapIds = new List<HashId>();
				InternalMapIdSet = new HashSet<HashId>();

				var levelsAsset = Resources.Load<TextAsset>($"{ApplicationSettings.JsonDataResourcePath}/levels");
				if (levelsAsset == null || string.IsNullOrWhiteSpace(levelsAsset.text))
					return;

				try
				{
					var root = JObject.Parse(levelsAsset.text);
					var mapIds = root["maps"] as JArray;
					if (mapIds == null)
						return;

					foreach (var token in mapIds)
					{
						var candidate = token?.Value<string>()?.Trim();
						if (string.IsNullOrWhiteSpace(candidate))
							continue;

						if (!ClassicTilestorm.Assets.ResourceIdUtil.TryParseCanonicalHash(candidate, out var hash) || hash == 0)
							continue;

						if (!InternalMapIdSet.Add(hash))
							continue;

						InternalMapIds.Add(hash);
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"MapCatalog: failed to load internal map list from levels.json: {ex.Message}");
					InternalMapIds.Clear();
					InternalMapIdSet.Clear();
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

			string candidate = fileStem;
			int suffixIndex = fileStem.LastIndexOf(ResourceFileNameBuilder.FileHashSeparator, StringComparison.Ordinal);
			if (suffixIndex >= 0 && suffixIndex + ResourceFileNameBuilder.FileHashSeparator.Length < fileStem.Length)
				candidate = fileStem.Substring(suffixIndex + ResourceFileNameBuilder.FileHashSeparator.Length);

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

			var normalized = StringUtil.EnsureTrailingNewline(json ?? string.Empty);
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
