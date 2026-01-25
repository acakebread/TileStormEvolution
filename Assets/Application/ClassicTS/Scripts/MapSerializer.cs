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
				.OrderBy(p => p.Order ?? int.MaxValue)
				.ThenBy(p => p.PropertyName);   // stable sort when Order is equal
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
					var def = ResourceManager.GetDefinition(hash);
					string namePart = (def != null && !string.IsNullOrEmpty(def.name))
						? def.name
						: "unknown";

					writer.WriteValue($"[{hashStr}]{namePart}");
				}
				else
				{
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
					// Write table exactly when its Order value says so
					WriteTableArray(writer, map, serializer);
					continue;
				}

				if (!IsAtomic && IsSuppressedInDatabaseFormat(prop.PropertyName))
					continue;

				var propValue = prop.ValueProvider?.GetValue(map);

				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(prop.PropertyName);
				serializer.Serialize(writer, propValue);
			}

			// Only atomic format gets these extra fields at the end
			if (IsAtomic)
			{
				WriteAtomicOnlyFields(writer, map, serializer);
			}

			writer.WriteEndObject();
		}

		private void WriteAtomicOnlyFields(JsonWriter writer, Map map, JsonSerializer serializer)
		{
			var usedHashes = (map.hashes ?? Array.Empty<int>())
				.Where(h => h != 0)
				.Distinct()
				.ToArray();

			var usedDefs = usedHashes
				.Select(h => ResourceManager.GetDefinition(h))
				.Where(d => d != null)
				.ToArray();

			if (usedDefs.Length > 0)
			{
				writer.WritePropertyName("definitions");
				serializer.Serialize(writer, usedDefs);
			}

			var usedBanks = usedDefs
				.Where(d => !string.IsNullOrEmpty(d?.texture))
				.Select(d => d.texture)
				.Distinct()
				.ToArray();

			var usedTextures = ResourceManager.TextureSequences
				.Where(ts => usedBanks.Contains(ts.id))
				.ToArray();

			if (usedTextures.Length > 0)
			{
				writer.WritePropertyName("textures");
				serializer.Serialize(writer, usedTextures);
			}

			writer.WritePropertyName("version");
			writer.WriteValue("1.0");

			writer.WritePropertyName("author");
			writer.WriteValue("Player");

			writer.WritePropertyName("exportedFrom");
			writer.WriteValue("ClassicTilestorm");
		}

		private static bool IsSuppressedInDatabaseFormat(string propertyName)
		{
			return propertyName is "definitions" or "textures" or "version" or "author" or "exportedFrom";
		}
	}

	public class AtomicMapConverter : MapConverterBase
	{
		public AtomicMapConverter() : base(true) { }
	}

	public class DatabaseMapConverter : MapConverterBase
	{
		public DatabaseMapConverter() : base(false) { }
	}
}