using System;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
#if !UNITY_WEBGL && !UNITY_EDITOR
	internal static class PlatformFileBrowser
	{
		internal static bool ExportAtomicMap(ClassicTilestorm.ResourceSerializer.AtomicMapExportData export, string defaultFolder = null)
		{
			if (export == null || !export.IsValid)
			{
				Debug.LogError("PlatformFileBrowser: invalid atomic export payload.");
				return false;
			}

			string path = ResolveTargetPath(defaultFolder, export);
			WriteExportToPath(export, path);
			Debug.Log($"{export.DisplayLabel} exported → {path}");
			return true;
		}

		private static string ResolveTargetPath(string defaultFolder, ClassicTilestorm.ResourceSerializer.AtomicMapExportData export)
		{
			string target = string.IsNullOrWhiteSpace(defaultFolder)
				? ClassicTilestorm.ApplicationSettings.UserFolder
				: defaultFolder;

			if (Path.HasExtension(target))
			{
				string directory = Path.GetDirectoryName(target);
				if (!string.IsNullOrWhiteSpace(directory))
					Directory.CreateDirectory(directory);
				return target;
			}

			Directory.CreateDirectory(target);
			return Path.Combine(target, export.FileName);
		}

		private static void WriteExportToPath(ClassicTilestorm.ResourceSerializer.AtomicMapExportData export, string path)
		{
			if (export.IsArchive)
			{
				if (File.Exists(path))
					File.Delete(path);

				File.WriteAllBytes(path, export.Archive);
				return;
			}

			File.WriteAllText(path, export.Json);
		}
	}
#endif
}
