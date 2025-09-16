#if UNITY_EDITOR
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
		return base.GetPropertyHeight(property, label);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		Rect adjustedPosition = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

		float labelWidth = EditorGUIUtility.labelWidth;
		float fieldWidth = adjustedPosition.width - labelWidth - 60f;

		Rect labelRect = new Rect(adjustedPosition.x, adjustedPosition.y, labelWidth, adjustedPosition.height);
		Rect fieldRect = new Rect(adjustedPosition.x + labelWidth, adjustedPosition.y, fieldWidth, adjustedPosition.height);
		Rect buttonRect = new Rect(adjustedPosition.x + labelWidth + fieldWidth + 5f, adjustedPosition.y, 55f, adjustedPosition.height);

		// Use a unique key per object instance
		string uniqueKey = $"ResourcePath_Displayed_{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";
		displayedPath = EditorPrefs.GetString(uniqueKey, displayedPath);
		if (string.IsNullOrEmpty(displayedPath)) displayedPath = property.stringValue; // Fallback to relative path

		EditorGUI.LabelField(labelRect, label);
		EditorGUI.SelectableLabel(fieldRect, displayedPath, EditorStyles.textField);

		GUIContent buttonContent = new GUIContent("Browse", "Select a path within any resource folder or resource subfolder in Assets/ for resource loading.");
		if (GUI.Button(buttonRect, buttonContent))
		{
			openDialog = true;
			propToUpdate = property;
			EditorApplication.delayCall += OpenResourcePanel;
		}

		EditorGUI.EndProperty();

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

		// Use a unique key per object instance
		string uniqueKey = $"ResourcePath_Displayed_{propToUpdate.serializedObject.targetObject.GetInstanceID()}_{propToUpdate.propertyPath}";
		string initialPath = Application.dataPath;
		string storedDisplayedPath = EditorPrefs.GetString(uniqueKey, "");
		if (!string.IsNullOrEmpty(storedDisplayedPath) && storedDisplayedPath.StartsWith("Assets/"))
		{
			string relativeUnityPath = storedDisplayedPath.Substring("Assets/".Length);
			initialPath = Path.Combine(Application.dataPath, relativeUnityPath);
			if (!Directory.Exists(initialPath))
			{
				initialPath = Application.dataPath;
			}
		}
		else if (!string.IsNullOrEmpty(lastSelectedPath) && Directory.Exists(lastSelectedPath))
		{
			initialPath = lastSelectedPath;
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

				int resourcesIndex = fullRelativePath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
				if (resourcesIndex >= 0 || fullRelativePath == "Resources/")
				{
					string resourceRelativePath = fullRelativePath;
					if (resourcesIndex >= 0)
					{
						resourceRelativePath = fullRelativePath.Substring(resourcesIndex + "/Resources/".Length);
					}
					resourceRelativePath = resourceRelativePath.TrimStart('/').TrimEnd('/') + "/";
					if (resourceRelativePath == "" || fullRelativePath == "Resources/")
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
					// Save displayedPath to EditorPrefs with unique key
					EditorPrefs.SetString(uniqueKey, displayedPath);
					propToUpdate.serializedObject.ApplyModifiedProperties();
					EditorUtility.SetDirty(propToUpdate.serializedObject.targetObject);
					lastSelectedPath = selectedPath;
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