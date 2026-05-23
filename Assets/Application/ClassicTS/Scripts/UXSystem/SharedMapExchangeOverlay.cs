using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassicTilestorm.Assets;
using MassiveHadronLtd;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ClassicTilestorm
{
	public sealed class SharedMapExchangeOverlay : MonoBehaviour
	{
		private static SharedMapExchangeOverlay instance;
		private static readonly Rect DefaultWindowRect = new Rect(40f, 80f, 840f, 600f);
		private static GUIStyle windowStyle;
		private static GUIStyle helpStyle;
		private static GUIStyle keyStyle;
		private static GUIStyle localBoxStyle;
		private static GUIStyle remoteBoxStyle;
		private static GUIStyle mixedBoxStyle;

		private bool isOpen;
		private bool isRefreshing;
		private Rect windowRect = DefaultWindowRect;
		private Vector2 scroll;
		private string statusLine;
		private string detailLine;
		private string repositoryUrl;
		private IReadOnlyList<SharedMapRepository.Entry> entries = Array.Empty<SharedMapRepository.Entry>();
		private IReadOnlyList<CatalogRow> catalogRows = Array.Empty<CatalogRow>();
		private SharedMapRepository.Entry pendingDeleteEntry;
		private ImguiRaycastBlocker raycastBlocker;
		private float deleteConfirmUntil;

		public static void Open() => EnsureInstance().SetOpen(true);
		public static void Toggle()
		{
			var overlay = EnsureInstance();
			overlay.SetOpen(!overlay.isOpen);
		}

		public static void Close()
		{
			if (instance != null)
				instance.SetOpen(false);
		}

		private static SharedMapExchangeOverlay EnsureInstance()
		{
			if (instance != null)
				return instance;

			instance = FindAnyObjectByType<SharedMapExchangeOverlay>(FindObjectsInactive.Include);
			if (instance != null)
				return instance;

			var go = new GameObject(nameof(SharedMapExchangeOverlay));
			go.hideFlags = HideFlags.HideAndDontSave;
			DontDestroyOnLoad(go);
			instance = go.AddComponent<SharedMapExchangeOverlay>();
			return instance;
		}

		private void Awake()
		{
			if (instance != null && instance != this)
			{
				Destroy(gameObject);
				return;
			}

			instance = this;
			DontDestroyOnLoad(gameObject);
			repositoryUrl = ApplicationSettings.MapRepositoryBaseUrl;
			enabled = false;
		}

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;

			raycastBlocker?.Destroy();
			raycastBlocker = null;
		}

		private void OnDisable()
		{
			raycastBlocker?.SetVisible(false);
		}

		private void SetOpen(bool open)
		{
			isOpen = open;
			if (isOpen)
			{
				repositoryUrl = ApplicationSettings.MapRepositoryBaseUrl;
				enabled = true;
				SyncRaycastBlocker();
				RefreshRepository();
			}
			else
			{
				raycastBlocker?.SetVisible(false);
				enabled = false;
			}
		}

		private void OnGUI()
		{
			if (!isOpen)
				return;

			var previousRect = windowRect;
			windowRect = GUILayout.Window(0x5A11D, windowRect, DrawWindow, "Map Catalogue", GetWindowStyle());
			SyncRaycastBlocker();
			ImguiInputBlocker.BlockMouseInput(windowRect);
			ImguiInputBlocker.BlockMouseInput(previousRect);
		}

		private void SyncRaycastBlocker()
		{
			if (!isOpen)
			{
				raycastBlocker?.SetVisible(false);
				return;
			}

			raycastBlocker ??= new ImguiRaycastBlocker($"{nameof(SharedMapExchangeOverlay)} Raycast Blocker");
			raycastBlocker.Sync(windowRect);
		}

		private void DrawWindow(int windowId)
		{
			GUILayout.Label("This catalogue combines the current loaded map, local user maps, and the live shared repository manifest. Built-in levels are hidden.", GetHelpStyle());
			GUILayout.Label("GitHub Pages can take a short while to publish changes, so Refresh shows whatever the public manifest says right now.", GetHelpStyle());

			GUILayout.Space(10f);
			GUILayout.BeginHorizontal();
			GUILayout.Label("Repo", GUILayout.Width(50f));
			GUILayout.Label(string.IsNullOrWhiteSpace(ApplicationSettings.MapRepositoryBaseUrl) ? "<not configured>" : ApplicationSettings.MapRepositoryBaseUrl);
			if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
				RefreshRepository();
			if (GUILayout.Button("Close", GUILayout.Width(80f)))
				SetOpen(false);
			GUILayout.EndHorizontal();

			GUILayout.Space(6f);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Import / Update All Remote", GUILayout.Height(30f)))
				ImportAllRemoteMaps();
			GUILayout.EndHorizontal();

			if (!string.IsNullOrWhiteSpace(statusLine))
			{
				GUILayout.Space(6f);
				GUILayout.Label(statusLine, GetHelpStyle());
			}
			if (!string.IsNullOrWhiteSpace(detailLine))
			{
				GUILayout.Label(detailLine, GetHelpStyle());
			}

			GUILayout.Space(8f);
			if (isRefreshing)
				GUILayout.Label("Loading repository manifest...");

			GUILayout.Label("Maps:");
			scroll = GUILayout.BeginScrollView(scroll);

			if (catalogRows == null || catalogRows.Count == 0)
			{
				GUILayout.Label("No maps found.");
			}
			else
			{
				foreach (var row in catalogRows)
					DrawRow(row);
			}

			GUILayout.EndScrollView();

			GUI.DragWindow(new Rect(0, 0, 10000, 24));
		}

		private void DrawRow(CatalogRow row)
		{
			if (row == null)
				return;

			GUILayout.BeginVertical(GetRowStyle(row));
			GUILayout.Label(row.DisplayName);
			GUILayout.Label(row.SourceSummary, GetHelpStyle());
			if (!string.IsNullOrWhiteSpace(row.FileSummary))
				GUILayout.Label(row.FileSummary, GetHelpStyle());
			if (!string.IsNullOrWhiteSpace(row.RemoteSummary))
				GUILayout.Label(row.RemoteSummary, GetHelpStyle());
			if (!string.IsNullOrWhiteSpace(row.KeySummary))
				GUILayout.Label(row.KeySummary, GetKeyStyle());

			GUILayout.BeginHorizontal();
			if (row.HasStoredLocal && GUILayout.Button("Load", GUILayout.Width(80f)))
				LoadLocalMap(row);
			if (row.HasRemote && GUILayout.Button(row.HasLocal ? "Update Local" : "Import", GUILayout.Width(110f)))
				StartImport(row.RemoteEntry);
			if (row.HasLocal && GUILayout.Button(row.IsCurrent ? "Publish Loaded" : "Publish", GUILayout.Width(120f)))
				PublishLocalMap(row);
#if UNITY_EDITOR
			if (row.HasRemote && ApplicationSettings.HasPrivateMapRepositoryUploadKey && GUILayout.Button(IsDeleteConfirmationActive(row.RemoteEntry) ? "Confirm Delete" : "Delete Remote", GUILayout.Width(130f)))
				DeleteMap(row.RemoteEntry);
#endif
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}

		private void RefreshRepository()
		{
			if (isRefreshing)
				return;

			repositoryUrl = ApplicationSettings.MapRepositoryBaseUrl;
			if (string.IsNullOrWhiteSpace(repositoryUrl))
			{
				statusLine = "Repository URL is not configured.";
				return;
			}

			StartCoroutine(LoadRepositoryCoroutine());
		}

		private System.Collections.IEnumerator LoadRepositoryCoroutine()
		{
			isRefreshing = true;
			statusLine = "Loading repository...";

			yield return SharedMapRepository.FetchManifest(
				manifest =>
				{
					entries = manifest?.entries ?? Array.Empty<SharedMapRepository.Entry>();
					RebuildCatalogRows(forceRefreshLocal: true);
					pendingDeleteEntry = null;
					statusLine = string.IsNullOrWhiteSpace(manifest?.repositoryName)
						? $"Loaded {catalogRows.Count} catalogue entries, including {entries.Count} remote map(s)."
						: $"Loaded {catalogRows.Count} catalogue entries, including {entries.Count} remote map(s) from {manifest.repositoryName}.";
					detailLine = $"Manifest source: {SharedMapRepository.BuildManifestUrl()}";
				},
				error =>
				{
					statusLine = error;
					detailLine = null;
				});

			isRefreshing = false;
		}

		private void RebuildCatalogRows(bool forceRefreshLocal)
		{
			var rows = new Dictionary<string, CatalogRow>(StringComparer.OrdinalIgnoreCase);
			var internalHashes = MapCatalog.GetInternalMaps(forceRefreshLocal)
				.Where(entry => entry.HashId != 0)
				.Select(entry => entry.HashId)
				.ToHashSet();

			foreach (var persistentEntry in MapCatalog.GetExternalMaps(forceRefreshLocal))
			{
				if (internalHashes.Contains(persistentEntry.HashId))
					continue;

				var row = GetOrCreateRow(rows, BuildLocalKey(persistentEntry));
				row.PersistentEntry = persistentEntry;
			}

			foreach (var remoteEntry in entries ?? Array.Empty<SharedMapRepository.Entry>())
			{
				if (remoteEntry == null)
					continue;

				if (TryGetRemoteHash(remoteEntry, out var remoteHash) && internalHashes.Contains(remoteHash))
					continue;

				var row = GetOrCreateRow(rows, BuildRemoteKey(remoteEntry));
				if (!row.HasRemote || remoteEntry.UpdatedUtcDateTime > row.RemoteEntry.UpdatedUtcDateTime)
					row.RemoteEntry = remoteEntry;
			}

			var currentMap = MainController.CurrentMap;
			if (currentMap != null)
			{
				currentMap.EnsureHashID();
				if (currentMap.HashID != 0 && !internalHashes.Contains(currentMap.HashID))
					GetOrCreateRow(rows, BuildHashKey(currentMap.HashID)).CurrentMap = currentMap;
			}

			catalogRows = rows.Values
				.Select(row =>
				{
					row.RefreshDerivedState();
					return row;
				})
				.OrderByDescending(row => row.IsCurrent)
				.ThenByDescending(row => row.RemoteUpdatedUtc)
				.ThenBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private static CatalogRow GetOrCreateRow(Dictionary<string, CatalogRow> rows, string key)
		{
			key = string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("N") : key;
			if (!rows.TryGetValue(key, out var row))
			{
				row = new CatalogRow { Key = key };
				rows[key] = row;
			}

			return row;
		}

		private void StartImport(SharedMapRepository.Entry entry)
		{
			if (entry == null)
			{
				statusLine = "No entry selected.";
				return;
			}

			if (isRefreshing)
				return;

			StartCoroutine(ImportCoroutine(entry));
		}

		private System.Collections.IEnumerator ImportCoroutine(SharedMapRepository.Entry entry)
		{
			isRefreshing = true;
			statusLine = $"Downloading {entry.DisplayName}...";
			detailLine = null;

			yield return SharedMapRepository.DownloadAndImport(
				entry,
				imported =>
				{
					if (imported == null)
					{
						statusLine = "Import finished without a map.";
						return;
					}

					var controller = FindAnyObjectByType<MainController>(FindObjectsInactive.Include);
					if (controller == null)
					{
						statusLine = "Map imported, but no MainController was available to load it.";
						return;
					}

					ApplicationSettings.LoadMapName = HTB50Settings.ToString(imported.HashID);
					controller.LoadMap(ApplicationSettings.LoadMapName);
					statusLine = $"Imported {imported.name}.";
					detailLine = null;
					RebuildCatalogRows(forceRefreshLocal: true);
				},
				error =>
				{
					statusLine = error;
					detailLine = null;
				});

			isRefreshing = false;
		}

		private void LoadLocalMap(CatalogRow row)
		{
			if (row == null || !row.HasLocal)
				return;

			var controller = FindAnyObjectByType<MainController>(FindObjectsInactive.Include);
			if (controller == null)
			{
				statusLine = "No MainController was available to load the map.";
				return;
			}

			ApplicationSettings.LoadMapName = HTB50Settings.ToString(row.LocalHash);
			controller.LoadMap(ApplicationSettings.LoadMapName);
			statusLine = $"Loaded {row.DisplayName}.";
			RebuildCatalogRows(forceRefreshLocal: false);
		}

		private void PublishLocalMap(CatalogRow row)
		{
			if (isRefreshing)
				return;

			if (row == null || !row.HasLocal)
			{
				statusLine = "No local map is available to publish.";
				return;
			}

			if (string.IsNullOrWhiteSpace(ApplicationSettings.MapRepositoryUploadKey))
			{
				statusLine = "Upload token is not configured.";
				return;
			}

			var map = row.IsCurrent && MainController.CurrentMap != null
				? MainController.CurrentMap
				: row.LocalMap;

			if (map == null)
			{
				statusLine = "The selected local map could not be loaded for publishing.";
				return;
			}

			StartCoroutine(UploadCoroutine(map));
		}

#if UNITY_EDITOR
		private void DeleteMap(SharedMapRepository.Entry entry)
		{
			if (isRefreshing)
				return;

			if (entry == null)
			{
				statusLine = "No shared map entry was provided for deletion.";
				return;
			}

			if (!ApplicationSettings.HasPrivateMapRepositoryUploadKey)
			{
				statusLine = "Admin deletion is not enabled on this machine.";
				return;
			}

			if (!IsDeleteConfirmationActive(entry))
			{
				pendingDeleteEntry = entry;
				deleteConfirmUntil = Time.realtimeSinceStartup + 8f;
				statusLine = $"Press Delete again on {entry.DisplayName} to permanently remove it.";
				detailLine = "This deletes the repository file and removes it from manifest.json.";
				return;
			}

			StartCoroutine(DeleteCoroutine(entry));
		}

		private bool IsDeleteConfirmationActive(SharedMapRepository.Entry entry)
			=> entry != null && pendingDeleteEntry == entry && Time.realtimeSinceStartup <= deleteConfirmUntil;

		private System.Collections.IEnumerator DeleteCoroutine(SharedMapRepository.Entry entry)
		{
			isRefreshing = true;
			statusLine = $"Deleting {entry.DisplayName}...";
			detailLine = null;

			yield return SharedMapRepository.DeleteMap(
				entry,
				message =>
				{
					statusLine = string.IsNullOrWhiteSpace(message) ? "Delete completed." : message;
					detailLine = null;
				},
				error =>
				{
					statusLine = error;
					detailLine = null;
				});

			pendingDeleteEntry = null;
			deleteConfirmUntil = 0f;
			isRefreshing = false;
			RefreshRepository();
		}
#endif

		private void ImportAllRemoteMaps()
		{
			if (isRefreshing)
				return;

			var remoteRows = catalogRows?.Where(row => row != null && row.HasRemote).ToArray() ?? Array.Empty<CatalogRow>();
			if (remoteRows.Length == 0)
			{
				statusLine = "No remote maps are available to import or update.";
				return;
			}

			StartCoroutine(ImportAllRemoteCoroutine(remoteRows));
		}

		private System.Collections.IEnumerator ImportAllRemoteCoroutine(IReadOnlyList<CatalogRow> remoteRows)
		{
			isRefreshing = true;
			int importedCount = 0;
			var errors = new List<string>();

			for (int i = 0; i < remoteRows.Count; i++)
			{
				var row = remoteRows[i];
				if (row?.RemoteEntry == null)
					continue;

				statusLine = $"Importing remote map {i + 1}/{remoteRows.Count}: {row.RemoteEntry.DisplayName}...";
				bool imported = false;
				string importError = null;
				yield return SharedMapRepository.DownloadAndImport(
					row.RemoteEntry,
					map =>
					{
						imported = map != null;
					},
					error =>
					{
						importError = error;
					});

				if (imported)
					importedCount++;
				else if (!string.IsNullOrWhiteSpace(importError))
					errors.Add($"{row.RemoteEntry.DisplayName}: {importError}");
			}

			RebuildCatalogRows(forceRefreshLocal: true);
			statusLine = $"Imported/updated {importedCount} remote map(s).";
			detailLine = errors.Count == 0 ? null : string.Join("\n", errors.Take(3));
			isRefreshing = false;
		}

		private System.Collections.IEnumerator UploadCoroutine(Map map)
		{
			isRefreshing = true;
			statusLine = "Uploading current map...";
			detailLine = null;

			yield return SharedMapRepository.UploadCurrentMap(
				map,
				crop: true,
				padded: false,
				verbose: false,
				onSuccess: response =>
				{
					statusLine = response != null && !string.IsNullOrWhiteSpace(response.message)
						? response.message
						: "Upload completed.";
					detailLine = response != null && !string.IsNullOrWhiteSpace(response.debugResponse)
						? response.debugResponse
						: null;
				},
				onError: error =>
				{
					statusLine = error;
					detailLine = null;
				});

			isRefreshing = false;
			RefreshRepository();
		}

		private static string BuildLocalKey(MapCatalog.MapEntry entry)
			=> BuildHashKey(entry.HashId);

		private static string BuildRemoteKey(SharedMapRepository.Entry entry)
		{
			if (TryGetRemoteHash(entry, out var hash))
				return BuildHashKey(hash);

			return string.IsNullOrWhiteSpace(entry?.fileName)
				? $"remote:{entry?.id}"
				: $"remote:{entry.fileName}";
		}

		private static bool TryGetRemoteHash(SharedMapRepository.Entry entry, out HashId hash)
		{
			hash = 0;
			if (entry == null)
				return false;

			if (TryParseHash(entry.mapHash, out hash))
				return true;

			return !string.IsNullOrWhiteSpace(entry.fileName) &&
			       MapCatalog.TryGetMapHashFromFileName(entry.fileName, out hash);
		}

		private static string BuildHashKey(HashId hash)
			=> hash == 0 ? null : $"hash:{HTB50Settings.ToString(hash)}";

		private static bool TryParseHash(string value, out HashId hash)
		{
			hash = 0;
			if (string.IsNullOrWhiteSpace(value))
				return false;

			try
			{
				hash = HTB50.Decode(value.Trim());
				return hash != 0;
			}
			catch
			{
				return false;
			}
		}

		private sealed class CatalogRow
		{
			public string Key;
			public Map CurrentMap;
			public MapCatalog.MapEntry PersistentEntry;
			public SharedMapRepository.Entry RemoteEntry;

			public bool IsCurrent { get; private set; }
			public bool HasPersistent => PersistentEntry.Map != null;
			public bool HasStoredLocal => HasPersistent;
			public bool HasLocal => CurrentMap != null || HasPersistent;
			public bool HasRemote => RemoteEntry != null;
			public Map LocalMap => CurrentMap ?? PersistentEntry.Map;
			public HashId LocalHash => CurrentMap != null ? CurrentMap.HashID : PersistentEntry.HashId;
			public DateTime RemoteUpdatedUtc => HasRemote ? RemoteEntry.UpdatedUtcDateTime : DateTime.MinValue;

			public string DisplayName
			{
				get
				{
					if (CurrentMap != null && !string.IsNullOrWhiteSpace(CurrentMap.name))
						return CurrentMap.name;

					if (HasPersistent && !string.IsNullOrWhiteSpace(PersistentEntry.DisplayName))
						return PersistentEntry.DisplayName;

					return HasRemote ? RemoteEntry.DisplayName : "Untitled";
				}
			}

			public string SourceSummary
			{
				get
				{
					var parts = new List<string>();
					if (IsCurrent)
						parts.Add("Loaded in memory");
					if (HasPersistent)
						parts.Add("Persistent");
					if (HasRemote)
						parts.Add("Remote");

					return parts.Count == 0 ? "Unknown source" : string.Join(" | ", parts);
				}
			}

			public string FileSummary
			{
				get
				{
					var files = new List<string>();
					var localFile = string.IsNullOrWhiteSpace(PersistentEntry.FilePath)
						? null
						: Path.GetFileName(PersistentEntry.FilePath);
					if (!string.IsNullOrWhiteSpace(localFile))
						files.Add($"Local file {localFile}");
					if (HasRemote && !string.IsNullOrWhiteSpace(RemoteEntry.fileName) && !files.Any(file => file.EndsWith(RemoteEntry.fileName, StringComparison.OrdinalIgnoreCase)))
						files.Add($"Remote file {RemoteEntry.fileName}");

					return string.Join(" | ", files);
				}
			}

			public string RemoteSummary
			{
				get
				{
					if (!HasRemote)
						return null;

					return $"{FormatSize(RemoteEntry.sizeBytes)} | {FormatTimestamp(RemoteUpdatedUtc)}";
				}
			}

			public string KeySummary => TryGetKeyHash(out var hash) ? $"Key {HTB50Settings.ToString(hash)}" : null;

			private bool TryGetKeyHash(out HashId hash)
			{
				hash = LocalHash;
				if (hash != 0)
					return true;

				return HasRemote && TryGetRemoteHash(RemoteEntry, out hash);
			}

			public void RefreshDerivedState()
			{
				if (CurrentMap != null)
				{
					CurrentMap.EnsureHashID();
					IsCurrent = CurrentMap.HashID != 0;
				}
				else
				{
					IsCurrent = false;
				}
			}
		}

		private static string FormatSize(long sizeBytes)
		{
			if (sizeBytes <= 0)
				return "0 B";

			if (sizeBytes < 1024)
				return $"{sizeBytes} B";
			if (sizeBytes < 1024 * 1024)
				return $"{sizeBytes / 1024f:F1} KB";
			return $"{sizeBytes / (1024f * 1024f):F1} MB";
		}

		private static string FormatTimestamp(DateTime utcTime)
		{
			if (utcTime == DateTime.MinValue)
				return "Unknown time";

			return utcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
		}

		private static GUIStyle GetWindowStyle()
		{
			if (windowStyle != null)
				return windowStyle;

			var style = new GUIStyle(GUI.skin.window)
			{
				padding = new RectOffset(12, 12, 16, 12),
				fontSize = 14
			};
			style.normal.background = MakeSolidTexture(new Color(0.12f, 0.13f, 0.15f, 0.98f));
			style.onNormal.background = style.normal.background;
			style.hover.background = style.normal.background;
			style.onHover.background = style.normal.background;
			style.active.background = style.normal.background;
			style.onActive.background = style.normal.background;
			windowStyle = style;
			return windowStyle;
		}

		private static GUIStyle GetHelpStyle()
		{
			if (helpStyle != null)
				return helpStyle;

			var style = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true,
				fontSize = 13
			};
			style.normal.textColor = new Color(0.88f, 0.92f, 0.98f, 1f);
			helpStyle = style;
			return helpStyle;
		}

		private static GUIStyle GetKeyStyle()
		{
			if (keyStyle != null)
				return keyStyle;

			var style = new GUIStyle(GetHelpStyle());
			style.normal.textColor = new Color(1f, 0.80f, 0.34f, 1f);
			style.fontStyle = FontStyle.Bold;
			keyStyle = style;
			return keyStyle;
		}

		private static GUIStyle GetRowStyle(CatalogRow row)
		{
			if (row == null)
				return GUI.skin.box;

			if (row.HasRemote && row.HasLocal)
				return mixedBoxStyle ??= CreateRowStyle(new Color(0.10f, 0.24f, 0.24f, 0.88f));

			if (row.HasRemote)
				return remoteBoxStyle ??= CreateRowStyle(new Color(0.08f, 0.24f, 0.12f, 0.88f));

			if (row.HasLocal)
				return localBoxStyle ??= CreateRowStyle(new Color(0.09f, 0.16f, 0.32f, 0.88f));

			return GUI.skin.box;
		}

		private static GUIStyle CreateRowStyle(Color color)
		{
			var style = new GUIStyle(GUI.skin.box);
			var texture = MakeSolidTexture(color);
			style.normal.background = texture;
			style.onNormal.background = texture;
			style.hover.background = texture;
			style.onHover.background = texture;
			style.active.background = texture;
			style.onActive.background = texture;
			return style;
		}

		private static Texture2D MakeSolidTexture(Color color)
		{
			var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			texture.hideFlags = HideFlags.HideAndDontSave;
			texture.SetPixel(0, 0, color);
			texture.Apply();
			return texture;
		}
	}
}
