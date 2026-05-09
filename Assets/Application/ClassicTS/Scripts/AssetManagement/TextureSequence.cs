using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	[Serializable]
	[JsonConverter(typeof(TextureSequenceConverter))]
	public class TextureSequence
	{
		public string id;
		public string name { get => id; }//future replacement for id - just the display name in the editor
		public bool alphaTest = false;

		// Canonical single texture (shorthand)
		public string texture;

		// Only used for real animated sequences
		public TextureFrame[] frames;

		private TextureFrame[] _resolvedFrames;

		[JsonIgnore]
		public TextureFrame[] ResolvedFrames
		{
			get
			{
				if (_resolvedFrames != null) return _resolvedFrames;

				if (!string.IsNullOrEmpty(texture))
				{
					_resolvedFrames = new[] { new TextureFrame { textureName = texture, duration = 0f } };
				}
				else
				{
					_resolvedFrames = frames?.Length > 0 ? frames : Array.Empty<TextureFrame>();
				}
				return _resolvedFrames;
			}
		}

		internal void SetResolvedFrames(TextureFrame[] resolved)
		{
			_resolvedFrames = resolved;
		}

		[JsonIgnore] public bool bAlphaTest => alphaTest;
		[JsonIgnore] public Texture2D FirstTexture => ResolvedFrames.Length > 0 ? ResolvedFrames[0].texture : null;
	}

	// Custom converter — this is what fixes loading AND saving
	public class TextureSequenceConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(TextureSequence);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;

			var jo = JObject.Load(reader);

			var result = new TextureSequence
			{
				id = jo["id"]?.ToString(),
				alphaTest = jo["alphaTest"]?.Value<bool>() ?? false
			};

			var textureToken = jo["texture"];
			var framesToken = jo["frames"];

			if (textureToken != null && textureToken.Type != JTokenType.Null)
			{
				result.texture = textureToken.ToString();
				result.frames = null;
			}
			else if (framesToken != null && framesToken.Type == JTokenType.Array)
			{
				var framesArray = (JArray)framesToken;

				if (framesArray.Count == 1)
				{
					var first = framesArray[0];
					var texName = first["texture"]?.ToString();
					var duration = first["duration"]?.Value<float>() ?? 0f;

					if (!string.IsNullOrEmpty(texName) && Math.Abs(duration) < 0.001f)
					{
						// Convert legacy single-frame → modern shorthand
						result.texture = texName;
						result.frames = null;
						return result;
					}
				}

				// Real animated sequence
				result.frames = framesToken.ToObject<TextureFrame[]>(serializer);
			}

			return result;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var ts = (TextureSequence)value;

			writer.WriteStartObject();

			writer.WritePropertyName("id");
			writer.WriteValue(ts.id);

			writer.WritePropertyName("name");
			writer.WriteValue(ts.id);

			if (ts.alphaTest)
			{
				writer.WritePropertyName("alphaTest");
				writer.WriteValue(true);
			}

			if (!string.IsNullOrEmpty(ts.texture))
			{
				writer.WritePropertyName("texture");
				writer.WriteValue(ts.texture);
			}
			else if (ts.frames != null && ts.frames.Length > 0)
			{
				writer.WritePropertyName("frames");
				serializer.Serialize(writer, ts.frames);
			}

			writer.WriteEndObject();
		}
	}
}