using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	[Serializable]
	[JsonConverter(typeof(PortableMaterialConverter))]
	public class PortableMaterial
	{
		[JsonProperty(Order = 1)] public string name;
		[JsonProperty(Order = 2)] public string shader;
		[JsonProperty(Order = 3)] public int renderQueue = 2000;

		[JsonProperty(Order = 10)] public List<PortableProperty> properties = new List<PortableProperty>();
		[JsonProperty(Order = 11)] public List<string> enabledKeywords = new List<string>();

		public PortableMaterial() { }

		public PortableMaterial(Material source, string materialName = null)
		{
			FromUnityMaterial(source, materialName);
		}

		public void FromUnityMaterial(Material mat, string materialName = null)
		{
			if (mat == null) return;

			name = materialName ?? mat.name;
			shader = mat.shader?.name ?? "Universal Render Pipeline/Lit";
			renderQueue = mat.renderQueue;

			enabledKeywords = mat.shaderKeywords?
				.Where(k => !string.IsNullOrEmpty(k) && mat.IsKeywordEnabled(k))
				.ToList() ?? new List<string>();

			properties.Clear();

			int count = mat.shader.GetPropertyCount();
			for (int i = 0; i < count; i++)
			{
				string propName = mat.shader.GetPropertyName(i);
				if (propName.StartsWith("unity_") || propName.StartsWith("_XRMotionVectors")) continue;

				var propType = mat.shader.GetPropertyType(i);
				var prop = new PortableProperty { name = propName };

				try
				{
					switch (propType)
					{
						case UnityEngine.Rendering.ShaderPropertyType.Texture:
							var tex = mat.GetTexture(propName);
							prop.texture = tex != null ? tex.name : null;
							var scale = mat.GetTextureScale(propName);
							var offset = mat.GetTextureOffset(propName);
							prop.textureScaleX = scale.x;
							prop.textureScaleY = scale.y;
							prop.textureOffsetX = offset.x;
							prop.textureOffsetY = offset.y;
							break;

						case UnityEngine.Rendering.ShaderPropertyType.Color:
							var c = mat.GetColor(propName);
							prop.colorR = c.r; prop.colorG = c.g; prop.colorB = c.b; prop.colorA = c.a;
							break;

						case UnityEngine.Rendering.ShaderPropertyType.Vector:
							var v = mat.GetVector(propName);
							prop.vectorX = v.x; prop.vectorY = v.y; prop.vectorZ = v.z; prop.vectorW = v.w;
							break;

						case UnityEngine.Rendering.ShaderPropertyType.Float:
						case UnityEngine.Rendering.ShaderPropertyType.Range:
							prop.floatValue = mat.GetFloat(propName);
							break;

						case UnityEngine.Rendering.ShaderPropertyType.Int:
							prop.intValue = mat.GetInt(propName);
							break;
					}

					if (ShouldSerialize(prop, propType, mat))
						properties.Add(prop);
				}
				catch { }
			}
		}

		private bool ShouldSerialize(PortableProperty p, UnityEngine.Rendering.ShaderPropertyType type, Material mat)
		{
			if (string.IsNullOrEmpty(p.name)) return false;

			// Always save textures
			if (!string.IsNullOrEmpty(p.texture)) return true;

			// Skip default white colors
			if (type == UnityEngine.Rendering.ShaderPropertyType.Color)
			{
				return !(Mathf.Approximately(p.colorR, 1f) &&
						Mathf.Approximately(p.colorG, 1f) &&
						Mathf.Approximately(p.colorB, 1f) &&
						Mathf.Approximately(p.colorA, 1f));
			}

			// Skip default zero vectors
			if (type == UnityEngine.Rendering.ShaderPropertyType.Vector)
			{
				return !(Mathf.Approximately(p.vectorX, 0f) &&
						Mathf.Approximately(p.vectorY, 0f) &&
						Mathf.Approximately(p.vectorZ, 0f) &&
						Mathf.Approximately(p.vectorW, 0f));
			}

			// Skip default floats (0 or 1)
			if (Mathf.Abs(p.floatValue) > 0.001f && Mathf.Abs(p.floatValue - 1f) > 0.001f)
				return true;

			return false;
		}

		public Material ToUnityMaterial()
		{
			var shaderObj = Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Lit");
			var mat = new Material(shaderObj)
			{
				name = name ?? "Portable Material",
				renderQueue = renderQueue
			};

			ApplyTo(mat);
			MaterialUtils.ForceMaterialRefresh(mat);

			return mat;
		}

		public void ApplyTo(Material target)
		{
			if (target == null) return;

			foreach (var p in properties.Where(p => !string.IsNullOrEmpty(p.texture)))
				p.ApplyTo(target);

			foreach (var p in properties.Where(p => string.IsNullOrEmpty(p.texture)))
				p.ApplyTo(target);

			if (enabledKeywords != null)
			{
				foreach (var kw in target.shaderKeywords)
					target.DisableKeyword(kw);

				foreach (var kw in enabledKeywords)
					if (!string.IsNullOrEmpty(kw))
						target.EnableKeyword(kw);
			}

			ApplyEmissionState(target);
		}

		private static void ApplyEmissionState(Material target)
		{
			if (target == null) return;

			bool shouldEmit = target.IsKeywordEnabled("_EMISSION") ||
							 (target.HasProperty("_EmissionMap") && target.GetTexture("_EmissionMap") != null) ||
							 (target.HasProperty("_EmissionColor") && target.GetColor("_EmissionColor").maxColorComponent > 0.01f);

			if (shouldEmit)
			{
				target.EnableKeyword("_EMISSION");
				target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
			}
		}
	}

	[Serializable]
	[JsonConverter(typeof(PortablePropertyConverter))]
	public class PortableProperty
	{
		public string name;
		public string texture;
		public float textureScaleX = 1f;
		public float textureScaleY = 1f;
		public float textureOffsetX;
		public float textureOffsetY;

		public float colorR = 1f, colorG = 1f, colorB = 1f, colorA = 1f;
		public float vectorX, vectorY, vectorZ, vectorW;

		public float floatValue;
		public int intValue;

		public void ApplyTo(Material mat)
		{
			if (string.IsNullOrEmpty(name) || mat == null || !mat.HasProperty(name)) return;

			try
			{
				if (!string.IsNullOrEmpty(texture))
				{
					var tex = ResourceResolvers.TextureResolver?.Find(texture);
					if (tex == null)
						tex = Resources.Load<Texture>(texture);
					if (tex != null)
					{
						mat.SetTexture(name, tex);
						if (name == "_BaseMap" || name == "_MainTex")
						{
							mat.mainTexture = tex;
							if (name == "_BaseMap") mat.SetTexture("_MainTex", tex);
							else mat.SetTexture("_BaseMap", tex);
						}
						mat.SetTextureScale(name, new Vector2(textureScaleX, textureScaleY));
						mat.SetTextureOffset(name, new Vector2(textureOffsetX, textureOffsetY));
					}
				}
				else if (name.ToLowerInvariant().Contains("color"))
				{
					mat.SetColor(name, new Color(colorR, colorG, colorB, colorA));
				}
				else if (name.ToLowerInvariant().Contains("vector"))
				{
					mat.SetVector(name, new Vector4(vectorX, vectorY, vectorZ, vectorW));
				}
				else
				{
					mat.SetFloat(name, floatValue);
					if (intValue != 0) mat.SetInt(name, intValue);
				}
			}
			catch { }
		}
	}

	public class PortableMaterialConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType) => objectType == typeof(PortableMaterial);

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;
			var jo = JObject.Load(reader);
			var pm = new PortableMaterial();
			serializer.Populate(jo.CreateReader(), pm);
			return pm;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var pm = (PortableMaterial)value;
			if (pm == null) { writer.WriteNull(); return; }

			var settings = new JsonSerializerSettings
			{
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
				DefaultValueHandling = DefaultValueHandling.Ignore,
				NullValueHandling = NullValueHandling.Ignore,
				ContractResolver = new PortableMaterialContractResolver()
			};

			var safeSerializer = JsonSerializer.Create(settings);

			writer.WriteStartObject();

			if (!string.IsNullOrEmpty(pm.name))
			{
				writer.WritePropertyName("name");
				safeSerializer.Serialize(writer, pm.name);
			}

			writer.WritePropertyName("shader");
			safeSerializer.Serialize(writer, pm.shader);

			if (pm.renderQueue != 2000)
			{
				writer.WritePropertyName("renderQueue");
				safeSerializer.Serialize(writer, pm.renderQueue);
			}

			if (pm.enabledKeywords?.Count > 0)
			{
				writer.WritePropertyName("enabledKeywords");
				safeSerializer.Serialize(writer, pm.enabledKeywords);
			}

			if (pm.properties?.Count > 0)
			{
				writer.WritePropertyName("properties");
				safeSerializer.Serialize(writer, pm.properties);
			}

			writer.WriteEndObject();
		}
	}

	// Custom Contract Resolver to skip default values
	public class PortableMaterialContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
	{
		protected override Newtonsoft.Json.Serialization.JsonProperty CreateProperty(
			System.Reflection.MemberInfo member, Newtonsoft.Json.MemberSerialization memberSerialization)
		{
			var property = base.CreateProperty(member, memberSerialization);

			if (member.DeclaringType == typeof(PortableProperty))
			{
				property.ShouldSerialize = instance =>
				{
					if (instance is not PortableProperty p) return true;
					return ShouldSerializePortablePropertyField(member.Name, p);
				};
			}

			return property;
		}

		private bool ShouldSerializePortablePropertyField(string memberName, PortableProperty p)
		{
			if (string.IsNullOrEmpty(p.name)) return false;

			switch (memberName)
			{
				case nameof(PortableProperty.name):
					return !string.IsNullOrEmpty(p.name);

				case nameof(PortableProperty.texture):
					return !string.IsNullOrEmpty(p.texture);

				case nameof(PortableProperty.textureScaleX):
					return !Mathf.Approximately(p.textureScaleX, 1f);

				case nameof(PortableProperty.textureScaleY):
					return !Mathf.Approximately(p.textureScaleY, 1f);

				case nameof(PortableProperty.textureOffsetX):
					return !Mathf.Approximately(p.textureOffsetX, 0f);

				case nameof(PortableProperty.textureOffsetY):
					return !Mathf.Approximately(p.textureOffsetY, 0f);

				case nameof(PortableProperty.colorR):
				case nameof(PortableProperty.colorG):
				case nameof(PortableProperty.colorB):
				case nameof(PortableProperty.colorA):
					return !(Mathf.Approximately(p.colorR, 1f) &&
						Mathf.Approximately(p.colorG, 1f) &&
						Mathf.Approximately(p.colorB, 1f) &&
						Mathf.Approximately(p.colorA, 1f));

				case nameof(PortableProperty.vectorX):
				case nameof(PortableProperty.vectorY):
				case nameof(PortableProperty.vectorZ):
				case nameof(PortableProperty.vectorW):
					return !(Mathf.Approximately(p.vectorX, 0f) &&
						Mathf.Approximately(p.vectorY, 0f) &&
						Mathf.Approximately(p.vectorZ, 0f) &&
						Mathf.Approximately(p.vectorW, 0f));

				case nameof(PortableProperty.floatValue):
					return !Mathf.Approximately(p.floatValue, 0f) && !Mathf.Approximately(p.floatValue, 1f);

				case nameof(PortableProperty.intValue):
					return p.intValue != 0;

				default:
					return true;
			}
		}
	}

	public class PortablePropertyConverter : JsonConverter
	{
		public override bool CanConvert(Type objectType) => objectType == typeof(PortableProperty);

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null) return null;
			var jo = JObject.Load(reader);
			var prop = new PortableProperty();
			serializer.Populate(jo.CreateReader(), prop);
			return prop;
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var p = (PortableProperty)value;
			if (p == null) { writer.WriteNull(); return; }

			writer.WriteStartObject();

			if (!string.IsNullOrEmpty(p.name))
			{
				writer.WritePropertyName("name");
				writer.WriteValue(p.name);
			}

			if (!string.IsNullOrEmpty(p.texture))
			{
				writer.WritePropertyName("texture");
				writer.WriteValue(p.texture);

				if (!Mathf.Approximately(p.textureScaleX, 1f))
				{
					writer.WritePropertyName("textureScaleX");
					writer.WriteValue(p.textureScaleX);
				}

				if (!Mathf.Approximately(p.textureScaleY, 1f))
				{
					writer.WritePropertyName("textureScaleY");
					writer.WriteValue(p.textureScaleY);
				}

				if (!Mathf.Approximately(p.textureOffsetX, 0f))
				{
					writer.WritePropertyName("textureOffsetX");
					writer.WriteValue(p.textureOffsetX);
				}

				if (!Mathf.Approximately(p.textureOffsetY, 0f))
				{
					writer.WritePropertyName("textureOffsetY");
					writer.WriteValue(p.textureOffsetY);
				}
			}
			else if (p.name != null && p.name.ToLowerInvariant().Contains("color"))
			{
				if (!Mathf.Approximately(p.colorR, 1f) || !Mathf.Approximately(p.colorG, 1f) ||
					!Mathf.Approximately(p.colorB, 1f) || !Mathf.Approximately(p.colorA, 1f))
				{
					writer.WritePropertyName("colorR");
					writer.WriteValue(p.colorR);
					writer.WritePropertyName("colorG");
					writer.WriteValue(p.colorG);
					writer.WritePropertyName("colorB");
					writer.WriteValue(p.colorB);
					writer.WritePropertyName("colorA");
					writer.WriteValue(p.colorA);
				}
			}
			else if (p.name != null && p.name.ToLowerInvariant().Contains("vector"))
			{
				if (!Mathf.Approximately(p.vectorX, 0f) || !Mathf.Approximately(p.vectorY, 0f) ||
					!Mathf.Approximately(p.vectorZ, 0f) || !Mathf.Approximately(p.vectorW, 0f))
				{
					writer.WritePropertyName("vectorX");
					writer.WriteValue(p.vectorX);
					writer.WritePropertyName("vectorY");
					writer.WriteValue(p.vectorY);
					writer.WritePropertyName("vectorZ");
					writer.WriteValue(p.vectorZ);
					writer.WritePropertyName("vectorW");
					writer.WriteValue(p.vectorW);
				}
			}
			else
			{
				if (!Mathf.Approximately(p.floatValue, 0f) && !Mathf.Approximately(p.floatValue, 1f))
				{
					writer.WritePropertyName("floatValue");
					writer.WriteValue(p.floatValue);
				}

				if (p.intValue != 0)
				{
					writer.WritePropertyName("intValue");
					writer.WriteValue(p.intValue);
				}
			}

			writer.WriteEndObject();
		}
	}
}
