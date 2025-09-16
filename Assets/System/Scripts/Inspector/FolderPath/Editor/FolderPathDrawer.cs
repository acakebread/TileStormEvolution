#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomPropertyDrawer(typeof(FolderPathAttribute))]
public class FolderPathDrawer : PropertyDrawer
{
	private bool openDialog = false;
	private SerializedProperty propToUpdate;
	private string lastSelectedPath = ""; // Instance-specific last selected system path
	private string displayedPath = "";   // Instance-specific full Unity path for display

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		float labelWidth = EditorGUIUtility.labelWidth;
		float fieldWidth = position.width - labelWidth - 60f;

		Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
		Rect fieldRect = new Rect(position.x + labelWidth, position.y, fieldWidth, position.height);
		Rect buttonRect = new Rect(position.x + labelWidth + fieldWidth + 5f, position.y, 55f, position.height);

		// Restore displayedPath from EditorPrefs if available
		string prefKey = $"FolderPath_Displayed_{property.propertyPath}";
		displayedPath = EditorPrefs.GetString(prefKey, displayedPath);
		if (string.IsNullOrEmpty(displayedPath)) displayedPath = property.stringValue; // Fallback to relative path

		EditorGUI.LabelField(labelRect, label); // Ensure label is drawn first
		EditorGUI.SelectableLabel(fieldRect, displayedPath, EditorStyles.textField);

		if (GUI.Button(buttonRect, "Browse"))
		{
			openDialog = true;
			propToUpdate = property;
			EditorApplication.delayCall += OpenFolderPanel;
		}

		EditorGUI.EndProperty();

		// Force repaint if GUI changes
		if (GUI.changed)
		{
			EditorWindow window = EditorWindow.focusedWindow;
			if (window != null) window.Repaint();
		}
	}

	private void OpenFolderPanel()
	{
		if (!openDialog || propToUpdate == null)
			return;

		openDialog = false;

		// Determine initial path using displayedPath from EditorPrefs if valid
		string initialPath = Application.dataPath; // Default to Assets root
		string prefKey = $"FolderPath_Displayed_{propToUpdate.propertyPath}";
		string storedDisplayedPath = EditorPrefs.GetString(prefKey, "");
		if (!string.IsNullOrEmpty(storedDisplayedPath) && storedDisplayedPath.StartsWith("Assets/"))
		{
			string relativeUnityPath = storedDisplayedPath.Substring("Assets/".Length);
			initialPath = Path.Combine(Application.dataPath, relativeUnityPath);
			if (!Directory.Exists(initialPath))
			{
				initialPath = Application.dataPath; // Fallback if the path doesn't exist
			}
		}
		else if (!string.IsNullOrEmpty(lastSelectedPath) && Directory.Exists(lastSelectedPath))
		{
			initialPath = lastSelectedPath; // Fallback to last selected system path
		}
		Debug.Log($"[FolderPathDrawer] Initial path for {propToUpdate.name}: {initialPath}");

		string selectedPath = EditorUtility.OpenFolderPanel("Select Folder", initialPath, "");

		if (!string.IsNullOrEmpty(selectedPath))
		{
			string normalizedPath = selectedPath.Replace('\\', '/');
			Debug.Log($"[FolderPathDrawer] Selected path for {propToUpdate.name}: {normalizedPath}");
			int assetsIndex = normalizedPath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);

			if (assetsIndex >= 0)
			{
				string fullRelativePath = normalizedPath.Substring(assetsIndex + "/Assets/".Length);
				fullRelativePath = fullRelativePath.TrimEnd('/') + "/";
				string potentialDisplayedPath = $"Assets/{fullRelativePath}";

				// Determine the resource loading path and validate
				int resourcesIndex = fullRelativePath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
				if (resourcesIndex >= 0 || fullRelativePath == "Resources/")
				{
					string resourceRelativePath = fullRelativePath;
					if (resourcesIndex >= 0)
					{
						resourceRelativePath = fullRelativePath.Substring(resourcesIndex + "/Resources/".Length);
					}
					resourceRelativePath = resourceRelativePath.TrimStart('/').TrimEnd('/') + "/";
					if (resourceRelativePath == "" || fullRelativePath == "Resources/") // Root Resources folder
					{
						propToUpdate.stringValue = "";
						displayedPath = potentialDisplayedPath;
						Debug.Log($"[FolderPathDrawer] Set path to Resources-relative for {propToUpdate.name}: {propToUpdate.stringValue} (Root selected)");
					}
					else
					{
						propToUpdate.stringValue = resourceRelativePath;
						displayedPath = potentialDisplayedPath;
						Debug.Log($"[FolderPathDrawer] Set path to Resources-relative for {propToUpdate.name}: {propToUpdate.stringValue}");
					}
					// Save displayedPath to EditorPrefs
					EditorPrefs.SetString(prefKey, displayedPath);
					propToUpdate.serializedObject.ApplyModifiedProperties();
					EditorUtility.SetDirty(propToUpdate.serializedObject.targetObject);
					lastSelectedPath = selectedPath; // Update instance-specific last selected path
				}
				else
				{
					Debug.LogWarning($"Selected folder is not a Resources folder or inside a Resources folder for {propToUpdate.name}. Resources.Load will not work. " + selectedPath);
					// Do not update displayedPath or propToUpdate.stringValue for invalid selections
				}
			}
			else
			{
				Debug.LogWarning($"Selected folder is not inside the Assets folder for {propToUpdate.name}. " + selectedPath);
			}
		}
		else
		{
			Debug.Log($"[FolderPathDrawer] Folder selection cancelled or invalid for {propToUpdate.name}.");
		}

		// Repaint inspector after property change
		if (propToUpdate != null && propToUpdate.serializedObject != null)
		{
			EditorUtility.SetDirty(propToUpdate.serializedObject.targetObject);
			EditorWindow window = EditorWindow.focusedWindow;
			if (window != null) window.Repaint();
		}

		propToUpdate = null;
	}
}
#endif

