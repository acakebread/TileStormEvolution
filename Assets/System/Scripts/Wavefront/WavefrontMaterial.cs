using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MassiveHadronLtd
{
	[Serializable]
	public class WavefrontMaterial
	{
		public string name = "Material";
		public string shader = "Universal Render Pipeline/Lit";
		public List<PortableProperty> properties = new List<PortableProperty>();
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

			var tempPortable = new PortableMaterial(mat, name);
			properties = tempPortable.properties;
			enabledKeywords = tempPortable.enabledKeywords;

			Debug.Log($"[WavefrontMaterial] Converted Unity material '{name}' → {properties.Count} properties");
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
				Path.Combine(baseDirectory, textureName + ".jpeg")
			};

			foreach (string path in candidates)
			{
				if (File.Exists(path))
				{
					byte[] data = File.ReadAllBytes(path);
					Texture2D tex = new Texture2D(2, 2);
					if (tex.LoadImage(data))
					{
						tex.name = textureName;
						Debug.Log($"[Wavefront] Loaded texture from disk: {path}");
						return tex;
					}
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

		private string ExtractTextureName(string line)
		{
			int spaceIndex = line.IndexOfAny(new[] { ' ', '\t' });
			if (spaceIndex == -1) return null;
			string rest = line.Substring(spaceIndex + 1).Trim();
			var parts = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			return parts.Length > 0 ? parts[^1] : null;
		}

		private void AddColorProperty(string propName, Color color)
		{
			var prop = new PortableProperty { name = propName };
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
			properties.Add(new PortableProperty { name = propName, floatValue = value });
		}

		private void AddTextureProperty(string propName, string texName)
		{
			if (string.IsNullOrEmpty(texName)) return;
			if (properties.Exists(p => p.name == propName && p.texture == texName)) return;

			properties.Add(new PortableProperty
			{
				name = propName,
				texture = texName,
				textureScaleX = 1f,
				textureScaleY = 1f
			});
		}

		private void RemoveDuplicateProperties()
		{
			var unique = new List<PortableProperty>();
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

			var portable = new PortableMaterial
			{
				name = name,
				shader = shader,
				properties = properties,
				enabledKeywords = enabledKeywords
			};

			portable.ApplyTo(mat);
			MaterialUtils.ForceMaterialRefresh(mat);

			return mat;
		}
	}
}

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	[Serializable]
//	public class WavefrontMaterial
//	{
//		public string name = "Material";
//		public string shader = "Universal Render Pipeline/Lit";
//		public List<PortableProperty> properties = new List<PortableProperty>();
//		public List<string> enabledKeywords = new List<string>();

//		public WavefrontMaterial() { }

//		// Load from .mtl file
//		public WavefrontMaterial(string mtlPath)
//		{
//			FromMtlFile(mtlPath);
//		}

//		public void FromMtlFile(string mtlPath)
//		{
//			if (!File.Exists(mtlPath))
//			{
//				Debug.LogError($"MTL file not found: {mtlPath}");
//				return;
//			}

//			string[] lines = File.ReadAllLines(mtlPath);
//			ParseMtlLines(lines);
//		}

//		// Convert from Unity Material (for export)
//		public void FromUnityMaterial(Material mat, string materialName = null)
//		{
//			if (mat == null) return;

//			name = materialName ?? mat.name ?? "Material";
//			shader = mat.shader?.name ?? "Universal Render Pipeline/Lit";

//			var tempPortable = new PortableMaterial(mat, name);
//			properties = tempPortable.properties;
//			enabledKeywords = tempPortable.enabledKeywords;

//			Debug.Log($"[WavefrontMaterial] Converted Unity material '{name}' → {properties.Count} properties");
//		}

//		private void ParseMtlLines(string[] lines)
//		{
//			properties.Clear();
//			enabledKeywords.Clear();

//			string currentMaterialName = "default";

//			foreach (string rawLine in lines)
//			{
//				string line = rawLine.Trim();
//				if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

//				string[] tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
//				if (tokens.Length == 0) continue;

//				string cmd = tokens[0].ToLowerInvariant();

//				switch (cmd)
//				{
//					case "newmtl":
//						currentMaterialName = tokens.Length > 1 ? tokens[1] : "default";
//						break;

//					case "kd":
//						if (tokens.Length >= 4)
//							AddColorProperty("_BaseColor", ParseColor(tokens));
//						break;

//					case "ke":
//						if (tokens.Length >= 4)
//						{
//							Color emission = ParseColor(tokens);
//							if (emission.maxColorComponent > 0.01f)
//							{
//								AddColorProperty("_EmissionColor", emission);
//								enabledKeywords.Add("_EMISSION");
//							}
//						}
//						break;

//					case "ks":
//						if (tokens.Length >= 4)
//							AddColorProperty("_SpecColor", ParseColor(tokens));
//						break;

//					case "ns":
//						if (tokens.Length > 1 && float.TryParse(tokens[1], System.Globalization.NumberStyles.Float,
//							System.Globalization.CultureInfo.InvariantCulture, out float ns))
//						{
//							float smoothness = Mathf.Clamp01(ns / 1000f);
//							AddFloatProperty("_Smoothness", smoothness);
//						}
//						break;

//					case "d":
//					case "tr":
//						if (tokens.Length > 1 && float.TryParse(tokens[1], System.Globalization.NumberStyles.Float,
//							System.Globalization.CultureInfo.InvariantCulture, out float val))
//						{
//							float alpha = (cmd == "tr") ? 1f - val : val;
//							AddColorAlpha("_BaseColor", alpha);
//						}
//						break;

//					case "map_kd":
//						string tex = ExtractTextureName(line);
//						AddTextureProperty("_BaseMap", tex);
//						AddTextureProperty("_MainTex", tex);
//						break;

//					case "map_ke":
//						AddTextureProperty("_EmissionMap", ExtractTextureName(line));
//						enabledKeywords.Add("_EMISSION");
//						break;

//					case "map_ks":
//						AddTextureProperty("_SpecGlossMap", ExtractTextureName(line));
//						break;

//					case "map_bump":
//					case "bump":
//						AddTextureProperty("_BumpMap", ExtractTextureName(line));
//						AddFloatProperty("_BumpScale", 1f);
//						break;

//					case "map_d":
//						AddTextureProperty("_BaseMap", ExtractTextureName(line));
//						break;
//				}
//			}

//			name = currentMaterialName;
//			RemoveDuplicateProperties();
//		}

//		public void ExportToMtl(string filePath)
//		{
//			StringBuilder sb = new StringBuilder();
//			sb.AppendLine($"# Wavefront MTL exported by WavefrontMaterial");
//			sb.AppendLine($"# Material: {name}");
//			sb.AppendLine();

//			sb.AppendLine($"newmtl {name}");

//			WriteColor(sb, "Kd", GetColor("_BaseColor", Color.white));
//			WriteColor(sb, "Ke", GetColor("_EmissionColor", Color.black));
//			WriteColor(sb, "Ks", GetColor("_SpecColor", new Color(0.2f, 0.2f, 0.2f)));

//			float smoothness = GetFloat("_Smoothness", 0.5f);
//			sb.AppendLine($"Ns {(int)(smoothness * 1000f)}");

//			float alpha = GetColor("_BaseColor", Color.white).a;
//			if (alpha < 0.999f)
//				sb.AppendLine($"d {alpha:F4}");

//			WriteTexture(sb, "map_Kd", GetTextureName("_BaseMap"));
//			WriteTexture(sb, "map_Ke", GetTextureName("_EmissionMap"));
//			WriteTexture(sb, "map_Ks", GetTextureName("_SpecGlossMap"));
//			WriteTexture(sb, "map_bump", GetTextureName("_BumpMap"));

//			File.WriteAllText(filePath, sb.ToString());
//			Debug.Log($"✅ Exported Wavefront MTL: {filePath}");
//		}

//		private void WriteColor(StringBuilder sb, string key, Color c)
//		{
//			sb.AppendLine($"{key} {c.r:F4} {c.g:F4} {c.b:F4}");
//		}

//		private void WriteTexture(StringBuilder sb, string key, string texName)
//		{
//			if (!string.IsNullOrEmpty(texName))
//				sb.AppendLine($"{key} {texName}.png");
//		}

//		private Color GetColor(string propName, Color defaultColor)
//		{
//			var prop = properties.Find(p => p.name == propName);
//			return prop != null ? new Color(prop.colorR, prop.colorG, prop.colorB, prop.colorA) : defaultColor;
//		}

//		private float GetFloat(string propName, float defaultValue)
//		{
//			var prop = properties.Find(p => p.name == propName);
//			return prop != null ? prop.floatValue : defaultValue;
//		}

//		private string GetTextureName(string propName)
//		{
//			var prop = properties.Find(p => p.name == propName && !string.IsNullOrEmpty(p.texture));
//			return prop?.texture;
//		}

//		private Color ParseColor(string[] tokens)
//		{
//			float r = tokens.Length > 1 ? ParseFloat(tokens[1]) : 1f;
//			float g = tokens.Length > 2 ? ParseFloat(tokens[2]) : r;
//			float b = tokens.Length > 3 ? ParseFloat(tokens[3]) : r;
//			return new Color(r, g, b, 1f);
//		}

//		private float ParseFloat(string s)
//		{
//			return float.TryParse(s, System.Globalization.NumberStyles.Float,
//				System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;
//		}

//		private string ExtractTextureName(string line)
//		{
//			int spaceIndex = line.IndexOfAny(new[] { ' ', '\t' });
//			if (spaceIndex == -1) return null;
//			string rest = line.Substring(spaceIndex + 1).Trim();
//			var parts = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
//			return parts.Length > 0 ? Path.GetFileNameWithoutExtension(parts[^1]) : null;
//		}

//		private void AddColorProperty(string propName, Color color)
//		{
//			var prop = new PortableProperty { name = propName };
//			prop.colorR = color.r; prop.colorG = color.g; prop.colorB = color.b; prop.colorA = color.a;
//			properties.Add(prop);
//		}

//		private void AddColorAlpha(string propName, float alpha)
//		{
//			var existing = properties.Find(p => p.name == propName);
//			if (existing != null)
//				existing.colorA = alpha;
//			else
//				AddColorProperty(propName, new Color(1, 1, 1, alpha));
//		}

//		private void AddFloatProperty(string propName, float value)
//		{
//			properties.Add(new PortableProperty { name = propName, floatValue = value });
//		}

//		private void AddTextureProperty(string propName, string texName)
//		{
//			if (string.IsNullOrEmpty(texName)) return;
//			if (properties.Exists(p => p.name == propName && p.texture == texName)) return;

//			properties.Add(new PortableProperty
//			{
//				name = propName,
//				texture = texName,
//				textureScaleX = 1f,
//				textureScaleY = 1f
//			});
//		}

//		private void RemoveDuplicateProperties()
//		{
//			var unique = new List<PortableProperty>();
//			var seen = new HashSet<string>();
//			foreach (var p in properties)
//			{
//				string key = p.name + "|" + (p.texture ?? "");
//				if (!seen.Contains(key))
//				{
//					seen.Add(key);
//					unique.Add(p);
//				}
//			}
//			properties = unique;
//		}

//		public Material ToUnityMaterial()
//		{
//			var shaderObj = Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Lit");
//			var mat = new Material(shaderObj) { name = name ?? "Material" };

//			var portable = new PortableMaterial
//			{
//				name = name,
//				shader = shader,
//				properties = properties,
//				enabledKeywords = enabledKeywords
//			};

//			portable.ApplyTo(mat);
//			MaterialUtils.ForceMaterialRefresh(mat);

//			return mat;
//		}
//	}
//}
