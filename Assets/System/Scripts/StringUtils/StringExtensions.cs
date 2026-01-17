namespace MassiveHadronLtd
{
	/// <summary>
	/// Extension methods for string instances, using StringUtil internally.
	/// </summary>
	public static class StringExtensions
	{
		/// <summary>
		/// Cleans the string by trimming and removing invisible/unwanted characters.
		/// </summary>
		public static string Clean(this string input)
		{
			return StringUtil.Clean(input);
		}

		/// <summary>
		/// Compares two strings for equality after cleaning, ignoring case.
		/// </summary>
		public static bool CleanEquals(this string a, string b)
		{
			return StringUtil.CleanEquals(a, b);
		}

		/// <summary>
		/// Converts the string to title case using the current culture.
		/// </summary>
		public static string ToTitleCase(this string str)
		{
			return StringUtil.ToTitleCase(str);
		}
	}
}
