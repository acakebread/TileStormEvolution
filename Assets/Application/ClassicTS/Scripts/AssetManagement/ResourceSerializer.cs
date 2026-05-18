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
	internal static class JsonSetup
	{
		private static bool _initialized = false;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		internal static void Init()
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

	internal static class ResourceSerializer
	{
		internal sealed class AtomicMapExportData
		{
			public bool IsArchive { get; }
			public string FileName { get; }
			public string JsonFileName { get; }
			public string MimeType { get; }
			public string Json { get; }
			public byte[] Archive { get; }

			public bool IsValid => IsArchive ? Archive != null && Archive.Length > 0 : !string.IsNullOrEmpty(Json);
			public string DisplayLabel => IsArchive ? "Map package" : "Map export";
			public string DefaultDialogTitle => IsArchive ? "Export Map Package" : "Export Map As Atomic JSON";
			public string FileExtension => Path.GetExtension(FileName).TrimStart('.');

			internal AtomicMapExportData(bool isArchive, string fileName, string mimeType, string json, byte[] archive)
			{
				IsArchive = isArchive;
				FileName = fileName;
				JsonFileName = Path.ChangeExtension(fileName, ".json");
				MimeType = mimeType;
				Json = json;
				Archive = archive;
			}
		}

		internal sealed class DatabaseSaveData
		{
			public string LevelsPath { get; }
			public string DefinitionsPath { get; }
			public string LevelsJson { get; }
			public string DefinitionsJson { get; }
			public int MapCount { get; }
			public int DefinitionCount { get; }
			public bool Verbose { get; }
			public bool CropAllMaps { get; }

			internal DatabaseSaveData(string levelsPath, string definitionsPath, string levelsJson, string definitionsJson, int mapCount, int definitionCount, bool verbose, bool cropAllMaps)
			{
				LevelsPath = levelsPath;
				DefinitionsPath = definitionsPath;
				LevelsJson = levelsJson;
				DefinitionsJson = definitionsJson;
				MapCount = mapCount;
				DefinitionCount = definitionCount;
				Verbose = verbose;
				CropAllMaps = cropAllMaps;
			}
		}

		internal static void Initialise()
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

		internal static DatabaseData LoadDatabase(string json, string definitionsJson = null)
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
				definitions = LoadDefinitions(definitionsJson, serializer),
				};

				var mapsToken = root["maps"] as JArray;
				if (mapsToken != null)
				{
					data.mapIds = mapsToken
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

		private static Definition[] LoadDefinitions(string definitionsJson, JsonSerializer serializer)
		{
			if (!string.IsNullOrWhiteSpace(definitionsJson))
			{
				var parsed = JToken.Parse(definitionsJson);
				if (parsed is JArray definitionsArray)
					return serializer.Deserialize<Definition[]>(definitionsArray.CreateReader()) ?? Array.Empty<Definition>();
			}

			return Array.Empty<Definition>();
		}

		internal static DatabaseSaveData SaveDatabase(DatabaseData data, string filepath = null, string definitionsPath = null, bool verbose = false, bool cropAllMaps = true)
		{
			if (data == null) return null;
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

			FileUtils.EnsureFolder(Path.GetDirectoryName(path));
			WriteJsonIfChanged(path, json);
			FileUtils.EnsureFolder(Path.GetDirectoryName(defsPath));
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

			Debug.Log($"Database saved → {path} / {defsPath} " +
					  $"(maps: {internalMapIds?.Length ?? 0}, " +
					  $"definitions: {data.definitions?.Length ?? 0}");

			return new DatabaseSaveData(
				path,
				defsPath,
				json,
				definitionsJson,
				internalMapIds?.Length ?? 0,
				data.definitions?.Length ?? 0,
				verbose,
				cropAllMaps);
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

		private static string BuildAtomicMapJson(Map originalMap, bool crop = true, bool padded = false, bool verbose = false)
		{
			if (originalMap == null)
				return null;

			var map = crop ? CreateCroppedCopy(originalMap) : originalMap.Clone();
			map.Optimise();

			using var _ = AtomicSerializationContext.PushExportOptions(verbose, !verbose);

			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = padded ? Formatting.Indented : Formatting.None,
				Converters = { new AtomicMapConverter() },
			};

			return JsonConvert.SerializeObject(map, settings);
		}

		internal static AtomicMapExportData ExportAtomicMap(Map originalMap, bool crop = true, bool padded = false, bool verbose = false)
		{
			if (originalMap == null)
				return null;

			var map = crop ? CreateCroppedCopy(originalMap) : originalMap;
			bool archiveRequired = ResourceDependencyHelpers.RequiresAtomicArchive(originalMap);
			string fileName = $"{BuildAtomicExportBaseFileName(map)}{(archiveRequired ? ".zip" : ".json")}";

			if (archiveRequired)
			{
				byte[] archive = BuildAtomicMapArchiveBytes(originalMap, crop: crop, padded: padded, verbose: verbose);
				return new AtomicMapExportData(true, fileName, "application/zip", null, archive);
			}

			string json = BuildAtomicMapJson(originalMap, crop: crop, padded: padded, verbose: verbose);
			return new AtomicMapExportData(false, fileName, "application/json;charset=utf-8", json, null);
		}

		private static string BuildAtomicExportBaseFileName(Map map)
		{
			var name = string.IsNullOrWhiteSpace(map?.name) ? "Untitled" : map.name;
			var safeName = StringUtil.SanitizeFileName(name);
			return $"{safeName}__{HTB50Settings.ToString(map?.HashID ?? 0)}";
		}

		internal static Map ImportAtomicMap(string filepath)
		{
			if (!File.Exists(filepath))
			{
				Debug.LogError($"Import failed: File not found → {filepath}");
				return null;
			}

			try
			{
				if (FileUtils.IsZipArchive(filepath))
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

				string jsonPath = Directory.GetFiles(stagingRoot, "*.json", SearchOption.TopDirectoryOnly)
					.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
					.FirstOrDefault();

				if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
				{
					Debug.LogError($"Import failed: archive does not contain a root JSON file → {archivePath}");
					return null;
				}

				RestoreAtomicMapArchiveContent(stagingRoot);

				string json = File.ReadAllText(jsonPath);
				return ImportAtomicMapJson(json, archivePath);
			}
			finally
			{
				FileUtils.TryDeleteDirectory(stagingRoot, nameof(ResourceSerializer));
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

		private static byte[] BuildAtomicMapArchiveBytes(Map originalMap, bool crop = true, bool padded = false, bool verbose = false)
		{
			if (originalMap == null)
				return null;

			string stagingRoot = Path.Combine(Path.GetTempPath(), $"TileStormAtomicExport_{Guid.NewGuid():N}");
			string archivePath = null;

			try
			{
				Directory.CreateDirectory(stagingRoot);
				BuildAtomicMapArchiveStaging(originalMap, stagingRoot, crop: crop, padded: padded, verbose: verbose);

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
				FileUtils.TryDeleteDirectory(stagingRoot, nameof(ResourceSerializer));
				FileUtils.TryDeleteFile(archivePath, nameof(ResourceSerializer));
			}
		}

		private static void BuildAtomicMapArchiveStaging(Map originalMap, string stagingRoot, bool crop, bool padded, bool verbose)
		{
			string json = BuildAtomicMapJson(originalMap, crop: crop, padded: padded, verbose: verbose);
			File.WriteAllText(Path.Combine(stagingRoot, BuildAtomicExportBaseFileName(originalMap) + ".json"), json);

			string contentRoot = Path.Combine(stagingRoot, "Content");
			FileUtils.EnsureFolder(contentRoot);

			foreach (var modelRoot in ResourceDependencyHelpers.GetAtomicArchiveModelRoots(originalMap))
			{
				if (string.IsNullOrWhiteSpace(modelRoot) || !Directory.Exists(modelRoot))
					continue;

				string relativeModelRoot = GetArchiveContentRelativePath(modelRoot);
				string destinationRoot = Path.Combine(contentRoot, relativeModelRoot);
				FileUtils.CopyDirectoryTree(modelRoot, destinationRoot);
			}

			CopyAtomicArchiveSourceRoot(ApplicationSettings.SystemMaterialsFolder, contentRoot);
			CopyAtomicArchiveSourceRoot(ApplicationSettings.SystemTexturesFolder, contentRoot);
			CopyAtomicArchiveSourceRoot(ApplicationSettings.SystemMusicFolder, contentRoot);
			CopyAtomicArchiveSourceRoot(ApplicationSettings.SystemSoundsFolder, contentRoot);
			CopyAtomicArchiveSourceRoot(ApplicationSettings.SystemSkyCubesFolder, contentRoot);
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

		private static void RestoreAtomicMapArchiveContent(string stagingRoot)
		{
			string contentRoot = Path.Combine(stagingRoot, "Content");
			if (!Directory.Exists(contentRoot))
				return;

			CopyAtomicArchiveRoot(contentRoot, ApplicationSettings.SystemModelsFolder);
			CopyAtomicArchiveRoot(contentRoot, ApplicationSettings.SystemMaterialsFolder);
			CopyAtomicArchiveRoot(contentRoot, ApplicationSettings.SystemTexturesFolder);
			CopyAtomicArchiveRoot(contentRoot, ApplicationSettings.SystemMusicFolder);
			CopyAtomicArchiveRoot(contentRoot, ApplicationSettings.SystemSoundsFolder);
			CopyAtomicArchiveRoot(contentRoot, ApplicationSettings.SystemSkyCubesFolder);

			RefreshImportedContentCaches();
		}

		private static void CopyAtomicArchiveSourceRoot(string sourceRoot, string contentRoot)
		{
			if (string.IsNullOrWhiteSpace(sourceRoot) || string.IsNullOrWhiteSpace(contentRoot))
				return;

			if (!Directory.Exists(sourceRoot))
				return;

			string archiveFolderName = ResourceDependencyHelpers.GetArchiveFolderName(sourceRoot);
			if (string.IsNullOrWhiteSpace(archiveFolderName))
				return;

			string destinationRoot = Path.Combine(contentRoot, archiveFolderName);
			FileUtils.CopyDirectoryTree(sourceRoot, destinationRoot);
		}

		private static void CopyAtomicArchiveRoot(string contentRoot, string destinationRoot)
		{
			if (string.IsNullOrWhiteSpace(contentRoot) || string.IsNullOrWhiteSpace(destinationRoot))
				return;

			string canonicalFolderName = Path.GetFileName(destinationRoot);
			string sourceFolder = ResourceDependencyHelpers.FindArchiveContentFolder(contentRoot, canonicalFolderName);
			if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
				return;

			FileUtils.EnsureFolder(destinationRoot);
			FileUtils.CopyDirectoryTree(sourceFolder, destinationRoot);
		}

		private static void RefreshImportedContentCaches()
		{
			ModelResourceTable.Refresh(forceRefresh: true);
			TextureResourceTable.ClearCache();
			MaterialResourceTable.ClearCache();
			SkycubeResourceTable.ClearCache();
			MusicResourceTable.ClearCache();
			SoundResourceTable.ClearCache();
			ImportedResourceLoader.ClearCache();
			ProjectAssets.RefreshAllNameCaches();
		}

		private static Map CreateCroppedCopy(Map map)
		{
			var copy = map.Clone();

			copy.CropToContent(true);

			//if (copy.CropToContent(true))
			//	Debug.Log($"[Export] Map '{copy.name}' table consolidated table {((Map.IHashAccess)map).Hashes.Length} or auto-cropped to {copy.width}x{copy.height}");

			return copy;
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
					Debug.LogWarning($"ResourceSerializer: failed to read existing file before save '{path}': {ex.Message}");
				}
			}

			File.WriteAllText(path, normalized);
		}
	}
}
