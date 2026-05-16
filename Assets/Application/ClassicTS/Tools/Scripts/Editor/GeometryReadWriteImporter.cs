using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm.Editor
{
	public class GeometryReadWriteImporter : AssetPostprocessor
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

			if (!TryGetWavefrontMaterialImport(assetPath, out var sourceMaterialName, out var baseColor, out var emissiveColor, out var emissionMap))
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

					if (!MaterialNameMatches(material.name, sourceMaterialName))
						continue;

					if (ApplyWavefrontMaterial(material, baseColor, emissiveColor, emissionMap))
					{
						patched++;
						Debug.Log($"GeometryReadWriteImporter: patched emissive material '{material.name}' from '{assetPath}' (source '{sourceMaterialName}')");
					}
				}
			}

			if (patched > 0)
				Debug.Log($"GeometryReadWriteImporter: patched {patched} emissive material(s) on '{assetPath}'.");
		}

		[MenuItem("Tools/Classic Tilestorm/Models/Geometry/Enable ReadWrite On Geometry Models")]
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

			Debug.Log($"GeometryReadWriteImporter: enabled Read/Write on {changed} model(s).");
		}

		private static bool TryGetWavefrontMaterialImport(string modelAssetPath, out string sourceMaterialName, out Color baseColor, out Color emissiveColor, out Texture2D emissionMap)
		{
			sourceMaterialName = null;
			baseColor = Color.white;
			emissiveColor = default;
			emissionMap = null;

			string fullPath = ToFullPath(modelAssetPath);
			if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
				return false;

			string[] lines = File.ReadAllLines(fullPath);
			string materialLibrary = null;
			string useMaterial = null;

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
							useMaterial = string.Join(" ", parts, 1, parts.Length - 1);
						break;
				}
			}

			sourceMaterialName = useMaterial ?? Path.GetFileNameWithoutExtension(modelAssetPath);
			string baseDirectory = Path.GetDirectoryName(fullPath);
			string mtlPath = ResolveMaterialPath(baseDirectory, materialLibrary, sourceMaterialName);
			if (string.IsNullOrWhiteSpace(mtlPath) || !File.Exists(mtlPath))
				return false;

			var wavefrontMaterial = new WavefrontMaterial(mtlPath);
			if (!TryGetWavefrontMaterialState(wavefrontMaterial, mtlPath, out baseColor, out emissiveColor, out emissionMap))
				return false;

			return true;
		}

		private static bool TryGetWavefrontMaterialState(WavefrontMaterial wavefrontMaterial, string mtlPath, out Color baseColor, out Color emissiveColor, out Texture2D emissiveMap)
		{
			baseColor = Color.white;
			emissiveColor = default;
			emissiveMap = null;

			if (wavefrontMaterial == null)
				return false;

			var baseColorProp = wavefrontMaterial.properties.FirstOrDefault(p => string.Equals(p?.name, "_BaseColor", System.StringComparison.OrdinalIgnoreCase));
			bool hasKeyword = wavefrontMaterial.enabledKeywords != null &&
							  wavefrontMaterial.enabledKeywords.Any(k => string.Equals(k, "_EMISSION", System.StringComparison.OrdinalIgnoreCase));
			bool hasBaseColor = baseColorProp != null;
			var emissionColorProp = wavefrontMaterial.properties.FirstOrDefault(p => string.Equals(p?.name, "_EmissionColor", System.StringComparison.OrdinalIgnoreCase));
			var emissionMapProp = wavefrontMaterial.properties.FirstOrDefault(p => string.Equals(p?.name, "_EmissionMap", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(p.texture));

			if (hasBaseColor)
				baseColor = new Color(baseColorProp.colorR, baseColorProp.colorG, baseColorProp.colorB, baseColorProp.colorA);

			bool hasColor = emissionColorProp != null &&
							(new Color(emissionColorProp.colorR, emissionColorProp.colorG, emissionColorProp.colorB, emissionColorProp.colorA).maxColorComponent > 0.01f);
			bool hasMap = emissionMapProp != null;

			if (!hasBaseColor && !hasKeyword && !hasColor && !hasMap)
				return false;

			if (hasColor)
			{
				emissiveColor = new Color(emissionColorProp.colorR, emissionColorProp.colorG, emissionColorProp.colorB, emissionColorProp.colorA);
			}
			else
			{
				emissiveColor = Color.white;
			}

			if (hasMap)
				emissiveMap = ResolveTextureAsset(Path.GetDirectoryName(mtlPath), emissionMapProp.texture);

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
