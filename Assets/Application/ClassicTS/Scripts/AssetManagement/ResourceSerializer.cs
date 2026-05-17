using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.IO;
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
					if (Directory.Exists(MapCatalog.PersistentMapsFolder))
					{
						foreach (var file in Directory.EnumerateFiles(MapCatalog.PersistentMapsFolder, "*.json", SearchOption.TopDirectoryOnly))
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

		public static string BuildAtomicMapJson(Map originalMap, bool verbose = false, bool crop = true)
		{
			if (originalMap == null)
				return null;

			var map = crop ? CreateCroppedCopy(originalMap) : originalMap.Clone();
			map.Optimise();

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
			string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			if (!string.IsNullOrWhiteSpace(documents))
				return Path.Combine(documents, "MHCommunity", "Maps");

			return Path.Combine(Application.persistentDataPath, "Maps");
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
				string json = File.ReadAllText(filepath);

				var settings = new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore,
					Converters = { new AtomicMapConverter() }
				};

				var importedMap = JsonConvert.DeserializeObject<Map>(json, settings);

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

				Debug.Log($"Map imported into database: {importedMap.name} [{HTB50Settings.ToString(importedMap.HashID)}]");
				return importedMap;
			}
			catch (Exception e)
			{
				Debug.LogError($"Import failed: {e.Message}");
				return null;
			}
		}

		public static void ExportAtomicMap(Map originalMap, string filepath = null, bool verbose = false, bool crop = true)
		{
			if (originalMap == null) return;

			var map = crop ? CreateCroppedCopy(originalMap) : originalMap;

			try
			{
				string json = BuildAtomicMapJson(originalMap, verbose, crop);

				var folder = string.IsNullOrEmpty(filepath)
					? Application.persistentDataPath
					: filepath;

				EnsureFolder(folder);
				string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(map.name) ? "Untitled" : map.name);
				string path = Path.Combine(folder, $"{safeName}__{HTB50Settings.ToString(map.HashID)}.json");

				WriteJsonIfChanged(path, json);

				Debug.Log($"ATOMIC MAP EXPORTED{(crop ? " (auto-cropped)" : "")} → {path} ({map.width}×{map.height})");
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to export atomic map '{map?.name ?? "unknown"}': {ex.Message}");
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
