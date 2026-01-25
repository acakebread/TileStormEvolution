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

		// The virtual Order position where "table" should appear in JSON
		// (matches the old [JsonProperty(Order = 20)] on the removed table field)
		private const int TableJsonOrderPosition = 20;

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
				.ThenBy(p => p.PropertyName);
		}

		protected HashId[] ParseTableToHashes(JArray tableArray)
		{
			if (tableArray == null) return Array.Empty<HashId>();

			var hashes = new HashId[tableArray.Count];

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

			var tableArray = jo["table"] as JArray;
			jo.Remove("table");

			serializer.Populate(jo.CreateReader(), map);

			if (tableArray != null)
			{
				((Map.IHashAccess)map).Hashes = ParseTableToHashes(tableArray);
			}

			return map;
		}

		public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
			=> ReadMapJson(reader, serializer);

		protected virtual void WriteTableArray(JsonWriter writer, Map map, JsonSerializer serializer)
		{
			writer.WritePropertyName("table");
			writer.WriteStartArray();

			var hashes = ((Map.IHashAccess)map).Hashes ?? Array.Empty<HashId>();

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

			bool tableWritten = false;

			foreach (var prop in OrderedProperties(serializer))
			{
				var name = prop.PropertyName;

				if (!IsAtomic && IsSuppressedInDatabaseFormat(name))
					continue;

				var propValue = prop.ValueProvider?.GetValue(map);

				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(name);
				serializer.Serialize(writer, propValue);

				// After any real property with Order < TableJsonOrderPosition,
				// check if next is >= TableJsonOrderPosition (or end) → insert table
				if (!tableWritten && prop.Order.GetValueOrDefault(int.MaxValue) < TableJsonOrderPosition)
				{
					var remaining = OrderedProperties(serializer).SkipWhile(p => p.PropertyName != name).Skip(1);
					var nextProp = remaining.FirstOrDefault();

					if (nextProp == null || nextProp.Order.GetValueOrDefault(int.MaxValue) >= TableJsonOrderPosition)
					{
						WriteTableArray(writer, map, serializer);
						tableWritten = true;
					}
				}
			}

			// Fallback if table wasn't inserted (very rare)
			if (!tableWritten)
			{
				WriteTableArray(writer, map, serializer);
			}

			if (IsAtomic)
				WriteAtomicOnlyFields(writer, map, serializer);

			writer.WriteEndObject();
		}

		private void WriteAtomicOnlyFields(JsonWriter writer, Map map, JsonSerializer serializer)
		{
			var usedHashes = (((Map.IHashAccess)map).Hashes ?? Array.Empty<HashId>())
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

	public class AtomicMapConverter : MapConverterBase { public AtomicMapConverter() : base(true) { } }

	public class DatabaseMapConverter : MapConverterBase { public DatabaseMapConverter() : base(false) { } }
}