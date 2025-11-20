using UnityEngine;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	public static class ResourceSerializer
	{
		// ─────── FOLDERS ───
		private static readonly string DatabaseFolder = Path.Combine(Application.persistentDataPath, "Data");
		private static readonly string ExportFolder = Path.Combine(Application.persistentDataPath, "Maps");
		private static readonly string IndividualMapsFolder = Path.Combine(Application.streamingAssetsPath, "Maps");

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

		public static void SaveDatabase(DatabaseData data)
		{
			EnsureFolder(DatabaseFolder);
			string path = Path.Combine(DatabaseFolder, "database.json");
			string json = SerializeDatabase(data, false);
			File.WriteAllText(path, json);
			Debug.Log($"ResourceSerializer: Saved full database → {path}");
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
		[System.Serializable]
		public class AtomicMap
		{
			public Map map;
			public Definition[] definitions;
			public TextureSequence[] textures;
			public string version = "2.0";
			public string author = "Player";
			public string exportedFrom = "ClassicTilestorm";
		}

		public static string SerializeAtomic(AtomicMap atomic, bool pretty = true)
			=> JsonConvert.SerializeObject(atomic, pretty ? Pretty : Minified);

		public static void ExportAtomicMap(Map map, string overridePath = null)
		{
			if (map == null) return;

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

			var atomic = new AtomicMap
			{
				map = map,
				definitions = usedDefs,
				textures = usedTextures
			};

			string json = SerializeAtomic(atomic, pretty: true);

			EnsureFolder(ExportFolder);
			string path = string.IsNullOrEmpty(overridePath)
				? Path.Combine(ExportFolder, $"{map.name}.json")
				: overridePath;

			File.WriteAllText(path, json);
			Debug.Log($"ATOMIC MAP EXPORTED → {path}");
		}
	}
}