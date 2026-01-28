using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using MassiveHadronLtd;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	[Flags]
	internal enum DefinitionFlags : int
	{
		None = 0,

		// ── Directions – must use exactly the same values as DirectionFlags ──
		North = DirectionFlags.North,  // (1 <<  0) 0b0000000000000001
		South = DirectionFlags.South,  // (1 <<  1) 0b0000000000000010
		East  = DirectionFlags.East,   // (1 <<  2) 0b0000000000000100
		West  = DirectionFlags.West,   // (1 <<  3) 0b0000000000001000

		// ── Gameplay flags – start from bit 8 and never touch 0–7 ─────────────
		Drag    = 1 <<  8,             // (1 <<  8) 0b0000000100000000
		Roll    = 1 <<  9,             // (1 <<  9) 0b0000001000000000
		Dock    = 1 << 10,             // (1 << 10) 0b0000010000000000
		Start   = 1 << 11,             // (1 << 11) 0b0000100000000000
		End     = 1 << 12,             // (1 << 12) 0b0001000000000000
		Door    = 1 << 13,             // (1 << 13) 0b0010000000000000
		Console = 1 << 14,             // (1 << 14) 0b0100000000000000

		// Newer gameplay flags (continuing sequentially)
		PuzzleBlock = 1 << 15,         // (1 << 15) 0b1000000000000000
		Sway        = 1 << 16,         // (1 << 16) 0b1 0000000000000000   (bit 16)
		Wash        = 1 << 17,         // (1 << 17) 0b10 0000000000000000  (bit 17)
	}

	internal interface IFlagAccess
	{
		int Flags { get; set; }
	}

	[Serializable]
	[JsonConverter(typeof(DefinitionConverter))]
	public class Definition : IFlagAccess
	{
		[JsonIgnore]
		public HashId HashID { get; set; } = default;

		public string name;
		public string model;
		public string texture;
		public string material;

		// ── The only flags field ───────────────────────────────────────────────
		[JsonIgnore]
		private int flags;

		int IFlagAccess.Flags
		{
			get => flags;
			set => flags = value;
		}

		// ── Public API remains identical (backward compatible) ────────────────
		[JsonIgnore] public bool bNorth { get => (flags & (int)DefinitionFlags.North) != 0; set => SetFlag(DefinitionFlags.North, value); }
		[JsonIgnore] public bool bSouth { get => (flags & (int)DefinitionFlags.South) != 0; set => SetFlag(DefinitionFlags.South, value); }
		[JsonIgnore] public bool bEast { get => (flags & (int)DefinitionFlags.East) != 0; set => SetFlag(DefinitionFlags.East, value); }
		[JsonIgnore] public bool bWest { get => (flags & (int)DefinitionFlags.West) != 0; set => SetFlag(DefinitionFlags.West, value); }

		[JsonIgnore] public bool bDrag { get => (flags & (int)DefinitionFlags.Drag) != 0; set => SetFlag(DefinitionFlags.Drag, value); }
		[JsonIgnore] public bool bRoll { get => (flags & (int)DefinitionFlags.Roll) != 0; set => SetFlag(DefinitionFlags.Roll, value); }
		[JsonIgnore] public bool bDock { get => (flags & (int)DefinitionFlags.Dock) != 0; set => SetFlag(DefinitionFlags.Dock, value); }
		[JsonIgnore] public bool bDoor { get => (flags & (int)DefinitionFlags.Door) != 0; set => SetFlag(DefinitionFlags.Door, value); }
		[JsonIgnore] public bool bStart { get => (flags & (int)DefinitionFlags.Start) != 0; set => SetFlag(DefinitionFlags.Start, value); }
		[JsonIgnore] public bool bEnd { get => (flags & (int)DefinitionFlags.End) != 0; set => SetFlag(DefinitionFlags.End, value); }
		[JsonIgnore] public bool bConsole { get => (flags & (int)DefinitionFlags.Console) != 0; set => SetFlag(DefinitionFlags.Console, value); }
		[JsonIgnore] public bool bPuzzleBlock { get => (flags & (int)DefinitionFlags.PuzzleBlock) != 0; set => SetFlag(DefinitionFlags.PuzzleBlock, value); }
		[JsonIgnore] public bool bSway { get => (flags & (int)DefinitionFlags.Sway) != 0; set => SetFlag(DefinitionFlags.Sway, value); }
		[JsonIgnore] public bool bWash { get => (flags & (int)DefinitionFlags.Wash) != 0; set => SetFlag(DefinitionFlags.Wash, value); }

		// ── Packed navigation (multi-bit field) ───────────────────────────────
		[JsonIgnore]
		public int Nav
		{
			get => flags & (int)DirectionFlags.Directions;
			set => flags = (flags & ~(int)DirectionFlags.Directions) | (value & (int)DirectionFlags.Directions);
		}

		// ── Helper to keep setters DRY ────────────────────────────────────────
		private void SetFlag(DefinitionFlags flag, bool value)
		{
			if (value)
				flags |= (int)flag;
			else
				flags &= ~(int)flag;
		}

		public static Definition GetDefaultTile()
		{
			const string legacyName = "tile_empty";
			int hash32 = RadixHash.GetStableHash32(legacyName);

			return new Definition
			{
				HashID = hash32,
				name = legacyName,
				model = null,
				texture = null,
				material = null,
				// flags remain 0 — correct for empty tile
			};
		}

		public static Definition Default => GetDefaultTile();

		public bool IsDefault() => HashID == GetDefaultTile().HashID;

		public bool IsDefaultEquivalent()
		{
			return string.IsNullOrWhiteSpace(model) &&
				   !bDrag && !bRoll && !bDock && !bDoor &&
				   !bStart && !bEnd && !bConsole && !bPuzzleBlock &&
				   !bSway && !bWash &&
				   !bNorth && !bEast && !bSouth && !bWest;
		}
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Centralized flag mapping
	// ─────────────────────────────────────────────────────────────────────────
	internal static class DefinitionFlagMapping
	{
		public static readonly IReadOnlyDictionary<string, DefinitionFlags> NameToFlag
			= new Dictionary<string, DefinitionFlags>(StringComparer.OrdinalIgnoreCase)
			{
				["Drag"] = DefinitionFlags.Drag,
				["Roll"] = DefinitionFlags.Roll,
				["Dock"] = DefinitionFlags.Dock,
				["Door"] = DefinitionFlags.Door,
				["Start"] = DefinitionFlags.Start,
				["End"] = DefinitionFlags.End,
				["Console"] = DefinitionFlags.Console,
				["PuzzleBlock"] = DefinitionFlags.PuzzleBlock,
				["Sway"] = DefinitionFlags.Sway,
				["Wash"] = DefinitionFlags.Wash,
				// Add future flags here — single place to maintain
			};
	}

	// ─────────────────────────────────────────────────────────────────────────
	// Converter – non-generic, uses IFlagAccess, compatible with Unity Newtonsoft
	// ─────────────────────────────────────────────────────────────────────────
	public class DefinitionConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType) => objectType == typeof(Definition);

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var def = (Definition)value;
			var flagAccess = (IFlagAccess)def;  // safe cast – we know Definition implements it

			writer.WriteStartObject();

			// id
			writer.WritePropertyName("id");
			writer.WriteValue(HTB50.EncodeFixed(
				def.HashID,
				length: HTB50Settings.FixedLength,
				padChar: '0',
				appendFlavor: false
			));

			// scalar fields
			if (!string.IsNullOrEmpty(def.name)) { writer.WritePropertyName("name"); serializer.Serialize(writer, def.name); }
			if (!string.IsNullOrEmpty(def.model)) { writer.WritePropertyName("model"); serializer.Serialize(writer, def.model); }
			if (!string.IsNullOrEmpty(def.texture)) { writer.WritePropertyName("texture"); serializer.Serialize(writer, def.texture); }
			if (!string.IsNullOrEmpty(def.material)) { writer.WritePropertyName("material"); serializer.Serialize(writer, def.material); }

			// Flags – collect active ones
			var activeFlags = new List<string>();
			foreach (var kv in DefinitionFlagMapping.NameToFlag)
			{
				if ((flagAccess.Flags & (int)kv.Value) != 0)
					activeFlags.Add(kv.Key);
			}

			if (activeFlags.Count > 0)
			{
				activeFlags.Sort(StringComparer.OrdinalIgnoreCase);
				writer.WritePropertyName("flags");
				writer.WriteValue(string.Join(", ", activeFlags));
			}

			// Connections – unchanged, uses public bXxx properties
			var activeDirs = new List<char>();
			if (def.bNorth) activeDirs.Add('N');
			if (def.bSouth) activeDirs.Add('S');
			if (def.bEast) activeDirs.Add('E');
			if (def.bWest) activeDirs.Add('W');

			if (activeDirs.Count > 0)
			{
				writer.WritePropertyName("connections");
				writer.WriteValue(new string(activeDirs.ToArray()));
			}

			writer.WriteEndObject();
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null;

			var def = existingValue as Definition ?? new Definition();
			var flagAccess = (IFlagAccess)def;

			var jo = JObject.Load(reader);

			// id → HashID
			if (jo["id"]?.Type == JTokenType.String)
			{
				string idStr = jo["id"].Value<string>();
				if (!string.IsNullOrEmpty(idStr))
				{
					try
					{
						def.HashID = HTB50.Decode(idStr);
					}
					catch (Exception ex)
					{
						Debug.LogWarning($"Failed to decode Definition id: {ex.Message}");
					}
				}
			}

			// scalar fields
			serializer.Populate(jo.CreateReader(), def);

			// ── Parse flags ────────────────────────────────────────────────────
			if (jo["flags"]?.Type == JTokenType.String)
			{
				string raw = jo["flags"].Value<string>() ?? "";
				var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

				foreach (var part in parts)
				{
					string trimmed = part.Trim();
					if (string.IsNullOrEmpty(trimmed)) continue;

					if (DefinitionFlagMapping.NameToFlag.TryGetValue(trimmed, out var flag))
					{
						flagAccess.Flags |= (int)flag;
					}
					else
					{
						Debug.LogWarning($"Unknown flag in JSON: '{trimmed}'");
					}
				}
			}

			// ── Parse connections ──────────────────────────────────────────────
			if (jo["connections"]?.Type == JTokenType.String)
			{
				string raw = jo["connections"].Value<string>() ?? "";
				foreach (char c in raw.ToUpperInvariant())
				{
					if (!char.IsLetter(c)) continue;
					switch (c)
					{
						case 'N': def.bNorth = true; break;
						case 'S': def.bSouth = true; break;
						case 'E': def.bEast = true; break;
						case 'W': def.bWest = true; break;
					}
				}
			}

			return def;
		}
	}
}