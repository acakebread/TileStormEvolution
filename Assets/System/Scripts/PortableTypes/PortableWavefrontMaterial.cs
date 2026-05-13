//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Text;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	[Serializable]
//	public class PortableWavefrontMaterial
//	{
//		public string name = "WavefrontMaterial";
//		public string shader = "Universal Render Pipeline/Lit";
//		public List<PortableProperty> properties = new List<PortableProperty>();
//		public List<string> enabledKeywords = new List<string>();

//		public PortableWavefrontMaterial() { }

//		// === Import from MTL ===
//		public PortableWavefrontMaterial(string mtlPath)
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

//		// === NEW: Import from Unity Material (for export pipeline) ===
//		public void FromUnityMaterial(Material mat, string materialName = null)
//		{
//			if (mat == null) return;

//			name = materialName ?? mat.name ?? "TestMaterial";
//			shader = mat.shader?.name ?? "Universal Render Pipeline/Lit";

//			properties.Clear();
//			enabledKeywords.Clear();

//			// Reuse existing PortableMaterial logic to extract properties
//			var tempPortable = new PortableMaterial(mat, name);

//			properties = tempPortable.properties;
//			enabledKeywords = tempPortable.enabledKeywords;

//			Debug.Log($"Converted Unity Material '{name}' to PortableWavefrontMaterial with {properties.Count} properties.");
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
//						if (tokens.Length > 1 && float.TryParse(tokens[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ns))
//						{
//							float smoothness = Mathf.Clamp01(ns / 1000f);
//							AddFloatProperty("_Smoothness", smoothness);
//						}
//						break;

//					case "d":
//					case "tr":
//						if (tokens.Length > 1 && float.TryParse(tokens[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
//						{
//							float alpha = (cmd == "tr") ? 1f - val : val;
//							AddColorAlpha("_BaseColor", alpha);
//						}
//						break;

//					case "map_kd":
//						AddTextureProperty("_BaseMap", ExtractTextureName(line));
//						AddTextureProperty("_MainTex", ExtractTextureName(line));
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

//		// === Export to MTL ===
//		public void ExportToMtl(string filePath)
//		{
//			StringBuilder sb = new StringBuilder();
//			sb.AppendLine($"# Wavefront MTL exported by PortableWavefrontMaterial");
//			sb.AppendLine($"# Material: {name}");
//			sb.AppendLine();

//			sb.AppendLine($"newmtl {name}");

//			// Colors
//			WriteColor(sb, "Kd", GetColor("_BaseColor", Color.white));
//			WriteColor(sb, "Ke", GetColor("_EmissionColor", Color.black));
//			WriteColor(sb, "Ks", GetColor("_SpecColor", new Color(0.2f, 0.2f, 0.2f)));

//			// Scalars
//			float smoothness = GetFloat("_Smoothness", 0.5f);
//			sb.AppendLine($"Ns {(int)(smoothness * 1000f)}");

//			float alpha = GetColor("_BaseColor", Color.white).a;
//			if (alpha < 0.999f)
//				sb.AppendLine($"d {alpha}");

//			// Textures
//			WriteTexture(sb, "map_Kd", GetTextureName("_BaseMap"));
//			WriteTexture(sb, "map_Ke", GetTextureName("_EmissionMap"));
//			WriteTexture(sb, "map_Ks", GetTextureName("_SpecGlossMap"));
//			WriteTexture(sb, "map_bump", GetTextureName("_BumpMap"));

//			File.WriteAllText(filePath, sb.ToString());
//			Debug.Log($"✅ Material exported to Wavefront MTL: {filePath}");
//		}

//		private void WriteColor(StringBuilder sb, string mtlKey, Color color)
//		{
//			sb.AppendLine($"{mtlKey} {color.r:F4} {color.g:F4} {color.b:F4}");
//		}

//		private void WriteTexture(StringBuilder sb, string mtlKey, string textureName)
//		{
//			if (!string.IsNullOrEmpty(textureName))
//				sb.AppendLine($"{mtlKey} {textureName}.png");
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

//		// Helper methods (same as before)
//		private Color ParseColor(string[] tokens)
//		{ /* ... same as previous version ... */
//			float r = tokens.Length > 1 ? ParseFloat(tokens[1]) : 1f;
//			float g = tokens.Length > 2 ? ParseFloat(tokens[2]) : r;
//			float b = tokens.Length > 3 ? ParseFloat(tokens[3]) : r;
//			return new Color(r, g, b, 1f);
//		}

//		private float ParseFloat(string s)
//		{
//			return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f;
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
//			if (existing != null) existing.colorA = alpha;
//			else AddColorProperty(propName, new Color(1, 1, 1, alpha));
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
//			var mat = new Material(shaderObj) { name = name ?? "Wavefront Material" };

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