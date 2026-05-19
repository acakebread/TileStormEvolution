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
		private static GUIStyle selectedBoxStyle;

		private bool isOpen;
		private bool isRefreshing;
		private Rect windowRect = DefaultWindowRect;
		private Vector2 scroll;
		private string statusLine;
		private string repositoryUrl;
		private IReadOnlyList<SharedMapRepository.Entry> entries = Array.Empty<SharedMapRepository.Entry>();
		private SharedMapRepository.Entry selectedEntry;
		private string selectedFilePath;

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
		}

		private void SetOpen(bool open)
		{
			isOpen = open;
			if (isOpen)
			{
				repositoryUrl = ApplicationSettings.MapRepositoryBaseUrl;
				enabled = true;
				RefreshRepository();
			}
			else
			{
				enabled = false;
			}
		}

		private void OnGUI()
		{
			if (!isOpen)
				return;

			windowRect = GUILayout.Window(0x5A11D, windowRect, DrawWindow, "Online Map Repository", GetWindowStyle());
		}

		private void DrawWindow(int windowId)
		{
			GUILayout.Label("This panel reads a GitHub Pages map repository over HTTPS. Players only need to open it, pick a map, and import.", GetHelpStyle());
			GUILayout.Label("Publishers can commit the current map back to the repository if the upload token is configured in the build.", GetHelpStyle());

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
			if (GUILayout.Button("Import Selected", GUILayout.Height(30f)))
				ImportSelectedEntry();
			if (GUILayout.Button("Import Latest", GUILayout.Height(30f)))
				ImportLatestEntry();
			if (GUILayout.Button("Publish Current", GUILayout.Height(30f)))
				PublishCurrentMap();
			GUILayout.EndHorizontal();

			if (!string.IsNullOrWhiteSpace(statusLine))
			{
				GUILayout.Space(6f);
				GUILayout.Label(statusLine, GetHelpStyle());
			}

			GUILayout.Space(8f);
			if (isRefreshing)
				GUILayout.Label("Loading repository manifest...");

			GUILayout.Label("Available maps:");
			scroll = GUILayout.BeginScrollView(scroll);

			if (entries == null || entries.Count == 0)
			{
				GUILayout.Label("No maps found.");
			}
			else
			{
				foreach (var entry in entries)
					DrawEntry(entry);
			}

			GUILayout.EndScrollView();

			if (!string.IsNullOrWhiteSpace(selectedFilePath))
			{
				GUILayout.Space(8f);
				GUILayout.Label($"Selected file: {selectedFilePath}");
			}

			GUI.DragWindow(new Rect(0, 0, 10000, 24));
		}

		private void DrawEntry(SharedMapRepository.Entry entry)
		{
			if (entry == null)
				return;

			bool isSelected = selectedEntry == entry;
			var prevColor = GUI.color;
			if (isSelected)
				GUI.color = new Color(0.82f, 0.9f, 1f, 1f);

			GUILayout.BeginVertical(isSelected ? GetSelectedBoxStyle() : GUI.skin.box);
			GUILayout.Label(entry.DisplayName);
			GUILayout.Label(string.IsNullOrWhiteSpace(entry.fileName) ? "<no file>" : entry.fileName);
			GUILayout.Label($"{FormatSize(entry.sizeBytes)}  |  {FormatTimestamp(entry.UpdatedUtcDateTime)}");
			if (!string.IsNullOrWhiteSpace(entry.description))
				GUILayout.Label(entry.description, GetHelpStyle());

			GUILayout.BeginHorizontal();
			if (GUILayout.Button(isSelected ? "Selected" : "Select", GUILayout.Width(90f)))
				SelectEntry(entry);
			if (GUILayout.Button("Import Now", GUILayout.Width(100f)))
				StartImport(entry);
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUI.color = prevColor;
		}

		private void SelectEntry(SharedMapRepository.Entry entry)
		{
			selectedEntry = entry;
			selectedFilePath = entry?.fileName;
			statusLine = entry == null ? "Selection cleared." : $"Selected {entry.DisplayName}.";
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
					selectedEntry = entries.FirstOrDefault();
					selectedFilePath = selectedEntry?.fileName;
					statusLine = string.IsNullOrWhiteSpace(manifest?.repositoryName)
						? $"Loaded {entries.Count} map(s) from the repository."
						: $"Loaded {entries.Count} map(s) from {manifest.repositoryName}.";
				},
				error => statusLine = error);

			isRefreshing = false;
		}

		private void ImportSelectedEntry()
		{
			if (selectedEntry == null)
			{
				statusLine = "Select a map first.";
				return;
			}

			StartImport(selectedEntry);
		}

		private void ImportLatestEntry()
		{
			var latest = entries?.OrderByDescending(e => e.UpdatedUtcDateTime).FirstOrDefault() ?? entries?.FirstOrDefault();
			if (latest == null)
			{
				statusLine = "No map entries are available.";
				return;
			}

			StartImport(latest);
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
					selectedFilePath = entry.fileName;
					statusLine = $"Imported {imported.name}.";
				},
				error => statusLine = error);

			isRefreshing = false;
		}

		private void PublishCurrentMap()
		{
			if (isRefreshing)
				return;

			if (string.IsNullOrWhiteSpace(ApplicationSettings.MapRepositoryUploadKey))
			{
				statusLine = "Upload token is not configured.";
				return;
			}

			var map = MainController.CurrentMap;
			if (map == null)
			{
				statusLine = "No current map is loaded.";
				return;
			}

			StartCoroutine(UploadCoroutine(map));
		}

		private System.Collections.IEnumerator UploadCoroutine(Map map)
		{
			isRefreshing = true;
			statusLine = "Uploading current map...";

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
				},
				onError: error => statusLine = error);

			isRefreshing = false;
			RefreshRepository();
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

		private static GUIStyle GetSelectedBoxStyle()
		{
			if (selectedBoxStyle != null)
				return selectedBoxStyle;

			var style = new GUIStyle(GUI.skin.box);
			style.normal.background = MakeSolidTexture(new Color(0.18f, 0.26f, 0.36f, 0.85f));
			selectedBoxStyle = style;
			return selectedBoxStyle;
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
