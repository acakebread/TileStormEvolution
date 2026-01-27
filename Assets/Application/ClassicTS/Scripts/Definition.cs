using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using MassiveHadronLtd;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	[Serializable]
	[JsonConverter(typeof(DefinitionConverter))]
	public class Definition
	{
		[JsonIgnore] public HashId HashID { get; set; } = default;  // defaults to HashId(0)
		public string name;
		public string model;
		public string texture;
		public string material;
		public string flags;         // comma/space separated, e.g. "Drag, Roll, Dock"
		public string connections;   // e.g. "NSEW" (uppercase, no separators)

		// ── CONNECTIONS (settable) ────────────────────────────────────────────
		[JsonIgnore] public bool bNorth { get => HasConnection('N'); set => SetConnection('N', value); }
		[JsonIgnore] public bool bSouth { get => HasConnection('S'); set => SetConnection('S', value); }
		[JsonIgnore] public bool bEast { get => HasConnection('E'); set => SetConnection('E', value); }
		[JsonIgnore] public bool bWest { get => HasConnection('W'); set => SetConnection('W', value); }

		// ── FLAGS (settable) ──────────────────────────────────────────────────
		[JsonIgnore] public bool bDrag { get => HasFlag("Drag"); set => SetFlag("Drag", value); }
		[JsonIgnore] public bool bRoll { get => HasFlag("Roll"); set => SetFlag("Roll", value); }
		[JsonIgnore] public bool bDock { get => HasFlag("Dock"); set => SetFlag("Dock", value); }
		[JsonIgnore] public bool bDoor { get => HasFlag("Door"); set => SetFlag("Door", value); }
		[JsonIgnore] public bool bStart { get => HasFlag("Start"); set => SetFlag("Start", value); }
		[JsonIgnore] public bool bEnd { get => HasFlag("End"); set => SetFlag("End", value); }
		[JsonIgnore] public bool bConsole { get => HasFlag("Console"); set => SetFlag("Console", value); }
		[JsonIgnore] public bool bPuzzleBlock { get => HasFlag("PuzzleBlock"); set => SetFlag("PuzzleBlock", value); }
		[JsonIgnore] public bool bSway { get => HasFlag("Sway"); set => SetFlag("Sway", value); }
		[JsonIgnore] public bool bWash { get => HasFlag("Wash"); set => SetFlag("Wash", value); }

		// ── CONDITIONAL SERIALIZATION ─────────────────────────────────────────
		public bool ShouldSerializename() => !string.IsNullOrEmpty(name);
		public bool ShouldSerializemodel() => !string.IsNullOrEmpty(model);
		public bool ShouldSerializetexture() => !string.IsNullOrEmpty(texture);
		public bool ShouldSerializematerial() => !string.IsNullOrEmpty(material);
		public bool ShouldSerializeflags() => !string.IsNullOrEmpty(flags);
		public bool ShouldSerializeconnections() => !string.IsNullOrEmpty(connections);

		// ── INTERNAL HELPERS ──────────────────────────────────────────────────
		private HashSet<string> _flagCache;
		private HashSet<char> _connCache;

		public bool HasFlag(string flag)
		{
			if (_flagCache == null) RebuildFlagCache();
			return _flagCache.Contains(flag, StringComparer.OrdinalIgnoreCase);
		}

		public bool HasConnection(char dir)
		{
			if (_connCache == null) RebuildConnectionCache();
			return _connCache.Contains(char.ToUpperInvariant(dir));
		}

		private void RebuildFlagCache()
		{
			_flagCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (!string.IsNullOrEmpty(flags))
			{
				foreach (var f in flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var trimmed = f.Trim();
					if (!string.IsNullOrEmpty(trimmed))
						_flagCache.Add(trimmed);
				}
			}
		}

		private void RebuildConnectionCache()
		{
			_connCache = new HashSet<char>();
			if (!string.IsNullOrEmpty(connections))
			{
				foreach (char c in connections)
				{
					if (char.IsLetter(c))
						_connCache.Add(char.ToUpperInvariant(c));
				}
			}
		}

		private void SetFlag(string flag, bool enabled)
		{
			if (_flagCache == null) RebuildFlagCache();

			bool currentlyHas = _flagCache.Contains(flag, StringComparer.OrdinalIgnoreCase);
			if (enabled == currentlyHas) return;

			var flagList = string.IsNullOrEmpty(flags)
				? new List<string>()
				: flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					   .Select(s => s.Trim())
					   .Where(f => !string.Equals(f, flag, StringComparison.OrdinalIgnoreCase))
					   .ToList();

			if (enabled) flagList.Add(flag);
			flags = string.Join(", ", flagList);
			RebuildFlagCache();
		}

		private void SetConnection(char dirChar, bool enabled)
		{
			char dir = char.ToUpperInvariant(dirChar);
			if (_connCache == null) RebuildConnectionCache();

			bool currentlyHas = _connCache.Contains(dir);
			if (enabled == currentlyHas) return;

			var connList = new List<char>();
			if (!string.IsNullOrEmpty(connections))
			{
				foreach (char c in connections)
				{
					if (char.IsLetter(c) && char.ToUpperInvariant(c) != dir)
						connList.Add(char.ToUpperInvariant(c));
				}
			}

			if (enabled) connList.Add(dir);
			connList.Sort((a, b) => GetDirectionOrder(a).CompareTo(GetDirectionOrder(b)));

			connections = new string(connList.ToArray());
			RebuildConnectionCache();
		}

		private static int GetDirectionOrder(char dir)
		{
			return dir switch
			{
				'N' => 0,
				'E' => 1,
				'S' => 2,
				'W' => 3,
				_ => 999
			};
		}

		public static Definition Default => GetDefaultTile();   // cached if you want, but not necessary

		public bool IsDefault() => HashID == GetDefaultTile().HashID;

		public bool IsDefaultEquivalent()
		{
			// A tile is "default-like" if it has **no rendering or gameplay identity**
			return
				string.IsNullOrWhiteSpace(model) &&
				//string.IsNullOrWhiteSpace(texture) &&
				//string.IsNullOrWhiteSpace(material) &&
				string.IsNullOrWhiteSpace(flags) &&
				string.IsNullOrWhiteSpace(connections);
		}

		// ── FACTORY ────────────────────────────────────────────────────────────
		public static Definition GetDefaultTile()
		{
			const string legacyNameForHash = "tile_empty";

			// Full-range 32-bit stable hash (no modulus)
			int hash32 = RadixHash.GetStableHash32(legacyNameForHash);

			return new Definition
			{
				HashID = hash32,
				name = legacyNameForHash,
				model = null,
				texture = null,
				material = null,
				flags = null,
				connections = null
			};
		}

		//public string GetHashId() => hashid ?? throw new InvalidOperationException($"Definition '{id ?? "unknown"}' missing hashid");
	}

	public static class DefinitionExtensions
	{
		public static bool HasConnection(this Definition def, char dir)
			=> def?.HasConnection(dir) ?? false;
	}

	public class DefinitionConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(Definition);
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var definition = (Definition)value;

			writer.WriteStartObject();

			// Write "id" FIRST — generated from HashID (never stored in object)
			writer.WritePropertyName("id");
			writer.WriteValue(HTB50.EncodeFixed(
				definition.HashID,                           // implicit HashId → int
				length: HTB50Settings.FixedLength,
				padChar: '0',
				appendFlavor: false
			));

			// Write all real properties normally
			if (!string.IsNullOrEmpty(definition.name))
			{
				writer.WritePropertyName("name");
				serializer.Serialize(writer, definition.name);
			}

			if (!string.IsNullOrEmpty(definition.model))
			{
				writer.WritePropertyName("model");
				serializer.Serialize(writer, definition.model);
			}

			if (!string.IsNullOrEmpty(definition.texture))
			{
				writer.WritePropertyName("texture");
				serializer.Serialize(writer, definition.texture);
			}

			if (!string.IsNullOrEmpty(definition.material))
			{
				writer.WritePropertyName("material");
				serializer.Serialize(writer, definition.material);
			}

			if (!string.IsNullOrEmpty(definition.flags))
			{
				writer.WritePropertyName("flags");
				serializer.Serialize(writer, definition.flags);
			}

			if (!string.IsNullOrEmpty(definition.connections))
			{
				writer.WritePropertyName("connections");
				serializer.Serialize(writer, definition.connections);
			}

			writer.WriteEndObject();
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null;

			var definition = existingValue as Definition ?? new Definition();

			var jo = JObject.Load(reader);

			// Handle legacy "id" string → decode into HashID
			var idToken = jo["id"];
			if (idToken != null && idToken.Type == JTokenType.String)
			{
				string idStr = idToken.Value<string>();
				if (!string.IsNullOrEmpty(idStr))
				{
					try
					{
						int decoded = HTB50.Decode(idStr);
						definition.HashID = decoded;
					}
					catch (Exception ex)
					{
						Debug.LogWarning($"Failed to decode legacy 'id' in Definition: {ex.Message}");
					}
				}
			}

			// Populate remaining properties normally
			serializer.Populate(jo.CreateReader(), definition);

			return definition;
		}
	}
}