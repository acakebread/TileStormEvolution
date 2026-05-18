using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using MassiveHadronLtd;

namespace MassiveHadronLtd.FileBrowserUtil
{
#if UNITY_EDITOR
	public static class RuntimeFileBrowser
	{
		private const string DefaultExternalFolderName = "External";

		public static string GetDefaultRootFolder()
		{
			return FileUtils.EnsureFolder(Path.Combine(Application.persistentDataPath, DefaultExternalFolderName));
		}

		public static void OpenObjFile(string title, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

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
		}

		public static void OpenFile(string title, string extension, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			OpenFile(title, new[] { extension }, onSelected, rootFolder, startFolder, onCancelled);
		}

		public static void OpenFile(string title, IEnumerable<string> extensions, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			var filters = BuildEditorFilters(extensions);
			var path = UnityEditor.EditorUtility.OpenFilePanelWithFilters(
				string.IsNullOrWhiteSpace(title) ? "Select File" : title,
				ResolveEditorStartFolder(rootFolder, startFolder),
				filters);

			if (string.IsNullOrWhiteSpace(path))
			{
				onCancelled?.Invoke();
				return;
			}

			onSelected(path);
		}

		private static string ResolveEditorStartFolder(string rootFolder, string startFolder)
		{
			if (!string.IsNullOrWhiteSpace(startFolder) && Directory.Exists(startFolder))
				return startFolder;

			if (!string.IsNullOrWhiteSpace(rootFolder) && Directory.Exists(rootFolder))
				return rootFolder;

			return GetDefaultRootFolder();
		}

		private static string[] BuildEditorFilters(IEnumerable<string> extensions)
		{
			if (extensions == null)
				return new[] { "All files", "*" };

			var normalizedExtensions = new List<string>();
			foreach (var ext in extensions)
			{
				if (string.IsNullOrWhiteSpace(ext))
					continue;

				string normalized = ext.Trim();
				if (normalized.StartsWith("."))
					normalized = normalized.Substring(1);
				normalizedExtensions.Add(normalized.ToLowerInvariant());
			}

			if (normalizedExtensions.Count == 0)
				return new[] { "All files", "*" };

			var filters = new List<string>
			{
				"Atomic Map Files",
				string.Join(",", normalizedExtensions.Distinct(StringComparer.OrdinalIgnoreCase))
			};

			filters.Add("All files");
			filters.Add("*");
			return filters.ToArray();
		}
	}
#endif
}
