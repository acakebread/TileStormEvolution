using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace ClassicTilestorm
{
	public static class ResourceSerializer
	{
		// ─────── FOLDERS ───
		private static readonly string DatabaseFolder = Path.Combine(Application.persistentDataPath, "Data");
		private static readonly string ExportFolder = Path.Combine(Application.persistentDataPath, "Maps");
		private static readonly string IndividualMapsFolder = Path.Combine(Application.streamingAssetsPath, "Maps");

		public static string GetExportFolder() => ExportFolder;

		private static void EnsureFolder(string path)
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
		}

		// ─────── MUTABLE DATABASE (persistentDataPath) ───────
		public static TextAsset GetMutableDatabaseTextAsset(TextAsset pristine)
		{
			EnsureFolder(DatabaseFolder);

			string fileName = pristine.name.EndsWith(".json") ? pristine.name : pristine.name + ".json";
			string path = Path.Combine(DatabaseFolder, fileName);

			if (!File.Exists(path))
			{
				File.WriteAllText(path, pristine.text);
				Debug.Log($"ResourceSerializer: Created mutable database → {path}");
			}

			return new TextAsset(File.ReadAllText(path)) { name = pristine.name };
		}

		public static void OverwriteMutableDatabaseWithPristine(TextAsset pristine)
		{
			EnsureFolder(DatabaseFolder);

			string fileName = pristine.name.EndsWith(".json") ? pristine.name : pristine.name + ".json";
			string path = Path.Combine(DatabaseFolder, fileName);
			File.WriteAllText(path, pristine.text);
			Debug.Log($"ResourceSerializer: Restored pristine database → {path}");
		}

		// ─────── DATABASE SERIALIZATION ───────
		private static readonly JsonSerializerSettings Minified = new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			MissingMemberHandling = MissingMemberHandling.Ignore
		};

		private static readonly JsonSerializerSettings Pretty = new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.Indented
		};

		public static DatabaseData DeserializeDatabase(string json)
		{
			if (string.IsNullOrEmpty(json)) return null;

			try
			{
				var root = JObject.Parse(json);
				var serializer = JsonSerializer.CreateDefault(Minified);

				var data = new DatabaseData
				{
					maps = root["maps"]?.ToObject<Map[]>(serializer) ?? System.Array.Empty<Map>(),
					definitions = root["definitions"]?.ToObject<Definition[]>(serializer) ?? System.Array.Empty<Definition>(),
					textures = root["textures"]?.ToObject<TextureSequence[]>(serializer) ?? System.Array.Empty<TextureSequence>(),
					buttons = root["buttons"]?.ToObject<Button[]>(serializer) ?? System.Array.Empty<Button>()
				};

				if (data.maps == null || data.definitions == null || data.textures == null ||
					data.maps.Length == 0 || data.definitions.Length == 0 || data.textures.Length == 0)
				{
					Debug.LogError("ResourceSerializer: Database failed validation");
					return null;
				}

				return data;
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"ResourceSerializer: Failed to deserialize database → {ex.Message}");
				return null;
			}
		}

		public static string SerializeDatabase(DatabaseData data, bool pretty = false)
			=> JsonConvert.SerializeObject(data, pretty ? Pretty : Minified);

		// Add this method to ResourceSerializer.cs — that's literally it
		public static void SaveDatabase(DatabaseData data, string overridePath = null)
		{
			if (data == null) return;

			string json = JsonConvert.SerializeObject(data, Minified); // ← Uses your shared Minified settings

			string path = string.IsNullOrEmpty(overridePath)
				? Path.Combine(DatabaseFolder, "database.json")
				: overridePath;

			EnsureFolder(Path.GetDirectoryName(path));
			File.WriteAllText(path, json);
		}

		// ─────── INDIVIDUAL MAPS (StreamingAssets) ───────
		public static Map DeserializeMap(string json)
			=> string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Map>(json, Minified);

		public static string SerializeMap(Map map, bool pretty = false)
			=> JsonConvert.SerializeObject(map, pretty ? Pretty : Minified);

		public static Map[] LoadIndividualMaps()
		{
			if (!Directory.Exists(IndividualMapsFolder)) return System.Array.Empty<Map>();

			var list = new System.Collections.Generic.List<Map>();
			foreach (string file in Directory.GetFiles(IndividualMapsFolder, "*.json"))
			{
				try
				{
					string json = File.ReadAllText(file);
					var map = DeserializeMap(json);
					if (map != null)
					{
						if (string.IsNullOrEmpty(map.name))
							map.name = Path.GetFileNameWithoutExtension(file);
						list.Add(map);
					}
				}
				catch (System.Exception ex)
				{
					Debug.LogError($"Failed to load individual map {file}: {ex.Message}");
				}
			}
			return list.ToArray();
		}

		public static bool SaveIndividualMap(Map map)
		{
			if (map == null || string.IsNullOrEmpty(map.name)) return false;

			EnsureFolder(IndividualMapsFolder);
			string safeName = string.Join("_", map.name.Split(Path.GetInvalidFileNameChars()));
			string path = Path.Combine(IndividualMapsFolder, safeName + ".json");
			string json = SerializeMap(map, false);
			File.WriteAllText(path, json);
			Debug.Log($"Saved individual map → {path}");
			return true;
		}

		// ─────── ATOMIC EXPORT ───────
		private class AtomicExportResolver : DefaultContractResolver
		{
			protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
			{
				var property = base.CreateProperty(member, memberSerialization);

				if (property.Ignored && member.DeclaringType == typeof(Map))
				{
					if (member.Name is "definitions" or "textures" or "version" or "author" or "exportedFrom")
					{
						property.Ignored = false;
						property.ShouldSerialize = _ => true;
					}
				}

				return property;
			}
		}

		public static void ExportAtomicMap(Map map, string overridePath = null)
		{
			if (map == null) return;

			// Collect used definitions & textures
			var usedTypes = map.table?
				.Where(t => !string.IsNullOrEmpty(t))
				.Distinct()
				.ToArray() ?? System.Array.Empty<string>();

			var usedDefs = ResourceManager.Definitions
				.Where(d => usedTypes.Contains(d.id))
				.ToArray();

			var usedBanks = usedDefs
				.Where(d => !string.IsNullOrEmpty(d.texture))
				.Select(d => d.texture)
				.Distinct()
				.ToArray();

			var usedTextures = ResourceManager.TextureSets
				.Where(ts => usedBanks.Contains(ts.name))
				.ToArray();

			// Inject atomic data
			map.definitions = usedDefs;
			map.textures = usedTextures;
			map.version = "1.0";
			map.author = "Player";
			map.exportedFrom = "ClassicTilestorm";

			try
			{
				var settings = new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore,
					Formatting = Formatting.Indented,
					ContractResolver = new AtomicExportResolver()  // Clean, readable, works everywhere
				};

				string json = JsonConvert.SerializeObject(map, settings);

				EnsureFolder(ExportFolder);
				string path = string.IsNullOrEmpty(overridePath)
					? Path.Combine(ExportFolder, $"{map.name}.json")
					: overridePath;

				File.WriteAllText(path, json);
				Debug.Log($"ATOMIC MAP EXPORTED → {path}");
			}
			finally
			{
				map.definitions = null;
				map.textures = null;
			}
		}

		public static void ImportAtomicMapFromFile(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError($"Import failed: File not found → {filePath}");
				return;
			}

			try
			{
				string json = File.ReadAllText(filePath);
				var importedMap = JsonConvert.DeserializeObject<Map>(json);

				if (importedMap == null || string.IsNullOrEmpty(importedMap.name))
				{
					Debug.LogError("Import failed: Invalid map or missing name");
					return;
				}

				// STRIP atomic-only fields — NEVER let them into the master database
				importedMap.definitions = null;
				importedMap.textures = null;
				importedMap.version = null;
				importedMap.author = null;
				importedMap.exportedFrom = null;

				var db = ResourceManager.GetCurrentData();
				if (db?.maps == null)
				{
					Debug.LogError("Database not loaded");
					return;
				}

				int existingIndex = System.Array.FindIndex(db.maps, m => m.name == importedMap.name);
				if (existingIndex >= 0)
				{
					db.maps[existingIndex] = importedMap;
					Debug.Log($"Imported map replaced existing: {importedMap.name}");
				}
				else
				{
					var list = db.maps.ToList();
					list.Add(importedMap);
					db.maps = list.ToArray();
					Debug.Log($"Imported new map added: {importedMap.name}");
				}

				ResourceManager.ApplyMapChanges(importedMap);

				// NO AUTO-SAVE TO PROJECT — user must click "Save Database" manually

				// Reload current map if it's the one we imported
				var currentManager = Object.FindFirstObjectByType<MapManager>(); // Fixed obsolete warning
				if (currentManager != null && currentManager.CurrentMap.name == importedMap.name)
				{
					var parent = currentManager.transform.parent;
					Object.Destroy(currentManager.gameObject);
					MapManager.Instantiate(importedMap, parent);
					Debug.Log($"Currently active map reloaded after import: {importedMap.name}");
				}

				Debug.Log($"Map imported successfully: {importedMap.name}");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Import failed: {e.Message}");
			}
		}
	}
}