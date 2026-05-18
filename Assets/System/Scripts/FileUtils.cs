using System;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class FileUtils
	{
		public static string EnsureFolder(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return path;

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			return path;
		}

		public static void CopyDirectoryTree(string sourceDirectory, string destinationDirectory)
		{
			EnsureFolder(destinationDirectory);

			foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				string relative = Path.GetRelativePath(sourceDirectory, file);
				string destination = Path.Combine(destinationDirectory, relative);
				string destinationParent = Path.GetDirectoryName(destination);
				EnsureFolder(destinationParent);
				File.Copy(file, destination, true);
			}
		}

		public static void TryDeleteDirectory(string path, string logContext = null)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
					Directory.Delete(path, recursive: true);
			}
			catch (Exception ex)
			{
				string prefix = string.IsNullOrWhiteSpace(logContext) ? "FileUtils" : logContext;
				Debug.LogWarning($"{prefix}: failed to clean temporary directory '{path}': {ex.Message}");
			}
		}

		public static void TryDeleteFile(string path, string logContext = null)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
					File.Delete(path);
			}
			catch (Exception ex)
			{
				string prefix = string.IsNullOrWhiteSpace(logContext) ? "FileUtils" : logContext;
				Debug.LogWarning($"{prefix}: failed to clean temporary file '{path}': {ex.Message}");
			}
		}

		public static bool IsZipArchive(string path)
		{
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
				return false;

			if (string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase))
				return true;

			try
			{
				using var stream = File.OpenRead(path);
				if (stream.Length < 4)
					return false;

				int b0 = stream.ReadByte();
				int b1 = stream.ReadByte();
				int b2 = stream.ReadByte();
				int b3 = stream.ReadByte();
				return b0 == 'P' && b1 == 'K' && (b2 == 3 || b2 == 5 || b2 == 7) && (b3 == 4 || b3 == 6 || b3 == 8);
			}
			catch
			{
				return false;
			}
		}
	}
}
