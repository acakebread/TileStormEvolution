using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
#if UNITY_WEBGL && !UNITY_EDITOR
	public static class RuntimeFileBrowser
	{
		private const string DefaultExternalFolderName = "WebGLUploads";

		public static string GetDefaultRootFolder()
		{
			return EnsureFolder(Path.Combine(Application.persistentDataPath, DefaultExternalFolderName));
		}

		public static void OpenObjFile(string title, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			WebGLRuntimeFileBrowser.OpenObjFile(title, onSelected, rootFolder, onCancelled);
		}

		public static void OpenFile(string title, string extension, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			OpenFile(title, new[] { extension }, onSelected, rootFolder, startFolder, onCancelled);
		}

		public static void OpenFile(string title, IEnumerable<string> extensions, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			WebGLRuntimeFileBrowser.OpenFile(title, extensions, onSelected, rootFolder, onCancelled);
		}

		private static string EnsureFolder(string folder)
		{
			if (string.IsNullOrWhiteSpace(folder))
				return folder;

			Directory.CreateDirectory(folder);
			return folder;
		}
	}
#endif
}
