// MapAttachment.cs
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ClassicTilestorm
{
	[Serializable]
	[JsonConverter(typeof(MapAttachmentConverter))]
	public abstract class MapAttachment
	{
		[JsonProperty(Order = 1)] public string type;
		[JsonProperty(Order = 2)] public string name;
		[JsonProperty(Order = 3)] public int tile = -1;

		[JsonIgnore]
		public virtual string TypeName => GetType().Name;
	}

	public class MapAttachmentConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return typeof(MapAttachment).IsAssignableFrom(objectType);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			var jo = JObject.Load(reader);
			var typeName = jo["type"]?.ToString() ?? "Emitter";

			MapAttachment result = typeName switch
			{
				"Emitter" => new Emitter(),
				_ => new Emitter()
			};

			// Remove our converter temporarily to prevent recursion during Populate
			var converters = serializer.Converters;
			bool removed = converters.Remove(this);
			try
			{
				serializer.Populate(jo.CreateReader(), result);
			}
			finally
			{
				if (removed) converters.Insert(0, this); // put it back
			}

			return result;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			// Temporarily remove ourselves from the converter list
			var converters = serializer.Converters;
			bool removed = converters.Remove(this);
			try
			{
				serializer.Serialize(writer, value);
			}
			finally
			{
				if (removed) converters.Insert(0, this);
			}
		}
	}
}