using System.IO;
using System.Linq;

namespace MassiveHadronLtd
{
	public static class ResourcePathUtils
	{
		/// <summary>
		/// Normalizes a resource path for use with Resources.Load:
		/// - Converts backslashes to forward slashes
		/// - Strips known file extensions (repeatedly, in case of .fbx.png etc.)
		/// - Returns path without extension, suitable for Resources.Load
		/// </summary>
		/// <param name="fullPath">Original path from data/source</param>
		/// <param name="knownExtensions">Array of extensions to strip (including the dot)</param>
		/// <returns>Clean path for Resources.Load</returns>
		public static string NormalizeForResourcesLoad(string fullPath, string[] knownExtensions)
		{
			if (string.IsNullOrEmpty(fullPath))
				return string.Empty;

			var normalized = fullPath.Replace('\\', '/');
			var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');

			//possibly add the trailing trim - needs testing
			//var normalized = fullPath.Replace('\\', '/').Trim('/'); ;
			//var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/').Trim('/'); ;

			var fileName = Path.GetFileNameWithoutExtension(normalized);

			string lowerExtension;
			while (!string.IsNullOrEmpty(fileName) &&
				   knownExtensions.Contains(lowerExtension = Path.GetExtension(fileName).ToLowerInvariant()))
			{
				// If extension matches, strip it and check again
				fileName = Path.GetFileNameWithoutExtension(fileName);
			}

			if (string.IsNullOrEmpty(directory))
				return fileName;

			return $"{directory}/{fileName}";
		}
	}
}