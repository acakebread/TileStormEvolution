using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
#if UNITY_WEBGL && !UNITY_EDITOR
	public static class PlatformFileBrowser
	{
		public static bool ExportAtomicMap(ClassicTilestorm.ResourceSerializer.AtomicMapExportData export, string defaultFolder = null)
		{
			if (export == null || !export.IsValid)
			{
				Debug.LogError("PlatformFileBrowser: invalid atomic export payload.");
				return false;
			}

			if (export.IsArchive)
				WebGLDownloadUtility.DownloadBytes(export.FileName, export.Archive, export.MimeType);
			else
				WebGLDownloadUtility.DownloadText(export.FileName, export.Json, export.MimeType);

			Debug.Log($"{export.DisplayLabel} prepared for browser download: {export.FileName}");
			return true;
		}
	}
#endif
}
