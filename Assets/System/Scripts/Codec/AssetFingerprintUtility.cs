using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class AssetFingerprintUtility
	{
		public static HashId GetContentHash(string assetPath)
		{
			if (string.IsNullOrWhiteSpace(assetPath))
				return 0;

			var normalized = Path.GetFullPath(assetPath);
			if (!File.Exists(normalized))
				return 0;

			using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			AppendPackage(hasher, normalized, visited);
			return ToHashId(hasher.GetHashAndReset());
		}

		public static HashId GetModelContentHash(string assetPath)
			=> GetContentHash(assetPath);

		private static void AppendPackage(IncrementalHash hasher, string path, HashSet<string> visited)
		{
			if (string.IsNullOrWhiteSpace(path))
				return;

			var fullPath = Path.GetFullPath(path);
			if (!File.Exists(fullPath) || !visited.Add(fullPath))
				return;

			switch (Path.GetExtension(fullPath).ToLowerInvariant())
			{
				case ".obj":
				case ".mtl":
				case ".json":
					AppendNormalizedText(hasher, File.ReadAllText(fullPath));
					break;

				default:
					AppendBytes(hasher, File.ReadAllBytes(fullPath));
					break;
			}

			switch (Path.GetExtension(fullPath).ToLowerInvariant())
			{
				case ".obj":
					foreach (var dep in GetObjDependencies(fullPath))
						AppendPackage(hasher, dep, visited);
					break;

				case ".mtl":
					foreach (var dep in GetMtlDependencies(fullPath))
						AppendPackage(hasher, dep, visited);
					break;

				case ".json":
					foreach (var dep in GetJsonDependencies(fullPath))
						AppendPackage(hasher, dep, visited);
					break;
			}

			var sidecarJson = Path.ChangeExtension(fullPath, ".json");
			if (!string.Equals(sidecarJson, fullPath, StringComparison.OrdinalIgnoreCase) && File.Exists(sidecarJson))
				AppendPackage(hasher, sidecarJson, visited);
		}

		private static IEnumerable<string> GetObjDependencies(string objPath)
		{
			try
			{
				var baseDir = Path.GetDirectoryName(objPath);
				if (string.IsNullOrWhiteSpace(baseDir))
					return Enumerable.Empty<string>();

				var deps = new List<string>();
				foreach (var rawLine in File.ReadAllLines(objPath))
				{
					var line = rawLine.Trim();
					if (!line.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
						continue;

					var rel = line.Substring(7).Trim();
					var resolved = ResolveRelativePath(baseDir, rel);
					if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
						deps.Add(resolved);
				}

				return deps.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"AssetFingerprintUtility: failed to inspect OBJ dependencies for '{objPath}': {ex.Message}");
				return Enumerable.Empty<string>();
			}
		}

		private static IEnumerable<string> GetMtlDependencies(string mtlPath)
		{
			try
			{
				var baseDir = Path.GetDirectoryName(mtlPath);
				if (string.IsNullOrWhiteSpace(baseDir))
					return Enumerable.Empty<string>();

				var deps = new List<string>();
				foreach (var rawLine in File.ReadAllLines(mtlPath))
				{
					var line = rawLine.Trim();
					if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
						continue;

					if (line.StartsWith("map_", StringComparison.OrdinalIgnoreCase) ||
						line.StartsWith("bump ", StringComparison.OrdinalIgnoreCase))
					{
						var textureRef = WavefrontMaterial.ExtractTextureName(line);
						if (string.IsNullOrWhiteSpace(textureRef))
							continue;

						var texturePath = ResolveTexturePath(baseDir, textureRef);
						if (!string.IsNullOrWhiteSpace(texturePath) && File.Exists(texturePath))
							deps.Add(texturePath);
					}
				}

				return deps.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"AssetFingerprintUtility: failed to inspect MTL dependencies for '{mtlPath}': {ex.Message}");
				return Enumerable.Empty<string>();
			}
		}

		private static IEnumerable<string> GetJsonDependencies(string jsonPath)
		{
			try
			{
				var directory = Path.GetDirectoryName(jsonPath);
				if (string.IsNullOrWhiteSpace(directory))
					return Enumerable.Empty<string>();

				var text = File.ReadAllText(jsonPath);
				if (string.IsNullOrWhiteSpace(text))
					return Enumerable.Empty<string>();

				var root = JObject.Parse(text);
				var deps = new List<string>();

				foreach (var token in root.DescendantsAndSelf().OfType<JProperty>())
				{
					if (!string.Equals(token.Name, "texture", StringComparison.OrdinalIgnoreCase))
						continue;

					if (token.Value?.Type != JTokenType.String)
						continue;

					var textureRef = token.Value.Value<string>();
					if (string.IsNullOrWhiteSpace(textureRef))
						continue;

					var texturePath = ResolveTexturePath(directory, textureRef);
					if (!string.IsNullOrWhiteSpace(texturePath) && File.Exists(texturePath))
						deps.Add(texturePath);
				}

				return deps.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"AssetFingerprintUtility: failed to inspect JSON dependencies for '{jsonPath}': {ex.Message}");
				return Enumerable.Empty<string>();
			}
		}

		private static string ResolveRelativePath(string baseDirectory, string relativePath)
		{
			if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(relativePath))
				return null;

			if (Path.IsPathRooted(relativePath))
				return Path.GetFullPath(relativePath);

			return Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
		}

		private static string ResolveTexturePath(string baseDirectory, string textureName)
		{
			if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(textureName))
				return null;

			string[] extensions = { "", ".png", ".jpg", ".jpeg", ".tga", ".bmp" };
			foreach (var ext in extensions)
			{
				var candidate = Path.Combine(baseDirectory, textureName + ext);
				if (File.Exists(candidate))
					return Path.GetFullPath(candidate);
			}

			return null;
		}

		private static void AppendBytes(IncrementalHash hasher, byte[] bytes)
		{
			if (hasher == null || bytes == null)
				return;

			byte[] lengthBytes = new byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, bytes.Length);
			hasher.AppendData(lengthBytes);
			hasher.AppendData(bytes);
		}

		private static void AppendNormalizedText(IncrementalHash hasher, string text)
		{
			if (hasher == null || string.IsNullOrEmpty(text))
				return;

			string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
			AppendBytes(hasher, Encoding.UTF8.GetBytes(normalized));
		}

		private static HashId ToHashId(byte[] digest)
		{
			if (digest == null || digest.Length < 4)
				return 0;

			int value = BinaryPrimitives.ReadInt32LittleEndian(digest);
			if (value == 0)
				value = 1;
			return value;
		}
	}
}
