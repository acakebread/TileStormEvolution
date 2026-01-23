using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Minimal static helper around UnityEngine.Debug.
	/// Provides consistent prefix, optional context object, and one place to upgrade logging later.
	/// </summary>
	public static class DebugUtil
	{
		private const string PREFIX = "[MHL]";

		public static void Log(object message, Object context = null)
		{
			Debug.Log($"{PREFIX} {message}", context);
		}

		public static void LogWarning(object message, Object context = null)
		{
			Debug.LogWarning($"{PREFIX} {message}", context);
		}

		public static void LogError(object message, Object context = null)
		{
			Debug.LogError($"{PREFIX} {message}", context);
		}

		// ── With caller-provided category/prefix ───────────────────────────────

		public static void LogC(string category, object message, Object context = null)
		{
			Debug.Log($"{PREFIX} {category,-12} {message}", context);
		}

		public static void WarnC(string category, object message, Object context = null)
		{
			Debug.LogWarning($"{PREFIX} {category,-12} {message}", context);
		}

		public static void ErrorC(string category, object message, Object context = null)
		{
			Debug.LogError($"{PREFIX} {category,-12} {message}", context);
		}

		// ── Very common formatted pattern ──────────────────────────────────────

		public static void LogContext(string context, object message, Object unityContext = null)
		{
			if (string.IsNullOrWhiteSpace(context))
			{
				Log(message, unityContext);
			}
			else
			{
				Log($"{context.Trim()} → {message}", unityContext);
			}
		}

		public static void WarnContext(string context, object message, Object unityContext = null)
		{
			if (string.IsNullOrWhiteSpace(context))
			{
				LogWarning(message, unityContext);
			}
			else
			{
				LogWarning($"{context.Trim()} → {message}", unityContext);
			}
		}
	}
}