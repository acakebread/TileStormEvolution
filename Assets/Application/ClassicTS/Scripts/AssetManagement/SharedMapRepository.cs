using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace ClassicTilestorm
{
	internal static class SharedMapRepository
	{
		private const string ManifestFileName = "manifest.json";
		private const string UploadPath = "upload";
		private const string UploadKeyHeader = "X-TileStorm-Upload-Key";
		private const string FileNameHeader = "X-TileStorm-FileName";
		private const string MapNameHeader = "X-TileStorm-MapName";
		private const string MapHashHeader = "X-TileStorm-MapHash";

		[Serializable]
		internal sealed class Manifest
		{
			public string repositoryName;
			public string generatedUtc;
			public Entry[] entries;
		}

		[Serializable]
		internal sealed class Entry
		{
			public string id;
			public string name;
			public string fileName;
			public string downloadUrl;
			public string contentType;
			public string mapHash;
			public string description;
			public string updatedUtc;
			public long sizeBytes;

			[JsonIgnore]
			public DateTime UpdatedUtcDateTime
			{
				get
				{
					return DateTime.TryParse(updatedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var value)
						? value
						: DateTime.MinValue;
				}
			}

			public string DisplayName => string.IsNullOrWhiteSpace(name)
				? !string.IsNullOrWhiteSpace(fileName)
					? Path.GetFileNameWithoutExtension(fileName)
					: id
				: name;
		}

		[Serializable]
		internal sealed class UploadResponse
		{
			public bool ok;
			public string message;
			public Entry entry;
		}

		internal static bool HasConfiguredBaseUrl => !string.IsNullOrWhiteSpace(ApplicationSettings.MapRepositoryBaseUrl);

		internal static string BuildManifestUrl()
			=> BuildUrl(ManifestFileName);

		internal static string BuildUploadUrl()
			=> BuildUrl(UploadPath);

		internal static string BuildDownloadUrl(Entry entry)
		{
			if (entry == null)
				return null;

			if (!string.IsNullOrWhiteSpace(entry.downloadUrl))
				return entry.downloadUrl;

			if (string.IsNullOrWhiteSpace(entry.fileName))
				return null;

			return BuildUrl("maps/" + entry.fileName);
		}

		internal static IEnumerator FetchManifest(Action<Manifest> onSuccess, Action<string> onError)
		{
			if (!TryGetBaseUrl(out var baseUrl))
			{
				onError?.Invoke("Repository URL is not configured.");
				yield break;
			}

			using var request = UnityWebRequest.Get(BuildManifestUrl());
			request.downloadHandler = new DownloadHandlerBuffer();
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				onError?.Invoke($"Failed to load repository manifest: {request.error}");
				yield break;
			}

			Manifest manifest;
			try
			{
				manifest = JsonConvert.DeserializeObject<Manifest>(request.downloadHandler.text);
			}
			catch (Exception ex)
			{
				onError?.Invoke($"Manifest parse failed: {ex.Message}");
				yield break;
			}

			if (manifest == null)
			{
				onError?.Invoke("Repository manifest was empty.");
				yield break;
			}

			NormalizeManifest(manifest, baseUrl);
			onSuccess?.Invoke(manifest);
		}

		internal static IEnumerator DownloadAndImport(Entry entry, Action<Map> onSuccess, Action<string> onError)
		{
			if (entry == null)
			{
				onError?.Invoke("No repository entry was selected.");
				yield break;
			}

			string downloadUrl = BuildDownloadUrl(entry);
			if (string.IsNullOrWhiteSpace(downloadUrl))
			{
				onError?.Invoke("Selected entry does not have a usable download URL.");
				yield break;
			}

			using var request = UnityWebRequest.Get(downloadUrl);
			request.downloadHandler = new DownloadHandlerBuffer();
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				onError?.Invoke($"Download failed: {request.error}");
				yield break;
			}

			byte[] bytes = request.downloadHandler.data;
			if (bytes == null || bytes.Length == 0)
			{
				onError?.Invoke("Downloaded map package was empty.");
				yield break;
			}

			string tempPath = GetTempPackagePath(entry);
			try
			{
				FileUtils.EnsureFolder(Path.GetDirectoryName(tempPath));
				File.WriteAllBytes(tempPath, bytes);
				var imported = ResourceSerializer.ImportAtomicMap(tempPath);
				if (imported == null)
				{
					onError?.Invoke("Import failed after download.");
					yield break;
				}

				onSuccess?.Invoke(imported);
			}
			catch (Exception ex)
			{
				onError?.Invoke($"Import failed: {ex.Message}");
			}
			finally
			{
				FileUtils.TryDeleteFile(tempPath, nameof(SharedMapRepository));
			}
		}

		internal static IEnumerator UploadCurrentMap(Map map, bool crop, bool padded, bool verbose, Action<UploadResponse> onSuccess, Action<string> onError)
		{
			if (map == null)
			{
				onError?.Invoke("No map is currently loaded.");
				yield break;
			}

			if (!TryGetBaseUrl(out var baseUrl))
			{
				onError?.Invoke("Repository URL is not configured.");
				yield break;
			}

			var export = ResourceSerializer.ExportAtomicMap(map, crop: crop, padded: padded, verbose: verbose);
			if (export == null || !export.IsValid)
			{
				onError?.Invoke("Failed to prepare export payload.");
				yield break;
			}

			byte[] payload = export.IsArchive
				? export.Archive
				: Encoding.UTF8.GetBytes(export.Json);

			using var request = new UnityWebRequest(BuildUploadUrl(), UnityWebRequest.kHttpVerbPOST);
			request.uploadHandler = new UploadHandlerRaw(payload);
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", export.MimeType);
			request.SetRequestHeader(FileNameHeader, export.FileName ?? string.Empty);
			request.SetRequestHeader(MapNameHeader, string.IsNullOrWhiteSpace(map.name) ? "Untitled" : map.name);
			request.SetRequestHeader(MapHashHeader, HTB50Settings.ToString(map.HashID));

			string uploadKey = ApplicationSettings.MapRepositoryUploadKey;
			if (!string.IsNullOrWhiteSpace(uploadKey))
				request.SetRequestHeader(UploadKeyHeader, uploadKey);

			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				string responseText = request.downloadHandler?.text;
				if (!string.IsNullOrWhiteSpace(responseText))
					onError?.Invoke($"Upload failed: {request.error} ({responseText})");
				else
					onError?.Invoke($"Upload failed: {request.error}");
				yield break;
			}

			UploadResponse response = null;
			try
			{
				if (!string.IsNullOrWhiteSpace(request.downloadHandler?.text))
					response = JsonConvert.DeserializeObject<UploadResponse>(request.downloadHandler.text);
			}
			catch (Exception ex)
			{
				onError?.Invoke($"Upload response parse failed: {ex.Message}");
				yield break;
			}

			onSuccess?.Invoke(response);
		}

		private static bool TryGetBaseUrl(out string baseUrl)
		{
			baseUrl = ApplicationSettings.MapRepositoryBaseUrl;
			if (string.IsNullOrWhiteSpace(baseUrl))
				return false;

			baseUrl = baseUrl.Trim();
			return Uri.TryCreate(baseUrl, UriKind.Absolute, out _);
		}

		private static string BuildUrl(string relativePath)
		{
			if (!TryGetBaseUrl(out var baseUrl))
				return null;

			relativePath = string.IsNullOrWhiteSpace(relativePath) ? string.Empty : relativePath.Trim().TrimStart('/');
			if (string.IsNullOrWhiteSpace(relativePath))
				return baseUrl.TrimEnd('/');

			return $"{baseUrl.TrimEnd('/')}/{relativePath}";
		}

		private static void NormalizeManifest(Manifest manifest, string baseUrl)
		{
			if (manifest.entries == null)
			{
				manifest.entries = Array.Empty<Entry>();
				return;
			}

			string root = baseUrl.TrimEnd('/');
			foreach (var entry in manifest.entries)
			{
				if (entry == null)
					continue;

				if (string.IsNullOrWhiteSpace(entry.downloadUrl) && !string.IsNullOrWhiteSpace(entry.fileName))
					entry.downloadUrl = $"{root}/maps/{entry.fileName}";
			}
		}

		private static string GetTempPackagePath(Entry entry)
		{
			string root = !string.IsNullOrWhiteSpace(Application.temporaryCachePath)
				? Application.temporaryCachePath
				: Application.persistentDataPath;
			if (string.IsNullOrWhiteSpace(root))
				root = Path.GetTempPath();

			string folder = Path.Combine(root, "RemoteMapRepository");
			string fileName = string.IsNullOrWhiteSpace(entry?.fileName)
				? $"map_{Guid.NewGuid():N}.bin"
				: Path.GetFileName(entry.fileName);
			return Path.Combine(folder, fileName);
		}
	}
}
