using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
	public static class RuntimeFileBrowser
	{
		private const string DefaultExternalFolderName = "external";

		public static string GetDefaultRootFolder()
		{
#if UNITY_WEBGL && !UNITY_EDITOR
			return EnsureFolder(Path.Combine(Application.persistentDataPath, "Downloads"));
#else
			return EnsureFolder(Path.Combine(Application.persistentDataPath, DefaultExternalFolderName));
#endif
		}

		public static void OpenObjFile(string title, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

#if UNITY_WEBGL && !UNITY_EDITOR
			WebGLRuntimeFileBrowser.OpenObjFile(title, onSelected, rootFolder, onCancelled);
#elif UNITY_EDITOR
			var path = UnityEditor.EditorUtility.OpenFilePanel(
				string.IsNullOrWhiteSpace(title) ? "Select OBJ File" : title,
				ResolveEditorStartFolder(rootFolder, startFolder),
				"obj");

			if (string.IsNullOrWhiteSpace(path))
			{
				onCancelled?.Invoke();
				return;
			}

			onSelected(path);
#else
			RuntimeFileBrowserGui.OpenObjFile(title, onSelected, rootFolder, startFolder, onCancelled);
#endif
		}

		public static void OpenFile(string title, string extension, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			OpenFile(title, new[] { extension }, onSelected, rootFolder, startFolder, onCancelled);
		}

		public static void OpenFile(string title, IEnumerable<string> extensions, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

#if UNITY_WEBGL && !UNITY_EDITOR
			WebGLRuntimeFileBrowser.OpenFile(title, extensions, onSelected, rootFolder, onCancelled);
#elif UNITY_EDITOR
			string extension = FirstExtension(extensions);
			var path = UnityEditor.EditorUtility.OpenFilePanel(
				string.IsNullOrWhiteSpace(title) ? "Select File" : title,
				ResolveEditorStartFolder(rootFolder, startFolder),
				extension);

			if (string.IsNullOrWhiteSpace(path))
			{
				onCancelled?.Invoke();
				return;
			}

			onSelected(path);
#else
			RuntimeFileBrowserGui.OpenFile(title, extensions, onSelected, rootFolder, startFolder, onCancelled);
#endif
		}

		private static string ResolveEditorStartFolder(string rootFolder, string startFolder)
		{
			if (!string.IsNullOrWhiteSpace(startFolder) && Directory.Exists(startFolder))
				return startFolder;

			if (!string.IsNullOrWhiteSpace(rootFolder) && Directory.Exists(rootFolder))
				return rootFolder;

			return GetDefaultRootFolder();
		}

		private static string FirstExtension(IEnumerable<string> extensions)
		{
			if (extensions == null)
				return string.Empty;

			foreach (var ext in extensions)
			{
				if (string.IsNullOrWhiteSpace(ext))
					continue;

				string normalized = ext.Trim();
				if (normalized.StartsWith("."))
					normalized = normalized.Substring(1);
				return normalized;
			}

			return string.Empty;
		}

		private static string EnsureFolder(string folder)
		{
			if (string.IsNullOrWhiteSpace(folder))
				return folder;

			Directory.CreateDirectory(folder);
			return folder;
		}
	}
}
