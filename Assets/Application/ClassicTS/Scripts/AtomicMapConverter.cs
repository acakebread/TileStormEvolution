//// AtomicMapConverter.cs
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using System;

//namespace ClassicTilestorm
//{
//	public class AtomicMapConverter : JsonConverter<Map>
//	{
//		// This flag is set ONLY during atomic export
//		public static bool ExportAtomicMode { get; set; } = false;

//		public override Map ReadJson(JsonReader reader, Type objectType, Map existingValue, bool hasExistingValue, JsonSerializer serializer)
//		{
//			// Always deserialize as normal Map first
//			var jObject = JObject.Load(reader);

//			var map = existingValue ?? new Map();
//			serializer.Populate(jObject.CreateReader(), map);

//			// If atomic fields exist in JSON → populate them
//			if (jObject["definitions"] != null)
//				map.definitions = jObject["definitions"].ToObject<Definition[]>(serializer);

//			if (jObject["textures"] != null)
//				map.textures = jObject["textures"].ToObject<TextureSequence[]>(serializer);

//			if (jObject["version"] != null)
//				map.version = jObject["version"].ToString();

//			if (jObject["author"] != null)
//				map.author = jObject["author"].ToString();

//			if (jObject["exportedFrom"] != null)
//				map.exportedFrom = jObject["exportedFrom"].ToString();

//			return map;
//		}

//		public override void WriteJson(JsonWriter writer, Map value, JsonSerializer serializer)
//		{
//			if (value == null)
//			{
//				writer.WriteNull();
//				return;
//			}

//			// If we're in atomic export mode → write full atomic format
//			if (ExportAtomicMode)
//			{
//				writer.WriteStartObject();

//				// Write all normal Map fields
//				WriteProperty(writer, "name", value.name);
//				WriteProperty(writer, "character", value.character);
//				WriteProperty(writer, "music", value.music);
//				WriteProperty(writer, "button", value.button);
//				WriteProperty(writer, "width", value.width);
//				WriteProperty(writer, "height", value.height);
//				WriteProperty(writer, "waypoints", value.waypoints, serializer);
//				WriteProperty(writer, "table", value.table, serializer);
//				WriteProperty(writer, "tiles", value.tiles, serializer);
//				WriteProperty(writer, "mixed", value.mixed, serializer);
//				if (value.ShouldSerializePickups())
//					WriteProperty(writer, "Pickups", value.Pickups, serializer);

//				// Now write atomic fields
//				WriteProperty(writer, "version", value.version ?? "2.0");
//				WriteProperty(writer, "author", value.author ?? "Player");
//				WriteProperty(writer, "exportedFrom", value.exportedFrom ?? "ClassicTilestorm");
//				WriteProperty(writer, "definitions", value.definitions, serializer);
//				WriteProperty(writer, "textures", value.textures, serializer);

//				writer.WriteEndObject();
//			}
//			else
//			{
//				// Normal compact mode — use default serializer (respects [JsonIgnore])
//				var settings = serializer.GetType().GetProperty("Settings")?.GetValue(serializer);
//				var defaultSerializer = JsonSerializer.CreateDefault();
//				if (settings != null)
//					defaultSerializer = JsonSerializer.Create((JsonSerializerSettings)settings);

//				defaultSerializer.Serialize(writer, value);
//			}
//		}

//		private void WriteProperty(JsonWriter writer, string name, object value, JsonSerializer serializer = null)
//		{
//			writer.WritePropertyName(name);
//			if (serializer != null && value != null && value.GetType().IsClass && !(value is string))
//				serializer.Serialize(writer, value);
//			else
//				writer.WriteValue(value);
//		}
//	}
//}