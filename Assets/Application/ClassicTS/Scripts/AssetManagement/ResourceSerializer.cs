using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class JsonSetup
	{
		private static bool _initialized = false;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void Init()
		{
			if (_initialized) return;

			var settings = new JsonSerializerSettings
			{
				Converters = 
				{
					new MapAttachmentConverter(),
					new DefinitionConverter()
				},
				NullValueHandling = NullValueHandling.Ignore,
			};

			JsonConvert.DefaultSettings = () => settings;
			_initialized = true;
			//Debug.Log("Json.NET configured with ordered properties (declaration order)");
		}
	}

	public static class ResourceSerializer
	{
		private static void EnsureFolder(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return;

			if (!Directory.Exists(path)) Directory.CreateDirectory(path);
		}

		public static void Initialise()
		{
			JsonSetup.Init();
			MapCatalog.ClearCache();
			var removedMaps = MapCatalog.CleanupExternalMapsCollidingWithInternal();
			if (removedMaps > 0)
				Debug.Log($"ResourceSerializer: removed {removedMaps} external map(s) colliding with internal storage");
			DefinitionCatalog.ClearCache();
			var removedDefinitions = DefinitionCatalog.CleanupExternalDefinitionsCollidingWithInternal();
			if (removedDefinitions > 0)
				Debug.Log($"ResourceSerializer: removed {removedDefinitions} external definition(s) colliding with internal storage");
			PrefabResourceTable.ClearCache();
			TextureResourceTable.ClearCache();
			MaterialResourceTable.ClearCache();
			SkycubeResourceTable.ClearCache();
			MusicResourceTable.ClearCache();
			SoundResourceTable.ClearCache();
			CharacterResourceTable.ClearCache();
			EffectResourceTable.ClearCache();
			ImportedResourceLoader.ClearCache();
			ProjectAssets.RefreshAllNameCaches();
			ResourceManager.database = null;// important

			var levelsAsset = LoadJsonAsset("levels");
			var definitionsAsset = LoadJsonAsset("definitions");

			if (levelsAsset == null)
			{
				Debug.LogError($"ResourceSerializer: missing levels.json at '{ApplicationSettings.JsonDataResourcePath}'");
				return;
			}

			if (definitionsAsset == null)
			{
				Debug.LogError($"ResourceSerializer: missing definitions.json at '{ApplicationSettings.JsonDataResourcePath}'");
				return;
			}

			ResourceManager.database = LoadDatabase(levelsAsset.text, definitionsAsset.text);
		}

		public static DatabaseData LoadDatabase(string json, string definitionsJson = null)
		{
			if (string.IsNullOrEmpty(json)) return null;

			try
			{
				var root = JObject.Parse(json);

				var settings = new JsonSerializerSettings
				{
					Converters = { new DatabaseMapConverter() },
					NullValueHandling = NullValueHandling.Ignore
				};

				var serializer = JsonSerializer.Create(settings);

				var data = new DatabaseData
				{
					definitions = LoadDefinitions(definitionsJson, root, serializer),
				};

				var mapsToken = root["maps"] as JArray;
				if (mapsToken != null && mapsToken.Count > 0 && mapsToken[0]?.Type == JTokenType.Object)
				{
					data.maps = serializer.Deserialize<Map[]>(mapsToken.CreateReader()) ?? Array.Empty<Map>();
					data.mapIds = data.maps.Select(m => HTB50Settings.ToString(m.HashID)).ToArray();
				}
				else if (mapsToken != null)
				{
					data.mapIds = mapsToken
						.Select(t => t?.Value<string>()?.Trim())
						.Where(s => !string.IsNullOrWhiteSpace(s))
						.ToArray();
					data.maps = MapCatalog.LoadMaps(data.mapIds).ToArray();
				}
				else if (root["mapIds"] is JArray mapIdsToken)
				{
					data.mapIds = mapIdsToken
						.Select(t => t?.Value<string>()?.Trim())
						.Where(s => !string.IsNullOrWhiteSpace(s))
						.ToArray();
					data.maps = MapCatalog.LoadMaps(data.mapIds).ToArray();
				}
				else
				{
					data.mapIds = Array.Empty<string>();
					data.maps = Array.Empty<Map>();
				}

				var externalMaps = MapCatalog.GetExternalMaps()
					.Select(entry => entry.Map)
					.Where(map => map != null)
					.ToArray();

				if (externalMaps.Length > 0)
				{
					var combined = (data.maps ?? Array.Empty<Map>()).ToList();
					var existingHashes = combined
						.Where(map => map != null)
						.Select(map => map.HashID)
						.Where(hash => hash != 0)
						.ToHashSet();

					foreach (var externalMap in externalMaps)
					{
						if (externalMap == null)
							continue;

						externalMap.EnsureHashID();
						if (externalMap.HashID == 0 || existingHashes.Contains(externalMap.HashID))
							continue;

						combined.Add(externalMap);
						existingHashes.Add(externalMap.HashID);
					}

					data.maps = combined.ToArray();
				}

				var externalDefinitions = DefinitionCatalog.GetExternalDefinitions()
					.Select(entry => entry.Definition)
					.Where(def => def != null)
					.ToArray();

				if (externalDefinitions.Length > 0)
				{
					var combinedDefinitions = (data.definitions ?? Array.Empty<Definition>()).ToList();
					var existingDefinitionHashes = combinedDefinitions
						.Where(def => def != null)
						.Select(def => def.HashID)
						.Where(hash => hash != 0)
						.ToHashSet();

					foreach (var externalDefinition in externalDefinitions)
					{
						if (externalDefinition == null)
							continue;

						if (externalDefinition.HashID == 0 || existingDefinitionHashes.Contains(externalDefinition.HashID))
							continue;

						combinedDefinitions.Add(externalDefinition);
						existingDefinitionHashes.Add(externalDefinition.HashID);
					}

					data.definitions = combinedDefinitions.ToArray();
				}

				if (data.maps == null || data.definitions == null ||
					data.maps.Length == 0 || data.definitions.Length == 0)
				{
					Debug.LogError("ResourceSerializer: Database failed validation");
					return null;
				}

				Map.EnsureUniqueHashIDs(data.maps);

				return data;
			}
			catch (Exception ex)
			{
				Debug.LogError($"ResourceSerializer: Failed to deserialize database → {ex.Message}\n{ex.StackTrace}");
				return null;
			}
		}

		private static Definition[] LoadDefinitions(string definitionsJson, JObject legacyRoot, JsonSerializer serializer)
		{
			if (!string.IsNullOrWhiteSpace(definitionsJson))
			{
				var parsed = JToken.Parse(definitionsJson);
				if (parsed is JArray definitionsArray)
					return serializer.Deserialize<Definition[]>(definitionsArray.CreateReader()) ?? Array.Empty<Definition>();

				if (parsed is JObject definitionsRoot && definitionsRoot["definitions"] is JArray nestedDefinitions)
					return serializer.Deserialize<Definition[]>(nestedDefinitions.CreateReader()) ?? Array.Empty<Definition>();
			}

			if (legacyRoot["definitions"] is JArray legacyDefinitions)
				return serializer.Deserialize<Definition[]>(legacyDefinitions.CreateReader()) ?? Array.Empty<Definition>();

			return Array.Empty<Definition>();
		}

		public static void SaveDatabase(DatabaseData data, string filepath = null, string definitionsPath = null, bool verbose = false, bool cropAllMaps = true)
		{
			if (data == null) return;
			if (data.maps != null)
			{
				foreach (var map in data.maps)
				{
					if (map == null)
						continue;

					map.music = MusicResourceTable.ToHashOrOriginal(map.music);
					map.skybox = SkycubeResourceTable.ToHashOrOriginal(map.skybox);
					map.character = CharacterResourceTable.ToHashOrOriginal(map.character);
					map.effect = EffectResourceTable.ToHashOrOriginal(map.effect);
				}
			}

			if (data.definitions != null)
			{
				foreach (var def in data.definitions)
				{
					if (def == null)
						continue;

					def.material = MaterialResourceTable.ToHashOrOriginal(def.material);
				}
			}

			var internalDefinitions = new List<Definition>();
			var externalDefinitions = new List<Definition>();
			var seenDefinitionHashes = new HashSet<HashId>();

			if (data.definitions != null)
			{
				foreach (var def in data.definitions)
				{
					if (def == null || seenDefinitionHashes.Contains(def.HashID))
						continue;

					seenDefinitionHashes.Add(def.HashID);

					if (DefinitionCatalog.GetStorageLocation(def.HashID) == DefinitionCatalog.DefinitionStorageLocation.Internal)
						internalDefinitions.Add(def);
					else
						externalDefinitions.Add(def);
				}
			}

			var internalMapIds = data.maps != null && data.maps.Length > 0
				? data.maps
					.Where(m => m != null && MapCatalog.GetStorageLocation(m.HashID) == MapCatalog.MapStorageLocation.Internal)
					.Select(m => HTB50Settings.ToString(m.HashID))
					.ToArray()
				: (data.mapIds ?? Array.Empty<string>());

			if (data.maps != null)
			{
				var externalMapIds = data.maps
					.Where(m => m != null && MapCatalog.GetStorageLocation(m.HashID) == MapCatalog.MapStorageLocation.External)
					.Select(m => HTB50Settings.ToString(m.HashID))
					.ToHashSet(StringComparer.OrdinalIgnoreCase);

				foreach (var map in data.maps)
				{
					if (map == null)
						continue;

					MapCatalog.SaveMap(map);
				}

				try
				{
					if (Directory.Exists(ApplicationSettings.SystemMapsFolder))
					{
						foreach (var file in Directory.EnumerateFiles(ApplicationSettings.SystemMapsFolder, "*.json", SearchOption.TopDirectoryOnly))
						{
							if (!MapCatalog.TryGetMapHashFromFileName(file, out var fileHash) ||
								!externalMapIds.Contains(HTB50Settings.ToString(fileHash)))
								File.Delete(file);
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"SaveDatabase: map file cleanup skipped → {ex.Message}");
				}
			}

			data.mapIds = internalMapIds;

			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = verbose ? Formatting.Indented : Formatting.None,
				Converters = { new DatabaseMapConverter() }
			};

			// Build JObject manually so we can skip empty buttons array
			var root = new JObject();

			if (internalMapIds != null && internalMapIds.Length > 0)
				root["maps"] = JArray.FromObject(internalMapIds, JsonSerializer.Create(settings));

			string json = root.ToString(verbose ? Formatting.Indented : Formatting.None);
			string definitionsJson = internalDefinitions.Count > 0
				? JArray.FromObject(internalDefinitions, JsonSerializer.Create(settings)).ToString(verbose ? Formatting.Indented : Formatting.None)
				: "[]";

			string path = string.IsNullOrEmpty(filepath)
				? Path.Combine(ApplicationSettings.JsonDataProjectPath, "levels.json")
				: filepath;
			string defsRoot = string.IsNullOrEmpty(filepath)
				? ApplicationSettings.JsonDataProjectPath
				: (Path.GetDirectoryName(filepath) ?? ApplicationSettings.JsonDataProjectPath);
			string defsPath = string.IsNullOrEmpty(definitionsPath)
				? Path.Combine(defsRoot, "definitions.json")
				: definitionsPath;

			EnsureFolder(Path.GetDirectoryName(path));
			WriteJsonIfChanged(path, json);
			EnsureFolder(Path.GetDirectoryName(defsPath));
			WriteJsonIfChanged(defsPath, definitionsJson);

			try
			{
				var externalDefinitionHashes = externalDefinitions
					.Where(def => def != null)
					.Select(def => HTB50Settings.ToString(def.HashID))
					.ToHashSet(StringComparer.OrdinalIgnoreCase);

				foreach (var externalDefinition in externalDefinitions)
				{
					if (externalDefinition == null)
						continue;

					DefinitionCatalog.SaveExternalDefinition(externalDefinition);
				}

				if (Directory.Exists(DefinitionCatalog.PersistentDefinitionsFolder))
				{
					foreach (var file in Directory.EnumerateFiles(DefinitionCatalog.PersistentDefinitionsFolder, "*.json", SearchOption.TopDirectoryOnly).ToArray())
					{
						if (!DefinitionCatalog.TryGetDefinitionHashFromFileName(file, out var fileHash) ||
							!externalDefinitionHashes.Contains(HTB50Settings.ToString(fileHash)))
						{
							File.Delete(file);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"SaveDatabase: definition file cleanup skipped → {ex.Message}");
			}

#if UNITY_EDITOR
			AssetDatabase.Refresh();
#endif
			MapCatalog.ClearCache();
			DefinitionCatalog.ClearCache();

			//Debug.Log($"Database saved → {path} " +
			//		  $"(maps: {mapsToSave?.Length ?? 0}, " +
			//		  $"definitions: {data.definitions?.Length ?? 0}, " +
			//		  $"textures: {data.textures?.Length ?? 0}");

			Debug.Log($"Database saved → {path} / {defsPath} " +
					  $"(maps: {internalMapIds?.Length ?? 0}, " +
					  $"definitions: {data.definitions?.Length ?? 0}");
		}

		private static TextAsset LoadJsonAsset(string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName))
				return null;

			var root = ApplicationSettings.JsonDataResourcePath?.Trim('/');
			if (string.IsNullOrWhiteSpace(root))
				root = "ClassicTS/Config";

			var asset = Resources.Load<TextAsset>($"{root}/{fileName}");
			if (asset != null)
				return asset;

			return Resources.Load<TextAsset>($"{root}/{fileName}.json");
		}

		public static string BuildAtomicMapJson(Map originalMap, bool verbose = false, bool crop = true, bool filteredDefs = false)
		{
			if (originalMap == null)
				return null;

			var map = crop ? CreateCroppedCopy(originalMap) : originalMap.Clone();
			map.Optimise();

			using var _ = AtomicSerializationContext.PushExportOptions(verbose, filteredDefs);

			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = verbose ? Formatting.Indented : Formatting.None,
				Converters = { new AtomicMapConverter() },
			};

			return JsonConvert.SerializeObject(map, settings);
		}

		public static string GetDefaultMapExportFolder()
		{
			return ApplicationSettings.UserFolder;
		}

		public static bool MapRequiresArchive(Map originalMap)
		{
			if (originalMap == null)
				return false;

			foreach (var definition in GetUsedDefinitions(originalMap))
			{
				if (definition == null || DefinitionCatalog.IsInternalDefinition(definition.HashID))
					continue;

				if (!string.IsNullOrWhiteSpace(definition.model) && TryResolveExternalModelPath(definition.model, out _))
					return true;
			}

			return false;
		}

		public static Map ImportAtomicMap(string filepath)
		{
			if (!File.Exists(filepath))
			{
				Debug.LogError($"Import failed: File not found → {filepath}");
				return null;
			}

			try
			{
				if (IsAtomicMapArchive(filepath))
					return ImportAtomicMapArchive(filepath);

				string json = File.ReadAllText(filepath);
				return ImportAtomicMapJson(json, filepath);
			}
			catch (Exception e)
			{
				Debug.LogError($"Import failed: {e.Message}");
				return null;
			}
		}

		private static Map ImportAtomicMapArchive(string archivePath)
		{
			string stagingRoot = Path.Combine(Path.GetTempPath(), $"TileStormAtomicImport_{Guid.NewGuid():N}");

			try
			{
				Directory.CreateDirectory(stagingRoot);
				ZipFile.ExtractToDirectory(archivePath, stagingRoot, overwriteFiles: true);

				string jsonPath = Path.Combine(stagingRoot, "map_save.json");
				if (!File.Exists(jsonPath))
					jsonPath = Path.Combine(stagingRoot, "map.json");

				if (!File.Exists(jsonPath))
				{
					Debug.LogError($"Import failed: archive does not contain map_save.json or map.json → {archivePath}");
					return null;
				}

				string contentModelsRoot = Path.Combine(stagingRoot, "Content", "Models");
				if (Directory.Exists(contentModelsRoot))
				{
					EnsureFolder(ApplicationSettings.SystemModelsFolder);
					CopyDirectoryTree(contentModelsRoot, ApplicationSettings.SystemModelsFolder);
					ModelResourceTable.Refresh(forceRefresh: true);
				}

				string json = File.ReadAllText(jsonPath);
				return ImportAtomicMapJson(json, archivePath);
			}
			finally
			{
				TryDeleteDirectory(stagingRoot);
			}
		}

		private static Map ImportAtomicMapJson(string json, string sourceLabel)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				Debug.LogError($"Import failed: Empty map data → {sourceLabel}");
				return null;
			}

			var root = JObject.Parse(json);
			var importedDefinitions = LoadImportedDefinitions(root);

			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Converters = { new AtomicMapConverter() }
			};

			var importedMap = root.ToObject<Map>(JsonSerializer.Create(settings));

			if (importedMap == null || string.IsNullOrEmpty(importedMap.name))
			{
				Debug.LogError("Import failed: Invalid map or missing name");
				return null;
			}

			importedMap.EnsureHashID();

			var db = ResourceManager.database;
			if (db?.maps == null)
			{
				Debug.LogError("Database not loaded");
				return null;
			}

			ResourceManager.ApplyDefinitionChanges(importedDefinitions);

			if (MapCatalog.IsInternalMap(importedMap.HashID))
			{
				MapCatalog.DeleteCommunityMap(importedMap.HashID);
				Debug.Log($"Imported internal map loaded into memory: {importedMap.name} [{HTB50Settings.ToString(importedMap.HashID)}]");
			}
			else
			{
				int existingIndex = Array.FindIndex(db.maps, m => m != null && m.HashID == importedMap.HashID);
				if (existingIndex >= 0)
				{
					db.maps[existingIndex] = importedMap;
					Debug.Log($"Imported map replaced existing: {importedMap.name} [{HTB50Settings.ToString(importedMap.HashID)}]");
				}
				else
				{
					var list = db.maps.ToList();
					list.Add(importedMap);
					db.maps = list.ToArray();
					Debug.Log($"Imported new map added: {importedMap.name} [{HTB50Settings.ToString(importedMap.HashID)}]");
				}
			}

			ResourceManager.ApplyMapChanges(importedMap);

			if (!MapCatalog.SaveCommunityMap(importedMap))
				Debug.LogWarning($"Imported map could not be written to system cache: {importedMap.name} [{HTB50Settings.ToString(importedMap.HashID)}]");

			Debug.Log($"Map imported into database: {importedMap.name} [{HTB50Settings.ToString(importedMap.HashID)}]");
			return importedMap;
		}

		private static bool IsAtomicMapArchive(string filepath)
		{
			if (string.IsNullOrWhiteSpace(filepath) || !File.Exists(filepath))
				return false;

			if (string.Equals(Path.GetExtension(filepath), ".zip", StringComparison.OrdinalIgnoreCase))
				return true;

			try
			{
				using var stream = File.OpenRead(filepath);
				if (stream.Length < 4)
					return false;

				int b0 = stream.ReadByte();
				int b1 = stream.ReadByte();
				int b2 = stream.ReadByte();
				int b3 = stream.ReadByte();
				return b0 == 'P' && b1 == 'K' && (b2 == 3 || b2 == 5 || b2 == 7) && (b3 == 4 || b3 == 6 || b3 == 8);
			}
			catch
			{
				return false;
			}
		}

		private static Definition[] LoadImportedDefinitions(JObject root)
		{
			if (root == null)
				return Array.Empty<Definition>();

			if (root["definitions"] is not JArray definitionsArray || definitionsArray.Count == 0)
				return Array.Empty<Definition>();

			try
			{
				var settings = new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore,
					Converters = { new DefinitionConverter() }
				};

				return definitionsArray.ToObject<Definition[]>(JsonSerializer.Create(settings)) ?? Array.Empty<Definition>();
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ResourceSerializer: failed to parse imported definitions → {ex.Message}");
				return Array.Empty<Definition>();
			}
		}

		public static void ExportAtomicMap(Map originalMap, string filepath = null, bool verbose = false, bool crop = true, bool filteredDefs = false)
		{
			if (originalMap == null) return;

			var map = crop ? CreateCroppedCopy(originalMap) : originalMap;

			try
			{
				bool archiveRequired = MapRequiresArchive(originalMap);
				if (archiveRequired)
				{
					string archivePath = ResolveAtomicArchivePath(filepath, map);
					WriteAtomicMapArchive(originalMap, archivePath, verbose, crop, filteredDefs);
					Debug.Log($"ATOMIC MAP PACKAGE EXPORTED{(crop ? " (auto-cropped)" : "")} → {archivePath} ({map.width}×{map.height})");
					return;
				}

				string json = BuildAtomicMapJson(originalMap, verbose, crop, filteredDefs);

				string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(map.name) ? "Untitled" : map.name);
				string defaultFileName = $"{safeName}__{HTB50Settings.ToString(map.HashID)}.json";
				string path;

				if (string.IsNullOrWhiteSpace(filepath))
				{
					var folder = GetDefaultMapExportFolder();
					EnsureFolder(folder);
					path = Path.Combine(folder, defaultFileName);
				}
				else if (Path.HasExtension(filepath))
				{
					if (!string.Equals(Path.GetExtension(filepath), ".json", StringComparison.OrdinalIgnoreCase))
						filepath = Path.ChangeExtension(filepath, ".json");

					var directory = Path.GetDirectoryName(filepath);
					if (!string.IsNullOrEmpty(directory))
						EnsureFolder(directory);
					path = filepath;
				}
				else
				{
					EnsureFolder(filepath);
					path = Path.Combine(filepath, defaultFileName);
				}

				WriteJsonIfChanged(path, json);

				Debug.Log($"ATOMIC MAP EXPORTED{(crop ? " (auto-cropped)" : "")} → {path} ({map.width}×{map.height})");
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to export atomic map '{map?.name ?? "unknown"}': {ex.Message}");
			}
		}

		public static byte[] BuildAtomicMapArchiveBytes(Map originalMap, bool verbose = false, bool crop = true, bool filteredDefs = false)
		{
			if (originalMap == null)
				return null;

			string stagingRoot = Path.Combine(Path.GetTempPath(), $"TileStormAtomicExport_{Guid.NewGuid():N}");
			string archivePath = null;

			try
			{
				Directory.CreateDirectory(stagingRoot);
				BuildAtomicMapArchiveStaging(originalMap, stagingRoot, verbose, crop, filteredDefs);

				archivePath = Path.Combine(Path.GetTempPath(), $"TileStormAtomicExport_{Guid.NewGuid():N}.zip");
				if (File.Exists(archivePath))
					File.Delete(archivePath);

				ZipFile.CreateFromDirectory(stagingRoot, archivePath, System.IO.Compression.CompressionLevel.Fastest, includeBaseDirectory: false);
				return File.ReadAllBytes(archivePath);
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to build atomic map archive for '{originalMap?.name ?? "unknown"}': {ex.Message}");
				return null;
			}
			finally
			{
				TryDeleteDirectory(stagingRoot);
				TryDeleteFile(archivePath);
			}
		}

		private static string ResolveAtomicArchivePath(string filepath, Map map)
		{
			string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(map?.name) ? "Untitled" : map.name);
			string defaultFileName = $"{safeName}__{HTB50Settings.ToString(map?.HashID ?? 0)}.zip";

			if (string.IsNullOrWhiteSpace(filepath))
			{
				var folder = GetDefaultMapExportFolder();
				EnsureFolder(folder);
				return Path.Combine(folder, defaultFileName);
			}

			if (Path.HasExtension(filepath))
			{
				if (!string.Equals(Path.GetExtension(filepath), ".zip", StringComparison.OrdinalIgnoreCase))
					filepath = Path.ChangeExtension(filepath, ".zip");

				var directory = Path.GetDirectoryName(filepath);
				if (!string.IsNullOrWhiteSpace(directory))
					EnsureFolder(directory);
				return filepath;
			}

			EnsureFolder(filepath);
			return Path.Combine(filepath, defaultFileName);
		}

		private static void WriteAtomicMapArchive(Map originalMap, string archivePath, bool verbose, bool crop, bool filteredDefs)
		{
			string stagingRoot = Path.Combine(Path.GetTempPath(), $"TileStormAtomicExport_{Guid.NewGuid():N}");

			try
			{
				Directory.CreateDirectory(stagingRoot);
				BuildAtomicMapArchiveStaging(originalMap, stagingRoot, verbose, crop, filteredDefs);

				if (File.Exists(archivePath))
					File.Delete(archivePath);

				string archiveDirectory = Path.GetDirectoryName(archivePath);
				if (!string.IsNullOrWhiteSpace(archiveDirectory))
					EnsureFolder(archiveDirectory);

				ZipFile.CreateFromDirectory(stagingRoot, archivePath, System.IO.Compression.CompressionLevel.Fastest, includeBaseDirectory: false);
			}
			finally
			{
				TryDeleteDirectory(stagingRoot);
			}
		}

		private static void BuildAtomicMapArchiveStaging(Map originalMap, string stagingRoot, bool verbose, bool crop, bool filteredDefs)
		{
			string json = BuildAtomicMapJson(originalMap, verbose, crop, filteredDefs);
			File.WriteAllText(Path.Combine(stagingRoot, "map_save.json"), json);

			string contentRoot = Path.Combine(stagingRoot, "Content");
			EnsureFolder(contentRoot);

			foreach (var modelRoot in GetAtomicArchiveModelRoots(originalMap))
			{
				if (string.IsNullOrWhiteSpace(modelRoot) || !Directory.Exists(modelRoot))
					continue;

				string relativeModelRoot = GetArchiveContentRelativePath(modelRoot);
				string destinationRoot = Path.Combine(contentRoot, relativeModelRoot);
				CopyDirectoryTree(modelRoot, destinationRoot);
			}
		}

		private static IEnumerable<string> GetAtomicArchiveModelRoots(Map originalMap)
		{
			if (originalMap == null)
				return Enumerable.Empty<string>();

			var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var definition in GetUsedDefinitions(originalMap))
			{
				if (definition == null || DefinitionCatalog.IsInternalDefinition(definition.HashID))
					continue;

				if (string.IsNullOrWhiteSpace(definition.model) || !TryResolveExternalModelPath(definition.model, out var modelPath))
					continue;

				string modelRoot = Path.GetDirectoryName(modelPath);
				if (!string.IsNullOrWhiteSpace(modelRoot))
					roots.Add(Path.GetFullPath(modelRoot));
			}

			return roots.OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
		}

		private static bool TryResolveExternalModelPath(string modelHash, out string filePath)
		{
			filePath = null;

			if (string.IsNullOrWhiteSpace(modelHash))
				return false;

			if (!ModelResourceTable.TryGetEntry(modelHash, out var entry))
				return false;

			if (entry.Kind != ModelResourceTable.EntryKind.File)
				return false;

			filePath = entry.FilePath;
			return !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
		}

		private static IEnumerable<Definition> GetUsedDefinitions(Map originalMap)
		{
			if (originalMap == null)
				return Enumerable.Empty<Definition>();

			var usedHashes = (((Map.IVariantAccess)originalMap).Variants ?? Array.Empty<Variant>())
				.Where(v => v.hash != 0)
				.Select(v => v.hash)
				.Distinct()
				.ToArray();

			return usedHashes
				.Select(ResourceManager.GetDefinition)
				.Where(d => d != null)
				.ToArray();
		}

		private static string GetArchiveContentRelativePath(string absolutePath)
		{
			if (string.IsNullOrWhiteSpace(absolutePath))
				return null;

			string normalized = Path.GetFullPath(absolutePath);
			try
			{
				string relative = Path.GetRelativePath(ApplicationSettings.SystemFolder, normalized);
				if (!string.IsNullOrWhiteSpace(relative) && !relative.StartsWith("..", StringComparison.Ordinal))
					return relative;
			}
			catch
			{
				// Fall back to the filename-based layout below.
			}

			string fallback = Path.GetFileName(normalized);
			return string.IsNullOrWhiteSpace(fallback) ? "Content" : fallback;
		}

		private static void CopyDirectoryTree(string sourceDirectory, string destinationDirectory)
		{
			EnsureFolder(destinationDirectory);

			foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(sourceDirectory, file);
				string destination = Path.Combine(destinationDirectory, relative);
				string destinationParent = Path.GetDirectoryName(destination);
				EnsureFolder(destinationParent);
				File.Copy(file, destination, true);
			}
		}

		private static void TryDeleteDirectory(string path)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
					Directory.Delete(path, recursive: true);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ResourceSerializer: failed to clean temporary directory '{path}': {ex.Message}");
			}
		}

		private static void TryDeleteFile(string path)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
					File.Delete(path);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ResourceSerializer: failed to clean temporary file '{path}': {ex.Message}");
			}
		}

		private static Map CreateCroppedCopy(Map map)
		{
			var copy = map.Clone();

			copy.CropToContent(true);

			//if (copy.CropToContent(true))
			//	Debug.Log($"[Export] Map '{copy.name}' table consolidated table {((Map.IHashAccess)map).Hashes.Length} or auto-cropped to {copy.width}x{copy.height}");

			return copy;
		}

		private static string SanitizeFileName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return "Untitled";

			var invalid = Path.GetInvalidFileNameChars();
			var chars = value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch).ToArray();
			return new string(chars).Trim('_');
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
					Debug.LogWarning($"ResourceSerializer: failed to read existing file before save '{path}': {ex.Message}");
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
	}
}
