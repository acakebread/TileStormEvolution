using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ClassicTilestorm.Assets;

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

		public static void Initialise(TextAsset jsonAsset)
		{
#if UNITY_EDITOR
			if (jsonAsset != null)
			{
				string path = AssetDatabase.GetAssetPath(jsonAsset);
				if (!string.IsNullOrEmpty(path))
				{
					AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
					AssetDatabase.Refresh();
				}
			}
#endif

			if (jsonAsset == null) return;

			JsonSetup.Init();
			MapCatalog.ClearCache();
			PrefabResourceTable.ClearCache();
			TextureResourceTable.ClearCache();
			MaterialResourceTable.ClearCache();
			SkycubeResourceTable.ClearCache();
			MusicResourceTable.ClearCache();
			SoundResourceTable.ClearCache();
			CharacterResourceTable.ClearCache();
			EffectResourceTable.ClearCache();
			ProjectAssets.RefreshAllNameCaches();
			ResourceManager.database = null;// important
			ResourceManager.database = LoadDatabase(jsonAsset.text);
		}

		public static DatabaseData LoadDatabase(string json)
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
					definitions = root["definitions"] != null
						? serializer.Deserialize<Definition[]>(root["definitions"].CreateReader())
						: Array.Empty<Definition>(),
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

				if (data.maps == null || data.definitions == null ||
					data.maps.Length == 0 || data.definitions.Length == 0)
				{
					Debug.LogError("ResourceSerializer: Database failed validation");
					return null;
				}

				Map.EnsureUniqueHashIDs(data.maps);
				data.mapIds = data.maps.Select(m => HTB50Settings.ToString(m.HashID)).ToArray();

				return data;
			}
			catch (Exception ex)
			{
				Debug.LogError($"ResourceSerializer: Failed to deserialize database → {ex.Message}\n{ex.StackTrace}");
				return null;
			}
		}

		public static void SaveDatabase(DatabaseData data, string filepath = null, bool verbose = false, bool cropAllMaps = true)
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

			data.mapIds = data.maps != null && data.maps.Length > 0
				? data.maps.Where(m => m != null).Select(m => HTB50Settings.ToString(m.HashID)).ToArray()
				: (data.mapIds ?? Array.Empty<string>());

			if (data.maps != null)
			{
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
						var allowed = data.mapIds
							.Where(id => !string.IsNullOrWhiteSpace(id))
							.ToHashSet(StringComparer.OrdinalIgnoreCase);

						foreach (var file in Directory.EnumerateFiles(MapCatalog.PersistentMapsFolder, "*.json", SearchOption.TopDirectoryOnly))
						{
							var name = Path.GetFileNameWithoutExtension(file);
							if (string.IsNullOrWhiteSpace(name))
								continue;

							if (!MapCatalog.TryGetMapHashFromFileName(file, out var fileHash) || !allowed.Contains(HTB50Settings.ToString(fileHash)))
								File.Delete(file);
						}
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"SaveDatabase: map file cleanup skipped → {ex.Message}");
				}
			}

			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = verbose ? Formatting.Indented : Formatting.None,
				Converters = { new DatabaseMapConverter() }
			};

			// Build JObject manually so we can skip empty buttons array
			var root = new JObject();

			if (data.mapIds != null && data.mapIds.Length > 0)
				root["maps"] = JArray.FromObject(data.mapIds, JsonSerializer.Create(settings));

			if (data.definitions != null && data.definitions.Length > 0)
				root["definitions"] = JArray.FromObject(data.definitions, JsonSerializer.Create(settings));

			//if (data.textures != null && data.textures.Length > 0)
			//	root["textures"] = JArray.FromObject(data.textures, JsonSerializer.Create(settings));

			string json = root.ToString(verbose ? Formatting.Indented : Formatting.None);

			string path = string.IsNullOrEmpty(filepath)
				? Path.Combine(Application.persistentDataPath, "database.json")
				: filepath;

			EnsureFolder(Path.GetDirectoryName(path));
			File.WriteAllText(path, json);

#if UNITY_EDITOR
			AssetDatabase.Refresh();
#endif

			//Debug.Log($"Database saved → {path} " +
			//		  $"(maps: {mapsToSave?.Length ?? 0}, " +
			//		  $"definitions: {data.definitions?.Length ?? 0}, " +
			//		  $"textures: {data.textures?.Length ?? 0}");

			Debug.Log($"Database saved → {path} " +
					  $"(maps: {data.mapIds?.Length ?? 0}, " +
					  $"definitions: {data.definitions?.Length ?? 0}");
		}

		public static string BuildAtomicMapJson(Map originalMap, bool verbose = false, bool crop = true)
		{
			if (originalMap == null)
				return null;

			var map = crop ? CreateCroppedCopy(originalMap) : originalMap;

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

				Map.EnsureUniqueHashIDs(db.maps);
				ResourceManager.SyncMapIds();
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

				File.WriteAllText(path, json);

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
	}
}
