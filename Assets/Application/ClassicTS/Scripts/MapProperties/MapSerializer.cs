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

		// Read TableJsonOrder from the Map class instead of using a local constant
		private static int GetTableJsonOrder()
		{
			var field = typeof(Map).GetField(nameof(Map.TableJsonOrder),
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			return field != null ? (int)field.GetValue(null) : 20;
		}

		private static int[] DecodeTilesSafely(int[] raw)
		{
			if (raw == null || raw.Length == 0) return Array.Empty<int>();

			var candidate = raw.SmartRleDecode();

			if (IsValidTileIndices(candidate))
				return candidate;

			var forced = raw.ForcedRleDecode();

			if (IsValidTileIndices(forced))
				return forced;

			return Array.Empty<int>();
		}

		private static bool IsValidTileIndices(int[] arr)
		{
			if (arr == null) return false;

			int len = arr.Length;

			if (len == 0) return true;

			if (len == 1)
			{
				return arr[0] >= 0;
			}

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

				string machinePart;
				int hashPos = entry.IndexOf('#');

				if (hashPos >= 0)
				{
					machinePart = entry.Substring(0, hashPos).TrimEnd();
				}
				else
				{
					machinePart = entry;

					if (machinePart.StartsWith("[") && machinePart.Contains("]"))
					{
						int close = machinePart.IndexOf(']', 1);
						if (close > 1)
						{
							machinePart = machinePart.Substring(1, close - 1).Trim();
						}
					}
				}

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

				string hashStr = parts[0];
				HashId hash = 0;
				try
				{
					hash = HTB50.Decode(hashStr);
				}
				catch
				{
					hash = 0;
				}

				var variant = new Variant(hash);

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
						}
						else if (deltaVal.StartsWith("+") || deltaVal.Contains("+"))
						{
							string numPart = "0";
							string suffix = "";

							int plusIndex = deltaVal.IndexOf('+');
							if (plusIndex > 0)
							{
								numPart = deltaVal.Substring(0, plusIndex).Trim();
								suffix = deltaVal.Substring(plusIndex + 1).Trim().ToLowerInvariant();
							}
							else if (plusIndex == 0)
							{
								suffix = deltaVal.Substring(1).Trim().ToLowerInvariant();
							}

							if (float.TryParse(numPart, System.Globalization.NumberStyles.Any,
											   System.Globalization.CultureInfo.InvariantCulture, out float yVal))
							{
								bool hasX = suffix.Contains("x");
								bool hasZ = suffix.Contains("z");

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
						else if (float.TryParse(deltaVal, System.Globalization.NumberStyles.Any,
												System.Globalization.CultureInfo.InvariantCulture, out float yOnly))
						{
							variant.delta = new Vector3(0f, yOnly, 0f);
						}
					}
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

			if (jo["tiles"]?.Type == JTokenType.Array)
			{
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
				//no longer needed - default tile is hashid zero so this is valid
				//if (v.hash == 0)
				//{
				//	writer.WriteValue("unknown");
				//	continue;
				//}

				var parts = new List<string> { HTB50Settings.ToString(v.hash) };

				//if (Math.Abs(v.angle) > 0.001f)
				//	parts.Add($"angle:{v.angle:F1}");
				if (Math.Abs(v.angle) > 0.001f)
				{
					//string angleStr = FormatAngle(v.angle);
					string angleStr = v.angle.ToCleanString();
					parts.Add($"angle:{angleStr}");
				}

				if (v.delta.sqrMagnitude > 0.000001f)
				{
					const float HALF = 0.5f;
					const float EPS = 0.001f;

					bool isHalfX = Mathf.Abs(v.delta.x - HALF) < EPS;
					bool isHalfZ = Mathf.Abs(v.delta.z - HALF) < EPS;
					bool isZeroX = Mathf.Abs(v.delta.x) < EPS;
					bool isZeroZ = Mathf.Abs(v.delta.z) < EPS;
					bool isZeroY = Mathf.Abs(v.delta.y) < EPS;

					string deltaStr;

					if (isZeroY)
					{
						if (isHalfX && isHalfZ)
							deltaStr = "+xz";
						else if (isHalfX)
							deltaStr = "+x";
						else if (isHalfZ)
							deltaStr = "+z";
						else
							deltaStr = $"{v.delta.x:F3},0.000,{v.delta.z:F3}";
					}
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
					else
					{
						deltaStr = $"{v.delta.x:F3},{v.delta.y:F3},{v.delta.z:F3}";
					}

					parts.Add($"delta:{deltaStr}");
				}

				string content = string.Join("|", parts);

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
					finalValue = content;
				}

				writer.WriteValue(finalValue);
			}

			writer.WriteEndArray();
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var map = (Map)value ?? throw new ArgumentNullException(nameof(value));

			writer.WriteStartObject();

			int tableOrder = GetTableJsonOrder();   // <-- now comes from Map class

			var allProps = OrderedProperties(serializer).ToList();

			bool tableWritten = false;

			// 1. Write everything before the table
			foreach (var prop in allProps)
			{
				if (prop.Order.GetValueOrDefault(int.MaxValue) >= tableOrder)
					break;

				string name = prop.PropertyName;

				if (name == "hashes" || name == "variants")
					continue;

				if (!IsAtomic && IsSuppressedInDatabaseFormat(name))
					continue;

				var propValue = prop.ValueProvider?.GetValue(map);

				if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
					continue;

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

			// 2. Write the table
			if (!tableWritten)
			{
				WriteTableArray(writer, map, serializer);
				tableWritten = true;
			}

			// 3. Write everything after the table
			bool pastTableSlot = false;
			foreach (var prop in allProps)
			{
				if (!pastTableSlot)
				{
					if (prop.Order.GetValueOrDefault(int.MaxValue) >= tableOrder)
						pastTableSlot = true;
					else
						continue;
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

			var usedTextures = ResourceManager.TextureInfos
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

		//private static string FormatAngle(float angle)
		//{
		//	const float EPSILON = 0.0001f; // tolerance for floating point precision

		//	// Check if it's very close to a whole number
		//	float rounded = Mathf.Round(angle);
		//	if (Mathf.Abs(angle - rounded) < EPSILON)
		//	{
		//		return rounded.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
		//	}

		//	// Otherwise keep one decimal place
		//	return angle.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
		//}

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
