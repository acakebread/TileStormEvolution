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
		private const string GitHubApiBaseUrl = "https://api.github.com";
		private const string GitHubApiVersion = "2022-11-28";

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
			public string debugResponse;
		}

		[Serializable]
		private sealed class GitHubRepositoryInfo
		{
			public string owner;
			public string repository;
			public string branch;

			public string ContentsApiBaseUrl => $"{GitHubApiBaseUrl}/repos/{owner}/{repository}";
		}

		[Serializable]
		private sealed class GitHubContentResponse
		{
			public string sha;
			public string content;
			public string encoding;
			public string path;
			public string name;
			public string download_url;
		}

		[Serializable]
		private sealed class GitHubWriteRequest
		{
			public string message;
			public string content;
			public string branch;
			public string sha;
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
			{
				if (Uri.TryCreate(entry.downloadUrl, UriKind.Absolute, out var absolute))
					return absolute.ToString();

				return BuildUrl(entry.downloadUrl);
			}

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

			if (TryGetGitHubRepositoryInfo(baseUrl, out var gitHubRepository))
			{
				yield return UploadCurrentMapToGitHub(map, crop, padded, verbose, gitHubRepository, onSuccess, onError);
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

			if (response != null)
				response.debugResponse = request.downloadHandler?.text;

			onSuccess?.Invoke(response);
		}

		private static IEnumerator UploadCurrentMapToGitHub(Map map, bool crop, bool padded, bool verbose, GitHubRepositoryInfo repository, Action<UploadResponse> onSuccess, Action<string> onError)
		{
			string uploadToken = ApplicationSettings.MapRepositoryUploadKey;
			if (string.IsNullOrWhiteSpace(uploadToken))
			{
				onError?.Invoke("GitHub upload token is not configured.");
				yield break;
			}

			var export = ResourceSerializer.ExportAtomicMap(map, crop: crop, padded: padded, verbose: verbose);
			if (export == null || !export.IsValid)
			{
				onError?.Invoke("Failed to prepare export payload.");
				yield break;
			}

			string mapPath = $"maps/{export.FileName}";
			var mapPayload = export.IsArchive
				? export.Archive
				: Encoding.UTF8.GetBytes(export.Json);
			string mapBase64 = Convert.ToBase64String(mapPayload);
			string mapSha = null;
			string mapFetchError = null;

			yield return FetchGitHubContent(repository, mapPath, uploadToken,
				content => mapSha = content?.sha,
				(statusCode, errorText) =>
				{
					if (statusCode != 404)
						mapFetchError = BuildGitHubError("Failed to inspect existing map file", statusCode, errorText);
				});

			if (!string.IsNullOrWhiteSpace(mapFetchError))
			{
				onError?.Invoke(mapFetchError);
				yield break;
			}

			string mapCommitMessage = $"Publish map {(string.IsNullOrWhiteSpace(map.name) ? "Untitled" : map.name)}";
			string mapWriteError = null;
			yield return PutGitHubContent(repository, mapPath, uploadToken, mapCommitMessage, mapBase64, export.MimeType, mapSha,
				(success, statusCode, responseText) =>
				{
					if (!success)
						mapWriteError = BuildGitHubError("Failed to publish map file", statusCode, responseText);
				});

			if (!string.IsNullOrWhiteSpace(mapWriteError))
			{
				onError?.Invoke(mapWriteError);
				yield break;
			}

			string manifestFetchError = null;
			GitHubContentResponse manifestContent = null;
			yield return FetchGitHubContent(repository, ManifestFileName, uploadToken,
				content => manifestContent = content,
				(statusCode, errorText) =>
				{
					if (statusCode != 404)
						manifestFetchError = BuildGitHubError("Failed to inspect manifest", statusCode, errorText);
				});

			if (!string.IsNullOrWhiteSpace(manifestFetchError))
			{
				onError?.Invoke(manifestFetchError);
				yield break;
			}

			Manifest manifest = null;
			try
			{
				if (!string.IsNullOrWhiteSpace(manifestContent?.content))
				{
					string manifestJson = DecodeGitHubBase64(manifestContent.content, manifestContent.encoding);
					if (!string.IsNullOrWhiteSpace(manifestJson))
						manifest = JsonConvert.DeserializeObject<Manifest>(manifestJson);
				}
			}
			catch (Exception ex)
			{
				onError?.Invoke($"Failed to parse repository manifest: {ex.Message}");
				yield break;
			}

			manifest ??= new Manifest();
			manifest.repositoryName = string.IsNullOrWhiteSpace(manifest.repositoryName) ? "TileStorm Shared Maps" : manifest.repositoryName;
			manifest.generatedUtc = DateTime.UtcNow.ToString("o");
			manifest.entries ??= Array.Empty<Entry>();

			var newEntry = new Entry
			{
				id = export.FileName,
				name = string.IsNullOrWhiteSpace(map.name) ? "Untitled" : map.name,
				fileName = export.FileName,
				downloadUrl = $"maps/{export.FileName}",
				contentType = export.MimeType,
				mapHash = HTB50Settings.ToString(map.HashID),
				description = string.Empty,
				updatedUtc = DateTime.UtcNow.ToString("o"),
				sizeBytes = mapPayload?.LongLength ?? 0L
			};

			var mergedEntries = manifest.entries
				.Where(entry => entry != null && !string.Equals(entry.fileName, newEntry.fileName, StringComparison.OrdinalIgnoreCase))
				.ToList();
			mergedEntries.Add(newEntry);
			mergedEntries = mergedEntries
				.OrderByDescending(entry => entry.UpdatedUtcDateTime)
				.ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
				.ToList();

			manifest.entries = mergedEntries.ToArray();
			string manifestJsonPayload = JsonConvert.SerializeObject(manifest, Formatting.Indented);
			string manifestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJsonPayload));
			string manifestWriteError = null;
			yield return PutGitHubContent(repository, ManifestFileName, uploadToken, "Update TileStorm map manifest", manifestBase64, "application/json; charset=utf-8", manifestContent?.sha,
				(success, statusCode, responseText) =>
				{
					if (!success)
						manifestWriteError = BuildGitHubError("Failed to update manifest", statusCode, responseText);
				});

			if (!string.IsNullOrWhiteSpace(manifestWriteError))
			{
				onError?.Invoke(manifestWriteError);
				yield break;
			}

			onSuccess?.Invoke(new UploadResponse
			{
				ok = true,
				message = $"Published {newEntry.name} to GitHub Pages.",
				entry = newEntry
			});
		}

		private static IEnumerator FetchGitHubContent(GitHubRepositoryInfo repository, string path, string token, Action<GitHubContentResponse> onSuccess, Action<int, string> onNotFoundOrError)
		{
			using var request = UnityWebRequest.Get(BuildGitHubContentsReadUrl(repository, path));
			ApplyGitHubHeaders(request, token);
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				Debug.LogWarning($"SharedMapRepository GitHub GET failed for '{path}': HTTP {(int)request.responseCode} {request.error}\n{request.downloadHandler?.text}");
				onNotFoundOrError?.Invoke((int)request.responseCode, request.error);
				yield break;
			}

			GitHubContentResponse content = null;
			try
			{
				if (!string.IsNullOrWhiteSpace(request.downloadHandler?.text))
					content = JsonConvert.DeserializeObject<GitHubContentResponse>(request.downloadHandler.text);
			}
			catch (Exception ex)
			{
				onNotFoundOrError?.Invoke((int)request.responseCode, ex.Message);
				yield break;
			}

			onSuccess?.Invoke(content);
		}

		private static IEnumerator PutGitHubContent(GitHubRepositoryInfo repository, string path, string token, string message, string base64Content, string contentType, string sha, Action<bool, int, string> onComplete)
		{
			var payload = new GitHubWriteRequest
			{
				message = message,
				content = base64Content,
				branch = string.IsNullOrWhiteSpace(repository.branch) ? "main" : repository.branch,
				sha = string.IsNullOrWhiteSpace(sha) ? null : sha
			};

			string json = JsonConvert.SerializeObject(payload, Formatting.None);
			using var request = new UnityWebRequest(BuildGitHubContentsWriteUrl(repository, path), UnityWebRequest.kHttpVerbPUT);
			request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
			ApplyGitHubHeaders(request, token);
			yield return request.SendWebRequest();

			bool success = request.result == UnityWebRequest.Result.Success && (request.responseCode == 200 || request.responseCode == 201);
			if (!success)
				Debug.LogWarning($"SharedMapRepository GitHub PUT failed for '{path}': HTTP {(int)request.responseCode} {request.error}\n{request.downloadHandler?.text}");
			onComplete?.Invoke(success, (int)request.responseCode, request.downloadHandler?.text);
		}

		private static void ApplyGitHubHeaders(UnityWebRequest request, string token)
		{
			if (request == null)
				return;

			request.SetRequestHeader("Accept", "application/vnd.github+json");
			request.SetRequestHeader("X-GitHub-Api-Version", GitHubApiVersion);

			if (!string.IsNullOrWhiteSpace(token))
				request.SetRequestHeader("Authorization", $"Bearer {token.Trim()}");
		}

		private static string BuildGitHubContentsReadUrl(GitHubRepositoryInfo repository, string path)
		{
			if (repository == null)
				return null;

			path = string.IsNullOrWhiteSpace(path)
				? string.Empty
				: string.Join("/", path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

			string baseUrl = $"{GitHubApiBaseUrl}/repos/{repository.owner}/{repository.repository}/contents";
			string url = string.IsNullOrWhiteSpace(path) ? baseUrl : $"{baseUrl}/{path}";
			string branch = string.IsNullOrWhiteSpace(repository.branch) ? "main" : repository.branch.Trim();
			return $"{url}?ref={Uri.EscapeDataString(branch)}";
		}

		private static string BuildGitHubContentsWriteUrl(GitHubRepositoryInfo repository, string path)
		{
			if (repository == null)
				return null;

			path = string.IsNullOrWhiteSpace(path)
				? string.Empty
				: string.Join("/", path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

			string baseUrl = $"{GitHubApiBaseUrl}/repos/{repository.owner}/{repository.repository}/contents";
			return string.IsNullOrWhiteSpace(path) ? baseUrl : $"{baseUrl}/{path}";
		}

		private static string BuildGitHubError(string prefix, int statusCode, string responseText)
		{
			if (!string.IsNullOrWhiteSpace(responseText))
				return $"{prefix}: HTTP {statusCode} ({responseText})";

			return $"{prefix}: HTTP {statusCode}";
		}

		private static string DecodeGitHubBase64(string content, string encoding)
		{
			if (string.IsNullOrWhiteSpace(content))
				return string.Empty;

			if (!string.IsNullOrWhiteSpace(encoding) && !string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
				return content;

			byte[] data = Convert.FromBase64String(content);
			return Encoding.UTF8.GetString(data);
		}

		private static bool TryGetGitHubRepositoryInfo(string baseUrl, out GitHubRepositoryInfo info)
		{
			info = null;

			string overrideRepository = ApplicationSettings.MapRepositoryGitHubRepository;
			string branch = string.IsNullOrWhiteSpace(ApplicationSettings.MapRepositoryGitHubBranch) ? "main" : ApplicationSettings.MapRepositoryGitHubBranch.Trim();

			if (!string.IsNullOrWhiteSpace(overrideRepository))
			{
				if (!TryParseRepositorySlug(overrideRepository, out var owner, out var repository))
					return false;

				info = new GitHubRepositoryInfo
				{
					owner = owner,
					repository = repository,
					branch = branch
				};
				return true;
			}

			if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
				return false;

			string host = uri.Host ?? string.Empty;
			if (!host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase))
				return false;

			string ownerName = host.Substring(0, host.Length - ".github.io".Length);
			if (string.IsNullOrWhiteSpace(ownerName))
				return false;

			string[] segments = uri.AbsolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			string repositoryName = segments.Length > 0
				? segments[0]
				: $"{ownerName}.github.io";

			info = new GitHubRepositoryInfo
			{
				owner = ownerName,
				repository = repositoryName,
				branch = branch
			};
			return true;
		}

		private static bool TryParseRepositorySlug(string value, out string owner, out string repository)
		{
			owner = null;
			repository = null;

			if (string.IsNullOrWhiteSpace(value))
				return false;

			var parts = value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2)
				return false;

			owner = parts[0].Trim();
			repository = parts[1].Trim();
			return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repository);
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
