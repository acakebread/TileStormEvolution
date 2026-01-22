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

		//protected void ParseTableOld(Map map, JArray tableArray)
		//{
		//	map._tileEntries.Clear();

		//	foreach (JToken token in tableArray)
		//	{
		//		string entry = token.Value<string>()?.Trim() ?? "tile_empty";
		//		string stableId = null;
		//		string displayName = entry;

		//		if (entry.StartsWith("[", StringComparison.Ordinal))
		//		{
		//			int close = entry.IndexOf(']', 1);
		//			if (close > 1)
		//			{
		//				string hash = entry.Substring(1, close - 1).Trim();
		//				string rest = entry.Substring(close + 1).Trim();

		//				stableId = hash;
		//				displayName = string.IsNullOrEmpty(rest) ? "PENDING_ID" : rest;
		//			}
		//		}

		//		map._tileEntries.Add(new Map.TileEntry(displayName, stableId));
		//	}
		//}

		protected void ParseTable(Map map, JArray tableArray)
		{
			map._tileEntries.Clear();

			foreach (JToken token in tableArray)
			{
				string entry = token.Value<string>()?.Trim();

				string stableId = null;
				string displayName = entry; // ← can be null or ""

				// Modern format: [HASH]Name or [HASH]
				if (!string.IsNullOrEmpty(entry) && entry.StartsWith("[", StringComparison.Ordinal))
				{
					int close = entry.IndexOf(']', 1);
					if (close > 1)
					{
						string hashPart = entry.Substring(1, close - 1).Trim();
						string namePart = entry.Substring(close + 1).Trim();

						stableId = hashPart;
						displayName = string.IsNullOrEmpty(namePart) ? null : namePart;
					}
					else
					{
						// malformed → keep as-is
						displayName = entry;
					}
				}

				// Legacy entries are kept exactly as they are (including null / "" / "tile_empty")
				map._tileEntries.Add(new Map.TileEntry(displayName, stableId));
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
					foreach (var e in map._tileEntries)
					{
						writer.WriteValue(
							!string.IsNullOrEmpty(e.StableId)
								? $"[{e.StableId}]{e.DisplayName}"
								: e.DisplayName);
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
					foreach (var e in map._tileEntries)
					{
						writer.WriteValue(
							!string.IsNullOrEmpty(e.StableId)
								? $"[{e.StableId}]"
								: e.DisplayName);
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
