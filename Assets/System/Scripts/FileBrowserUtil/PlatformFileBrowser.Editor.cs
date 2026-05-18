using System;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
#if UNITY_EDITOR
	internal static class PlatformFileBrowser
	{
		internal static bool ExportAtomicMap(ClassicTilestorm.ResourceSerializer.AtomicMapExportData export, string defaultFolder = null)
		{
			if (export == null || !export.IsValid)
			{
				Debug.LogError("PlatformFileBrowser: invalid atomic export payload.");
				return false;
			}

			ResolveEditorTarget(defaultFolder, export, out string folder, out string suggestedName, out string suggestedExtension);

			Directory.CreateDirectory(folder);
			string path = UnityEditor.EditorUtility.SaveFilePanel(
				export.DefaultDialogTitle,
				folder,
				suggestedName,
				suggestedExtension);

			if (string.IsNullOrWhiteSpace(path))
			{
				Debug.Log("Export cancelled by user.");
				return false;
			}

			WriteExportToPath(export, path);
			UnityEditor.EditorUtility.DisplayDialog("Export Successful", $"Map exported successfully!\n\nPath: {path}", "OK");
			return true;
		}

		private static void ResolveEditorTarget(string defaultFolder, ClassicTilestorm.ResourceSerializer.AtomicMapExportData export, out string folder, out string suggestedName, out string suggestedExtension)
		{
			string fallbackFolder = ClassicTilestorm.ApplicationSettings.UserFolder;
			string target = string.IsNullOrWhiteSpace(defaultFolder) ? fallbackFolder : defaultFolder;

			if (Path.HasExtension(target))
			{
				folder = Path.GetDirectoryName(target);
				if (string.IsNullOrWhiteSpace(folder))
					folder = fallbackFolder;

				suggestedName = Path.GetFileNameWithoutExtension(target);
				suggestedExtension = Path.GetExtension(target).TrimStart('.');
			}
			else
			{
				folder = target;
				suggestedName = Path.GetFileNameWithoutExtension(export.FileName);
				suggestedExtension = export.FileExtension;
			}

			if (string.IsNullOrWhiteSpace(suggestedName))
				suggestedName = Path.GetFileNameWithoutExtension(export.FileName);

			if (string.IsNullOrWhiteSpace(suggestedExtension))
				suggestedExtension = export.FileExtension;
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
