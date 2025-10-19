using System;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class SerializerUtility
	{
		public static string ReadText(string filePath)
		{
			if (string.IsNullOrEmpty(filePath))
			{
				Debug.LogError("SerializerUtility: File path is null or empty.");
				return null;
			}

			try
			{
				if (!File.Exists(filePath))
				{
					// Instead of logging an error, return null to indicate the file doesn't exist
					return null;
				}

				return File.ReadAllText(filePath);
			}
			catch (Exception ex)
			{
				Debug.LogError($"SerializerUtility: Failed to read text from {filePath}: {ex.Message}");
				return null;
			}
		}

		public static void WriteText(string filePath, string content)
		{
			if (string.IsNullOrEmpty(filePath))
			{
				Debug.LogError("SerializerUtility: File path is null or empty.");
				return;
			}

			if (string.IsNullOrEmpty(content))
			{
				Debug.LogError("SerializerUtility: Content is null or empty.");
				return;
			}

			try
			{
				string targetFolder = Path.GetDirectoryName(filePath);
				if (!Directory.Exists(targetFolder))
				{
					Directory.CreateDirectory(targetFolder);
					Debug.Log($"SerializerUtility: Created directory: {targetFolder}");
				}

				File.WriteAllText(filePath, content);
				Debug.Log($"SerializerUtility: Saved text to: {filePath}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"SerializerUtility: Failed to write text to {filePath}: {ex.Message}");
			}
		}
	}
}