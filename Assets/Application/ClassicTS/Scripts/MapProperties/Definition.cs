using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
    [Flags]
    internal enum DefinitionFlags : int
    {
        None = 0,

        // ── Directions – must use exactly the same values as DirectionFlags ──
        North = 1 << 0,   // 0b0001
        South = 1 << 1,   // 0b0010
        East  = 1 << 2,   // 0b0100
        West  = 1 << 3,   // 0b1000

		//1 << 4 reserved for diagonals
		//1 << 5 reserved for diagonals
		//1 << 6 reserved for diagonals
		//1 << 7 reserved for diagonals

		DirMask = 0b1111,  // bits 0–3 only (N=1, S=2, E=4, W=8)

		// ── Gameplay flags – start from bit 8 and never touch 0–7 ─────────────
		Bake = 1 << 8,   // internal inverse of serialized "Move"
		Roll = 1 << 9,   // explicit opt-in for roll behaviour
        Door = 1 << 10,
        Desk = 1 << 11,

		// Newer gameplay flags (continuing sequentially)
		Wash = 1 << 12,
        Sway = 1 << 13,
		Plan = 1 << 14,

        // ────────────────────────────────────────────────────────────────
        // Reserved for future gameplay flags (bits 18+)
        // Do NOT reuse bits 0–7 — they are permanently reserved for directions
        // ────────────────────────────────────────────────────────────────
    }

    internal interface IFlagAccess { int Flags { get; set; } }

    [Serializable]
    [JsonConverter(typeof(DefinitionConverter))]
    public class Definition : IFlagAccess
    {
        [JsonIgnore] public HashId HashID { get; set; } = default;

        public string name;
        public string model;
        public string texture;
        public string material;

        [JsonIgnore] private int flags;

        // Explicit interface impl (optional – can be removed later if not used polymorphically)
        int IFlagAccess.Flags
        {
            get => flags;
            set => flags = value;
        }

        // ── Public API (backward compatible) ─────────────────────────────────
        [JsonIgnore] public bool North       { get => (flags & (int)DefinitionFlags.North)       != 0; set => SetFlag(DefinitionFlags.North,       value); }
        [JsonIgnore] public bool South       { get => (flags & (int)DefinitionFlags.South)       != 0; set => SetFlag(DefinitionFlags.South,       value); }
        [JsonIgnore] public bool East        { get => (flags & (int)DefinitionFlags.East)        != 0; set => SetFlag(DefinitionFlags.East,        value); }
        [JsonIgnore] public bool West        { get => (flags & (int)DefinitionFlags.West)        != 0; set => SetFlag(DefinitionFlags.West,        value); }

		[JsonIgnore] public bool Bake        { get => (flags & (int)DefinitionFlags.Bake)        != 0; set => SetFlag(DefinitionFlags.Bake,        value); }
        [JsonIgnore] public bool Roll        { get => (flags & (int)DefinitionFlags.Roll)        != 0; set => SetFlag(DefinitionFlags.Roll,        value); }
        [JsonIgnore] public bool Door        { get => (flags & (int)DefinitionFlags.Door)        != 0; set => SetFlag(DefinitionFlags.Door,        value); }
		[JsonIgnore] public bool Desk        { get => (flags & (int)DefinitionFlags.Desk)        != 0; set => SetFlag(DefinitionFlags.Desk,        value); }
        [JsonIgnore] public bool Sway        { get => (flags & (int)DefinitionFlags.Sway)        != 0; set => SetFlag(DefinitionFlags.Sway,        value); }
        [JsonIgnore] public bool Wash        { get => (flags & (int)DefinitionFlags.Wash)        != 0; set => SetFlag(DefinitionFlags.Wash,        value); }
        [JsonIgnore] public bool Plan        { get => (flags & (int)DefinitionFlags.Plan)        != 0; set => SetFlag(DefinitionFlags.Plan,        value); }

        //for a tile to be moveable at all it needs to be non static or not baked, so Drag, Fold and Roll all have this in common
		public bool Drag => !Bake && !string.IsNullOrWhiteSpace(model);

		//for tiles to be able to fold they currently must have no model - I may enhance this restriction
		public bool Fold => !Bake && string.IsNullOrWhiteSpace(model);

		[JsonIgnore]
        public int Nav
        {
            get => flags & (int)(DefinitionFlags.DirMask);
            set => flags = (flags & ~(int)DefinitionFlags.DirMask) | (value & (int)DefinitionFlags.DirMask);
        }

        private void SetFlag(DefinitionFlags flag, bool value)
        {
            if (value)
                flags |= (int)flag;
            else
                flags &= ~(int)flag;
        }

		public static Definition Default => new()
		{
			HashID = 0,
			name = "default",
			model = null,
			texture = null,
			material = null,
			// flags == 0 implicitly
		};

		public bool IsDefault() => HashID == Default.HashID;

        public bool IsDefaultEquivalent() =>
            0 == flags && 
            string.IsNullOrWhiteSpace(model) &&
			string.IsNullOrWhiteSpace(texture) &&
			string.IsNullOrWhiteSpace(material);
	}

    public class DefinitionConverter : JsonConverter
    {
        private static readonly IReadOnlyDictionary<string, DefinitionFlags> FlagLookup = new Dictionary<string, DefinitionFlags>(StringComparer.OrdinalIgnoreCase)
		{
			["Move"] = DefinitionFlags.Bake,
			["Roll"] = DefinitionFlags.Roll,
			["Door"] = DefinitionFlags.Door,
			["Desk"] = DefinitionFlags.Desk,
			["Plan"] = DefinitionFlags.Plan,
			["Sway"] = DefinitionFlags.Sway,
			["Wash"] = DefinitionFlags.Wash,
		};

		private static readonly IReadOnlyDictionary<string, DefinitionFlags> ConnectionLookup = new Dictionary<string, DefinitionFlags>(StringComparer.OrdinalIgnoreCase)
        {
            ["N"] = DefinitionFlags.North,
            ["S"] = DefinitionFlags.South,
            ["E"] = DefinitionFlags.East,
            ["W"] = DefinitionFlags.West,
        };

        public override bool CanConvert(Type objectType) => objectType == typeof(Definition);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null) { writer.WriteNull(); return; }

            var def = (Definition)value;

            writer.WriteStartObject();

            writer.WritePropertyName("id");
            writer.WriteValue(HTB50Settings.ToString(def.HashID));

            if (!string.IsNullOrEmpty(def.name))    { writer.WritePropertyName("name");     serializer.Serialize(writer, def.name); }
            if (!string.IsNullOrEmpty(def.model))   { writer.WritePropertyName("model");    serializer.Serialize(writer, def.model); }
            if (!string.IsNullOrEmpty(def.texture)) { writer.WritePropertyName("texture");  serializer.Serialize(writer, def.texture); }
            if (!string.IsNullOrEmpty(def.material)){ writer.WritePropertyName("material"); serializer.Serialize(writer, def.material); }

            // Gameplay flags
            var activeFlags = new List<string>();
            foreach (var kv in FlagLookup)
			{
				if (string.Equals(kv.Key, "Move", StringComparison.OrdinalIgnoreCase))
				{
					if (!def.Bake)
						activeFlags.Add(kv.Key);
					continue;
				}

				if ((((IFlagAccess)def).Flags & (int)kv.Value) != 0)
                    activeFlags.Add(kv.Key);
            }

            if (activeFlags.Count > 0)
            {
                activeFlags.Sort(StringComparer.OrdinalIgnoreCase);
                writer.WritePropertyName("flags");
                writer.WriteValue(string.Join(", ", activeFlags));
            }

			// Connections — fixed N-S-E-W order
			var activeDirs = new List<char>(4);
			var flagsValue = ((IFlagAccess)def).Flags;           // once only
			if ((flagsValue & (int)DefinitionFlags.North) != 0) activeDirs.Add('N');
			if ((flagsValue & (int)DefinitionFlags.South) != 0) activeDirs.Add('S');
			if ((flagsValue & (int)DefinitionFlags.East) != 0) activeDirs.Add('E');
			if ((flagsValue & (int)DefinitionFlags.West) != 0) activeDirs.Add('W');

			if (activeDirs.Count > 0)
            {
                writer.WritePropertyName("connections");
                writer.WriteValue(new string(activeDirs.ToArray()));
            }

            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var def = existingValue as Definition ?? new Definition();
			((IFlagAccess)def).Flags = (int)DefinitionFlags.Bake;
            var jo = JObject.Load(reader);

            if (jo["id"]?.Value<string>() is { } idStr && !string.IsNullOrEmpty(idStr))
            {
                try { def.HashID = HTB50.Decode(idStr); }
                catch (Exception ex) { Debug.LogWarning($"Failed to decode id: {ex.Message}"); }
            }

            serializer.Populate(jo.CreateReader(), def);

            // Gameplay flags
            if (jo["flags"]?.Value<string>() is { } flagsStr && !string.IsNullOrEmpty(flagsStr))
            {
                var parts = flagsStr.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    if (string.Equals(trimmed, "Exit", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(trimmed, "Home", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (FlagLookup.TryGetValue(trimmed, out var flag))
					{
						if (flag == DefinitionFlags.Bake)
							((IFlagAccess)def).Flags &= ~(int)DefinitionFlags.Bake;
						else
							((IFlagAccess)def).Flags |= (int)flag;
                    }
                    else
                        Debug.LogWarning($"Unknown flag in JSON: '{trimmed}'");
                }
            }

            // Connections
            if (jo["connections"]?.Value<string>() is { } connStr && !string.IsNullOrEmpty(connStr))
            {
                foreach (char c in connStr.ToUpperInvariant())
                {
                    if (!char.IsLetter(c)) continue;

                    string key = c.ToString();

                    if (ConnectionLookup.TryGetValue(key, out var flag))
                        ((IFlagAccess)def).Flags |= (int)flag;
                    else
                        Debug.LogWarning($"Unknown direction in connections: '{c}'");
                }
            }

            return def;
        }
    }
}
