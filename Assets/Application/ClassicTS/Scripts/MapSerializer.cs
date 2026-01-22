using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ClassicTilestorm
{
	public abstract class MapConverterBase : JsonConverter
	{
		public override bool CanConvert(Type objectType)
			=> typeof(Map).IsAssignableFrom(objectType);

		protected static IEnumerable<JsonProperty> OrderedProperties(JsonSerializer serializer)
		{
			var contract = (JsonObjectContract)
				serializer.ContractResolver.ResolveContract(typeof(Map));

			return contract.Properties
				.Where(p => !p.Ignored)
				.OrderBy(p => p.Order ?? int.MaxValue);
		}

		protected void ParseTable(Map map, JArray tableArray)
		{
			map._tileEntries.Clear();

			foreach (JToken token in tableArray)
			{
				string entry = token.Value<string>()?.Trim();

				if (string.IsNullOrEmpty(entry))
				{
					map._tileEntries.Add(new Map.TileEntry(""));
					continue;
				}

				string hashValue;

				if (entry.StartsWith("[", StringComparison.Ordinal))
				{
					int close = entry.IndexOf(']', 1);
					if (close > 1)
					{
						hashValue = entry.Substring(1, close - 1).Trim();
					}
					else
					{
						hashValue = entry; // malformed → treat as hash
					}
				}
				else
				{
					// Bare hash (from previous save) or legacy name (will be migrated)
					hashValue = entry;
				}

				map._tileEntries.Add(new Map.TileEntry(hashValue));
			}
		}
	}

	// ─────────────────────────────────────────────
	// ATOMIC MAP CONVERTER
	// ─────────────────────────────────────────────
	public class AtomicMapConverter : MapConverterBase
	{
		public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
		{
			var jo = JObject.Load(reader);
			var map = new Map();

			serializer.Populate(jo.CreateReader(), map);

			if (jo["table"] is JArray table)
				ParseTable(map, table);

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;

			writer.WriteStartObject();

			foreach (var prop in OrderedProperties(serializer))
			{
				if (prop.UnderlyingName == nameof(Map._tileEntries)) continue;

				var propValue = prop.ValueProvider.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(prop.PropertyName);

				if (prop.PropertyName == "table")
				{
					writer.WriteStartArray();

					// Write from table (hashes) + lookup name for readability
					foreach (string hash in map.table ?? Array.Empty<string>())
					{
						if (string.IsNullOrEmpty(hash))
						{
							writer.WriteValue("unknown");
							continue;
						}

						var def = ResourceManager.GetDefinition(hash); // prefers hash
						string name = def?.id ?? hash; // fallback to hash if no def
						string valueToWrite = $"[{hash}]{name}";

						writer.WriteValue(valueToWrite);
					}

					writer.WriteEndArray();
				}
				else
				{
					serializer.Serialize(writer, propValue);
				}
			}

			writer.WriteEndObject();
		}
	}

	// ─────────────────────────────────────────────
	// DATABASE MAP CONVERTER
	// ─────────────────────────────────────────────
	public class DatabaseMapConverter : MapConverterBase
	{
		static readonly HashSet<string> SuppressedAtomicFields = new()
		{
			"definitions",
			"textures",
			"version",
			"author",
			"exportedFrom"
		};

		public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
		{
			var jo = JObject.Load(reader);
			var map = new Map();

			serializer.Populate(jo.CreateReader(), map);

			if (jo["table"] is JArray table)
				ParseTable(map, table);

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;

			writer.WriteStartObject();

			foreach (var prop in OrderedProperties(serializer))
			{
				if (SuppressedAtomicFields.Contains(prop.PropertyName)) continue;
				if (prop.UnderlyingName == nameof(Map._tileEntries)) continue;

				var propValue = prop.ValueProvider.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(prop.PropertyName);

				if (prop.PropertyName == "table")
				{
					writer.WriteStartArray();

					// Write from table (hashes) → [hash] only
					foreach (string hash in map.table ?? Array.Empty<string>())
					{
						if (string.IsNullOrEmpty(hash))
						{
							writer.WriteValue("unknown");
						}
						else
						{
							writer.WriteValue($"[{hash}]");
						}
					}

					writer.WriteEndArray();
				}
				else
				{
					serializer.Serialize(writer, propValue);
				}
			}

			writer.WriteEndObject();
		}
	}
}
