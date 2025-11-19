// ResourceSerializer.cs — FINAL UNIFIED, PURE SERIALIZER (NO I/O!)
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ClassicTilestorm
{
	public static class ResourceSerializer
	{
		private static readonly JsonSerializerSettings Settings = new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			MissingMemberHandling = MissingMemberHandling.Ignore
		};

		private static readonly JsonSerializerSettings PrettySettings = new()
		{
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Formatting.Indented
		};

		// ─────── Database (full) ───────
		public static DatabaseSerializer.DatabaseData DeserializeDatabase(string json)
		{
			if (string.IsNullOrEmpty(json)) return null;

			try
			{
				var root = JObject.Parse(json);
				var serializer = JsonSerializer.CreateDefault(Settings);

				var data = new DatabaseSerializer.DatabaseData
				{
					maps = root["maps"]?.ToObject<Map[]>(serializer) ?? Array.Empty<Map>(),
					definitions = root["definitions"]?.ToObject<Definition[]>(serializer) ?? Array.Empty<Definition>(),
					textures = root["textures"]?.ToObject<TextureSequence[]>(serializer) ?? Array.Empty<TextureSequence>(),
					buttons = root["buttons"]?.ToObject<Button[]>(serializer) ?? Array.Empty<Button>()
				};

				if (data.maps == null || data.definitions == null || data.textures == null ||
					data.maps.Length == 0 || data.definitions.Length == 0 || data.textures.Length == 0)
				{
					Debug.LogError("ResourceSerializer: Database failed validation (missing required content)");
					return null;
				}

				return data;
			}
			catch (Exception ex)
			{
				Debug.LogError($"ResourceSerializer: Failed to deserialize database → {ex.Message}");
				return null;
			}
		}

		public static string SerializeDatabase(DatabaseSerializer.DatabaseData data, bool pretty = false)
			=> JsonConvert.SerializeObject(data, pretty ? PrettySettings : Settings);

		// ─────── Individual Map ───────
		public static Map DeserializeMap(string json)
			=> string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Map>(json, Settings);

		public static string SerializeMap(Map map, bool pretty = false)
			=> JsonConvert.SerializeObject(map, pretty ? PrettySettings : Settings);

		// ─────── Atomic Export (Map + Dependencies) ───────
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

		public static string SerializeAtomic(Map map, Definition[] defs, TextureSequence[] textures, bool pretty = true)
			=> JsonConvert.SerializeObject(new AtomicMap { map = map, definitions = defs, textures = textures }, pretty ? PrettySettings : Settings);
	}
}