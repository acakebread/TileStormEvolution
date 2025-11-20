using UnityEngine;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	public static class ResourceSerializer
	{
		private static readonly string DatabaseFolder =
			System.IO.Path.Combine(Application.persistentDataPath, "Data");

		private static readonly string ExportFolder =
			System.IO.Path.Combine(Application.persistentDataPath, "Maps");

		private static readonly string IndividualMapsFolder =
			System.IO.Path.Combine(Application.streamingAssetsPath, "Maps");

		// ─────────────────────────────────────────────
		// JSON SETTINGS + DATA STRUCTURES (unchanged)
		// ─────────────────────────────────────────────

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

		// ─────────────────────────────────────────────
		// SERIALIZATION ONLY — NO FILE ACCESS
		// ─────────────────────────────────────────────

		public static DatabaseData DeserializeDatabase(string json)
		{
			if (string.IsNullOrEmpty(json)) return null;

			try
			{
				var root = JObject.Parse(json);
				var serializer = JsonSerializer.CreateDefault(Minified);

				return new DatabaseData
				{
					maps = root["maps"]?.ToObject<Map[]>(serializer) ?? System.Array.Empty<Map>(),
					definitions = root["definitions"]?.ToObject<Definition[]>(serializer) ?? System.Array.Empty<Definition>(),
					textures = root["textures"]?.ToObject<TextureSequence[]>(serializer) ?? System.Array.Empty<TextureSequence>(),
					buttons = root["buttons"]?.ToObject<Button[]>(serializer) ?? System.Array.Empty<Button>()
				};
			}
			catch
			{
				return null;
			}
		}

		public static string SerializeDatabase(DatabaseData data, bool pretty = false)
			=> JsonConvert.SerializeObject(data, pretty ? Pretty : Minified);

		public static Map DeserializeMap(string json)
			=> string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Map>(json, Minified);

		public static string SerializeMap(Map map, bool pretty = false)
			=> JsonConvert.SerializeObject(map, pretty ? Pretty : Minified);

		public static string SerializeAtomic(AtomicMap atomic, bool pretty = true)
			=> JsonConvert.SerializeObject(atomic, pretty ? Pretty : Minified);

		// ─────────────────────────────────────────────
		// FILE OPERATIONS
		// ─────────────────────────────────────────────

		public static TextAsset GetMutableDatabaseTextAsset(TextAsset pristine)
		{
			ResourceFileIO.EnsureFolder(DatabaseFolder);

			string fileName = pristine.name.EndsWith(".json")
				? pristine.name : pristine.name + ".json";

			string path = System.IO.Path.Combine(DatabaseFolder, fileName);

			if (!ResourceFileIO.FileExists(path))
			{
				ResourceFileIO.WriteText(path, pristine.text);
				Debug.Log($"ResourceSerializer: Created mutable DB → {path}");
			}

			string contents = ResourceFileIO.ReadText(path);
			return new TextAsset(contents) { name = pristine.name };
		}

		public static void OverwriteMutableDatabaseWithPristine(TextAsset pristine)
		{
			ResourceFileIO.EnsureFolder(DatabaseFolder);

			string fileName = pristine.name.EndsWith(".json")
				? pristine.name : pristine.name + ".json";

			string path = System.IO.Path.Combine(DatabaseFolder, fileName);

			ResourceFileIO.WriteText(path, pristine.text);
		}

		public static void SaveDatabase(DatabaseData data)
		{
			ResourceFileIO.EnsureFolder(DatabaseFolder);
			string json = SerializeDatabase(data, false);

			string path = System.IO.Path.Combine(DatabaseFolder, "database.json");
			ResourceFileIO.WriteText(path, json);
		}

		public static Map[] LoadIndividualMaps()
		{
			var files = ResourceFileIO.GetFiles(IndividualMapsFolder, "*.json");
			return files
				.Select(f => DeserializeMap(ResourceFileIO.ReadText(f)))
				.Where(m => m != null)
				.ToArray();
		}

		public static bool SaveIndividualMap(Map map)
		{
			if (map == null || string.IsNullOrEmpty(map.name)) return false;

			ResourceFileIO.EnsureFolder(IndividualMapsFolder);

			string safeName = string.Join("_", map.name.Split(System.IO.Path.GetInvalidFileNameChars()));
			string path = System.IO.Path.Combine(IndividualMapsFolder, safeName + ".json");

			string json = SerializeMap(map, false);
			ResourceFileIO.WriteText(path, json);
			return true;
		}

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

			ResourceFileIO.EnsureFolder(ExportFolder);
			string path = overridePath ?? System.IO.Path.Combine(ExportFolder, $"{map.name}.json");

			ResourceFileIO.WriteText(path, json);
		}
	}
}
