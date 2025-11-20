using UnityEngine;
using System.IO;

namespace ClassicTilestorm
{
	public static class ResourceFileIO
	{
		public static void EnsureFolder(string path)
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
		}

		public static bool FileExists(string path)
		{
			return File.Exists(path);
		}

		public static string ReadText(string path)
		{
			return File.ReadAllText(path);
		}

		public static void WriteText(string path, string contents)
		{
			File.WriteAllText(path, contents);
		}

		public static string[] GetFiles(string folder, string pattern)
		{
			return Directory.Exists(folder)
				? Directory.GetFiles(folder, pattern)
				: System.Array.Empty<string>();
		}
	}
}
