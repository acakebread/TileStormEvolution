using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	[Serializable]
	public class PortableWavefrontMaterial
	{
		public string name = "WavefrontMaterial";
		public string shader = "Universal Render Pipeline/Lit";
		public List<PortableProperty> properties = new List<PortableProperty>();
		public List<string> enabledKeywords = new List<string>();

		public PortableWavefrontMaterial() { }

		public PortableWavefrontMaterial(string mtlPath)
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
						// We currently take the first material defined
						break;

					// === Colors ===
					case "kd": // Diffuse / Albedo
						if (tokens.Length >= 4)
							AddColorProperty("_BaseColor", ParseColor(tokens));
						break;

					case "ke": // Emission
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

					case "ks": // Specular
						if (tokens.Length >= 4)
							AddColorProperty("_SpecColor", ParseColor(tokens));
						break;

					case "ka": // Ambient (often ignored)
						break;

					// === Scalars ===
					case "ns": // Specular exponent → Smoothness
						if (tokens.Length > 1 && float.TryParse(tokens[1],
							System.Globalization.NumberStyles.Float,
							System.Globalization.CultureInfo.InvariantCulture, out float ns))
						{
							float smoothness = Mathf.Clamp01(ns / 1000f);
							AddFloatProperty("_Smoothness", smoothness);
						}
						break;

					case "d":
					case "tr": // Transparency / Dissolve
						if (tokens.Length > 1 && float.TryParse(tokens[1],
							System.Globalization.NumberStyles.Float,
							System.Globalization.CultureInfo.InvariantCulture, out float val))
						{
							float alpha = (cmd == "tr") ? 1f - val : val;
							// For now we just set base color alpha. Full transparency setup can be extended later.
							AddColorAlpha("_BaseColor", alpha);
						}
						break;

					// === Texture Maps ===
					case "map_kd":
						AddTextureProperty("_BaseMap", ExtractTextureName(line));
						AddTextureProperty("_MainTex", ExtractTextureName(line)); // alias
						break;

					case "map_ke":
						AddTextureProperty("_EmissionMap", ExtractTextureName(line));
						enabledKeywords.Add("_EMISSION");
						break;

					case "map_ks":
						AddTextureProperty("_SpecGlossMap", ExtractTextureName(line)); // or _MetallicGlossMap depending on workflow
						break;

					case "map_bump":
					case "bump":
						AddTextureProperty("_BumpMap", ExtractTextureName(line));
						AddFloatProperty("_BumpScale", 1f);
						break;

					case "map_d":
						AddTextureProperty("_BaseMap", ExtractTextureName(line)); // alpha in diffuse
						break;

						// You can easily extend this list later
				}
			}

			name = currentMaterialName;

			// Clean up duplicates (in case map_Kd and map_d both hit _BaseMap)
			RemoveDuplicateProperties();
		}

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

			if (parts.Length == 0) return null;

			// Take the last token (handles MTL options like -o, -s, -mm etc.)
			string filename = parts[^1];
			return Path.GetFileNameWithoutExtension(filename);
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

			// Avoid adding exact duplicate
			if (properties.Exists(p => p.name == propName && p.texture == texName))
				return;

			var prop = new PortableProperty
			{
				name = propName,
				texture = texName,
				textureScaleX = 1f,
				textureScaleY = 1f
			};
			properties.Add(prop);
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

		public Material ToUnityMaterial()
		{
			var shaderObj = Shader.Find(shader) ?? Shader.Find("Universal Render Pipeline/Lit");
			var mat = new Material(shaderObj) { name = name ?? "Wavefront Material" };

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