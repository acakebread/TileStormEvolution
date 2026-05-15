using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MassiveHadronLtd.FileBrowserUtil
{
	internal static class WebGLRuntimeFileBrowser
	{
#if UNITY_WEBGL && !UNITY_EDITOR
		[DllImport("__Internal")]
		private static extern void MassiveHadron_OpenFilePicker(
			string title,
			string accept,
			int allowMultiple,
			int allowDirectory,
			string receiverObject,
			string onFileMethod,
			string onCompleteMethod);

		[DllImport("__Internal")]
		private static extern void MassiveHadron_OpenDirectoryPicker(
			string title,
			string receiverObject,
			string onFileMethod,
			string onCompleteMethod);
#endif

		private const string UploadRootFolderName = "WebGLUploads";
		private static UploadHost host;

		public static void OpenFile(string title, IEnumerable<string> extensions, Action<string> onSelected, string rootFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			EnsureHost().Open(new UploadRequest
			{
				Title = string.IsNullOrWhiteSpace(title) ? "Select File" : title,
				Extensions = NormalizeExtensions(extensions),
				AllowMultiple = false,
				AllowDirectory = false,
				RootFolder = NormalizeRoot(rootFolder),
				OnSelected = onSelected,
				OnCancelled = onCancelled,
				SelectionKind = UploadSelectionKind.SingleFile
			});
		}

		public static void OpenObjFile(string title, Action<string> onSelected, string rootFolder = null, Action onCancelled = null)
		{
			if (onSelected == null)
				throw new ArgumentNullException(nameof(onSelected));

			EnsureHost().Open(new UploadRequest
			{
				Title = string.IsNullOrWhiteSpace(title) ? "Select Wavefront Model Folder" : title,
				Extensions = Array.Empty<string>(),
				AllowMultiple = false,
				AllowDirectory = true,
				RootFolder = NormalizeRoot(rootFolder),
				OnSelected = onSelected,
				OnCancelled = onCancelled,
				SelectionKind = UploadSelectionKind.ObjPackage
			});
		}

		private static UploadHost EnsureHost()
		{
			if (host != null)
				return host;

			var go = new GameObject(nameof(WebGLRuntimeFileBrowser));
			go.hideFlags = HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(go);
			host = go.AddComponent<UploadHost>();
			return host;
		}

		private static string NormalizeRoot(string rootFolder)
		{
			string root = string.IsNullOrWhiteSpace(rootFolder)
				? Path.Combine(Application.persistentDataPath, DefaultRootFolder())
				: rootFolder;

			root = Path.GetFullPath(root);
			Directory.CreateDirectory(root);
			return root;
		}

		private static string DefaultRootFolder()
		{
			return UploadRootFolderName;
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

		private enum UploadSelectionKind
		{
			SingleFile = 0,
			ObjPackage = 1
		}

		[Serializable]
		private sealed class UploadRequest
		{
			public string Title;
			public string RootFolder;
			public string[] Extensions;
			public bool AllowMultiple;
			public bool AllowDirectory;
			public UploadSelectionKind SelectionKind;
			public Action<string> OnSelected;
			public Action OnCancelled;
		}

		[Serializable]
		private sealed class UploadFilePayload
		{
			public string name;
			public string relativePath;
			public string base64;
		}

		private sealed class UploadHost : MonoBehaviour
		{
			private UploadRequest request;
			private readonly List<UploadFilePayload> receivedFiles = new();

			public void Open(UploadRequest newRequest)
			{
				request = newRequest;
				receivedFiles.Clear();
				enabled = true;

#if UNITY_WEBGL && !UNITY_EDITOR
				OpenNativePicker();
#else
				Debug.LogWarning("WebGLRuntimeFileBrowser was invoked outside WebGL.");
				Close();
#endif
			}

#if UNITY_WEBGL && !UNITY_EDITOR
			private void OpenNativePicker()
			{
				string title = request?.Title ?? "Select File";

				if (request?.AllowDirectory == true)
				{
					MassiveHadron_OpenDirectoryPicker(
						title,
						gameObject.name,
						nameof(ReceiveFile),
						nameof(ReceiveComplete));
					return;
				}

				string accept = request?.Extensions == null ? string.Empty : string.Join(",", request.Extensions);
				MassiveHadron_OpenFilePicker(
					title,
					accept,
					request?.AllowMultiple == true ? 1 : 0,
					request?.AllowDirectory == true ? 1 : 0,
					gameObject.name,
					nameof(ReceiveFile),
					nameof(ReceiveComplete));
			}
#endif

			public void ReceiveFile(string json)
			{
				if (request == null || string.IsNullOrWhiteSpace(json))
					return;

				try
				{
					var payload = JsonUtility.FromJson<UploadFilePayload>(json);
					if (payload == null || string.IsNullOrWhiteSpace(payload.name) || string.IsNullOrWhiteSpace(payload.base64))
						return;

					receivedFiles.Add(payload);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"WebGLRuntimeFileBrowser: failed to parse uploaded file payload: {ex.Message}");
				}
			}

			public void ReceiveComplete(string _)
			{
				if (request == null)
					return;

				if (receivedFiles.Count == 0)
				{
					var onCancelled = request.OnCancelled;
					Close();
					onCancelled?.Invoke();
					return;
				}

				try
				{
					string root = CreateStagingRoot(request.RootFolder);
					foreach (var file in receivedFiles)
						WriteFile(root, file);

					string selectedPath = ResolveSelectedPath(root, request);
					var onSelected = request.OnSelected;
					var onCancelled = request.OnCancelled;
					Close();

					if (!string.IsNullOrWhiteSpace(selectedPath))
						onSelected?.Invoke(selectedPath);
					else
					{
						Debug.LogWarning("WebGLRuntimeFileBrowser: no matching .obj file was found in the uploaded folder.");
						onCancelled?.Invoke();
					}
				}
				catch (Exception ex)
				{
					Debug.LogError($"WebGLRuntimeFileBrowser: upload processing failed: {ex.Message}");
					var onCancelled = request.OnCancelled;
					Close();
					onCancelled?.Invoke();
				}
			}

			private static string CreateStagingRoot(string rootFolder)
			{
				string baseRoot = string.IsNullOrWhiteSpace(rootFolder)
					? Path.Combine(Application.persistentDataPath, DefaultRootFolder())
					: rootFolder;

				Directory.CreateDirectory(baseRoot);

				string stagingRoot = Path.Combine(baseRoot, "upload_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff"));
				Directory.CreateDirectory(stagingRoot);
				return Path.GetFullPath(stagingRoot);
			}

			private static void WriteFile(string stagingRoot, UploadFilePayload file)
			{
				string relative = string.IsNullOrWhiteSpace(file.relativePath)
					? file.name
					: file.relativePath;

				relative = relative.Replace('\\', '/');

				string targetPath = Path.GetFullPath(Path.Combine(stagingRoot, relative));
				if (!IsInsideRoot(targetPath, stagingRoot))
					targetPath = Path.Combine(stagingRoot, Path.GetFileName(relative));

				string targetDir = Path.GetDirectoryName(targetPath);
				if (!string.IsNullOrWhiteSpace(targetDir))
					Directory.CreateDirectory(targetDir);

				byte[] bytes = Convert.FromBase64String(file.base64);
				File.WriteAllBytes(targetPath, bytes);
			}

			private static string ResolveSelectedPath(string stagingRoot, UploadRequest request)
			{
				var files = Directory.GetFiles(stagingRoot, "*", SearchOption.AllDirectories)
					.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
					.ToArray();

				if (files.Length == 0)
					return null;

				if (request.SelectionKind == UploadSelectionKind.ObjPackage)
				{
					return files.FirstOrDefault(path =>
						string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase));
				}

				if (request.Extensions == null || request.Extensions.Length == 0)
					return files[0];

				foreach (var file in files)
				{
					string ext = Path.GetExtension(file);
					if (!string.IsNullOrWhiteSpace(ext) && request.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
						return file;
				}

				return files[0];
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

			private void Close()
			{
				request = null;
				receivedFiles.Clear();
				enabled = false;
			}
		}
	}
}
