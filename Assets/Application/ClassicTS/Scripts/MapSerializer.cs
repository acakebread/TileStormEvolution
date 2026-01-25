using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public abstract class MapConverterBase : JsonConverter
	{
		protected readonly bool IsAtomic;

		protected MapConverterBase(bool isAtomic)
		{
			IsAtomic = isAtomic;
		}

		public override bool CanConvert(Type objectType)
			=> typeof(Map).IsAssignableFrom(objectType);

		protected static IEnumerable<JsonProperty> OrderedProperties(JsonSerializer serializer)
		{
			var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(typeof(Map));
			return contract.Properties
				.Where(p => !p.Ignored)
				.OrderBy(p => p.Order ?? int.MaxValue);
		}

		protected int[] ParseTableToHashes(JArray tableArray)
		{
			if (tableArray == null) return Array.Empty<int>();

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

				if (entry.StartsWith("[", StringComparison.Ordinal))
				{
					int close = entry.IndexOf(']', 1);
					if (close > 1)
						hashStr = entry.Substring(1, close - 1).Trim();
				}

				if (string.IsNullOrEmpty(hashStr) ||
					hashStr.Equals("unknown", StringComparison.OrdinalIgnoreCase))
				{
					hashes[i] = 0;
					continue;
				}

				try
				{
					hashes[i] = HTB50.Decode(hashStr);
				}
				catch
				{
					hashes[i] = 0;
				}
			}

			return hashes;
		}

		protected Map ReadMapJson(JsonReader reader, JsonSerializer serializer)
		{
			var jo = JObject.Load(reader);
			var map = new Map();

			JArray tableArray = jo["table"] as JArray;
			jo.Remove("table");

			serializer.Populate(jo.CreateReader(), map);

			if (tableArray != null)
			{
				map.hashes = ParseTableToHashes(tableArray);
				map.table = null;
			}

			return map;
		}

		public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
		{
			return ReadMapJson(reader, serializer);
		}

		// ─────────────────────────────────────────────
		// Now implemented in base — uses IsAtomic to decide format
		// ─────────────────────────────────────────────
		protected virtual void WriteTableArray(JsonWriter writer, Map map, JsonSerializer serializer)
		{
			writer.WritePropertyName("table");
			writer.WriteStartArray();

			var hashes = map.hashes ?? Array.Empty<int>();

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

				if (IsAtomic)
				{
					// Atomic format: [hash]Name
					var def = ResourceManager.GetDefinition(hash);
					string namePart = (def != null && !string.IsNullOrEmpty(def.name))
						? def.name
						: "unknown";

					writer.WriteValue($"[{hashStr}]{namePart}");
				}
				else
				{
					// Database format: [hash]
					writer.WriteValue($"[{hashStr}]");
				}
			}

			writer.WriteEndArray();
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value ?? throw new ArgumentNullException(nameof(value));

			writer.WriteStartObject();

			foreach (var prop in OrderedProperties(serializer))
			{
				if (prop.PropertyName == "table")
				{
					WriteTableArray(writer, map, serializer);
					continue;
				}

				// Skip fields only present in atomic format when writing database format
				if (!IsAtomic && IsSuppressedInDatabaseFormat(prop.PropertyName))
					continue;

				var propValue = prop.ValueProvider.GetValue(map);

				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(prop.PropertyName);
				serializer.Serialize(writer, propValue);
			}

			writer.WriteEndObject();
		}

		private static bool IsSuppressedInDatabaseFormat(string propertyName)
		{
			return propertyName switch
			{
				"definitions" or "textures" or "version" or "author" or "exportedFrom" => true,
				_ => false
			};
		}
	}

	// ─────────────────────────────────────────────
	// Atomic variant — almost empty now
	// ─────────────────────────────────────────────
	public class AtomicMapConverter : MapConverterBase
	{
		public AtomicMapConverter() : base(isAtomic: true) { }

		// If you ever need atomic-specific table tweaks, override here:
		// protected override void WriteTableArray(...) { ... }
	}

	// ─────────────────────────────────────────────
	// Database variant — also very thin
	// ─────────────────────────────────────────────
	public class DatabaseMapConverter : MapConverterBase
	{
		public DatabaseMapConverter() : base(isAtomic: false) { }

		// Override only if database needs different table style in future
	}
}