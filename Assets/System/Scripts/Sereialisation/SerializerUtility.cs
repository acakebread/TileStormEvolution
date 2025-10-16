using System;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class SerializerUtility
	{
		public static string ReadTextAsset(string filePath, TextAsset sourceAsset = null)
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
					string targetFolder = Path.GetDirectoryName(filePath);
					if (!Directory.Exists(targetFolder))
					{
						Directory.CreateDirectory(targetFolder);
						Debug.Log($"SerializerUtility: Created directory: {targetFolder}");
					}

					if (sourceAsset != null)
					{
						File.WriteAllText(filePath, sourceAsset.text);
						Debug.Log($"SerializerUtility: Copied text asset to: {filePath}");
					}
					else
					{
						Debug.LogError($"SerializerUtility: Cannot read {filePath}: File does not exist and no source TextAsset provided.");
						return null;
					}
				}

				return File.ReadAllText(filePath);
			}
			catch (Exception ex)
			{
				Debug.LogError($"SerializerUtility: Failed to read text asset from {filePath}: {ex.Message}");
				return null;
			}
		}

		public static void WriteTextFile(string filePath, string content)
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
				Debug.Log($"SerializerUtility: Saved text file to: {filePath}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"SerializerUtility: Failed to write text file to {filePath}: {ex.Message}");
			}
		}
	}
}