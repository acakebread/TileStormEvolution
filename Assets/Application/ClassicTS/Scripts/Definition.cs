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