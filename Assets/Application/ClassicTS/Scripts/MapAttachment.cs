// MapAttachment.cs
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

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

		public MapAttachment ShallowClone() => (MapAttachment)MemberwiseClone();
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
				"Viewpoint" => new Viewpoint(),   // ← NOW LOADS CORRECTLY!
				"Pickup" => new Pickup(),
				_ => new Emitter()
			};

			// Prevent recursion during Populate
			var converters = serializer.Converters;
			bool removed = converters.Remove(this);
			try
			{
				serializer.Populate(jo.CreateReader(), result);
			}
			finally
			{
				if (removed) converters.Insert(0, this);
			}

			return result;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var attachment = (MapAttachment)value;

			writer.WriteStartObject();

			writer.WritePropertyName("type");
			writer.WriteValue(attachment.type ?? attachment.GetType().Name);

			var converters = serializer.Converters;
			bool removed = converters.Remove(this);

			try
			{
				// Collect all public instance fields from the type hierarchy
				var type = value.GetType();
				var fieldList = new List<(FieldInfo field, int order)>();

				while (type != null)
				{
					foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
					{
						var jp = field.GetCustomAttribute<JsonPropertyAttribute>();
						int ord = jp != null ? jp.Order : int.MaxValue;
						fieldList.Add((field, ord));
					}
					type = type.BaseType;
				}

				// Sort by JsonProperty Order, then by name
				fieldList = fieldList.OrderBy(x => x.order).ThenBy(x => x.field.Name).ToList();

				foreach (var (field, _) in fieldList)
				{
					if (Attribute.IsDefined(field, typeof(JsonIgnoreAttribute))) continue;
					if (field.Name == nameof(MapAttachment.type)) continue; // already written

					var fieldValue = field.GetValue(value);
					if (fieldValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
						continue;

					var jsonProp = field.GetCustomAttribute<JsonPropertyAttribute>();
					string propertyName = jsonProp?.PropertyName ?? field.Name;

					writer.WritePropertyName(propertyName);
					serializer.Serialize(writer, fieldValue);
				}
			}
			finally
			{
				if (removed)
					converters.Insert(0, this);
			}

			writer.WriteEndObject();
		}
	}
}



//// MapAttachment.cs
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
//using System;
//using System.Collections.Generic;
//using System.Reflection;

//namespace ClassicTilestorm
//{
//	[Serializable]
//	[JsonConverter(typeof(MapAttachmentConverter))]
//	public abstract class MapAttachment
//	{
//		[JsonProperty(Order = 1)] public string type;
//		[JsonProperty(Order = 2)] public string name;
//		[JsonProperty(Order = 3)] public int tile = -1;

//		[JsonIgnore]
//		public virtual string TypeName => GetType().Name;

//		public MapAttachment ShallowClone() => (MapAttachment)MemberwiseClone();
//	}

//	public class MapAttachmentConverter : JsonConverter
//	{
//		public override bool CanConvert(Type objectType)
//		{
//			return typeof(MapAttachment).IsAssignableFrom(objectType);
//		}

//		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//		{
//			if (reader.TokenType == JsonToken.Null) return null;

//			var jo = JObject.Load(reader);
//			var typeName = jo["type"]?.ToString() ?? "Emitter";

//			MapAttachment result = typeName switch
//			{
//				"Emitter" => new Emitter(),
//				"Viewpoint" => new Viewpoint(),   // ← NOW LOADS CORRECTLY!
//				"Pickup" => new Pickup(),
//				_ => new Emitter()
//			};

//			// Prevent recursion during Populate
//			var converters = serializer.Converters;
//			bool removed = converters.Remove(this);
//			try
//			{
//				serializer.Populate(jo.CreateReader(), result);
//			}
//			finally
//			{
//				if (removed) converters.Insert(0, this);
//			}

//			return result;
//		}

//		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//		{
//			var converters = serializer.Converters;
//			bool removed = converters.Remove(this);
//			try
//			{
//				serializer.Serialize(writer, value); // This now works perfectly because of the global resolver
//			}
//			finally
//			{
//				if (removed) converters.Insert(0, this);
//			}
//		}

//		//public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//		//{
//		//	if (value == null)
//		//	{
//		//		writer.WriteNull();
//		//		return;
//		//	}

//		//	var attachment = (MapAttachment)value;

//		//	writer.WriteStartObject();

//		//	// Write discriminator first
//		//	writer.WritePropertyName("type");
//		//	writer.WriteValue(attachment.type ?? attachment.GetType().Name);

//		//	var converters = serializer.Converters;
//		//	bool removed = converters.Remove(this);

//		//	try
//		//	{
//		//		// CRITICAL: Include inherited properties!
//		//		foreach (var prop in value.GetType().GetProperties(
//		//			System.Reflection.BindingFlags.Public |
//		//			System.Reflection.BindingFlags.Instance |
//		//			System.Reflection.BindingFlags.FlattenHierarchy))
//		//		{
//		//			if (!prop.CanRead) continue;
//		//			if (Attribute.IsDefined(prop, typeof(JsonIgnoreAttribute))) continue;

//		//			// Skip the base "type" field — we already wrote it manually
//		//			if (prop.DeclaringType == typeof(MapAttachment) && prop.Name == "type") continue;

//		//			var propValue = prop.GetValue(value);
//		//			if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
//		//				continue;

//		//			var jsonProp = prop.GetCustomAttribute<JsonPropertyAttribute>();
//		//			string propertyName = jsonProp?.PropertyName ?? prop.Name;

//		//			writer.WritePropertyName(propertyName);
//		//			serializer.Serialize(writer, propValue);
//		//		}
//		//	}
//		//	finally
//		//	{
//		//		if (removed)
//		//			converters.Insert(0, this);
//		//	}

//		//	writer.WriteEndObject();
//		//}
//	}

//	//public class MapAttachmentConverter : JsonConverter
//	//{
//	//	public override bool CanConvert(Type objectType)
//	//	{
//	//		return typeof(MapAttachment).IsAssignableFrom(objectType);
//	//	}

//	//	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//	//	{
//	//		if (reader.TokenType == JsonToken.Null) return null;

//	//		var jo = JObject.Load(reader);
//	//		var typeName = jo["type"]?.ToString() ?? "Emitter";

//	//		MapAttachment result = typeName switch
//	//		{
//	//			"Emitter" => new Emitter(),
//	//			_ => new Emitter()
//	//		};

//	//		// Remove our converter temporarily to prevent recursion during Populate
//	//		var converters = serializer.Converters;
//	//		bool removed = converters.Remove(this);
//	//		try
//	//		{
//	//			serializer.Populate(jo.CreateReader(), result);
//	//		}
//	//		finally
//	//		{
//	//			if (removed) converters.Insert(0, this); // put it back
//	//		}

//	//		return result;
//	//	}

//	//	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//	//	{
//	//		// Temporarily remove ourselves from the converter list
//	//		var converters = serializer.Converters;
//	//		bool removed = converters.Remove(this);
//	//		try
//	//		{
//	//			serializer.Serialize(writer, value);
//	//		}
//	//		finally
//	//		{
//	//			if (removed) converters.Insert(0, this);
//	//		}
//	//	}
//	//}
//}