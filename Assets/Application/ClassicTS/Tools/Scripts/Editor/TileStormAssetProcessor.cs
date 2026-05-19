using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm.Editor
{
	public class TileStormAssetProcessor : AssetPostprocessor
	{
		private static bool IsGeometryModelPath(string path)
		{
			if (string.IsNullOrEmpty(path)) return false;

			var normalized = path.Replace('\\', '/');
			return normalized.EndsWith(".fbx") || normalized.EndsWith(".obj");
		}

		void OnPreprocessModel()
		{
			if (!IsGeometryModelPath(assetPath))
				return;

			if (assetImporter is not ModelImporter importer)
				return;

			if (!importer.isReadable)
				importer.isReadable = true;
		}

		void OnPostprocessModel(GameObject gameObject)
		{
			if (!IsGeometryModelPath(assetPath) || gameObject == null)
				return;

			if (!TryGetWavefrontMaterialImports(assetPath, out var imports) || imports.Count == 0)
				return;

			var renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			var patched = 0;

			foreach (var renderer in renderers)
			{
				var materials = renderer.sharedMaterials;
				if (materials == null || materials.Length == 0)
					continue;

				for (var i = 0; i < materials.Length; i++)
				{
					var material = materials[i];
					if (material == null)
						continue;

					var import = imports.FirstOrDefault(candidate => MaterialNameMatches(material.name, candidate.SourceMaterialName));
					if (import == null)
						continue;

					if (ApplyWavefrontMaterial(material, import.BaseColor, import.EmissiveColor, import.EmissionMap))
					{
						patched++;
						Debug.Log($"TileStormAssetProcessor: patched emissive material '{material.name}' from '{assetPath}' (source '{import.SourceMaterialName}')");
					}
				}
			}

			if (patched > 0)
				Debug.Log($"TileStormAssetProcessor: patched {patched} emissive material(s) on '{assetPath}'.");
		}

		[MenuItem("Tools/Classic Tilestorm/Asset Processing/Enable ReadWrite On Geometry Models")]
		private static void EnableReadWriteOnGeometryModels()
		{
			var modelPaths = AssetDatabase
				.FindAssets("t:Model")
				.Select(AssetDatabase.GUIDToAssetPath)
				.Where(IsGeometryModelPath)
				.ToArray();

			var changed = 0;
			foreach (var path in modelPaths)
			{
				if (AssetImporter.GetAtPath(path) is not ModelImporter importer || importer.isReadable)
					continue;

				importer.isReadable = true;
				importer.SaveAndReimport();
				changed++;
			}

			Debug.Log($"TileStormAssetProcessor: enabled Read/Write on {changed} model(s).");
		}

		private sealed class WavefrontMaterialImport
		{
			public string SourceMaterialName;
			public Color BaseColor = Color.white;
			public Color EmissiveColor = default;
			public Texture2D EmissionMap;
		}

		private static bool TryGetWavefrontMaterialImports(string modelAssetPath, out List<WavefrontMaterialImport> imports)
		{
			imports = null;

			string fullPath = ToFullPath(modelAssetPath);
			if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
				return false;

			string[] lines = File.ReadAllLines(fullPath);
			string materialLibrary = null;
			var useMaterials = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

			foreach (var rawLine in lines)
			{
				var line = rawLine.Trim();
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", System.StringComparison.Ordinal))
					continue;

				var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
					continue;

				switch (parts[0].ToLowerInvariant())
				{
					case "mtllib":
						if (parts.Length > 1)
							materialLibrary = string.Join(" ", parts, 1, parts.Length - 1);
						break;
					case "usemtl":
						if (parts.Length > 1)
							useMaterials.Add(string.Join(" ", parts, 1, parts.Length - 1));
						break;
				}
			}

			string baseDirectory = Path.GetDirectoryName(fullPath);
			string fallbackMaterialName = Path.GetFileNameWithoutExtension(modelAssetPath);
			string mtlPath = ResolveMaterialPath(baseDirectory, materialLibrary, useMaterials.FirstOrDefault() ?? fallbackMaterialName);
			if (string.IsNullOrWhiteSpace(mtlPath) || !File.Exists(mtlPath))
				return false;

			if (useMaterials.Count == 0)
				useMaterials.Add(fallbackMaterialName);

			imports = new List<WavefrontMaterialImport>();
			foreach (var useMaterial in useMaterials)
			{
				if (!TryGetWavefrontMaterialState(mtlPath, useMaterial, out var baseColor, out var emissiveColor, out var emissionMap))
					continue;

				imports.Add(new WavefrontMaterialImport
				{
					SourceMaterialName = useMaterial,
					BaseColor = baseColor,
					EmissiveColor = emissiveColor,
					EmissionMap = emissionMap
				});
			}

			if (imports.Count == 0)
				return false;

			return true;
		}

		private static bool TryGetWavefrontMaterialState(string mtlPath, string sourceMaterialName, out Color baseColor, out Color emissiveColor, out Texture2D emissiveMap)
		{
			baseColor = Color.white;
			emissiveColor = default;
			emissiveMap = null;

			if (string.IsNullOrWhiteSpace(mtlPath) || !File.Exists(mtlPath) || string.IsNullOrWhiteSpace(sourceMaterialName))
				return false;

			bool inRequestedMaterial = false;
			bool foundMaterialSection = false;
			bool hasBaseColor = false;
			bool hasColor = false;
			bool hasMap = false;
			string emissionMapName = null;

			foreach (var rawLine in File.ReadAllLines(mtlPath))
			{
				var line = rawLine.Trim();
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", System.StringComparison.Ordinal))
					continue;

				var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
					continue;

				var command = parts[0].ToLowerInvariant();
				if (command == "newmtl")
				{
					var materialName = parts.Length > 1 ? string.Join(" ", parts, 1, parts.Length - 1) : string.Empty;
					if (foundMaterialSection && inRequestedMaterial)
						break;

					inRequestedMaterial = string.Equals(materialName, sourceMaterialName, System.StringComparison.OrdinalIgnoreCase);
					foundMaterialSection |= inRequestedMaterial;
					continue;
				}

				if (!inRequestedMaterial)
					continue;

				switch (command)
				{
					case "kd":
						if (TryParseColor(parts, out var parsedBaseColor))
						{
							baseColor = parsedBaseColor;
							hasBaseColor = true;
						}
						break;

					case "ke":
						if (TryParseColor(parts, out var parsedEmissionColor) && parsedEmissionColor.maxColorComponent > 0.01f)
						{
							emissiveColor = parsedEmissionColor;
							hasColor = true;
						}
						break;

					case "map_ke":
						emissionMapName = WavefrontMaterial.ExtractTextureName(line);
						hasMap = !string.IsNullOrWhiteSpace(emissionMapName);
						break;
				}
			}

			if (!foundMaterialSection || (!hasBaseColor && !hasColor && !hasMap))
				return false;

			if (!hasColor && hasMap)
				emissiveColor = Color.white;

			if (hasMap)
				emissiveMap = ResolveTextureAsset(Path.GetDirectoryName(mtlPath), emissionMapName);

			return hasColor || emissiveMap != null;
		}

		private static bool TryParseColor(string[] parts, out Color color)
		{
			color = default;
			if (parts == null || parts.Length < 4)
				return false;

			if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var r) ||
				!float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var g) ||
				!float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
				return false;

			color = new Color(r, g, b, 1f);
			return true;
		}

		private static bool ApplyWavefrontMaterial(Material material, Color baseColor, Color emissiveColor, Texture2D emissionMap)
		{
			if (material == null)
				return false;

			bool changed = false;

			if (material.HasProperty("_BaseColor"))
			{
				material.SetColor("_BaseColor", baseColor);
				changed = true;
			}
			else if (material.HasProperty("_Color"))
			{
				material.SetColor("_Color", baseColor);
				changed = true;
			}

			if (material.HasProperty("_EmissionColor"))
			{
				material.SetColor("_EmissionColor", emissiveColor);
				changed = true;
			}

			if (material.HasProperty("_EmissionMap") && emissionMap != null)
			{
				material.SetTexture("_EmissionMap", emissionMap);
				changed = true;
			}

			if (material.HasProperty("_BaseMap") && emissionMap != null && material.GetTexture("_BaseMap") == null)
			{
				material.SetTexture("_BaseMap", emissionMap);
				changed = true;
			}

			if (material.HasProperty("_MainTex") && emissionMap != null && material.GetTexture("_MainTex") == null)
			{
				material.SetTexture("_MainTex", emissionMap);
				changed = true;
			}

			material.EnableKeyword("_EMISSION");
			material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
			MaterialUtils.ForceMaterialRefresh(material);
			EditorUtility.SetDirty(material);
			return changed;
		}

		private static bool MaterialNameMatches(string importedName, string sourceName)
		{
			if (string.IsNullOrWhiteSpace(importedName) || string.IsNullOrWhiteSpace(sourceName))
				return false;

			string left = importedName.Trim();
			string right = sourceName.Trim();
			return string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase) ||
				   string.Equals(left, Path.GetFileNameWithoutExtension(right), System.StringComparison.OrdinalIgnoreCase) ||
				   string.Equals(Path.GetFileNameWithoutExtension(left), right, System.StringComparison.OrdinalIgnoreCase);
		}

		private static Texture2D ResolveTextureAsset(string baseDirectory, string textureName)
		{
			if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(textureName))
				return null;

			string[] extensions = { "", ".png", ".jpg", ".jpeg", ".tga", ".bmp" };
			foreach (var ext in extensions)
			{
				var fullPath = Path.Combine(baseDirectory, textureName + ext);
				if (!File.Exists(fullPath))
					continue;

				var assetPath = ToAssetPath(fullPath);
				if (string.IsNullOrWhiteSpace(assetPath))
					continue;

				var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
				if (tex != null)
					return tex;
			}

			return null;
		}

		private static string ResolveMaterialPath(string baseDirectory, string materialLibrary, string sourceMaterialName)
		{
			string fromMtllib = ResolveRelativePath(baseDirectory, materialLibrary);
			if (!string.IsNullOrWhiteSpace(fromMtllib))
				return fromMtllib;

			if (!string.IsNullOrWhiteSpace(sourceMaterialName))
				return Path.Combine(baseDirectory, sourceMaterialName + ".mtl");

			return null;
		}

		private static string ResolveRelativePath(string baseDirectory, string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				return null;

			if (Path.IsPathRooted(relativePath))
				return relativePath;

			return Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
		}

		private static string ToFullPath(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
				return null;

			var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
			return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
		}

		private static string ToAssetPath(string fullPath)
		{
			if (string.IsNullOrWhiteSpace(fullPath))
				return null;

			var normalized = fullPath.Replace('\\', '/');
			var assetsRoot = Application.dataPath.Replace('\\', '/');
			if (!normalized.StartsWith(assetsRoot, System.StringComparison.OrdinalIgnoreCase))
				return null;

			return "Assets" + normalized.Substring(assetsRoot.Length);
		}
	}
}
