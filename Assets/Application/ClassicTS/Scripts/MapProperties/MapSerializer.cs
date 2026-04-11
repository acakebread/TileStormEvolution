using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public abstract class MapSerializer : JsonConverter
	{
		protected readonly bool IsAtomic;

		private const int TableJsonOrderPosition = 20;

		protected MapSerializer(bool isAtomic)
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

		private static int[] DecodeTilesSafely(int[] raw)
		{
			if (raw == null || raw.Length == 0) return Array.Empty<int>();

			var candidate = raw.SmartRleDecode();

			if (IsValidTileIndices(candidate))
				return candidate;

			// Debug/info point: smart decode produced invalid indices → trying forced
			// Debug.Log($"Tiles: smart decode gave length {candidate.Length} with invalid indices → attempting forced RLE");

			var forced = raw.ForcedRleDecode();

			if (IsValidTileIndices(forced))
				return forced;

			// Both failed — this is likely a corrupt file
			// Debug.LogWarning($"Tiles decode failed: smart len={candidate.Length}, forced len={forced.Length}, raw len={raw.Length}");
			return Array.Empty<int>();  // or throw FormatException if you prefer hard failure
		}

		private static bool IsValidTileIndices(int[] arr)
		{
			if (arr == null) return false;

			int len = arr.Length;

			// Allow empty map (0×0)
			if (len == 0) return true;

			// For length 1: only check >= 0 (your rule says it will be 0 anyway)
			if (len == 1)
			{
				return arr[0] >= 0;
			}

			// Normal case
			for (int i = 0; i < len; i++)
			{
				int v = arr[i];
				if (v < 0 || v >= len) return false;
			}

			return true;
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
						string deltaVal = val.Trim().ToLowerInvariant();

						// ── Case 1: full comma-separated (arbitrary XYZ) ────────────────
						if (deltaVal.Contains(','))
						{
							var nums = deltaVal.Split(',')
											   .Select(s => s.Trim())
											   .Where(s => !string.IsNullOrEmpty(s))
											   .ToArray();

							if (nums.Length == 3 &&
								float.TryParse(nums[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float dx) &&
								float.TryParse(nums[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float dy) &&
								float.TryParse(nums[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float dz))
							{
								variant.delta = new Vector3(dx, dy, dz);
							}
							// invalid → ignore
						}
						// ── Case 2: +suffix style (with or without leading number) ──────
						else if (deltaVal.StartsWith("+") || deltaVal.Contains("+"))
						{
							string numPart = "0"; // default to y=0 when no number given
							string suffix = "";

							int plusIndex = deltaVal.IndexOf('+');
							if (plusIndex > 0)
							{
								// has number before +
								numPart = deltaVal.Substring(0, plusIndex).Trim();
								suffix = deltaVal.Substring(plusIndex + 1).Trim().ToLowerInvariant();
							}
							else if (plusIndex == 0)
							{
								// starts with + → y=0 implied
								numPart = "0";
								suffix = deltaVal.Substring(1).Trim().ToLowerInvariant();
							}

							if (float.TryParse(numPart, System.Globalization.NumberStyles.Any,
											   System.Globalization.CultureInfo.InvariantCulture, out float yVal))
							{
								bool hasX = suffix.Contains("x");
								bool hasZ = suffix.Contains("z");

								// normalize synonyms
								if (suffix == "zx" || suffix == "xz" || suffix.Contains("xz") || suffix.Contains("zx"))
								{
									hasX = true;
									hasZ = true;
								}

								float xVal = hasX ? 0.5f : 0f;
								float zVal = hasZ ? 0.5f : 0f;

								variant.delta = new Vector3(xVal, yVal, zVal);
							}
						}
						// ── Case 3: plain number → only Y ───────────────────────────────
						else if (float.TryParse(deltaVal, System.Globalization.NumberStyles.Any,
												System.Globalization.CultureInfo.InvariantCulture, out float yOnly))
						{
							variant.delta = new Vector3(0f, yOnly, 0f);
						}
						// else invalid → ignore silently
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

			// Decode tiles & solve using the smart decoder (handles both plain and RLE)
			if (jo["tiles"]?.Type == JTokenType.Array)
			{
				//var data = jo["tiles"].ToObject<int[]>(serializer);
				//((Map)map).tiles = data?.SmartRleDecode() ?? Array.Empty<int>();
				map.tiles = DecodeTilesSafely(jo["tiles"]?.ToObject<int[]>(serializer));
			}

			if (jo["solve"]?.Type == JTokenType.Array)
			{
				var data = jo["solve"].ToObject<int[]>(serializer);
				map.solve = data?.SmartRleDecode() ?? Array.Empty<int>();
			}

			if (tableArray != null)
			{
				((Map.IVariantAccess)map).Variants = ParseTableToVariants(tableArray);
			}

			//map.AutoAmbient = null == jo["ambient"];
			//map.AutoSunlight = null == jo["skyrgb"];

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

				var parts = new List<string> { HTB50Settings.ToString(v.hash) };

				if (Math.Abs(v.angle) > 0.001f)
					parts.Add($"angle:{v.angle:F1}");

				if (v.delta.sqrMagnitude > 0.000001f) // non-zero delta
				{
					const float HALF = 0.5f;
					const float EPS = 0.001f;

					bool isHalfX = Mathf.Abs(v.delta.x - HALF) < EPS;
					bool isHalfZ = Mathf.Abs(v.delta.z - HALF) < EPS;
					bool isZeroX = Mathf.Abs(v.delta.x) < EPS;
					bool isZeroZ = Mathf.Abs(v.delta.z) < EPS;
					bool isZeroY = Mathf.Abs(v.delta.y) < EPS;

					string deltaStr;

					// ── Special compact forms when y ≈ 0 ────────────────────────────
					if (isZeroY)
					{
						if (isHalfX && isHalfZ)
							deltaStr = "+xz";
						else if (isHalfX)
							deltaStr = "+x";
						else if (isHalfZ)
							deltaStr = "+z";
						else
							deltaStr = $"{v.delta.x:F3},0.000,{v.delta.z:F3}"; // rare fallback
					}
					// ── Classic + suffix forms when y != 0 ───────────────────────────
					else if (isZeroX && isZeroZ)
					{
						deltaStr = $"{v.delta.y:F3}";
					}
					else if (isHalfX && isHalfZ)
					{
						deltaStr = $"{v.delta.y:F3}+xz";
					}
					else if (isHalfX)
					{
						deltaStr = $"{v.delta.y:F3}+x";
					}
					else if (isHalfZ)
					{
						deltaStr = $"{v.delta.y:F3}+z";
					}
					// ── Full arbitrary XYZ ──────────────────────────────────────────
					else
					{
						deltaStr = $"{v.delta.x:F3},{v.delta.y:F3},{v.delta.z:F3}";
					}

					parts.Add($"delta:{deltaStr}");
				}

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

			var allProps = OrderedProperties(serializer).ToList(); // materialize once

			bool tableWritten = false;

			// 1. Write everything before the table (Order < 20)
			foreach (var prop in allProps)
			{
				if (prop.Order.GetValueOrDefault(int.MaxValue) >= TableJsonOrderPosition)
					break;   // stop when we reach the table slot

				string name = prop.PropertyName;

				if (name == "hashes" || name == "variants")
					continue;

				if (!IsAtomic && IsSuppressedInDatabaseFormat(name))
					continue;

				var propValue = prop.ValueProvider?.GetValue(map);

				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				// Special handling for tiles / solve / ambient / skyrgb (your existing code)
				if (name == "tiles" && map.tiles != null && map.tiles.Length > 0)
				{
					writer.WritePropertyName("tiles");
					var encoded = map.tiles.SmartRleEncode();
					serializer.Serialize(writer, encoded);
					continue;
				}

				if (name == "solve" && map.solve != null && map.solve.Length > 0)
				{
					writer.WritePropertyName("solve");
					var encoded = map.solve.SmartRleEncode();
					serializer.Serialize(writer, encoded);
					continue;
				}

				if (name == "ambient")
				{
					writer.WritePropertyName("ambient");
					writer.WriteValue(map.AmbientRGB.ToHexString(includeAlpha: true));
					continue;
				}

				if (name == "skyrgb")
				{
					writer.WritePropertyName("skyrgb");
					writer.WriteValue(map.SkyRGB.ToHexString(includeAlpha: true));
					continue;
				}

				writer.WritePropertyName(name);
				serializer.Serialize(writer, propValue);
			}

			// 2. Write the table exactly once, at the desired position
			if (!tableWritten)
			{
				WriteTableArray(writer, map, serializer);
				tableWritten = true;
			}

			// 3. Write everything after the table (Order >= 20)
			bool pastTableSlot = false;
			foreach (var prop in allProps)
			{
				if (!pastTableSlot)
				{
					if (prop.Order.GetValueOrDefault(int.MaxValue) >= TableJsonOrderPosition)
						pastTableSlot = true;
					else
						continue; // already written in first loop
				}

				string name = prop.PropertyName;

				if (name == "hashes" || name == "variants")
					continue;

				if (!IsAtomic && IsSuppressedInDatabaseFormat(name))
					continue;

				var propValue = prop.ValueProvider?.GetValue(map);

				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

				writer.WritePropertyName(name);
				serializer.Serialize(writer, propValue);
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

	public class AtomicMapConverter : MapSerializer
	{
		public AtomicMapConverter() : base(true) { }
	}

	public class DatabaseMapConverter : MapSerializer
	{
		public DatabaseMapConverter() : base(false) { }
	}
}
