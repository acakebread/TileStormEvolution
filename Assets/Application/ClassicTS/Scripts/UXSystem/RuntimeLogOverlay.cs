using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ClassicTilestorm
{
	public sealed class RuntimeLogOverlay : MonoBehaviour
	{
		private const int MaxEntries = 300;
		private const int WindowId = 190542;
		private static readonly List<Entry> entries = new List<Entry>(MaxEntries);
		private static RuntimeLogOverlay instance;
		private static bool subscribed;
		private static GUIStyle windowStyle;
		private static GUIStyle textStyle;
		private static GUIStyle helpStyle;

		private Rect windowRect = new Rect(30f, 60f, 760f, 520f);
		private Vector2 scroll;
		private bool isOpen;
		private ImguiRaycastBlocker raycastBlocker;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			EnsureInstance();
			Subscribe();
			AddInternal(LogType.Log, "Runtime log ready.", null);
		}

		public static void Toggle()
		{
			var overlay = EnsureInstance();
			overlay.SetOpen(!overlay.isOpen);
		}

		public static void Open() => EnsureInstance().SetOpen(true);
		public static void Close()
		{
			if (instance != null)
				instance.SetOpen(false);
		}

		public static void Add(string message) => AddInternal(LogType.Log, message, null);
		public static void AddWarning(string message) => AddInternal(LogType.Warning, message, null);
		public static void AddError(string message) => AddInternal(LogType.Error, message, null);

		private static RuntimeLogOverlay EnsureInstance()
		{
			if (instance != null)
				return instance;

			instance = FindAnyObjectByType<RuntimeLogOverlay>(FindObjectsInactive.Include);
			if (instance != null)
				return instance;

			var go = new GameObject(nameof(RuntimeLogOverlay));
			go.hideFlags = HideFlags.HideAndDontSave;
			DontDestroyOnLoad(go);
			instance = go.AddComponent<RuntimeLogOverlay>();
			return instance;
		}

		private static void Subscribe()
		{
			if (subscribed)
				return;

			Application.logMessageReceived += HandleUnityLog;
			subscribed = true;
		}

		private static void HandleUnityLog(string condition, string stackTrace, LogType type)
		{
			AddInternal(type, condition, stackTrace);
		}

		private static void AddInternal(LogType type, string message, string stackTrace)
		{
			if (string.IsNullOrWhiteSpace(message))
				return;

			if (entries.Count >= MaxEntries)
				entries.RemoveAt(0);

			entries.Add(new Entry
			{
				time = DateTime.Now.ToString("HH:mm:ss"),
				type = type,
				message = message,
				stackTrace = stackTrace
			});

			if (instance != null)
				instance.scroll.y = float.MaxValue;
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
			enabled = false;
		}

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;

			raycastBlocker?.Destroy();
			raycastBlocker = null;

			if (subscribed && instance == null)
			{
				Application.logMessageReceived -= HandleUnityLog;
				subscribed = false;
			}
		}

		private void OnDisable()
		{
			raycastBlocker?.SetVisible(false);
		}

		private void SetOpen(bool open)
		{
			isOpen = open;
			if (isOpen)
				SyncRaycastBlocker();
			else
				raycastBlocker?.SetVisible(false);

			enabled = isOpen;
		}

		private void OnGUI()
		{
			if (!isOpen)
				return;

			EnsureStyles();
			windowRect.width = Mathf.Min(windowRect.width, Screen.width - 20f);
			windowRect.height = Mathf.Min(windowRect.height, Screen.height - 20f);
			var previousRect = windowRect;
			windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "Runtime Log", windowStyle);
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

			raycastBlocker ??= new ImguiRaycastBlocker($"{nameof(RuntimeLogOverlay)} Raycast Blocker");
			raycastBlocker.Sync(windowRect);
		}

		private void DrawWindow(int id)
		{
			GUILayout.Label("This captures Unity logs inside the running build, including WebGL launch-map diagnostics.", helpStyle);

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Clear", GUILayout.Width(90f), GUILayout.Height(26f)))
				entries.Clear();

			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Close", GUILayout.Width(90f), GUILayout.Height(26f)))
				SetOpen(false);
			GUILayout.EndHorizontal();

			scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
			GUILayout.TextArea(BuildLogText(), textStyle, GUILayout.ExpandHeight(true));
			GUILayout.EndScrollView();

			GUI.DragWindow(new Rect(0f, 0f, 10000f, 26f));
		}

		private static string BuildLogText()
		{
			if (entries.Count == 0)
				return "No runtime log entries yet.";

			var builder = new StringBuilder(entries.Count * 96);
			foreach (var entry in entries)
			{
				builder.Append(entry.time);
				builder.Append(" | ");
				builder.Append(entry.type);
				builder.Append(" | ");
				builder.AppendLine(entry.message);

				if ((entry.type == LogType.Exception || entry.type == LogType.Error) && !string.IsNullOrWhiteSpace(entry.stackTrace))
				{
					builder.AppendLine(entry.stackTrace);
				}
			}

			return builder.ToString();
		}

		private static void EnsureStyles()
		{
			if (windowStyle != null)
				return;

			var darkTexture = MakeTex(new Color(0.05f, 0.06f, 0.08f, 0.96f));
			var textTexture = MakeTex(new Color(0.01f, 0.012f, 0.016f, 0.92f));

			windowStyle = new GUIStyle(GUI.skin.window)
			{
				normal = { background = darkTexture, textColor = Color.white },
				fontSize = 16,
				padding = new RectOffset(14, 14, 24, 14)
			};

			helpStyle = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true,
				fontSize = 13,
				normal = { textColor = new Color(0.78f, 0.84f, 0.9f, 1f) }
			};

			textStyle = new GUIStyle(GUI.skin.textArea)
			{
				wordWrap = true,
				fontSize = 13,
				normal = { background = textTexture, textColor = new Color(0.86f, 0.92f, 1f, 1f) },
				focused = { background = textTexture, textColor = new Color(0.86f, 0.92f, 1f, 1f) },
				padding = new RectOffset(8, 8, 8, 8)
			};
		}

		private static Texture2D MakeTex(Color color)
		{
			var texture = new Texture2D(1, 1);
			texture.SetPixel(0, 0, color);
			texture.Apply();
			return texture;
		}

		private sealed class Entry
		{
			public string time;
			public LogType type;
			public string message;
			public string stackTrace;
		}
	}
}
