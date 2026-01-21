using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ClassicTilestorm
{
	public abstract class MapConverterBase : JsonConverter
	{
		public override bool CanConvert(Type objectType) => typeof(Map).IsAssignableFrom(objectType);

		protected void ParseTable(Map map, JArray tableArray)
		{
			map._tileEntries.Clear();

			foreach (JToken token in tableArray)
			{
				string entry = token.Value<string>()?.Trim() ?? "tile_empty";
				string stableId = null;
				string displayName = entry;  // default to full entry

				if (entry.StartsWith("[", StringComparison.Ordinal))
				{
					int close = entry.IndexOf(']', 1);
					if (close > 1)  // found a closing bracket
					{
						string hashAndOptions = entry.Substring(1, close - 1).Trim();
						string namePart = (close + 1 < entry.Length) ? entry.Substring(close + 1).Trim() : "";

						// Take hash as first part before any comma (for future overrides)
						string hashPart = hashAndOptions.Split(',')[0].Trim();

						if (!string.IsNullOrEmpty(hashPart))
						{
							stableId = hashPart;
							displayName = string.IsNullOrWhiteSpace(namePart) ? "PENDING_ID" : namePart;
						}
					}
				}

				map._tileEntries.Add(new Map.TileEntry(displayName, stableId));
			}
		}

		protected virtual string ProcessRest(string rest)
		{
			return string.IsNullOrWhiteSpace(rest) ? "tile_empty" : rest;
		}

		protected abstract string GetOutput(Map.TileEntry entry, bool enriched);
	}

	public class AtomicMapConverter : MapConverterBase
	{
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			var jo = JObject.Load(reader);
			var map = (Map)(existingValue ?? Activator.CreateInstance(objectType));

			serializer.Populate(jo.CreateReader(), map);

			if (jo["table"] is JArray tableArray && tableArray.Count > 0)
			{
				ParseTable(map, tableArray);
			}

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;
			writer.WriteStartObject();

			var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(typeof(Map));
			foreach (var prop in contract.Properties)
			{
				if (prop.Ignored) continue;

				// Skip the backing field
				if (prop.UnderlyingName == nameof(Map._tileEntries)) continue;

				var propValue = prop.ValueProvider?.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore) continue;

				writer.WritePropertyName(prop.PropertyName ?? prop.UnderlyingName);

				if (prop.PropertyName == "table")
				{
					writer.WriteStartArray();

					foreach (var entry in map._tileEntries)
					{
						string output = GetOutput(entry, true); // always enriched for atomic
						writer.WriteValue(output);
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

		protected override string GetOutput(Map.TileEntry entry, bool enriched)
		{
			// Atomic mode: ALWAYS include name if StableId exists
			if (!string.IsNullOrEmpty(entry.StableId))
			{
				return $"[{entry.StableId}]{entry.DisplayName}";
			}

			return entry.DisplayName;
		}
	}

	public class DatabaseMapConverter : MapConverterBase
	{
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			var jo = JObject.Load(reader);
			var map = (Map)(existingValue ?? Activator.CreateInstance(objectType));

			serializer.Populate(jo.CreateReader(), map);

			if (jo["table"] is JArray tableArray && tableArray.Count > 0)
			{
				ParseTable(map, tableArray);
			}

			return map;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value;
			writer.WriteStartObject();

			var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(typeof(Map));
			foreach (var prop in contract.Properties)
			{
				if (prop.Ignored) continue;

				// Skip the backing field
				if (prop.UnderlyingName == nameof(Map._tileEntries)) continue;

				var propValue = prop.ValueProvider?.GetValue(map);
				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore) continue;

				writer.WritePropertyName(prop.PropertyName ?? prop.UnderlyingName);

				if (prop.PropertyName == "table")
				{
					writer.WriteStartArray();

					foreach (var entry in map._tileEntries)
					{
						string output = GetOutput(entry, false); // database mode: [hash] only
						writer.WriteValue(output);
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

		protected override string GetOutput(Map.TileEntry entry, bool enriched)
		{
			return !string.IsNullOrEmpty(entry.StableId)
				? $"[{entry.StableId}]"
				: entry.DisplayName;
		}

		protected override string ProcessRest(string rest)
		{
			return "tile_empty"; // no name in database mode
		}
	}
}