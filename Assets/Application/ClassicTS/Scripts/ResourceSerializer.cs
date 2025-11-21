using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ClassicTilestorm
{
	public static class ResourceSerializer
	{
		// ─────── FOLDERS ───
		private static readonly string DatabaseFolder = Path.Combine(Application.persistentDataPath, "Data");
		private static readonly string ExportFolder = Path.Combine(Application.persistentDataPath, "Maps");

		public static string GetExportFolder() => ExportFolder;

		private static void EnsureFolder(string path)
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
		}

		public static DatabaseData DeserializeDatabase(string json)
		{
			if (string.IsNullOrEmpty(json)) return null;

			try
			{
				var root = JObject.Parse(json);
				var serializer = JsonSerializer.CreateDefault();

				var data = new DatabaseData
				{
					maps = root["maps"]?.ToObject<Map[]>(serializer) ?? System.Array.Empty<Map>(),
					definitions = root["definitions"]?.ToObject<Definition[]>(serializer) ?? System.Array.Empty<Definition>(),
					textures = root["textures"]?.ToObject<TextureSequence[]>(serializer) ?? System.Array.Empty<TextureSequence>(),
					buttons = root["buttons"]?.ToObject<Button[]>(serializer) ?? System.Array.Empty<Button>()
				};

				if (data.maps == null || data.definitions == null || data.textures == null || data.maps.Length == 0 || data.definitions.Length == 0 || data.textures.Length == 0)
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

		public static void SaveDatabase(DatabaseData data, string overridePath = null, bool verbose = false)
		{
			if (data == null) return;

			// Minified settings by default
			JsonSerializerSettings settings = new()
			{
				NullValueHandling = NullValueHandling.Ignore,
				MissingMemberHandling = MissingMemberHandling.Ignore,
				Formatting = verbose ? Formatting.Indented : Formatting.None,
			};

			string json = JsonConvert.SerializeObject(data, settings); 
			string path = string.IsNullOrEmpty(overridePath) ? Path.Combine(DatabaseFolder, "database.json") : overridePath;

			EnsureFolder(Path.GetDirectoryName(path));
			File.WriteAllText(path, json);
		}

		public static void ImportAtomicMap(string filePath)
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

				// STRIP atomic-only fields — this is the ONLY thing Import should do
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

				Debug.Log($"Map imported into database: {importedMap.name}");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"Import failed: {e.Message}");
			}
		}

		public static void ExportAtomicMap(Map map, string overridePath = null, bool verbose = false)
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
					Formatting = verbose ? Formatting.Indented : Formatting.None,
					ContractResolver = new AtomicExportResolver()
				};

				string json = JsonConvert.SerializeObject(map, settings);

				EnsureFolder(ExportFolder);
				string path = string.IsNullOrEmpty(overridePath) ? Path.Combine(ExportFolder, $"{map.name}.json") : overridePath;

				File.WriteAllText(path, json);
				Debug.Log($"ATOMIC MAP EXPORTED → {path}");
			}
			finally
			{
				map.definitions = null;
				map.textures = null;
			}
		}

		private class AtomicExportResolver : DefaultContractResolver
		{
			protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
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
	}
}