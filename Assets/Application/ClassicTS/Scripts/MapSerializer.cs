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
			var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(typeof(Map));
			return contract.Properties
				.Where(p => !p.Ignored)
				.OrderBy(p => p.Order ?? int.MaxValue);
		}

		// ─────────────────────────────────────────────
		// NEW: Parse JSON table → int[] TableHashes
		// ─────────────────────────────────────────────
		protected int[] ParseTableToHashes(JArray tableArray)
		{
			var hashes = new int[tableArray.Count];

			for (int i = 0; i < tableArray.Count; i++)
			{
				string entry = tableArray[i]?.Value<string>()?.Trim() ?? "";

				if (string.IsNullOrEmpty(entry))
				{
					hashes[i] = 0;
					continue;
				}

				string hashStr = entry;

				// Strip [ ] if present
				if (entry.StartsWith("[", StringComparison.Ordinal))
				{
					int close = entry.IndexOf(']', 1);
					if (close > 1)
						hashStr = entry.Substring(1, close - 1).Trim();
				}

				if (string.IsNullOrEmpty(hashStr) || hashStr.Equals("unknown", StringComparison.OrdinalIgnoreCase))
				{
					hashes[i] = 0;
					continue;
				}

				// Decode base50 → int
				try
				{
					hashes[i] = HTB50.Decode(hashStr);   // assuming Decode exists; use DecodeFixed if needed
				}
				catch
				{
					hashes[i] = 0; // malformed hash → treat as unknown (0)
				}
			}

			return hashes;
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

			// Extract and remove "table" so Populate doesn't set the string[] version
			JArray tableArray = jo["table"] as JArray;
			jo.Remove("table");

			serializer.Populate(jo.CreateReader(), map);

			if (tableArray != null)
			{
				map.TableHashes = ParseTableToHashes(tableArray);
				map.table = null;                    // ← clean state
			}

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;

			writer.WriteStartObject();

			foreach (var prop in OrderedProperties(serializer))
			{
				if (prop.PropertyName == "table")
				{
					writer.WritePropertyName("table");
					writer.WriteStartArray();

					var hashes = map.TableHashes ?? Array.Empty<int>();

					foreach (int hash in hashes)
					{
						if (hash == 0)
						{
							writer.WriteValue("unknown");
							continue;
						}

						var def = ResourceManager.GetDefinition(hash);
						string namePart = (def != null && !string.IsNullOrEmpty(def.id)) ? def.id : "unknown";

						string hashStr = HTB50.EncodeFixed(
							hash,
							length: HTB50Settings.FixedLength,
							padChar: '0',
							appendFlavor: false
						);

						writer.WriteValue($"[{hashStr}]{namePart}");
					}

					writer.WriteEndArray();
				}
				else
				{
					var propValue = prop.ValueProvider.GetValue(map);
					if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
						continue;

					writer.WritePropertyName(prop.PropertyName);
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

			JArray tableArray = jo["table"] as JArray;
			jo.Remove("table");

			serializer.Populate(jo.CreateReader(), map);

			if (tableArray != null)
			{
				map.TableHashes = ParseTableToHashes(tableArray);
				map.table = null;                    // ← clean state
			}

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;

			writer.WriteStartObject();

			foreach (var prop in OrderedProperties(serializer))
			{
				if (SuppressedAtomicFields.Contains(prop.PropertyName)) continue;

				if (prop.PropertyName == "table")
				{
					writer.WritePropertyName("table");
					writer.WriteStartArray();

					var hashes = map.TableHashes ?? Array.Empty<int>();

					foreach (int hash in hashes)
					{
						if (hash == 0)
						{
							writer.WriteValue("unknown");
							continue;
						}

						string hashStr = HTB50.EncodeFixed(
							hash,
							length: HTB50Settings.FixedLength,
							padChar: '0',
							appendFlavor: false
						);

						writer.WriteValue($"[{hashStr}]");
					}

					writer.WriteEndArray();
				}
				else
				{
					var propValue = prop.ValueProvider.GetValue(map);
					if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
						continue;

					writer.WritePropertyName(prop.PropertyName);
					serializer.Serialize(writer, propValue);
				}
			}

			writer.WriteEndObject();
		}
	}
}