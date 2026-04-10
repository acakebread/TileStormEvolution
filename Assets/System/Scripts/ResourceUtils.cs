using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Fast file-system and Resources utilities.
	/// Replaces slow Resources.LoadAll<T> scanning with direct directory enumeration.
	/// </summary>
	public static class ResourceUtils
	{
		/// <summary>
		/// Returns all files of the specified type(s) in the given directory (and optionally subdirectories).
		/// </summary>
		/// <param name="directoryPath">Full path to the folder to scan (e.g. Application.dataPath + "/MyFolder")</param>
		/// <param name="extensions">File extensions to look for (e.g. ".json", ".txt", ".asset"). Case-insensitive.</param>
		/// <param name="searchOption">SearchOption.AllDirectories to include subfolders.</param>
		/// <returns>Full paths to matching files.</returns>
		public static IEnumerable<string> GetFilesOfType(
			string directoryPath,
			IEnumerable<string> extensions,
			SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
				return Enumerable.Empty<string>();

			var extSet = new HashSet<string>(
				extensions.Select(e => e.StartsWith(".") ? e.ToLowerInvariant() : "." + e.ToLowerInvariant()),
				StringComparer.OrdinalIgnoreCase);

			return Directory.EnumerateFiles(directoryPath, "*.*", searchOption)
							.Where(file => extSet.Contains(Path.GetExtension(file)));
		}

		/// <summary>
		/// Returns all files with a specific extension in the given directory.
		/// </summary>
		public static IEnumerable<string> GetFilesOfType(
			string directoryPath,
			string extension,
			SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			return GetFilesOfType(directoryPath, new[] { extension }, searchOption);
		}

		/// <summary>
		/// Returns only the filenames (without path or extension) of matching files.
		/// Useful for name-based lookup systems.
		/// </summary>
		public static IEnumerable<string> GetFileNamesOfType(
			string directoryPath,
			IEnumerable<string> extensions,
			SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			return GetFilesOfType(directoryPath, extensions, searchOption)
				   .Select(file => Path.GetFileNameWithoutExtension(file));
		}

		/// <summary>
		/// Returns only the filenames (without path or extension) of matching files.
		/// </summary>
		public static IEnumerable<string> GetFileNamesOfType(
			string directoryPath,
			string extension,
			SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			return GetFileNamesOfType(directoryPath, new[] { extension }, searchOption);
		}

		/// <summary>
		/// Reads the first matching file and returns its content as text.
		/// Returns null if no matching file is found.
		/// </summary>
		public static string ReadFirstFileOfType(
			string directoryPath,
			string extension,
			SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			var file = GetFilesOfType(directoryPath, extension, searchOption).FirstOrDefault();
			return file != null ? File.ReadAllText(file) : null;
		}

		/// <summary>
		/// Similar to your old GetJob() — returns the first matching file's full path and (optionally) archives it.
		/// </summary>
		public static string GetFirstFileAndArchive(
			string directoryPath,
			string extension,
			string archiveDirectory = null,
			bool archive = false)
		{
			var files = GetFilesOfType(directoryPath, extension, SearchOption.TopDirectoryOnly).ToList();
			if (files.Count == 0)
				return null;

			string firstFile = files[0];

			if (archive && !string.IsNullOrEmpty(archiveDirectory))
			{
				if (!Directory.Exists(archiveDirectory))
					Directory.CreateDirectory(archiveDirectory);

				string dest = Path.Combine(archiveDirectory, Path.GetFileName(firstFile));
				File.Delete(dest); // remove old version if exists
				File.Move(firstFile, dest);
			}

			return firstFile;
		}

		/// <summary>
		/// Quick helper to get all .json files in a folder (very common case from your old code)
		/// </summary>
		public static IEnumerable<string> GetJsonFiles(string directoryPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
			=> GetFilesOfType(directoryPath, ".json", searchOption);

		public static IEnumerable<string> GetJsonFileNames(string directoryPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
			=> GetFileNamesOfType(directoryPath, ".json", searchOption);


		/// <summary>
		/// Returns ALL "Resources" folders in the project, optionally skipping folders that start with '.' 
		/// (Unity ignores hidden/dot folders anyway).
		/// </summary>
		public static IEnumerable<string> GetAllResourcesFolders(bool skipDotFolders = true)
		{
			string assetsRoot = Application.dataPath;
			if (!Directory.Exists(assetsRoot))
				yield break;

			var options = skipDotFolders
				? SearchOption.AllDirectories
				: SearchOption.AllDirectories;

			foreach (var dir in Directory.GetDirectories(assetsRoot, "Resources", options))
			{
				if (skipDotFolders)
				{
					// Skip any Resources folder whose path contains a folder starting with '.'
					var parts = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					if (parts.Any(p => p.StartsWith(".")))
						continue;
				}

				yield return dir.Replace('\\', '/');
			}
		}

		/// <summary>
		/// Gets typical file extensions for a given asset type inside Resources.
		/// Supports multiple extensions (e.g. AudioClip = .wav + .mp3).
		/// </summary>
		public static IEnumerable<string> GetExtensionsForType<T>() where T : UnityEngine.Object
		{
			if (typeof(T) == typeof(GameObject)) return new[] { ".prefab", ".obj", ".fbx" };
			if (typeof(T) == typeof(Texture) || typeof(T) == typeof(Texture2D))
				return new[] { ".png", ".jpg", ".jpeg", ".tga" };   // add more if needed

			if (typeof(T) == typeof(Material)) return new[] { ".mat" };
			if (typeof(T) == typeof(AudioClip)) return new[] { ".wav", ".mp3", ".ogg" };

			return new[] { ".*" }; // fallback
		}

		/// <summary>
		/// NEW: Platform-safe name listing.
		/// Editor / Standalone / Android / iOS → fast filesystem
		/// WebGL → loads pre-generated tiny manifest (no memory spike)
		/// </summary>
		public static IEnumerable<string> GetAssetNamesFromResources<T>(string[] roots, string manifestName)
			where T : UnityEngine.Object
		{
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: use pre-built manifest (super lightweight TextAsset)
            return GetNamesFromManifest(manifestName);
#else
			// Everywhere else: your fast filesystem scan (unchanged behaviour)
			var extensions = GetExtensionsForType<T>();

			foreach (var resFolder in GetAllResourcesFolders(skipDotFolders: true))
			{
				foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r)))
				{
					string fullPath = Path.Combine(resFolder, root).Replace('\\', '/');
					if (!Directory.Exists(fullPath)) continue;

					foreach (var ext in extensions)
					{
						foreach (var name in GetFileNamesOfType(fullPath, ext, SearchOption.AllDirectories))
						{
							if (!string.IsNullOrEmpty(name))
								yield return name;
						}
					}
				}
			}
#endif
		}

		/// <summary>
		/// Loads a pre-generated manifest (used only in WebGL builds)
		/// </summary>
		private static IEnumerable<string> GetNamesFromManifest(string manifestName)
		{
			var ta = Resources.Load<TextAsset>($"AssetManifests/{manifestName}");
			if (ta == null || string.IsNullOrEmpty(ta.text))
				return Enumerable.Empty<string>();

			return ta.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
						  .Select(n => n.Trim())
						  .Where(n => !string.IsNullOrEmpty(n));
		}
	}
}