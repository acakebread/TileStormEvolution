using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using MassiveHadronLtd;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	[Flags] internal enum DefinitionFlags : int
	{
		None = 0,

		// ── Directions – must use exactly the same values as DirectionFlags ──
		North = DirectionFlags.North,//(1 << 0) 0b000000000000001
		South = DirectionFlags.South,//(1 << 1) 0b000000000000010
		East = DirectionFlags.East,  //(1 << 2) 0b000000000000100
		West = DirectionFlags.West,  //(1 << 3) 0b000000000001000

		// ── Gameplay flags – start from bit 8 and never touch 0–7 ─────────────
		Drag = 1 << 8,               //(1 << 8) 0b000000100000000
		Roll = 1 << 9,               //(1 << 9) 0b000001000000000
		Dock = 1 << 10,              //(1 <<10) 0b000010000000000
		Start = 1 << 11,             //(1 <<11) 0b000100000000000
		End = 1 << 12,               //(1 <<12) 0b001000000000000
		Door = 1 << 13,              //(1 <<13) 0b010000000000000
		Console = 1 << 14            //(1 <<14) 0b100000000000000
	}

	internal struct DefinitionData
	{
		private int flags;

		public DefinitionData(Definition def)
		{
			flags = def == null ? 0 : CombineFlags(def);

			static int CombineFlags(Definition d)
			{
				int f = 0;
				if (d.bNorth) f |= (int)DefinitionFlags.North;
				if (d.bSouth) f |= (int)DefinitionFlags.South;
				if (d.bEast) f |= (int)DefinitionFlags.East;
				if (d.bWest) f |= (int)DefinitionFlags.West;
				if (d.bDrag) f |= (int)DefinitionFlags.Drag;
				if (d.bRoll) f |= (int)DefinitionFlags.Roll;
				if (d.bDock) f |= (int)DefinitionFlags.Dock;
				if (d.bStart) f |= (int)DefinitionFlags.Start;
				if (d.bEnd) f |= (int)DefinitionFlags.End;
				if (d.bDoor) f |= (int)DefinitionFlags.Door;
				if (d.bConsole) f |= (int)DefinitionFlags.Console;
				return f;
			}
		}

#if DEBUG
		// One-time check that nobody messed up the bit assignments
		static DefinitionData()
		{
			const int directionBits = (int)DirectionFlags.Directions;

			// Check EVERY gameplay flag against the direction bits
			if (((int)DefinitionFlags.Drag & directionBits) != 0 ||
				((int)DefinitionFlags.Roll & directionBits) != 0 ||
				((int)DefinitionFlags.Dock & directionBits) != 0 ||
				((int)DefinitionFlags.Start & directionBits) != 0 ||
				((int)DefinitionFlags.End & directionBits) != 0 ||
				((int)DefinitionFlags.Door & directionBits) != 0 ||
				((int)DefinitionFlags.Console & directionBits) != 0)
			{
				throw new InvalidProgramException(
					"CRITICAL: One or more gameplay flags overlap with direction bits 0–3. " +
					"Directions are permanently reserved — do NOT use bits 0–3 for new flags.");
			}
		}
#endif

		public readonly bool IsStart => (flags & (int)DefinitionFlags.Start) != 0;
		public readonly bool IsEnd => (flags & (int)DefinitionFlags.End) != 0;
		public readonly bool IsConsole => (flags & (int)DefinitionFlags.Console) != 0;
		public readonly bool IsDrag => (flags & (int)DefinitionFlags.Drag) != 0;
		public readonly bool IsDock => (flags & (int)DefinitionFlags.Dock) != 0;
		public readonly bool IsRoll => (flags & (int)DefinitionFlags.Roll) != 0;

		public int Nav 
		{ 
			get => flags & (int)DirectionFlags.Directions;
			set
			{
				flags &= ~(int)DirectionFlags.Directions;
				flags |= value;
			}
		}
	}

	[Serializable]
	[JsonConverter(typeof(DefinitionConverter))]
	public class Definition
	{
		[JsonIgnore]
		public HashId HashID { get; set; } = default;

		public string name;
		public string model;
		public string texture;
		public string material;

		// ── CONNECTIONS ───────────────────────────────────────────────────────
		[JsonIgnore] public bool bNorth;
		[JsonIgnore] public bool bSouth;
		[JsonIgnore] public bool bEast;
		[JsonIgnore] public bool bWest;

		// ── FLAGS ─────────────────────────────────────────────────────────────
		[JsonIgnore] public bool bDrag;
		[JsonIgnore] public bool bRoll;
		[JsonIgnore] public bool bDock;
		[JsonIgnore] public bool bDoor;
		[JsonIgnore] public bool bStart;
		[JsonIgnore] public bool bEnd;
		[JsonIgnore] public bool bConsole;
		[JsonIgnore] public bool bPuzzleBlock;
		[JsonIgnore] public bool bSway;
		[JsonIgnore] public bool bWash;

		// ── DEFAULT & HELPERS ─────────────────────────────────────────────────
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
	// All conversion logic lives here — Definition stays dumb
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

			// flags — collect all true flags, sort alphabetically, join with ", "
			var activeFlags = new List<string>();
			if (def.bDrag) activeFlags.Add("Drag");
			if (def.bRoll) activeFlags.Add("Roll");
			if (def.bDock) activeFlags.Add("Dock");
			if (def.bDoor) activeFlags.Add("Door");
			if (def.bStart) activeFlags.Add("Start");
			if (def.bEnd) activeFlags.Add("End");
			if (def.bConsole) activeFlags.Add("Console");
			if (def.bPuzzleBlock) activeFlags.Add("PuzzleBlock");
			if (def.bSway) activeFlags.Add("Sway");
			if (def.bWash) activeFlags.Add("Wash");

			if (activeFlags.Count > 0)
			{
				activeFlags.Sort(StringComparer.OrdinalIgnoreCase);
				writer.WritePropertyName("flags");
				writer.WriteValue(string.Join(", ", activeFlags));
			}

			// connections — collect active directions in NESW order
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

			// Populate scalar fields
			serializer.Populate(jo.CreateReader(), def);

			// ── Parse flags ────────────────────────────────────────────────────
			if (jo["flags"]?.Type == JTokenType.String)
			{
				string raw = jo["flags"].Value<string>() ?? "";
				var parts = raw.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
							   .Select(s => s.Trim())
							   .Where(s => !string.IsNullOrEmpty(s));

				foreach (var flag in parts)
				{
					switch (flag.ToLowerInvariant())
					{
						case "drag": def.bDrag = true; break;
						case "roll": def.bRoll = true; break;
						case "dock": def.bDock = true; break;
						case "door": def.bDoor = true; break;
						case "start": def.bStart = true; break;
						case "end": def.bEnd = true; break;
						case "console": def.bConsole = true; break;
						case "puzzleblock": def.bPuzzleBlock = true; break;
						case "sway": def.bSway = true; break;
						case "wash": def.bWash = true; break;
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