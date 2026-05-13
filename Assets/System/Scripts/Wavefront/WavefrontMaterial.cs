using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MassiveHadronLtd
{
	[Serializable]
	public class WavefrontProperty
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
	}

	[Serializable]
	public class WavefrontMaterial
	{
		public string name = "Material";
		public string shader = "Universal Render Pipeline/Lit";
		public List<WavefrontProperty> properties = new List<WavefrontProperty>();
		public List<string> enabledKeywords = new List<string>();

		public WavefrontMaterial() { }

		// Load from .mtl file
		public WavefrontMaterial(string mtlPath)
		{
			FromMtlFile(mtlPath);
		}

		public void FromMtlFile(string mtlPath)
		{
			if (!File.Exists(mtlPath))
			{
				Debug.LogError($"MTL file not found: {mtlPath}");
				return;
			}

			string[] lines = File.ReadAllLines(mtlPath);
			ParseMtlLines(lines);
		}

		// Convert from Unity Material (for export)
		public void FromUnityMaterial(Material mat, string materialName = null)
		{
			if (mat == null) return;

			name = materialName ?? mat.name ?? "Material";
			shader = mat.shader?.name ?? "Universal Render Pipeline/Lit";

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
				var prop = new WavefrontProperty { name = propName };

				try
				{
					switch (propType)
					{
						case UnityEngine.Rendering.ShaderPropertyType.Texture:
							var tex = mat.GetTexture(propName);
							if (tex != null)
							{
								prop.texture = tex.name;
								var scale = mat.GetTextureScale(propName);
								var offset = mat.GetTextureOffset(propName);
								prop.textureScaleX = scale.x;
								prop.textureScaleY = scale.y;
								prop.textureOffsetX = offset.x;
								prop.textureOffsetY = offset.y;
							}
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

					if (ShouldSerializeProperty(prop, propType))
						properties.Add(prop);
				}
				catch { }
			}

			Debug.Log($"[WavefrontMaterial] Converted Unity material '{name}' → {properties.Count} properties");
		}

		private bool ShouldSerializeProperty(WavefrontProperty p, UnityEngine.Rendering.ShaderPropertyType type)
		{
			if (string.IsNullOrEmpty(p.name)) return false;
			if (!string.IsNullOrEmpty(p.texture)) return true;

			if (type == UnityEngine.Rendering.ShaderPropertyType.Color)
			{
				return !(Mathf.Approximately(p.colorR, 1f) && Mathf.Approximately(p.colorG, 1f) &&
						Mathf.Approximately(p.colorB, 1f) && Mathf.Approximately(p.colorA, 1f));
			}

			if (type == UnityEngine.Rendering.ShaderPropertyType.Vector)
			{
				return !(Mathf.Approximately(p.vectorX, 0f) && Mathf.Approximately(p.vectorY, 0f) &&
						Mathf.Approximately(p.vectorZ, 0f) && Mathf.Approximately(p.vectorW, 0f));
			}

			if (Mathf.Abs(p.floatValue) > 0.001f && Mathf.Abs(p.floatValue - 1f) > 0.001f)
				return true;

			return p.intValue != 0;
		}

		private void ParseMtlLines(string[] lines)
		{
			properties.Clear();
			enabledKeywords.Clear();

			string currentMaterialName = "default";

			foreach (string rawLine in lines)
			{
				string line = rawLine.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

				string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length == 0) continue;

				string cmd = tokens[0].ToLowerInvariant();

				switch (cmd)
				{
					case "newmtl":
						currentMaterialName = tokens.Length > 1 ? tokens[1] : "default";
						break;

					case "kd":
						if (tokens.Length >= 4)
							AddColorProperty("_BaseColor", ParseColor(tokens));
						break;

					case "ke":
						if (tokens.Length >= 4)
						{
							Color emission = ParseColor(tokens);
							if (emission.maxColorComponent > 0.01f)
							{
								AddColorProperty("_EmissionColor", emission);
								enabledKeywords.Add("_EMISSION");
							}
						}
						break;

					case "ks":
						if (tokens.Length >= 4)
							AddColorProperty("_SpecColor", ParseColor(tokens));
						break;

					case "ns":
						if (tokens.Length > 1 && float.TryParse(tokens[1], System.Globalization.NumberStyles.Float,
							System.Globalization.CultureInfo.InvariantCulture, out float ns))
						{
							float smoothness = Mathf.Clamp01(ns / 1000f);
							AddFloatProperty("_Smoothness", smoothness);
						}
						break;

					case "d":
					case "tr":
						if (tokens.Length > 1 && float.TryParse(tokens[1], System.Globalization.NumberStyles.Float,
							System.Globalization.CultureInfo.InvariantCulture, out float val))
						{
							float alpha = (cmd == "tr") ? 1f - val : val;
							AddColorAlpha("_BaseColor", alpha);
						}
						break;

					case "map_kd":
						string tex = ExtractTextureName(line);
						AddTextureProperty("_BaseMap", tex);
						AddTextureProperty("_MainTex", tex);
						break;

					case "map_ke":
						AddTextureProperty("_EmissionMap", ExtractTextureName(line));
						enabledKeywords.Add("_EMISSION");
						break;

					case "map_ks":
						AddTextureProperty("_SpecGlossMap", ExtractTextureName(line));
						break;

					case "map_bump":
					case "bump":
						AddTextureProperty("_BumpMap", ExtractTextureName(line));
						AddFloatProperty("_BumpScale", 1f);
						break;

					case "map_d":
						AddTextureProperty("_BaseMap", ExtractTextureName(line));
						break;
				}
			}

			name = currentMaterialName;
			RemoveDuplicateProperties();
		}

		public void ExportToMtl(string filePath)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"# Wavefront MTL exported by WavefrontMaterial");
			sb.AppendLine($"# Material: {name}");
			sb.AppendLine();

			sb.AppendLine($"newmtl {name}");

			WriteColor(sb, "Kd", GetColor("_BaseColor", Color.white));
			WriteColor(sb, "Ke", GetColor("_EmissionColor", Color.black));
			WriteColor(sb, "Ks", GetColor("_SpecColor", new Color(0.2f, 0.2f, 0.2f)));

			float smoothness = GetFloat("_Smoothness", 0.5f);
			sb.AppendLine($"Ns {(int)(smoothness * 1000f)}");

			float alpha = GetColor("_BaseColor", Color.white).a;
			if (alpha < 0.999f)
				sb.AppendLine($"d {alpha:F4}");

			WriteTexture(sb, "map_Kd", GetTextureName("_BaseMap"));
			WriteTexture(sb, "map_Ke", GetTextureName("_EmissionMap"));
			WriteTexture(sb, "map_Ks", GetTextureName("_SpecGlossMap"));
			WriteTexture(sb, "map_bump", GetTextureName("_BumpMap"));

			File.WriteAllText(filePath, sb.ToString());
			Debug.Log($"✅ Exported Wavefront MTL: {filePath}");
		}

		// ====================== DISK TEXTURE LOADING ======================
		public static Texture2D TryLoadTexture(string baseDirectory, string textureName)
		{
			if (string.IsNullOrEmpty(textureName)) return null;

			string[] candidates =
			{
				Path.Combine(baseDirectory, textureName),
				Path.Combine(baseDirectory, textureName + ".png"),
				Path.Combine(baseDirectory, textureName + ".jpg"),
				Path.Combine(baseDirectory, textureName + ".jpeg"),
				Path.Combine(baseDirectory, textureName + ".tga")
			};

			foreach (string path in candidates)
			{
				if (!File.Exists(path)) continue;

				byte[] data = File.ReadAllBytes(path);
				Texture2D tex = null;

				string ext = Path.GetExtension(path).ToLowerInvariant();

				if (ext == ".tga")
				{
					tex = TgaLoader.LoadTGA(data);
				}
				else
				{
					tex = new Texture2D(2, 2);
					if (!tex.LoadImage(data))
						tex = null;
				}

				if (tex != null)
				{
					tex.name = textureName;
					Debug.Log($"[Wavefront] Loaded texture: {path}");
					return tex;
				}
			}

			Debug.LogWarning($"Could not load texture: {textureName}");
			return null;
		}

		// ====================== Helper Methods ======================
		private Color ParseColor(string[] tokens)
		{
			float r = tokens.Length > 1 ? ParseFloat(tokens[1]) : 1f;
			float g = tokens.Length > 2 ? ParseFloat(tokens[2]) : r;
			float b = tokens.Length > 3 ? ParseFloat(tokens[3]) : r;
			return new Color(r, g, b, 1f);
		}

		private float ParseFloat(string s)
		{
			return float.TryParse(s, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;
		}

		public static string ExtractTextureName(string line)
		{
			int spaceIndex = line.IndexOfAny(new[] { ' ', '\t' });
			if (spaceIndex == -1) return null;
			string rest = line.Substring(spaceIndex + 1).Trim();
			var parts = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			return parts.Length > 0 ? parts[^1] : null;
		}

		private void AddColorProperty(string propName, Color color)
		{
			var prop = new WavefrontProperty { name = propName };
			prop.colorR = color.r; prop.colorG = color.g; prop.colorB = color.b; prop.colorA = color.a;
			properties.Add(prop);
		}

		private void AddColorAlpha(string propName, float alpha)
		{
			var existing = properties.Find(p => p.name == propName);
			if (existing != null)
				existing.colorA = alpha;
			else
				AddColorProperty(propName, new Color(1, 1, 1, alpha));
		}

		private void AddFloatProperty(string propName, float value)
		{
			properties.Add(new WavefrontProperty { name = propName, floatValue = value });
		}

		private void AddTextureProperty(string propName, string texName)
		{
			if (string.IsNullOrEmpty(texName)) return;
			if (properties.Exists(p => p.name == propName && p.texture == texName)) return;

			properties.Add(new WavefrontProperty
			{
				name = propName,
				texture = texName,
				textureScaleX = 1f,
				textureScaleY = 1f
			});
		}

		private void RemoveDuplicateProperties()
		{
			var unique = new List<WavefrontProperty>();
			var seen = new HashSet<string>();
			foreach (var p in properties)
			{
				string key = p.name + "|" + (p.texture ?? "");
				if (!seen.Contains(key))
				{
					seen.Add(key);
					unique.Add(p);
				}
			}
			properties = unique;
		}

		private void WriteColor(StringBuilder sb, string key, Color c)
		{
			sb.AppendLine($"{key} {c.r:F4} {c.g:F4} {c.b:F4}");
		}

		private void WriteTexture(StringBuilder sb, string key, string texName)
		{
			if (!string.IsNullOrEmpty(texName))
				sb.AppendLine($"{key} {texName}.png");
		}

		private Color GetColor(string propName, Color defaultColor)
		{
			var prop = properties.Find(p => p.name == propName);
			return prop != null ? new Color(prop.colorR, prop.colorG, prop.colorB, prop.colorA) : defaultColor;
		}

		private float GetFloat(string propName, float defaultValue)
		{
			var prop = properties.Find(p => p.name == propName);
			return prop != null ? prop.floatValue : defaultValue;
		}

		private string GetTextureName(string propName)
		{
			var prop = properties.Find(p => p.name == propName && !string.IsNullOrEmpty(p.texture));
			return prop?.texture;
		}

		public Material ToUnityMaterial()
		{
			var shaderObj = Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Lit");
			var mat = new Material(shaderObj) { name = name ?? "Material" };

			ApplyTo(mat);
			MaterialUtils.ForceMaterialRefresh(mat);

			return mat;
		}

		public void ApplyTo(Material target)
		{
			if (target == null) return;

			foreach (var p in properties)
			{
				if (string.IsNullOrEmpty(p.name) || !target.HasProperty(p.name)) continue;

				try
				{
					if (!string.IsNullOrEmpty(p.texture))
					{
						var tex = Resources.Load<Texture>(p.texture) ?? Resources.Load<Texture2D>(p.texture);
						if (tex != null)
						{
							target.SetTexture(p.name, tex);
							if (p.name == "_BaseMap" || p.name == "_MainTex")
							{
								target.mainTexture = tex;
								if (p.name == "_BaseMap") target.SetTexture("_MainTex", tex);
								else target.SetTexture("_BaseMap", tex);
							}
							target.SetTextureScale(p.name, new Vector2(p.textureScaleX, p.textureScaleY));
							target.SetTextureOffset(p.name, new Vector2(p.textureOffsetX, p.textureOffsetY));
						}
					}
					else if (p.name.ToLowerInvariant().Contains("color"))
					{
						target.SetColor(p.name, new Color(p.colorR, p.colorG, p.colorB, p.colorA));
					}
					else if (p.name.ToLowerInvariant().Contains("vector"))
					{
						target.SetVector(p.name, new Vector4(p.vectorX, p.vectorY, p.vectorZ, p.vectorW));
					}
					else
					{
						target.SetFloat(p.name, p.floatValue);
						if (p.intValue != 0) target.SetInt(p.name, p.intValue);
					}
				}
				catch { }
			}

			// Apply keywords
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
}