//using UnityEngine;
//using UnityEditor;
//using System;

//namespace ClassicTilestorm
//{
//	[CustomPropertyDrawer(typeof(FolderPathAttribute))]
//	public class FolderPathDrawer : PropertyDrawer
//	{
//		private static bool openDialog = false;
//		private static SerializedProperty propToUpdate;

//		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
//		{
//			EditorGUI.BeginProperty(position, label, property);

//			float labelWidth = EditorGUIUtility.labelWidth;
//			float fieldWidth = position.width - labelWidth - 60f;

//			Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
//			Rect fieldRect = new Rect(position.x + labelWidth, position.y, fieldWidth, position.height);
//			Rect buttonRect = new Rect(position.x + labelWidth + fieldWidth + 5f, position.y, 55f, position.height);

//			EditorGUI.LabelField(labelRect, label);
//			EditorGUI.SelectableLabel(fieldRect, property.stringValue, EditorStyles.textField);

//			if (GUI.Button(buttonRect, "Browse"))
//			{
//				openDialog = true;
//				propToUpdate = property;
//				// Register callback to open folder panel outside OnGUI
//				EditorApplication.delayCall += OpenFolderPanel;
//			}

//			EditorGUI.EndProperty();
//		}

//		private void OpenFolderPanel()
//		{
//			if (!openDialog || propToUpdate == null)
//				return;

//			openDialog = false;

//			string selectedPath = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, "");

//			if (!string.IsNullOrEmpty(selectedPath))
//			{
//				string normalizedPath = selectedPath.Replace('\\', '/');
//				int resourcesIndex = normalizedPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);

//				if (resourcesIndex >= 0)
//				{
//					string relativePath = normalizedPath.Substring(resourcesIndex + "/Resources/".Length);
//					relativePath = relativePath.TrimEnd('/') + "/";
//					propToUpdate.stringValue = relativePath;
//					propToUpdate.serializedObject.ApplyModifiedProperties();

//					Debug.Log($"[FolderPathDrawer] Set path to Resources-relative: {propToUpdate.stringValue}");
//				}
//				else
//				{
//					Debug.LogWarning("Selected folder is not inside a Resources folder. Resources.Load will not work. " + selectedPath);
//				}
//			}

//			// Repaint inspector after property change
//			if (propToUpdate != null && propToUpdate.serializedObject != null)
//			{
//				EditorWindow window = EditorWindow.focusedWindow;
//				if (window != null) window.Repaint();
//			}

//			propToUpdate = null;
//		}
//	}
//}
