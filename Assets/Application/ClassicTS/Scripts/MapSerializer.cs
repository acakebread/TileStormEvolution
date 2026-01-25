using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using MassiveHadronLtd.IDs.HTB50;
using static ClassicTilestorm.ResourceManager;

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

		// Keep ParseTable to strip brackets on load
		protected void ParseTable(Map map, JArray tableArray)
		{
			map.table = new string[tableArray.Count];

			for (int i = 0; i < tableArray.Count; i++)
			{
				string entry = tableArray[i].Value<string>()?.Trim();

				if (string.IsNullOrEmpty(entry))
				{
					map.table[i] = "";
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
						hashValue = entry; // malformed → keep whole
					}
				}
				else
				{
					hashValue = entry; // bare hash or legacy name
				}

				map.table[i] = hashValue;
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
				var propValue = prop.ValueProvider.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(prop.PropertyName);

				if (prop.PropertyName == "table")
				{
					writer.WriteStartArray();

					var hashes = map.TableHashes ?? Array.Empty<int>();

					foreach (int hash in hashes)
					{
						if (hash == 0)
						{
							writer.WriteValue("unknown");
							continue;
						}

						// Get the definition to fetch the legacy name
						var def = ResourceManager.GetDefinition(hash);
						string namePart = (def != null && !string.IsNullOrEmpty(def.id))
							? def.id
							: "unknown";

						// Re-encode the int hash to clean base50 string (positive, fixed length)
						string hashStr = HTB50.EncodeFixed(
							hash,
							length: HTB50Settings.FixedLength,
							padChar: '0',
							appendFlavor: false
						);

						// Final format: "[base50]name"
						string entry = $"[{hashStr}]{namePart}";

						writer.WriteValue(entry);
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
			"definitions", "textures", "version", "author", "exportedFrom"
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

				var propValue = prop.ValueProvider.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(prop.PropertyName);

				if (prop.PropertyName == "table")
				{
					writer.WriteStartArray();

					var hashes = map.TableHashes ?? Array.Empty<int>();

					foreach (int hash in hashes)
					{
						if (hash == 0)
						{
							writer.WriteValue("unknown");
							continue;
						}

						// Re-encode the int hash back to clean base50 string
						string hashStr = HTB50.EncodeFixed(
							hash,
							length: HTB50Settings.FixedLength,
							padChar: '0',
							appendFlavor: false
						);

						// Output only the hash wrapped in [] — no name appended
						string entry = $"[{hashStr}]";

						writer.WriteValue(entry);
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