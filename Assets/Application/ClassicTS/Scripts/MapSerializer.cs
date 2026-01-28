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

		protected Variant[] ParseTableToVariants(JArray tableArray)
		{
			if (tableArray == null) return Array.Empty<Variant>();

			var variants = new Variant[tableArray.Count];

			for (int i = 0; i < tableArray.Count; i++)
			{
				string entry = tableArray[i]?.Value<string>()?.Trim() ?? "";

				if (string.IsNullOrEmpty(entry) || entry.Equals("unknown", StringComparison.OrdinalIgnoreCase))
				{
					variants[i] = new Variant(0);
					continue;
				}

				// ─────────────────────────────────────────────────────────────
				// Step 1: Separate machine-readable part from optional #comment/name
				// ─────────────────────────────────────────────────────────────
				string machinePart;
				int hashPos = entry.IndexOf('#');

				if (hashPos >= 0)
				{
					// New format: everything after # is ignored (name / comment)
					machinePart = entry.Substring(0, hashPos).TrimEnd();
				}
				else
				{
					// No # separator found → use whole string (old files or no name)
					machinePart = entry;

					// Optional fallback support for very old bracketed format
					// (you can remove this block once all files are migrated)
					if (machinePart.StartsWith("[") && machinePart.Contains("]"))
					{
						int close = machinePart.IndexOf(']', 1);
						if (close > 1)
						{
							machinePart = machinePart.Substring(1, close - 1).Trim(); // strip [ and ]
						}
					}
				}

				// ─────────────────────────────────────────────────────────────
				// Step 2: Parse the machine-readable content
				// ─────────────────────────────────────────────────────────────
				if (string.IsNullOrWhiteSpace(machinePart))
				{
					variants[i] = new Variant(0);
					continue;
				}

				var parts = machinePart.Split('|')
									   .Select(p => p.Trim())
									   .Where(p => !string.IsNullOrEmpty(p))
									   .ToArray();

				if (parts.Length == 0)
				{
					variants[i] = new Variant(0);
					continue;
				}

				// First part must be the hash
				string hashStr = parts[0];
				HashId hash = 0;
				try
				{
					hash = HTB50.Decode(hashStr);
				}
				catch
				{
					hash = 0; // invalid hash → treat as empty variant
				}

				var variant = new Variant(hash);

				// Parse known parameters (angle, delta) – ignore unknown keys
				for (int p = 1; p < parts.Length; p++)
				{
					var kv = parts[p].Split(new[] { ':' }, 2);
					if (kv.Length != 2) continue;

					string key = kv[0].Trim().ToLowerInvariant();
					string val = kv[1].Trim();

					if (key == "angle")
					{
						if (float.TryParse(val, System.Globalization.NumberStyles.Any,
										   System.Globalization.CultureInfo.InvariantCulture, out float ang))
						{
							variant.angle = ang;
						}
					}
					else if (key == "delta")
					{
						if (float.TryParse(val, System.Globalization.NumberStyles.Any,
										   System.Globalization.CultureInfo.InvariantCulture, out float del))
						{
							variant.delta = del;
						}
					}
					// Unknown keys are silently ignored → future-proof
				}

				variants[i] = variant;
			}

			return variants;
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
				((Map.IVariantAccess)map).Variants = ParseTableToVariants(tableArray);
			}

			return map;
		}

		public override object ReadJson(JsonReader reader, Type type, object existingValue, JsonSerializer serializer)
			=> ReadMapJson(reader, serializer);

		protected virtual void WriteTableArray(JsonWriter writer, Map map, JsonSerializer serializer)
		{
			writer.WritePropertyName("table");
			writer.WriteStartArray();

			var variants = ((Map.IVariantAccess)map).Variants ?? Array.Empty<Variant>();

			foreach (var v in variants)
			{
				if (v.hash == 0)
				{
					writer.WriteValue("unknown");
					continue;
				}

				string hashStr = HTB50.EncodeFixed(
					v.hash,
					length: HTB50Settings.FixedLength,
					padChar: '0',
					appendFlavor: false
				);

				var parts = new List<string> { hashStr };

				if (Math.Abs(v.angle) > 0.001f)
					parts.Add($"angle:{v.angle:F1}");

				if (Math.Abs(v.delta) > 0.001f)
					parts.Add($"delta:{v.delta:F3}");

				string content = string.Join("|", parts);   // renamed from inner for clarity

				string finalValue;

				if (IsAtomic)
				{
					var def = ResourceManager.GetDefinition(v.hash);
					string name = (def != null && !string.IsNullOrEmpty(def.name))
						? def.name
						: "unknown";

					finalValue = $"{content}#{name}";
				}
				else
				{
					finalValue = content;                   // no name in database mode
				}

				writer.WriteValue(finalValue);
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

				if (name == "hashes" || name == "variants")
					continue;

				if (!IsAtomic && IsSuppressedInDatabaseFormat(name))
					continue;

				var propValue = prop.ValueProvider?.GetValue(map);

				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(name);
				serializer.Serialize(writer, propValue);

				if (!tableWritten && prop.Order.GetValueOrDefault(int.MaxValue) < TableJsonOrderPosition)
				{
					var remaining = OrderedProperties(serializer)
						.SkipWhile(p => p.PropertyName != name)
						.Skip(1);

					var nextProp = remaining.FirstOrDefault();

					if (nextProp == null || nextProp.Order.GetValueOrDefault(int.MaxValue) >= TableJsonOrderPosition)
					{
						WriteTableArray(writer, map, serializer);
						tableWritten = true;
					}
				}
			}

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
			var usedHashes = (((Map.IVariantAccess)map).Variants ?? Array.Empty<Variant>())
				.Where(v => v.hash != 0)
				.Select(v => v.hash)
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
