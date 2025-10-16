using UnityEngine;
using System.Reflection;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public static class URPCameraHelper
	{
		/// <summary>
		/// Sets the 'Clear Depth' flag on a URP overlay camera at runtime, 
		/// using the same underlying mechanism the Inspector uses.
		/// </summary>
		public static bool SetClearDepth(UniversalAdditionalCameraData data, bool value)
		{
			if (data == null)
			{
				Debug.LogError("URPCameraHelper: CameraData is null.");
				return false;
			}

			// 1. Try a public setter first (future-proof)
			var prop = typeof(UniversalAdditionalCameraData)
				.GetProperty("clearDepth", BindingFlags.Instance | BindingFlags.Public);

			if (prop != null && prop.CanWrite)
			{
				prop.SetValue(data, value);
				return true;
			}

			// 2. Fallback: private backing field used by Inspector
			var field = typeof(UniversalAdditionalCameraData)
				.GetField("m_ClearDepth", BindingFlags.Instance | BindingFlags.NonPublic);

			if (field != null)
			{
				field.SetValue(data, value);
				return true;
			}

			Debug.LogWarning(
				$"URPCameraHelper: Failed to set clearDepth on '{data.gameObject.name}'. " +
				"Field/property not found. This URP version may have changed internal implementation."
			);
			return false;
		}

		/// <summary>
		/// Checks the current value of clearDepth.
		/// </summary>
		public static bool GetClearDepth(UniversalAdditionalCameraData data)
		{
			if (data == null) return false;

			// Public getter
			var prop = typeof(UniversalAdditionalCameraData)
				.GetProperty("clearDepth", BindingFlags.Instance | BindingFlags.Public);

			if (prop != null && prop.CanRead)
				return (bool)prop.GetValue(data);

			// Private field fallback
			var field = typeof(UniversalAdditionalCameraData)
				.GetField("m_ClearDepth", BindingFlags.Instance | BindingFlags.NonPublic);

			if (field != null)
				return (bool)field.GetValue(data);

			Debug.LogWarning($"URPCameraHelper: Unable to get clearDepth for '{data.gameObject.name}'.");
			return false;
		}
	}
}