using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	internal static class SharedMapExchange
	{
		internal sealed class SharedMapEntry
		{
			public string FilePath { get; }
			public string FileName { get; }
			public DateTime LastWriteTimeUtc { get; }
			public long LengthBytes { get; }

			public SharedMapEntry(string filePath)
			{
				FilePath = filePath;
				FileName = Path.GetFileName(filePath);

				try
				{
					var info = new FileInfo(filePath);
					LastWriteTimeUtc = info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
					LengthBytes = info.Exists ? info.Length : 0L;
				}
				catch
				{
					LastWriteTimeUtc = DateTime.MinValue;
					LengthBytes = 0L;
				}
			}
		}

		private static readonly string[] AllowedExtensions = { ".json", ".zip" };

		internal static string SharedFolder => ApplicationSettings.SharedMapsFolder;

		internal static string PublishCurrentMap(Map map, bool crop = true, bool padded = false, bool verbose = false)
		{
			if (map == null)
				return null;

			var export = ResourceSerializer.ExportAtomicMap(map, crop: crop, padded: padded, verbose: verbose);
			if (export == null || !export.IsValid)
				return null;

			string folder = FileUtils.EnsureFolder(SharedFolder);
			string targetPath = Path.Combine(folder, export.FileName);

			WriteExport(targetPath, export);
			return targetPath;
		}

		internal static IReadOnlyList<SharedMapEntry> GetSharedMapEntries()
		{
			string folder = FileUtils.EnsureFolder(SharedFolder);
			if (!Directory.Exists(folder))
				return Array.Empty<SharedMapEntry>();

			try
			{
				return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
					.Where(IsSupportedMapFile)
					.Select(path => new SharedMapEntry(path))
					.OrderByDescending(entry => entry.LastWriteTimeUtc)
					.ThenBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
					.ToArray();
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"SharedMapExchange: failed to enumerate shared maps in '{folder}': {ex.Message}");
				return Array.Empty<SharedMapEntry>();
			}
		}

		internal static Map ImportSharedMap(string filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
				return null;

			if (!File.Exists(filePath))
			{
				Debug.LogError($"SharedMapExchange: file not found -> {filePath}");
				return null;
			}

			return ResourceSerializer.ImportAtomicMap(filePath);
		}

		internal static Map ImportLatestSharedMap()
		{
			var latest = GetSharedMapEntries().FirstOrDefault();
			return latest == null ? null : ImportSharedMap(latest.FilePath);
		}

		private static bool IsSupportedMapFile(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return false;

			string extension = Path.GetExtension(path);
			return AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
		}

		private static void WriteExport(string targetPath, ResourceSerializer.AtomicMapExportData export)
		{
			FileUtils.EnsureFolder(Path.GetDirectoryName(targetPath));

			if (export.IsArchive)
			{
				if (File.Exists(targetPath))
					File.Delete(targetPath);

				File.WriteAllBytes(targetPath, export.Archive);
			}
			else
			{
				File.WriteAllText(targetPath, export.Json);
			}

			Debug.Log($"SharedMapExchange: published {export.DisplayLabel} -> {targetPath}");
		}
	}
}
