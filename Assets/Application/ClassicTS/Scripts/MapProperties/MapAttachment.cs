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
		public virtual bool HasTransform => false;

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
				"View" => new View(),
				"Emitter" => new Emitter(),
				"Pickup" => new Pickup(),
				_ => new Pickup()
			};

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

				fieldList = fieldList.OrderBy(x => x.order).ThenBy(x => x.field.Name).ToList();

				foreach (var (field, _) in fieldList)
				{
					if (Attribute.IsDefined(field, typeof(JsonIgnoreAttribute))) continue;
					if (field.Name == nameof(MapAttachment.type)) continue;

					var fieldValue = field.GetValue(value);

					// SPECIAL HANDLING FOR 'data' ARRAY — 4 significant figures on rotation + distance
					if (field.Name == "data" && fieldValue is float[] dataArray && dataArray.Length == 7)
					{
						writer.WritePropertyName("data");
						WriteDataWithSignificantFigures(writer, dataArray, serializer);
						continue;
					}

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

		/// <summary>
		/// Writes the 7-element data array with reduced precision:
		/// - Indices 0-2 (position): full precision
		/// - Indices 3-5 (Quaternion3 rotation) and 6 (distance): rounded to 4 significant figures
		/// </summary>
		private static void WriteDataWithSignificantFigures(JsonWriter writer, float[] original, JsonSerializer serializer)
		{
			writer.WriteStartArray();

			for (int i = 0; i < 7; i++)
			{
				float val = original[i];

				if (i <= 2)
				{
					// Position: never touch precision
					serializer.Serialize(writer, val);
				}
				else
				{
					// Rotation components + Distance: 4 significant figures
					float reduced = RoundToSignificantFigures(val, 4);
					serializer.Serialize(writer, reduced);
				}
			}

			writer.WriteEndArray();
		}

		/// <summary>
		/// Rounds a float to a specified number of significant figures.
		/// Handles the full range including very small and very large numbers.
		/// </summary>
		private static float RoundToSignificantFigures(float value, int sigFigs)
		{
			if (value == 0f) return 0f;
			if (float.IsNaN(value) || float.IsInfinity(value)) return value;

			double d = value; // work in double for better intermediate precision

			double scale = Math.Pow(10, Math.Floor(Math.Log10(Math.Abs(d))) + 1 - sigFigs);
			double rounded = Math.Round(d / scale, 0, MidpointRounding.AwayFromZero) * scale;

			return (float)rounded;
		}
	}
}