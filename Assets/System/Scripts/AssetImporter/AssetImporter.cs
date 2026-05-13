using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class AssetImporter
	{
		public static string ImportWavefrontModel(string sourceObjPath)
		{
			if (string.IsNullOrEmpty(sourceObjPath) || !File.Exists(sourceObjPath))
			{
				Debug.LogError($"Source OBJ not found: {sourceObjPath}");
				return null;
			}

			string modelName = Path.GetFileNameWithoutExtension(sourceObjPath);
			string importRoot = Path.Combine(Application.persistentDataPath, "Imported", modelName);

			Directory.CreateDirectory(importRoot);

			string destObjPath = Path.Combine(importRoot, Path.GetFileName(sourceObjPath));
			File.Copy(sourceObjPath, destObjPath, true);

			Debug.Log($"Imported OBJ to: {destObjPath}");

			CopyDependenciesWithStructure(sourceObjPath, importRoot);

			Debug.Log($"✅ Import completed: {importRoot}");
			return destObjPath;
		}

		private static void CopyDependenciesWithStructure(string sourceObjPath, string importRoot)
		{
			string mtlRelative = FindMtlRelativePath(sourceObjPath);
			if (string.IsNullOrEmpty(mtlRelative))
			{
				Debug.LogWarning("No mtllib found in OBJ");
				return;
			}

			string sourceDir = Path.GetDirectoryName(sourceObjPath);
			string sourceMtlPath = Path.Combine(sourceDir, mtlRelative);

			if (!File.Exists(sourceMtlPath))
			{
				Debug.LogWarning($"MTL not found: {sourceMtlPath}");
				return;
			}

			// Copy MTL
			string destMtlPath = Path.Combine(importRoot, mtlRelative);
			Directory.CreateDirectory(Path.GetDirectoryName(destMtlPath));
			File.Copy(sourceMtlPath, destMtlPath, true);
			Debug.Log($"Copied MTL → {mtlRelative}");

			string mtlDestDir = Path.GetDirectoryName(destMtlPath);

			// Extract and copy textures relative to the .mtl location
			var textureRefs = ExtractTextureReferencesFromMtl(destMtlPath);

			string mtlSourceDir = Path.GetDirectoryName(sourceMtlPath);

			foreach (string texRef in textureRefs)
			{
				Debug.Log($"Found texture reference: '{texRef}'");

				string sourceTexPath = FindTextureFile(mtlSourceDir, texRef);
				if (string.IsNullOrEmpty(sourceTexPath))
				{
					Debug.LogWarning($"Could not find texture file for: {texRef}");
					continue;
				}

				// Place texture next to the .mtl file (important!)
				string destTexPath = Path.Combine(mtlDestDir, Path.GetFileName(texRef));

				File.Copy(sourceTexPath, destTexPath, true);
				Debug.Log($"✅ Copied texture → {Path.GetRelativePath(importRoot, destTexPath)}");
			}
		}

		private static string FindMtlRelativePath(string objPath)
		{
			string[] lines = File.ReadAllLines(objPath);
			foreach (string line in lines)
			{
				if (line.Trim().StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
				{
					string path = line.Trim().Substring(7).Trim();
					Debug.Log($"Found mtllib reference: {path}");
					return path;
				}
			}
			return null;
		}

		private static List<string> ExtractTextureReferencesFromMtl(string mtlPath)
		{
			var list = new List<string>();
			string[] lines = File.ReadAllLines(mtlPath);

			foreach (string line in lines)
			{
				string trimmed = line.Trim();
				if (trimmed.StartsWith("map_", StringComparison.OrdinalIgnoreCase) ||
					trimmed.StartsWith("bump ", StringComparison.OrdinalIgnoreCase))
				{
					string tex = WavefrontMaterial.ExtractTextureName(line);
					if (!string.IsNullOrEmpty(tex))
						list.Add(tex);
				}
			}
			return list;
		}

		private static string FindTextureFile(string baseDir, string texReference)
		{
			string fullPath = Path.Combine(baseDir, texReference);
			if (File.Exists(fullPath)) return fullPath;

			// Try common extensions
			string dir = Path.GetDirectoryName(fullPath);
			string nameNoExt = Path.GetFileNameWithoutExtension(fullPath);

			string[] exts = { ".jpg", ".png", ".jpeg", ".tga", ".bmp" };
			foreach (string ext in exts)
			{
				string test = Path.Combine(dir, nameNoExt + ext);
				if (File.Exists(test)) return test;
			}

			return null;
		}
	}
}