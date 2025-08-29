using System;
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomPropertyDrawer(typeof(ResourcePathAttribute))]
public class ResourcePathDrawer : PropertyDrawer
{
	private bool openDialog = false;
	private SerializedProperty propToUpdate;
	private string lastSelectedPath = ""; // Instance-specific last selected system path
	private string displayedPath = "";   // Instance-specific full Unity path for display

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{
		// Commented out: Dynamic height calculation for help box
		/*
		// Create a GUIStyle for the help box text to calculate height
		GUIStyle helpBoxTextStyle = new GUIStyle(EditorStyles.label)
		{
			fontSize = 12,
			wordWrap = true,
			padding = new RectOffset(6, 6, 4, 4)
		};

		// Calculate the required height for the wrapped text
		string helpText = "Select a path within any resource folder or resource subfolder in Assets/ for resource loading.";
		float textWidth = EditorGUIUtility.currentViewWidth - 12f; // Account for padding and margins
		float textHeight = helpBoxTextStyle.CalcHeight(new GUIContent(helpText), textWidth);

		// Add extra height for the help box and property
		return base.GetPropertyHeight(property, label) + textHeight + 8f; // 8f for padding above/below
		*/

		// Use default height without help box
		return base.GetPropertyHeight(property, label);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		// Commented out: Help box rendering code
		/*
		// Create a custom GUIStyle for the help box text
		GUIStyle helpBoxTextStyle = new GUIStyle(EditorStyles.label)
		{
			fontSize = 12, // Increase font size (default is ~9-11, 12 is noticeably larger)
			wordWrap = true,
			padding = new RectOffset(6, 6, 4, 4) // Match help box padding for consistency
		};

		// Create a custom GUIStyle for the help box background
		GUIStyle helpBoxStyle = new GUIStyle(EditorStyles.helpBox);

		// Calculate the required height for the wrapped text
		string helpText = "Select a path within any resource folder or resource subfolder in Assets/ for resource loading.";
		float textWidth = position.width - 12f; // Account for padding
		float textHeight = helpBoxTextStyle.CalcHeight(new GUIContent(helpText), textWidth);

		// Draw custom help box with dynamic height
		Rect helpBoxRect = new Rect(position.x, position.y, position.width, textHeight + 4f);
		GUI.Box(helpBoxRect, "", helpBoxStyle); // Empty string to avoid default text

		// Draw the help box text with custom style
		Rect textRect = new Rect(position.x + 2, position.y + 2, position.width - 4, textHeight);
		GUI.Label(textRect, helpText, helpBoxTextStyle);

		// Adjust position for the property field and button
		Rect adjustedPosition = new Rect(position.x, position.y + textHeight + 8f, position.width, EditorGUIUtility.singleLineHeight);
		*/

		// Use default position without help box
		Rect adjustedPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

		float labelWidth = EditorGUIUtility.labelWidth;
		float fieldWidth = adjustedPosition.width - labelWidth - 60f;

		Rect labelRect = new Rect(adjustedPosition.x, adjustedPosition.y, labelWidth, adjustedPosition.height);
		Rect fieldRect = new Rect(adjustedPosition.x + labelWidth, adjustedPosition.y, fieldWidth, adjustedPosition.height);
		Rect buttonRect = new Rect(adjustedPosition.x + labelWidth + fieldWidth + 5f, adjustedPosition.y, 55f, adjustedPosition.height);

		// Restore displayedPath from EditorPrefs if available
		string prefKey = $"ResourcePath_Displayed_{property.propertyPath}";
		displayedPath = EditorPrefs.GetString(prefKey, displayedPath);
		if (string.IsNullOrEmpty(displayedPath)) displayedPath = property.stringValue; // Fallback to relative path

		EditorGUI.LabelField(labelRect, label); // Ensure label is drawn first
		EditorGUI.SelectableLabel(fieldRect, displayedPath, EditorStyles.textField);

		// Add tooltip to Browse button
		GUIContent buttonContent = new GUIContent("Browse", "Select a path within any resource folder or resource subfolder in Assets/ for resource loading.");
		if (GUI.Button(buttonRect, buttonContent))
		{
			openDialog = true;
			propToUpdate = property;
			EditorApplication.delayCall += OpenResourcePanel;
		}

		EditorGUI.EndProperty();

		// Force repaint if GUI changes
		if (GUI.changed)
		{
			EditorWindow window = EditorWindow.focusedWindow;
			if (window != null) window.Repaint();
		}
	}

	private void OpenResourcePanel()
	{
		if (!openDialog || propToUpdate == null)
			return;

		openDialog = false;

		// Determine initial path using displayedPath from EditorPrefs if valid
		string initialPath = Application.dataPath; // Default to Assets root
		string prefKey = $"ResourcePath_Displayed_{propToUpdate.propertyPath}";
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
		Debug.Log($"[ResourcePathDrawer] Initial path for {propToUpdate.name}: {initialPath}");

		string selectedPath = EditorUtility.OpenFolderPanel("Select Resource Folder", initialPath, "");

		if (!string.IsNullOrEmpty(selectedPath))
		{
			string normalizedPath = selectedPath.Replace('\\', '/');
			Debug.Log($"[ResourcePathDrawer] Selected path for {propToUpdate.name}: {normalizedPath}");
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
						Debug.Log($"[ResourcePathDrawer] Set path to Resources-relative for {propToUpdate.name}: {propToUpdate.stringValue} (Root selected)");
					}
					else
					{
						propToUpdate.stringValue = resourceRelativePath;
						displayedPath = potentialDisplayedPath;
						Debug.Log($"[ResourcePathDrawer] Set path to Resources-relative for {propToUpdate.name}: {propToUpdate.stringValue}");
					}
					// Save displayedPath to EditorPrefs
					EditorPrefs.SetString(prefKey, displayedPath);
					propToUpdate.serializedObject.ApplyModifiedProperties();
					EditorUtility.SetDirty(propToUpdate.serializedObject.targetObject);
					lastSelectedPath = selectedPath; // Update instance-specific last selected path
				}
				else
				{
					EditorUtility.DisplayDialog("Invalid Folder Selected",
						$"The selected folder is not a Resources folder or inside a Resources folder for {propToUpdate.name}. Resources.Load will not work.\n\nSelected Path: {selectedPath}",
						"OK");
				}
			}
			else
			{
				EditorUtility.DisplayDialog("Invalid Folder Selected",
					$"The selected folder is not inside the Assets folder for {propToUpdate.name}.\n\nSelected Path: {selectedPath}",
					"OK");
			}
		}
		else
		{
			Debug.Log($"[ResourcePathDrawer] Resource folder selection cancelled or invalid for {propToUpdate.name}.");
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