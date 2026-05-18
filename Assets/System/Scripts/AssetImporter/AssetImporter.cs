using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicTilestorm;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class WavefrontAssetImporter
	{
		public enum ImportOption
		{
			Root = 0,
			AssetFileNameFolder = 1,
			HashIdFolder = 2
		}

		/// <summary>
		/// Imports a Wavefront .obj file and its dependencies.
		/// </summary>
		/// <param name="sourceObjPath">Full path to the source .obj file</param>
		/// <param name="importOption">Folder strategy for the imported asset set.</param>
		public static string ImportWavefrontModel(string sourceObjPath, ImportOption importOption = ImportOption.HashIdFolder, string destinationRoot = null, bool registerImported = true)
		{
			if (string.IsNullOrEmpty(sourceObjPath) || !File.Exists(sourceObjPath))
			{
				Debug.LogError($"Source OBJ not found: {sourceObjPath}");
				return null;
			}

			string objFileName = Path.GetFileName(sourceObjPath);
			string modelName = Path.GetFileNameWithoutExtension(sourceObjPath);
			string importFolderName = GetImportFolderName(sourceObjPath, modelName, importOption);

			string rootBase = string.IsNullOrWhiteSpace(destinationRoot)
				? ApplicationSettings.SystemModelsFolder
				: destinationRoot;

			string importRoot = string.IsNullOrEmpty(importFolderName)
				? rootBase
				: Path.Combine(rootBase, importFolderName);

			Directory.CreateDirectory(importRoot);

			string destObjPath = Path.Combine(importRoot, objFileName);
			File.Copy(sourceObjPath, destObjPath, true);

			Debug.Log($"Imported OBJ to: {destObjPath}");

			CopyDependenciesWithStructure(sourceObjPath, importRoot);
			if (registerImported)
				ModelResourceTable.RegisterImported(ExtractImportHash(sourceObjPath, importOption), destObjPath);

			Debug.Log($"Import completed: {importRoot}");
			return destObjPath;
		}

		private static string GetImportFolderName(string sourceObjPath, string modelName, ImportOption importOption)
		{
			switch (importOption)
			{
				case ImportOption.AssetFileNameFolder:
					return SanitizeFolderName(modelName);

				case ImportOption.HashIdFolder:
				{
					var contentHash = AssetFingerprintUtility.GetModelContentHash(sourceObjPath);
					return contentHash == 0 ? HTB50.EncodeFixed(RadixHash.GetStableHash32(Path.GetFileNameWithoutExtension(sourceObjPath)), 6)
						: HTB50.EncodeFixed(contentHash, 6);
				}

				case ImportOption.Root:
				default:
					return null;
			}
		}

		private static string ExtractImportHash(string sourceObjPath, ImportOption importOption)
		{
			var fileName = Path.GetFileNameWithoutExtension(sourceObjPath);
			switch (importOption)
			{
				case ImportOption.AssetFileNameFolder:
					return HTB50.EncodeFixed(RadixHash.GetStableHash32(fileName), 6);

				case ImportOption.HashIdFolder:
				{
					var contentHash = AssetFingerprintUtility.GetModelContentHash(sourceObjPath);
					return contentHash == 0 ? HTB50.EncodeFixed(RadixHash.GetStableHash32(fileName), 6)
						: HTB50.EncodeFixed(contentHash, 6);
				}

				case ImportOption.Root:
				default:
					return HTB50.EncodeFixed(RadixHash.GetStableHash32(fileName), 6);
			}
		}

		private static string SanitizeFolderName(string folderName)
		{
			if (string.IsNullOrWhiteSpace(folderName))
				return null;

			char[] invalidChars = Path.GetInvalidFileNameChars();
			char[] chars = folderName.Trim().ToCharArray();

			for (int i = 0; i < chars.Length; i++)
			{
				if (Array.IndexOf(invalidChars, chars[i]) >= 0)
					chars[i] = '_';
			}

			return new string(chars);
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

			string destMtlPath = Path.Combine(importRoot, mtlRelative);
			Directory.CreateDirectory(Path.GetDirectoryName(destMtlPath));
			File.Copy(sourceMtlPath, destMtlPath, true);
			Debug.Log($"Copied MTL -> {mtlRelative}");

			string mtlDestDir = Path.GetDirectoryName(destMtlPath);

			var textureRefs = ExtractTextureReferencesFromMtl(destMtlPath);
			string mtlSourceDir = Path.GetDirectoryName(sourceMtlPath);
			var visitedAnimationTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (string texRef in textureRefs)
			{
				Debug.Log($"Found texture reference: '{texRef}'");

				string sourceTexPath = FindTextureFile(mtlSourceDir, texRef);
				if (string.IsNullOrEmpty(sourceTexPath))
				{
					Debug.LogWarning($"Could not find texture: {texRef}");
					continue;
				}

				string destTexPath = Path.Combine(mtlDestDir, Path.GetFileName(texRef));

				File.Copy(sourceTexPath, destTexPath, true);
				Debug.Log($"Copied texture -> {Path.GetRelativePath(importRoot, destTexPath)}");

				CopyAnimationSidecars(sourceTexPath, destTexPath, visitedAnimationTextures);
			}
		}

		private static void CopyAnimationSidecars(string sourceTexturePath, string destTexturePath, HashSet<string> visitedTextures)
		{
			if (string.IsNullOrWhiteSpace(sourceTexturePath) || string.IsNullOrWhiteSpace(destTexturePath))
				return;

			string normalizedSourceTexture = Path.GetFullPath(sourceTexturePath);
			if (visitedTextures != null && !visitedTextures.Add(normalizedSourceTexture))
				return;

			string sourceJsonPath = Path.ChangeExtension(sourceTexturePath, ".json");
			if (!File.Exists(sourceJsonPath))
				return;

			string destJsonPath = Path.ChangeExtension(destTexturePath, ".json");
			Directory.CreateDirectory(Path.GetDirectoryName(destJsonPath));
			File.Copy(sourceJsonPath, destJsonPath, true);
			Debug.Log($"Copied animation json -> {Path.GetRelativePath(Path.GetDirectoryName(destTexturePath), destJsonPath)}");

			foreach (string animationTexture in ExtractAnimationTextureReferences(sourceJsonPath))
			{
				string sourceDir = Path.GetDirectoryName(sourceJsonPath);
				string sourceFrameTexture = FindTextureFile(sourceDir, animationTexture);
				if (string.IsNullOrWhiteSpace(sourceFrameTexture))
				{
					Debug.LogWarning($"Could not find animation texture: {animationTexture}");
					continue;
				}

				string destFrameTexture = Path.Combine(Path.GetDirectoryName(destJsonPath), Path.GetFileName(sourceFrameTexture));
				File.Copy(sourceFrameTexture, destFrameTexture, true);
				Debug.Log($"Copied animation texture -> {Path.GetRelativePath(Path.GetDirectoryName(destTexturePath), destFrameTexture)}");

				CopyAnimationSidecars(sourceFrameTexture, destFrameTexture, visitedTextures);
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

		private static IEnumerable<string> ExtractAnimationTextureReferences(string jsonPath)
		{
			try
			{
				string json = File.ReadAllText(jsonPath);
				if (string.IsNullOrWhiteSpace(json))
					return Enumerable.Empty<string>();

				var root = JObject.Parse(json);
				var textures = root
					.DescendantsAndSelf()
					.OfType<JProperty>()
					.Where(prop => string.Equals(prop.Name, "texture", StringComparison.OrdinalIgnoreCase))
					.Select(prop => prop.Value?.Type == JTokenType.String ? prop.Value.Value<string>() : null)
					.Where(value => !string.IsNullOrWhiteSpace(value))
					.ToList();

				return textures;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Failed to parse animation json '{jsonPath}': {ex.Message}");
				return Enumerable.Empty<string>();
			}
		}

		private static string FindTextureFile(string baseDir, string texReference)
		{
			string fullPath = Path.Combine(baseDir, texReference);
			if (File.Exists(fullPath)) return fullPath;

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
