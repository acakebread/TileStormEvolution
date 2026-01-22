using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System;
using UnityEditor;
using MassiveHadronLtd;
using MassiveHadronLtd.IDs.HTB50;
using System.Collections.Generic;

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
				Converters = { new MapAttachmentConverter() },
				NullValueHandling = NullValueHandling.Ignore,
			};

			JsonConvert.DefaultSettings = () => settings;
			_initialized = true;
			Debug.Log("Json.NET configured with ordered properties (declaration order)");
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
					maps = root["maps"] != null
						? serializer.Deserialize<Map[]>(root["maps"].CreateReader())
						: Array.Empty<Map>(),

					definitions = root["definitions"] != null
						? serializer.Deserialize<Definition[]>(root["definitions"].CreateReader())
						: Array.Empty<Definition>(),

					textures = root["textures"] != null
						? serializer.Deserialize<TextureSequence[]>(root["textures"].CreateReader())
						: Array.Empty<TextureSequence>(),

					buttons = root["buttons"] != null
						? serializer.Deserialize<Legacy.Button[]>(root["buttons"].CreateReader())
						: Array.Empty<Legacy.Button>()
				};

				if (data.maps == null || data.definitions == null || data.textures == null ||
					data.maps.Length == 0 || data.definitions.Length == 0 || data.textures.Length == 0)
				{
					Debug.LogError("ResourceSerializer: Database failed validation");
					return null;
				}

				// PHASE 1: Ensure every definition has a hashid
				bool defsChanged = false;
				foreach (var def in data.definitions.Where(d => d != null && string.IsNullOrEmpty(d.hashid)))
				{
					def.hashid = def.GetStableId();
					defsChanged = true;
				}
				if (defsChanged)
				{
					Debug.Log($"Assigned hashids to {data.definitions.Count(d => !string.IsNullOrEmpty(d.hashid))} definitions");
				}

				// PHASE 2: Convert every map's table to contain hashids only
				// PHASE 2: Convert legacy name-only tables → hashid-based tables (only if needed)
				int mapsMigrated = 0;
				int entriesConverted = 0;

				// Pre-build name → hash lookup for speed
				var nameToHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (var def in data.definitions.Where(d => d != null))
				{
					if (!string.IsNullOrEmpty(def.id) && !string.IsNullOrEmpty(def.hashid))
						nameToHash[def.id] = def.hashid;
				}

				foreach (var map in data.maps.Where(m => m != null && m._tileEntries != null))
				{
					bool mapChanged = false;

					for (int i = 0; i < map._tileEntries.Count; i++)
					{
						var entry = map._tileEntries[i];
						string current = entry.DisplayName?.Trim();

						if (string.IsNullOrEmpty(current))
							continue;

						// Skip if it already looks like a hash (6 chars, alphanumeric)
						if (current.Length == Definition.HTB50Settings.FixedLength &&
							current.All(c => char.IsLetterOrDigit(c)))
						{
							// Already a hash → leave alone
							continue;
						}

						// Try to resolve as name
						var def = ResourceManager.GetDefinition(current);
						string hashToUse = def?.hashid;

						if (string.IsNullOrEmpty(hashToUse))
						{
							// Fallback: check if it's a known name
							if (nameToHash.TryGetValue(current, out string knownHash))
							{
								hashToUse = knownHash;
							}
							else
							{
								// Last resort: generate deterministic hash
								long hash64 = RadixHash.HashToRange64(current, Definition.HTB50Settings.Modulus);
								hashToUse = HTB50.EncodeFixed(hash64, Definition.HTB50Settings.FixedLength, appendFlavor: false);

								Debug.LogWarning($"Generated hash '{hashToUse}' for unmapped tile '{current}' in map '{map.name}'");
							}
						}

						if (current != hashToUse)
						{
							map._tileEntries[i] = new Map.TileEntry(hashToUse);
							mapChanged = true;
							entriesConverted++;
						}
					}

					if (mapChanged)
					{
						// Rebuild table so getter returns hashes
						map.table = map._tileEntries.Select(e => e.DisplayName).ToArray();
						mapsMigrated++;
						Debug.Log($"Migrated table to hashids in map '{map.name}' ({entriesConverted} entries)");
					}
				}

				if (mapsMigrated > 0 || entriesConverted > 0)
				{
					Debug.Log($"Load-time migration: {mapsMigrated} maps, {entriesConverted} entries converted to hashids");
				}

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
				Converters = { new DatabaseMapConverter() }  // ← add this
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

				var settings = new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore,
					Converters = { new AtomicMapConverter() }
				};

				var importedMap = JsonConvert.DeserializeObject<Map>(json, settings);

				if (importedMap == null || string.IsNullOrEmpty(importedMap.name))
				{
					Debug.LogError("Import failed: Invalid map or missing name");
					return;
				}

				// STRIP atomic-only fields
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

				int existingIndex = Array.FindIndex(db.maps, m => m.name == importedMap.name);
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
			catch (Exception e)
			{
				Debug.LogError($"Import failed: {e.Message}");
			}
		}

		public static void ExportAtomicMap(Map originalMap, string filepath = null, bool verbose = false, bool crop = true)
		{
			if (originalMap == null) return;

			var map = crop ? originalMap.CreateCroppedCopy() : originalMap;

			var usedTypes = map.table?
				.Where(t => !string.IsNullOrEmpty(t))
				.Distinct()
				.ToArray() ?? Array.Empty<string>();

			var usedDefs = ResourceManager.Definitions
				.Where(d => usedTypes.Contains(d.id))
				.ToArray();

			var usedBanks = usedDefs
				.Where(d => !string.IsNullOrEmpty(d.texture))
				.Select(d => d.texture)
				.Distinct()
				.ToArray();

			var usedTextures = ResourceManager.TextureSequences
				.Where(ts => usedBanks.Contains(ts.id))
				.ToArray();

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
					ContractResolver = new AtomicExportResolver(),
					Converters = { new AtomicMapConverter() }
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
				map.definitions = null;
				map.textures = null;
			}
		}

		private class AtomicExportResolver : UnityContractResolver
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
	}
}
