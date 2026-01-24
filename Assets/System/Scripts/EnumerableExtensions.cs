using System;
using System.Linq;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class EnumerableExtensions
	{
		/// <summary>
		/// Returns distinct items sorted by frequency (descending), then by item itself.
		/// Perfect for building compact palettes, string tables, remap tables, etc.
		/// </summary>
		public static T[] ToFrequencySortedDistinct<T>(this IEnumerable<T> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			return source
				.Where(item => item != null)
				.GroupBy(item => item)
				.Select(g => new { Key = g.Key, Count = g.Count() })
				.OrderByDescending(x => x.Count)
				.ThenBy(x => x.Key)           // assumes T is comparable
				.Select(x => x.Key)
				.ToArray();
		}

		// Overload with explicit comparer for strings (recommended)
		public static string[] ToFrequencySortedTable(this IEnumerable<string> source)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));

			return source
				.Where(s => !string.IsNullOrEmpty(s))
				.GroupBy(s => s)
				.Select(g => new { Key = g.Key, Count = g.Count() })
				.OrderByDescending(x => x.Count)
				.ThenBy(x => x.Key, StringComparer.Ordinal)   // now applied after projection → no stability issue
				.Select(x => x.Key)
				.ToArray();
		}
	}
}


//untested bidirectional version below

///// <summary>
///// Returns distinct items sorted by frequency (descending), then by item itself.
///// Perfect for building compact palettes, string tables, remap tables, etc.
///// </summary>
//public static T[] ToFrequencySortedDistinct<T>(this IEnumerable<T> source)
//{
//	if (source == null) throw new ArgumentNullException(nameof(source));

//	return source
//		.Where(item => item != null) // skip nulls
//		.GroupBy(item => item)
//		.OrderByDescending(g => g.Count())
//		.ThenBy(g => g.Key) // requires T : IComparable<T>, or use comparer
//		.Select(g => g.Key)
//		.ToArray();
//}

//// Overload with explicit comparer for strings (recommended)
//public static string[] ToFrequencySortedTable(this IEnumerable<string> source)
//{
//	if (source == null) throw new ArgumentNullException(nameof(source));

//	return source
//		.Where(s => !string.IsNullOrEmpty(s))
//		.GroupBy(s => s)
//		.OrderByDescending(g => g.Count())
//		.ThenBy(g => g.Key, StringComparer.Ordinal) // deterministic + case-sensitive if desired
//		.Select(g => g.Key)
//		.ToArray();
//}