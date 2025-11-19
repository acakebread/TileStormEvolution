using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	public static class ResourceSerializer
	{
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

		// ─────── Full Database ───────
		[System.Serializable]
		public class DatabaseData
		{
			public Map[] maps;
			public Definition[] definitions;
			public TextureSequence[] textures;
			public Button[] buttons;
		}

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
					Debug.LogError("ResourceSerializer: Database failed validation — missing required content");
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

		// ─────── Individual Map ───────
		public static Map DeserializeMap(string json)
			=> string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Map>(json, Minified);

		public static string SerializeMap(Map map, bool pretty = false)
			=> JsonConvert.SerializeObject(map, pretty ? Pretty : Minified);

		// ─────── Atomic Export ───────
		[System.Serializable]
		public class AtomicMap
		{
			public Map map;
			public Definition[] definitions;
			public TextureSequence[] textures;
			public string version = "1.0";
			public string author = "Player";
			public string exportedFrom = "ClassicTilestorm";
		}

		public static string SerializeAtomic(Map map, Definition[] defs, TextureSequence[] tex, bool pretty = true)
			=> JsonConvert.SerializeObject(new AtomicMap { map = map, definitions = defs, textures = tex }, pretty ? Pretty : Minified);
	}
}