using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
#if !UNITY_WEBGL && !UNITY_EDITOR
	public static class RuntimeFileBrowser
	{
		public static string GetDefaultRootFolder()
		{
			return RuntimeFileBrowserGui.GetDefaultRootFolder();
		}

		public static void OpenObjFile(string title, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			RuntimeFileBrowserGui.OpenObjFile(title, onSelected, rootFolder, startFolder, onCancelled);
		}

		public static void OpenFile(string title, string extension, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			OpenFile(title, new[] { extension }, onSelected, rootFolder, startFolder, onCancelled);
		}

		public static void OpenFile(string title, IEnumerable<string> extensions, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			RuntimeFileBrowserGui.OpenFile(title, extensions, onSelected, rootFolder, startFolder, onCancelled);
		}
	}
#endif
}
