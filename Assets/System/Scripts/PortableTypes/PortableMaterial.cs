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

			enabledKeywords = mat.shaderKeywords?.Where(k => !string.IsNullOrEmpty(k)).ToList() ?? new List<string>();

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

					if (ShouldSerialize(prop, propType))
						properties.Add(prop);
				}
				catch { }
			}
		}

		private bool ShouldSerialize(PortableProperty p, UnityEngine.Rendering.ShaderPropertyType type)
		{
			if (string.IsNullOrEmpty(p.name)) return false;
			if (!string.IsNullOrEmpty(p.texture)) return true;
			if (type == UnityEngine.Rendering.ShaderPropertyType.Color) return true;
			if (Mathf.Abs(p.floatValue) > 0.001f) return true;
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

			// Textures first
			foreach (var p in properties.Where(p => !string.IsNullOrEmpty(p.texture)))
				p.ApplyTo(target);

			// Other properties
			foreach (var p in properties.Where(p => string.IsNullOrEmpty(p.texture)))
				p.ApplyTo(target);

			// Keywords
			if (enabledKeywords != null)
			{
				foreach (var kw in target.shaderKeywords)
					target.DisableKeyword(kw);

				foreach (var kw in enabledKeywords)
					if (!string.IsNullOrEmpty(kw))
						target.EnableKeyword(kw);
			}
		}
	}

	[Serializable]
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
					var tex = Resources.Load<Texture>(texture) ?? Resources.Load<Texture2D>(texture);
					if (tex != null)
					{
						mat.SetTexture(name, tex);

						// Critical sync for URP
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

			var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
			var safe = JsonSerializer.Create(settings);

			writer.WriteStartObject();

			if (!string.IsNullOrEmpty(pm.name)) { writer.WritePropertyName("name"); safe.Serialize(writer, pm.name); }
			writer.WritePropertyName("shader"); safe.Serialize(writer, pm.shader);
			if (pm.renderQueue != 2000) { writer.WritePropertyName("renderQueue"); safe.Serialize(writer, pm.renderQueue); }

			if (pm.enabledKeywords?.Count > 0)
			{
				writer.WritePropertyName("enabledKeywords");
				safe.Serialize(writer, pm.enabledKeywords);
			}

			if (pm.properties?.Count > 0)
			{
				writer.WritePropertyName("properties");
				safe.Serialize(writer, pm.properties);
			}

			writer.WriteEndObject();
		}
	}
}