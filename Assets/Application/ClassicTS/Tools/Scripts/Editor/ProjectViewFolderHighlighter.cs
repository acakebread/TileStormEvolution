using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ClassicTilestorm.Editor
{
	[InitializeOnLoad]
	internal static class ProjectViewFolderHighlighter
	{
		static ProjectViewFolderHighlighter()
		{
			EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemOnGUI;
		}

		private static void OnProjectWindowItemOnGUI(string guid, Rect rect)
		{
			if (Event.current.type != EventType.Repaint)
				return;

			if (!ProjectFolderColourSettings.instance.TryGetFolderColour(guid, out var colour))
				return;

			DrawFolderTint(rect, colour);
		}

		private static void DrawFolderTint(Rect rect, Color colour)
		{
			var fill = colour;
			fill.a = Mathf.Clamp(fill.a <= 0f ? 0.18f : fill.a, 0.06f, 0.35f);

			var stripe = colour;
			stripe.a = Mathf.Clamp(stripe.a <= 0f ? 0.75f : stripe.a, 0.35f, 1f);

			EditorGUI.DrawRect(rect, fill);

			var stripeRect = rect;
			stripeRect.width = 3f;
			EditorGUI.DrawRect(stripeRect, stripe);
		}
	}

	internal static class ProjectFolderColourUtility
	{
		public static List<string> GetSelectedFolderGuids()
		{
			return Selection.assetGUIDs
				.Where(IsFolderGuid)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		public static bool IsFolderGuid(string guid)
		{
			if (string.IsNullOrWhiteSpace(guid))
				return false;

			var path = AssetDatabase.GUIDToAssetPath(guid);
			return !string.IsNullOrWhiteSpace(path) && AssetDatabase.IsValidFolder(path);
		}
	}

	[FilePath("ProjectSettings/ProjectFolderColours.asset", FilePathAttribute.Location.ProjectFolder)]
	public sealed class ProjectFolderColourSettings : ScriptableSingleton<ProjectFolderColourSettings>
	{
		[Serializable]
		private struct FolderColourEntry
		{
			public string folderGuid;
			public Color colour;
		}

		[Serializable]
		public readonly struct ResolvedFolderColourEntry
		{
			public readonly string FolderGuid;
			public readonly string FolderPath;
			public readonly Color Colour;

			public ResolvedFolderColourEntry(string folderGuid, string folderPath, Color colour)
			{
				FolderGuid = folderGuid;
				FolderPath = folderPath;
				Colour = colour;
			}
		}

		[SerializeField] private Color defaultFolderColour = new Color(0.83f, 0.20f, 0.20f, 0.18f);
		[SerializeField] private List<FolderColourEntry> entries = new List<FolderColourEntry>();

		public Color DefaultFolderColour
		{
			get => defaultFolderColour;
			set
			{
				defaultFolderColour = value;
				Save(true);
			}
		}

		public IReadOnlyList<ResolvedFolderColourEntry> Entries
		{
			get
			{
				var resolved = new List<ResolvedFolderColourEntry>(entries.Count);
				foreach (var entry in entries)
				{
					if (string.IsNullOrWhiteSpace(entry.folderGuid))
						continue;

					resolved.Add(new ResolvedFolderColourEntry(entry.folderGuid, ResolveFolderPath(entry.folderGuid), entry.colour));
				}

				return resolved;
			}
		}

		public bool TryGetFolderColour(string folderGuid, out Color colour)
		{
			colour = default;

			if (string.IsNullOrWhiteSpace(folderGuid))
				return false;

			for (var i = 0; i < entries.Count; i++)
			{
				if (!string.Equals(entries[i].folderGuid, folderGuid, StringComparison.OrdinalIgnoreCase))
					continue;

				colour = entries[i].colour;
				return true;
			}

			return false;
		}

		public void SetFolderColour(string folderGuid, Color colour)
		{
			if (string.IsNullOrWhiteSpace(folderGuid))
				return;

			SetFolderColourInternal(folderGuid, colour);
			SaveAndRefresh();
		}

		public int SetFolderColours(IEnumerable<string> folderGuids, Color colour)
		{
			var uniqueGuids = NormalizeGuids(folderGuids);
			var changed = 0;

			for (var i = 0; i < uniqueGuids.Count; i++)
			{
				if (SetFolderColourInternal(uniqueGuids[i], colour))
					changed++;
			}

			if (changed > 0)
				SaveAndRefresh();

			return changed;
		}

		public bool ClearFolderColour(string folderGuid)
		{
			if (string.IsNullOrWhiteSpace(folderGuid))
				return false;

			var removed = entries.RemoveAll(entry => string.Equals(entry.folderGuid, folderGuid, StringComparison.OrdinalIgnoreCase)) > 0;
			if (removed)
				SaveAndRefresh();

			return removed;
		}

		public int ClearFolderColours(IEnumerable<string> folderGuids)
		{
			var uniqueGuids = NormalizeGuids(folderGuids);
			var removed = 0;
			var changed = false;

			for (var i = 0; i < uniqueGuids.Count; i++)
			{
				if (entries.RemoveAll(entry => string.Equals(entry.folderGuid, uniqueGuids[i], StringComparison.OrdinalIgnoreCase)) > 0)
				{
					removed++;
					changed = true;
				}
			}

			if (changed)
				SaveAndRefresh();

			return removed;
		}

		public bool TryGetResolvedEntry(string folderGuid, out ResolvedFolderColourEntry resolvedEntry)
		{
			resolvedEntry = default;

			for (var i = 0; i < entries.Count; i++)
			{
				if (!string.Equals(entries[i].folderGuid, folderGuid, StringComparison.OrdinalIgnoreCase))
					continue;

				resolvedEntry = new ResolvedFolderColourEntry(entries[i].folderGuid, ResolveFolderPath(entries[i].folderGuid), entries[i].colour);
				return true;
			}

			return false;
		}

		private void SaveAndRefresh()
		{
			Save(true);
			EditorApplication.RepaintProjectWindow();
			EditorUtility.SetDirty(this);
		}

		private bool SetFolderColourInternal(string folderGuid, Color colour)
		{
			for (var i = 0; i < entries.Count; i++)
			{
				if (!string.Equals(entries[i].folderGuid, folderGuid, StringComparison.OrdinalIgnoreCase))
					continue;

				entries[i] = new FolderColourEntry
				{
					folderGuid = folderGuid,
					colour = colour
				};
				return true;
			}

			entries.Add(new FolderColourEntry
			{
				folderGuid = folderGuid,
				colour = colour
			});
			return true;
		}

		private static List<string> NormalizeGuids(IEnumerable<string> folderGuids)
		{
			return (folderGuids ?? Enumerable.Empty<string>())
				.Where(guid => !string.IsNullOrWhiteSpace(guid))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static string ResolveFolderPath(string folderGuid)
		{
			if (string.IsNullOrWhiteSpace(folderGuid))
				return null;

			var path = AssetDatabase.GUIDToAssetPath(folderGuid);
			return string.IsNullOrWhiteSpace(path) ? null : path.Replace('\\', '/');
		}
	}

	internal static class ProjectFolderColourMenu
	{
		[MenuItem("Assets/Folder Colour/Set Colour...", false, 2000)]
		private static void SetColour()
		{
			ProjectFolderColourWindow.ShowForSelection();
		}

		[MenuItem("Assets/Folder Colour/Set Colour...", true)]
		private static bool ValidateSetColour()
		{
			return ProjectFolderColourUtility.GetSelectedFolderGuids().Count > 0;
		}

		[MenuItem("Assets/Folder Colour/Clear Colour", false, 2001)]
		private static void ClearColour()
		{
			var folderGuids = ProjectFolderColourUtility.GetSelectedFolderGuids();
			if (folderGuids.Count == 0)
				return;

			ProjectFolderColourSettings.instance.ClearFolderColours(folderGuids);
		}

		[MenuItem("Assets/Folder Colour/Clear Colour", true)]
		private static bool ValidateClearColour()
		{
			return ProjectFolderColourUtility.GetSelectedFolderGuids().Count > 0;
		}
	}

	internal sealed class ProjectFolderColourWindow : EditorWindow
	{
		private readonly List<string> folderGuids = new List<string>();
		private Color selectedColour;
		private Vector2 scrollPosition;
		private bool hasSelection;
		private bool mixedSelectionColour;

		public static void ShowForSelection()
		{
			var selectedFolderGuids = ProjectFolderColourUtility.GetSelectedFolderGuids();

			if (selectedFolderGuids.Count == 0)
				return;

			var window = CreateInstance<ProjectFolderColourWindow>();
			window.titleContent = new GUIContent("Folder Colour");
			window.minSize = new Vector2(360f, 220f);
			window.Initialize(selectedFolderGuids);
			window.ShowUtility();
		}

		private void Initialize(List<string> selectedFolderGuids)
		{
			folderGuids.Clear();
			folderGuids.AddRange(selectedFolderGuids);

			hasSelection = folderGuids.Count > 0;
			selectedColour = ProjectFolderColourSettings.instance.DefaultFolderColour;
			mixedSelectionColour = true;

			if (TryGetSharedColour(folderGuids, out var sharedColour))
			{
				selectedColour = sharedColour;
				mixedSelectionColour = false;
			}
		}

		private void OnGUI()
		{
			if (!hasSelection)
			{
				EditorGUILayout.HelpBox("Select one or more folders in the Project window, then open this window again.", MessageType.Info);
				return;
			}

			EditorGUILayout.LabelField(folderGuids.Count == 1 ? "Selected folder" : "Selected folders", EditorStyles.boldLabel);

			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(80f));
			foreach (var folderGuid in folderGuids)
			{
				var path = AssetDatabase.GUIDToAssetPath(folderGuid);
				EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(path) ? "<missing folder>" : path);
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space(6f);

			EditorGUI.BeginChangeCheck();
			selectedColour = EditorGUILayout.ColorField("Folder Colour", selectedColour);
			if (EditorGUI.EndChangeCheck())
				mixedSelectionColour = false;

			if (mixedSelectionColour)
				EditorGUILayout.HelpBox("The current selection has multiple colors. Pick a new one to apply to all selected folders.", MessageType.None);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Apply", GUILayout.Height(28f)))
			{
				ProjectFolderColourSettings.instance.SetFolderColours(folderGuids, selectedColour);
				Close();
			}

			if (GUILayout.Button("Clear", GUILayout.Height(28f)))
			{
				ProjectFolderColourSettings.instance.ClearFolderColours(folderGuids);
				Close();
			}

			if (GUILayout.Button("Cancel", GUILayout.Height(28f)))
			{
				Close();
			}
			EditorGUILayout.EndHorizontal();
		}

		private static bool TryGetSharedColour(List<string> selectedFolderGuids, out Color colour)
		{
			colour = default;

			if (selectedFolderGuids == null || selectedFolderGuids.Count == 0)
				return false;

			var firstColour = default(Color);
			var hasFirst = false;

			foreach (var folderGuid in selectedFolderGuids)
			{
				if (!ProjectFolderColourSettings.instance.TryGetFolderColour(folderGuid, out var currentColour))
					return false;

				if (!hasFirst)
				{
					firstColour = currentColour;
					hasFirst = true;
					continue;
				}

				if (!AreApproximatelyEqual(firstColour, currentColour))
					return false;
			}

			if (!hasFirst)
				return false;

			colour = firstColour;
			return true;
		}

		private static bool AreApproximatelyEqual(Color left, Color right)
		{
			return Mathf.Abs(left.r - right.r) < 0.001f &&
				   Mathf.Abs(left.g - right.g) < 0.001f &&
				   Mathf.Abs(left.b - right.b) < 0.001f &&
				   Mathf.Abs(left.a - right.a) < 0.001f;
		}
	}

	internal static class ProjectFolderColourSettingsProvider
	{
		private static Vector2 scrollPosition;

		[SettingsProvider]
		public static SettingsProvider CreateProvider()
		{
			return new SettingsProvider("Project/Folder Colours", SettingsScope.Project)
			{
				label = "Folder Colours",
				guiHandler = DrawGUI,
				keywords = new HashSet<string>(new[] { "folder", "colour", "color", "project", "tint" })
			};
		}

		private static void DrawGUI(string searchContext)
		{
			var settings = ProjectFolderColourSettings.instance;

			EditorGUILayout.HelpBox("Right-click folders in the Project window and choose Folder Colour > Set Colour... to tag one or more folders.", MessageType.Info);

			EditorGUI.BeginChangeCheck();
			var defaultColour = EditorGUILayout.ColorField("Default Colour", settings.DefaultFolderColour);
			if (EditorGUI.EndChangeCheck())
				settings.DefaultFolderColour = defaultColour;

			EditorGUILayout.Space(8f);
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Selected Folder(s)"))
				AddSelectedFolders(settings);

			if (GUILayout.Button("Clear Selected Folder(s)"))
				ClearSelectedFolders(settings);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Configured Folders", EditorStyles.boldLabel);

			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
			var entries = settings.Entries;
			if (entries.Count == 0)
			{
				EditorGUILayout.HelpBox("No folder colours are configured yet.", MessageType.None);
			}
			else
			{
				foreach (var entry in entries)
					DrawEntry(settings, entry);
			}
			EditorGUILayout.EndScrollView();
		}

		private static void AddSelectedFolders(ProjectFolderColourSettings settings)
		{
			var selectedGuids = ProjectFolderColourUtility.GetSelectedFolderGuids();

			if (selectedGuids.Count == 0)
				return;

			settings.SetFolderColours(selectedGuids, settings.DefaultFolderColour);
		}

		private static void ClearSelectedFolders(ProjectFolderColourSettings settings)
		{
			var selectedGuids = ProjectFolderColourUtility.GetSelectedFolderGuids();

			if (selectedGuids.Count == 0)
				return;

			settings.ClearFolderColours(selectedGuids);
		}

		private static void DrawEntry(ProjectFolderColourSettings settings, ProjectFolderColourSettings.ResolvedFolderColourEntry entry)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(entry.FolderPath) ? "<missing folder>" : entry.FolderPath, GUILayout.ExpandWidth(true));

			if (GUILayout.Button("Ping", GUILayout.Width(50f)))
				PingFolder(entry.FolderPath);

			if (GUILayout.Button("Remove", GUILayout.Width(60f)))
			{
				settings.ClearFolderColour(entry.FolderGuid);
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				return;
			}
			EditorGUILayout.EndHorizontal();

			EditorGUI.BeginChangeCheck();
			var updatedColour = EditorGUILayout.ColorField("Colour", entry.Colour);
			if (EditorGUI.EndChangeCheck())
				settings.SetFolderColour(entry.FolderGuid, updatedColour);

			EditorGUILayout.EndVertical();
		}

		private static void PingFolder(string folderPath)
		{
			if (string.IsNullOrWhiteSpace(folderPath))
				return;

			var folderObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
			if (folderObject != null)
				EditorGUIUtility.PingObject(folderObject);
		}
	}
}
