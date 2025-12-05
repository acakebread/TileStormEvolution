using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System;

namespace ClassicTilestorm
{
	public static class JsonSetup
	{
		private static bool _initialized = false;

		//[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		//public static void Init()
		//{
		//	if (_initialized) return;

		//	// Create settings once
		//	var settings = new JsonSerializerSettings();
		//	settings.Converters.Add(new MapAttachmentConverter());

		//	// Apply to ALL future JsonConvert calls
		//	JsonConvert.DefaultSettings = () => settings;

		//	_initialized = true;
		//	Debug.Log("MapAttachmentConverter registered globally (Unity-compatible)");
		//}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void Init()
		{
			if (_initialized) return;

			var settings = new JsonSerializerSettings
			{
				Converters = { new MapAttachmentConverter() },
				NullValueHandling = NullValueHandling.Ignore,
				// This works on ALL versions of Json.NET
				ContractResolver = new UnityContractResolver()
			};

			JsonConvert.DefaultSettings = () => settings;
			_initialized = true;
			Debug.Log("Json.NET configured to serialize public fields (Unity-compatible)");
		}
	}

	public class UnityContractResolver : DefaultContractResolver
	{
		protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
		{
			var property = base.CreateProperty(member, memberSerialization);

			// Force serialization of public fields (vSrc, vDst, pickupType, etc.)
			if (member is FieldInfo field)
			{
				if (field.IsPublic && !Attribute.IsDefined(field, typeof(JsonIgnoreAttribute)))
				{
					property.Ignored = false;
					property.Readable = true;
					property.Writable = true;
				}
			}

			return property;
		}
	}

	public static class ResourceSerializer
	{
		private static void EnsureFolder(string path) { if (!Directory.Exists(path)) Directory.CreateDirectory(path); }//helper

		public static void Initialise(TextAsset json)
		{
			if (json == null)
			{
				Debug.LogError("ResourceManager: invalid DatabaseJsonFile");
				return;
			}
			JsonSetup.Init();
			ResourceManager.database = LoadDatabase(json.text);
			Debug.Log("Database loaded from DatabaseJsonFile");
		}
		
		public static DatabaseData LoadDatabase(string json)
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

		public static void SaveDatabase(DatabaseData data, string filepath = null, bool verbose = false, bool cropAllMaps = true)
		{
			if (data == null) return;

			Map[] mapsToSave = data.maps;

			if (cropAllMaps && data.maps != null)
			{
				mapsToSave = data.maps
					.Select(m => m?.CreateCroppedCopy() ?? m)
					.ToArray();

				Debug.Log($"Saving database with {mapsToSave.Length} cropped maps");
			}

			var saveData = new DatabaseData
			{
				maps = mapsToSave,
				definitions = data.definitions,
				textures = data.textures,
				buttons = data.buttons
			};

			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				Formatting = verbose ? Formatting.Indented : Formatting.None,
			};

			string json = JsonConvert.SerializeObject(saveData, settings);
			string path = string.IsNullOrEmpty(filepath)
				? Path.Combine(Application.persistentDataPath, "database.json")
				: filepath;

			EnsureFolder(Path.GetDirectoryName(path));
			File.WriteAllText(path, json);

			Debug.Log($"Database saved {(cropAllMaps ? "with all maps cropped" : "preserving original sizes")} → {path}");
		}

		public static void ImportAtomicMap(string filepath)
		{
			if (!File.Exists(filepath))
			{
				Debug.LogError($"Import failed: File not found → {filepath}");
				return;
			}

			try
			{
				string json = File.ReadAllText(filepath);
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

				var db = ResourceManager.database;
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

		public static void ExportAtomicMap(Map originalMap, string filepath = null, bool verbose = false, bool crop = true)
		{
			if (originalMap == null) return;

			// THIS IS THE KEY: Work on a cropped copy — original stays untouched
			var map = crop ? originalMap.CreateCroppedCopy() : originalMap;

			// Collect used definitions & textures (from original — safer)
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

				var folder = string.IsNullOrEmpty(filepath) ? Application.persistentDataPath : filepath;
				EnsureFolder(folder);
				string path = Path.Combine(folder, $"{map.name}.json");

				File.WriteAllText(path, json);
				Debug.Log($"ATOMIC MAP EXPORTED (auto-cropped) → {path} ({map.width}x{map.height})");
			}
			finally
			{
				// Clean up atomic fields — not needed anymore
				map.definitions = null;
				map.textures = null;
			}
		}

		//private class AtomicExportResolver : DefaultContractResolver
		//{
		//	protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
		//	{
		//		var property = base.CreateProperty(member, memberSerialization);
		//		if (property.Ignored && member.DeclaringType == typeof(Map))
		//		{
		//			if (member.Name is "definitions" or "textures" or "version" or "author" or "exportedFrom")
		//			{
		//				property.Ignored = false;
		//				property.ShouldSerialize = _ => true;
		//			}
		//		}
		//		return property;
		//	}
		//}

		// In ResourceSerializer.cs, change AtomicExportResolver base class

		private class AtomicExportResolver : UnityContractResolver  // Change to inherit from UnityContractResolver
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