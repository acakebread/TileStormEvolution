using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class CompressionUtility
	{
		/// <summary>
		/// Compresses a string (e.g., JSON) into a gzip-compressed byte array.
		/// </summary>
		/// <param name="input">The input string to compress.</param>
		/// <returns>The compressed byte array, or null if compression fails.</returns>
		public static byte[] Compress(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				Debug.LogError("CompressionUtility: Cannot compress null or empty input.");
				return null;
			}

			try
			{
				byte[] inputBytes = Encoding.UTF8.GetBytes(input);
				using (MemoryStream memoryStream = new MemoryStream())
				{
					using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
					{
						gzipStream.Write(inputBytes, 0, inputBytes.Length);
					}
					byte[] compressedBytes = memoryStream.ToArray();
					Debug.Log($"CompressionUtility: Compressed {input.Length} chars to {compressedBytes.Length} bytes");
					return compressedBytes;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"CompressionUtility: Compression failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
				return null;
			}
		}

		/// <summary>
		/// Decompresses a gzip-compressed byte array into a string.
		/// </summary>
		/// <param name="compressedData">The compressed byte array to decompress.</param>
		/// <returns>The decompressed string, or null if decompression fails.</returns>
		public static string Decompress(byte[] compressedData)
		{
			if (compressedData == null || compressedData.Length == 0)
			{
				Debug.LogError("CompressionUtility: Cannot decompress null or empty data.");
				return null;
			}

			try
			{
				using (MemoryStream memoryStream = new MemoryStream(compressedData))
				using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
				using (MemoryStream outputStream = new MemoryStream())
				{
					gzipStream.CopyTo(outputStream);
					string decompressed = Encoding.UTF8.GetString(outputStream.ToArray());
					Debug.Log($"CompressionUtility: Decompressed {compressedData.Length} bytes to {decompressed.Length} chars");
					return decompressed;
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"CompressionUtility: Decompression failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
				return null;
			}
		}

		/// <summary>
		/// Saves a compressed byte array to a file.
		/// </summary>
		/// <param name="compressedData">The compressed byte array to save.</param>
		/// <param name="filePath">The file path to save to (e.g., "path/to/file.gz").</param>
		public static void SaveCompressedData(byte[] compressedData, string filePath)
		{
			if (compressedData == null || compressedData.Length == 0)
			{
				Debug.LogError($"CompressionUtility: Cannot save null or empty compressed data to {filePath}");
				return;
			}

			try
			{
				string outputDir = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
				{
					Directory.CreateDirectory(outputDir);
					Debug.Log($"CompressionUtility: Created directory: {outputDir}");
				}

				File.WriteAllBytes(filePath, compressedData);
				Debug.Log($"CompressionUtility: Saved compressed data to {filePath} ({compressedData.Length} bytes)");
			}
			catch (Exception ex)
			{
				Debug.LogError($"CompressionUtility: Failed to save compressed data to {filePath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
			}
		}

		/// <summary>
		/// Loads a compressed byte array from a file.
		/// </summary>
		/// <param name="filePath">The file path to load from (e.g., "path/to/file.gz").</param>
		/// <returns>The compressed byte array, or null if loading fails.</returns>
		public static byte[] LoadCompressedData(string filePath)
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError($"CompressionUtility: File does not exist: {filePath}");
				return null;
			}

			try
			{
				byte[] compressedData = File.ReadAllBytes(filePath);
				Debug.Log($"CompressionUtility: Loaded compressed data from {filePath} ({compressedData.Length} bytes)");
				return compressedData;
			}
			catch (Exception ex)
			{
				Debug.LogError($"CompressionUtility: Failed to load compressed data from {filePath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
				return null;
			}
		}
	}
}