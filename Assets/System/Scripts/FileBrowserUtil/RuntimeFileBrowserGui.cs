using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
	public static class RuntimeFileBrowserGui
	{
		private const string DefaultExternalFolderName = "external";
		private static FileBrowserHost host;

		public static string GetDefaultRootFolder()
		{
			return EnsureFolder(Path.Combine(Application.persistentDataPath, DefaultExternalFolderName));
		}

		public static void OpenObjFile(string title, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			OpenFile(title, new[] { ".obj" }, onSelected, rootFolder, startFolder, onCancelled);
		}

		public static void OpenFile(string title, string extension, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			OpenFile(title, new[] { extension }, onSelected, rootFolder, startFolder, onCancelled);
		}

		public static void OpenFile(string title, IEnumerable<string> extensions, Action<string> onSelected, string rootFolder = null, string startFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			EnsureHost().Open(new FileBrowserRequest
			{
				Title = string.IsNullOrWhiteSpace(title) ? "Select File" : title,
				RootFolder = NormalizeRoot(rootFolder),
				StartFolder = startFolder,
				AllowedExtensions = NormalizeExtensions(extensions),
				OnSelected = onSelected,
				OnCancelled = onCancelled
			});
		}

		private static FileBrowserHost EnsureHost()
		{
			if (host != null)
				return host;

			var go = new GameObject(nameof(RuntimeFileBrowserGui));
			go.hideFlags = HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(go);
			host = go.AddComponent<FileBrowserHost>();
			return host;
		}

		private static string NormalizeRoot(string rootFolder)
		{
			string root = string.IsNullOrWhiteSpace(rootFolder) ? GetDefaultRootFolder() : rootFolder;
			root = Path.GetFullPath(root);
			return EnsureFolder(root);
		}

		private static string[] NormalizeExtensions(IEnumerable<string> extensions)
		{
			if (extensions == null)
				return Array.Empty<string>();

			return extensions
				.Where(ext => !string.IsNullOrWhiteSpace(ext))
				.Select(ext =>
				{
					ext = ext.Trim();
					if (!ext.StartsWith("."))
						ext = "." + ext;
					return ext.ToLowerInvariant();
				})
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private static string EnsureFolder(string folder)
		{
			if (string.IsNullOrWhiteSpace(folder))
				return folder;

			Directory.CreateDirectory(folder);
			return folder;
		}

		private sealed class FileBrowserRequest
		{
			public string Title;
			public string RootFolder;
			public string StartFolder;
			public string[] AllowedExtensions;
			public Action<string> OnSelected;
			public Action OnCancelled;
		}

		private sealed class FileBrowserHost : MonoBehaviour
		{
			private FileBrowserRequest request;
			private string currentFolder;
			private Vector2 scroll;
			private Rect windowRect = new Rect(20f, 20f, 760f, 560f);
			private const int WindowId = 0x5FB7C;
			private static GUIStyle windowStyle;

			public void Open(FileBrowserRequest newRequest)
			{
				request = newRequest;
				currentFolder = ResolveStartFolder(newRequest);
				scroll = Vector2.zero;
				enabled = true;
			}

			private void OnGUI()
			{
				if (request == null)
					return;

				windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, request.Title, GetWindowStyle());
			}

			private void DrawWindow(int windowId)
			{
				var req = request;
				if (req == null)
					return;

				GUILayout.Label($"Root: {req.RootFolder}");
				GUILayout.Label($"Current: {currentFolder}");

				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Up", GUILayout.Width(80f)))
					GoUp();
				if (GUILayout.Button("Root", GUILayout.Width(80f)))
					OpenFolder(req.RootFolder);
				if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
					Refresh();
				if (GUILayout.Button("Cancel", GUILayout.Width(80f)))
					Cancel();
				GUILayout.EndHorizontal();

				GUILayout.Space(8f);
				scroll = GUILayout.BeginScrollView(scroll);

				var directories = SafeGetDirectories(currentFolder);
				var files = SafeGetFiles(currentFolder, req.AllowedExtensions);

				GUILayout.Label("Folders");
				foreach (var dir in directories)
				{
					string name = Path.GetFileName(dir);
					if (GUILayout.Button("[DIR] " + name))
						OpenFolder(dir);
				}

				GUILayout.Space(8f);
				GUILayout.Label("Files");
				foreach (var file in files)
				{
					string name = Path.GetFileName(file);
					if (GUILayout.Button("[FILE] " + name))
						Select(file);
				}

				GUILayout.EndScrollView();
				GUI.DragWindow(new Rect(0, 0, 10000, 24));
			}

			private static GUIStyle GetWindowStyle()
			{
				if (windowStyle != null)
					return windowStyle;

				var baseStyle = new GUIStyle(GUI.skin.window);
				baseStyle.normal.background = MakeSolidTexture(new Color(0.10f, 0.11f, 0.13f, 0.98f));
				baseStyle.onNormal.background = baseStyle.normal.background;
				baseStyle.hover.background = baseStyle.normal.background;
				baseStyle.onHover.background = baseStyle.normal.background;
				baseStyle.active.background = baseStyle.normal.background;
				baseStyle.onActive.background = baseStyle.normal.background;
				windowStyle = baseStyle;
				return windowStyle;
			}

			private static Texture2D MakeSolidTexture(Color color)
			{
				var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
				tex.hideFlags = HideFlags.HideAndDontSave;
				tex.SetPixel(0, 0, color);
				tex.Apply();
				return tex;
			}

			private void OpenFolder(string folder)
			{
				if (string.IsNullOrWhiteSpace(folder))
					return;

				string full = Path.GetFullPath(folder);
				if (!IsInsideRoot(full, request.RootFolder))
					return;

				if (!Directory.Exists(full))
					return;

				currentFolder = full;
			}

			private void GoUp()
			{
				if (string.Equals(currentFolder, request.RootFolder, StringComparison.OrdinalIgnoreCase))
					return;

				string parent = Directory.GetParent(currentFolder)?.FullName;
				if (string.IsNullOrWhiteSpace(parent))
					return;

				if (IsInsideRoot(parent, request.RootFolder))
					currentFolder = parent;
				else
					currentFolder = request.RootFolder;
			}

			private void Refresh()
			{
				currentFolder = ResolveExistingFolder(currentFolder, request.RootFolder);
			}

			private void Select(string filePath)
			{
				if (string.IsNullOrWhiteSpace(filePath) || request == null)
					return;

				string fullPath = Path.GetFullPath(filePath);
				if (!File.Exists(fullPath) || !IsInsideRoot(fullPath, request.RootFolder))
					return;

				var onSelected = request.OnSelected;
				Close();
				onSelected?.Invoke(fullPath);
			}

			private void Cancel()
			{
				var onCancelled = request?.OnCancelled;
				Close();
				onCancelled?.Invoke();
			}

			private void Close()
			{
				request = null;
				currentFolder = null;
				scroll = Vector2.zero;
				enabled = false;
			}

			private string ResolveStartFolder(FileBrowserRequest newRequest)
			{
				string root = NormalizeFolder(newRequest.RootFolder);
				string start = NormalizeFolder(newRequest.StartFolder);

				if (!string.IsNullOrWhiteSpace(start) && IsInsideRoot(start, root) && Directory.Exists(start))
					return start;

				return ResolveExistingFolder(root, root);
			}

			private static string ResolveExistingFolder(string folder, string root)
			{
				string current = NormalizeFolder(folder);
				string rootFolder = NormalizeFolder(root);

				while (!string.IsNullOrWhiteSpace(current) && !Directory.Exists(current))
				{
					string parent = Directory.GetParent(current)?.FullName;
					if (string.IsNullOrWhiteSpace(parent) || !IsInsideRoot(parent, rootFolder))
						return rootFolder;

					current = parent;
				}

				return string.IsNullOrWhiteSpace(current) ? rootFolder : current;
			}

			private static string NormalizeFolder(string folder)
			{
				if (string.IsNullOrWhiteSpace(folder))
					return null;

				return Path.GetFullPath(folder);
			}

			private static bool IsInsideRoot(string path, string root)
			{
				if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
					return false;

				string normalizedPath = Path.GetFullPath(path)
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
					+ Path.DirectorySeparatorChar;
				string normalizedRoot = Path.GetFullPath(root)
					.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
					+ Path.DirectorySeparatorChar;

				return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
			}

			private static IEnumerable<string> SafeGetDirectories(string folder)
			{
				try
				{
					return Directory.Exists(folder)
						? Directory.GetDirectories(folder).OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
						: Array.Empty<string>();
				}
				catch
				{
					return Array.Empty<string>();
				}
			}

			private static IEnumerable<string> SafeGetFiles(string folder, IReadOnlyCollection<string> extensions)
			{
				try
				{
					if (!Directory.Exists(folder))
						return Array.Empty<string>();

					var files = Directory.GetFiles(folder);
					if (extensions == null || extensions.Count == 0)
						return files.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

					return files
						.Where(path =>
						{
							string ext = Path.GetExtension(path);
							return !string.IsNullOrWhiteSpace(ext) && extensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
						})
						.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
				}
				catch
				{
					return Array.Empty<string>();
				}
			}
		}
	}
}
