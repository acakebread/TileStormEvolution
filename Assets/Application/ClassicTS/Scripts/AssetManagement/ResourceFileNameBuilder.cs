using MassiveHadronLtd;

namespace ClassicTilestorm
{
	internal static class ResourceFileNameBuilder
	{
		internal const string FileHashSeparator = "__";

		internal static string BuildJsonFileName(HashId hash, string displayName = null, bool prefixReadableName = false)
		{
			return BuildFileName(hash, ".json", displayName, prefixReadableName);
		}

		internal static string BuildFileName(HashId hash, string extension, string displayName = null, bool prefixReadableName = false)
		{
			var canonicalHash = HTB50Settings.ToString(hash);
			if (!prefixReadableName)
				return $"{canonicalHash}{extension}";

			var safeName = StringUtil.SanitizeFileName(string.IsNullOrWhiteSpace(displayName) ? "Untitled" : displayName);
			return $"{safeName}{FileHashSeparator}{canonicalHash}{extension}";
		}
	}
}
