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

		[Serializable]
		private sealed class GitHubDeleteRequest
		{
			public string message;
			public string sha;
			public string branch;
		}

		internal static bool HasConfiguredBaseUrl => !string.IsNullOrWhiteSpace(ApplicationSettings.MapRepositoryBaseUrl);

		internal static string BuildManifestUrl(bool cacheBust = false)
		{
			string url = BuildUrl(ManifestFileName);
			return cacheBust ? AddCacheBust(url) : url;
		}

		internal static string BuildUploadUrl()
			=> BuildUrl(UploadPath);

		internal static string BuildDownloadUrl(Entry entry)
		{
			if (entry == null)
				return null;

			if (!string.IsNullOrWhiteSpace(entry.downloadUrl))
			{
				if (Uri.TryCreate(entry.downloadUrl, UriKind.Absolute, out var absolute))
					return AddCacheBust(absolute.ToString());

				return AddCacheBust(BuildUrl(entry.downloadUrl));
			}

			if (string.IsNullOrWhiteSpace(entry.fileName))
				return null;

			return AddCacheBust(BuildUrl("maps/" + entry.fileName));
		}

		internal static string BuildThumbnailUrl(Entry entry)
		{
			if (entry == null)
				return null;

			string key = BuildThumbnailKey(entry);
			if (string.IsNullOrWhiteSpace(key))
				return null;

			return BuildUrl($"thumbs/{key}.png");
		}

		internal static IEnumerator FetchManifest(Action<Manifest> onSuccess, Action<string> onError)
		{
			if (!TryGetBaseUrl(out var baseUrl))
			{
				onError?.Invoke("Repository URL is not configured.");
				yield break;
			}

			using var request = UnityWebRequest.Get(BuildManifestUrl(cacheBust: true));
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

			if (LooksLikeHtml(bytes))
			{
				string sample = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 120)).Trim();
				onError?.Invoke($"Downloaded content was HTML instead of a map package. Check the launch URL or repository file URL. Sample: {sample}");
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

		internal static IEnumerator DeleteMap(Entry entry, Action<string> onSuccess, Action<string> onError)
		{
			if (entry == null)
			{
				onError?.Invoke("No repository entry was selected.");
				yield break;
			}

			if (!TryGetBaseUrl(out var baseUrl))
			{
				onError?.Invoke("Repository URL is not configured.");
				yield break;
			}

			if (!TryGetGitHubRepositoryInfo(baseUrl, out var repository))
			{
				onError?.Invoke("Deletion is currently only supported for GitHub Pages repositories.");
				yield break;
			}

			string token = ApplicationSettings.MapRepositoryUploadKey;
			if (string.IsNullOrWhiteSpace(token))
			{
				onError?.Invoke("GitHub upload token is not configured.");
				yield break;
			}

			string filePath = string.IsNullOrWhiteSpace(entry.fileName)
				? null
				: $"maps/{Path.GetFileName(entry.fileName)}";

			if (!string.IsNullOrWhiteSpace(filePath))
			{
				string mapSha = null;
				string mapFetchError = null;
				yield return FetchGitHubContent(repository, filePath, token,
					content => mapSha = content?.sha,
					(statusCode, errorText) =>
					{
						if (statusCode != 404)
							mapFetchError = BuildGitHubError("Failed to inspect map file before deletion", statusCode, errorText);
					});

				if (!string.IsNullOrWhiteSpace(mapFetchError))
				{
					onError?.Invoke(mapFetchError);
					yield break;
				}

				if (!string.IsNullOrWhiteSpace(mapSha))
				{
					string deleteError = null;
					yield return DeleteGitHubContent(repository, filePath, token, $"Delete TileStorm map {entry.DisplayName}", mapSha,
						(success, statusCode, responseText) =>
						{
							if (!success)
								deleteError = BuildGitHubError("Failed to delete map file", statusCode, responseText);
						});

					if (!string.IsNullOrWhiteSpace(deleteError))
					{
						onError?.Invoke(deleteError);
						yield break;
					}
				}
			}

			string thumbnailPath = BuildThumbnailPath(entry);
			if (!string.IsNullOrWhiteSpace(thumbnailPath))
			{
				string thumbSha = null;
				string thumbFetchError = null;
				yield return FetchGitHubContent(repository, thumbnailPath, token,
					content => thumbSha = content?.sha,
					(statusCode, errorText) =>
					{
						if (statusCode != 404)
							thumbFetchError = BuildGitHubError("Failed to inspect thumbnail before deletion", statusCode, errorText);
					});

				if (!string.IsNullOrWhiteSpace(thumbFetchError))
				{
					onError?.Invoke(thumbFetchError);
					yield break;
				}

				if (!string.IsNullOrWhiteSpace(thumbSha))
				{
					string deleteError = null;
					yield return DeleteGitHubContent(repository, thumbnailPath, token, $"Delete TileStorm map thumbnail {entry.DisplayName}", thumbSha,
						(success, statusCode, responseText) =>
						{
							if (!success)
								deleteError = BuildGitHubError("Failed to delete thumbnail file", statusCode, responseText);
						});

					if (!string.IsNullOrWhiteSpace(deleteError))
						Debug.LogWarning(deleteError);
				}
			}

			string manifestFetchError = null;
			GitHubContentResponse manifestContent = null;
			yield return FetchGitHubContent(repository, ManifestFileName, token,
				content => manifestContent = content,
				(statusCode, errorText) =>
				{
					if (statusCode != 404)
						manifestFetchError = BuildGitHubError("Failed to inspect manifest before deletion", statusCode, errorText);
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
			manifest.entries ??= Array.Empty<Entry>();

			int beforeCount = manifest.entries.Length;
			manifest.entries = manifest.entries
				.Where(existing => existing != null && !EntryMatches(existing, entry))
				.OrderByDescending(existing => existing.UpdatedUtcDateTime)
				.ThenBy(existing => existing.DisplayName, StringComparer.OrdinalIgnoreCase)
				.ToArray();
			manifest.generatedUtc = DateTime.UtcNow.ToString("o");

			if (manifestContent == null && beforeCount == manifest.entries.Length)
			{
				onSuccess?.Invoke($"Deleted {entry.DisplayName} file if present. No manifest entry was found.");
				yield break;
			}

			string manifestJsonPayload = JsonConvert.SerializeObject(manifest, Formatting.Indented);
			string manifestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(manifestJsonPayload));
			string manifestWriteError = null;
			yield return PutGitHubContent(repository, ManifestFileName, token, "Update TileStorm map manifest after deletion", manifestBase64, "application/json; charset=utf-8", manifestContent?.sha,
				(success, statusCode, responseText) =>
				{
					if (!success)
						manifestWriteError = BuildGitHubError("Failed to update manifest after deletion", statusCode, responseText);
				});

			if (!string.IsNullOrWhiteSpace(manifestWriteError))
			{
				onError?.Invoke(manifestWriteError);
				yield break;
			}

			onSuccess?.Invoke($"Deleted {entry.DisplayName} from the shared repository.");
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

			var mapPayload = export.IsArchive
				? export.Archive
				: Encoding.UTF8.GetBytes(export.Json);
			string repositoryFileName = BuildRepositoryMapFileName(map, export);
			string mapBase64 = Convert.ToBase64String(mapPayload);
			string mapSha = null;
			string mapFetchError = null;
			string mapPath = $"maps/{repositoryFileName}";

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

			byte[] thumbnailBytes = null;
			string thumbnailError = null;
			yield return MapThumbnailGenerator.CapturePng(
				map,
				bytes => thumbnailBytes = bytes,
				error => thumbnailError = error,
				MapThumbnailGenerator.DefaultThumbnailWidth,
				MapThumbnailGenerator.DefaultThumbnailHeight);

			if (!string.IsNullOrWhiteSpace(thumbnailError))
				Debug.LogWarning(thumbnailError);

			string thumbnailPath = BuildThumbnailPath(map, export);
			if (thumbnailBytes != null && thumbnailBytes.Length > 0 && !string.IsNullOrWhiteSpace(thumbnailPath))
			{
				string thumbnailBase64 = Convert.ToBase64String(thumbnailBytes);
				string thumbnailSha = null;
				string thumbnailFetchError = null;
				yield return FetchGitHubContent(repository, thumbnailPath, uploadToken,
					content => thumbnailSha = content?.sha,
					(statusCode, errorText) =>
					{
						if (statusCode != 404)
							thumbnailFetchError = BuildGitHubError("Failed to inspect existing thumbnail file", statusCode, errorText);
					});

				if (!string.IsNullOrWhiteSpace(thumbnailFetchError))
				{
					onError?.Invoke(thumbnailFetchError);
					yield break;
				}

				string thumbnailCommitMessage = $"Publish map thumbnail {(string.IsNullOrWhiteSpace(map.name) ? "Untitled" : map.name)}";
				string thumbnailWriteError = null;
				yield return PutGitHubContent(repository, thumbnailPath, uploadToken, thumbnailCommitMessage, thumbnailBase64, "image/png", thumbnailSha,
					(success, statusCode, responseText) =>
					{
						if (!success)
							thumbnailWriteError = BuildGitHubError("Failed to publish thumbnail file", statusCode, responseText);
					});

				if (!string.IsNullOrWhiteSpace(thumbnailWriteError))
				{
					Debug.LogWarning(thumbnailWriteError);
				}
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
				id = repositoryFileName,
				name = string.IsNullOrWhiteSpace(map.name) ? "Untitled" : map.name,
				fileName = repositoryFileName,
				downloadUrl = $"maps/{repositoryFileName}",
				contentType = export.MimeType,
				mapHash = HTB50Settings.ToString(map.HashID),
				description = string.Empty,
				updatedUtc = DateTime.UtcNow.ToString("o"),
				sizeBytes = mapPayload?.LongLength ?? 0L
			};

			var mergedEntries = manifest.entries
				.Where(entry => entry != null && !EntryMatches(entry, newEntry))
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

		private static string BuildRepositoryMapFileName(Map map, ResourceSerializer.AtomicMapExportData export)
		{
			string baseName = BuildRepositoryResourceBaseName(map, export);
			string extension = export.IsArchive ? ".zip" : ".json";
			return $"{baseName}{extension}";
		}

		private static string BuildThumbnailPath(Map map, ResourceSerializer.AtomicMapExportData export)
		{
			string baseName = BuildRepositoryResourceBaseName(map, export);
			return string.IsNullOrWhiteSpace(baseName) ? null : $"thumbs/{baseName}.png";
		}

		private static string BuildThumbnailPath(Entry entry)
		{
			string baseName = BuildThumbnailKey(entry);
			return string.IsNullOrWhiteSpace(baseName) ? null : $"thumbs/{baseName}.png";
		}

		private static string BuildThumbnailKey(Entry entry)
		{
			if (entry == null)
				return null;

			if (!string.IsNullOrWhiteSpace(entry.mapHash))
				return entry.mapHash.Trim();

			if (!string.IsNullOrWhiteSpace(entry.fileName))
				return Path.GetFileNameWithoutExtension(entry.fileName).Trim();

			return null;
		}

		private static string BuildRepositoryResourceBaseName(Map map, ResourceSerializer.AtomicMapExportData export)
		{
			if (map != null)
				map.EnsureHashID();

			string hash = HTB50Settings.ToString(map?.HashID ?? 0);
			if (string.IsNullOrWhiteSpace(hash))
				hash = Path.GetFileNameWithoutExtension(export.FileName);

			return hash;
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

		private static IEnumerator DeleteGitHubContent(GitHubRepositoryInfo repository, string path, string token, string message, string sha, Action<bool, int, string> onComplete)
		{
			var payload = new GitHubDeleteRequest
			{
				message = message,
				sha = sha,
				branch = string.IsNullOrWhiteSpace(repository.branch) ? "main" : repository.branch
			};

			string json = JsonConvert.SerializeObject(payload, Formatting.None);
			using var request = new UnityWebRequest(BuildGitHubContentsWriteUrl(repository, path), UnityWebRequest.kHttpVerbDELETE);
			request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
			ApplyGitHubHeaders(request, token);
			yield return request.SendWebRequest();

			bool success = request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
			if (!success)
				Debug.LogWarning($"SharedMapRepository GitHub DELETE failed for '{path}': HTTP {(int)request.responseCode} {request.error}\n{request.downloadHandler?.text}");
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

		private static bool EntryMatches(Entry left, Entry right)
		{
			if (left == null || right == null)
				return false;

			return Matches(left.fileName, right.fileName) ||
			       Matches(left.id, right.id) ||
			       Matches(left.mapHash, right.mapHash) ||
			       Matches(left.fileName, right.id) ||
			       Matches(left.id, right.fileName);

			static bool Matches(string a, string b)
				=> !string.IsNullOrWhiteSpace(a) &&
				   !string.IsNullOrWhiteSpace(b) &&
				   string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
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

		private static bool LooksLikeHtml(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
				return false;

			string prefix = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 32)).TrimStart('\uFEFF', '\u0000', ' ', '\t', '\r', '\n');
			return prefix.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
			       prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
			       prefix.StartsWith("<head", StringComparison.OrdinalIgnoreCase);
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

		private static string AddCacheBust(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return url;

			string separator = url.Contains("?") ? "&" : "?";
			return $"{url}{separator}ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
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
