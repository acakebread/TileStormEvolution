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
	private string lastSelectedPath = "";
	private string displayedPath = "";

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		float labelWidth = EditorGUIUtility.labelWidth;
		float fieldWidth = position.width - labelWidth - 60f;

		Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
		Rect fieldRect = new Rect(position.x + labelWidth, position.y, fieldWidth, position.height);
		Rect buttonRect = new Rect(position.x + labelWidth + fieldWidth + 5f, position.y, 55f, position.height);

		string prefKey = $"FolderPath_Displayed_{property.serializedObject.targetObject.GetInstanceID()}_{property.propertyPath}";
		displayedPath = EditorPrefs.GetString(prefKey, displayedPath);
		if (string.IsNullOrEmpty(displayedPath)) displayedPath = property.stringValue;

		EditorGUI.LabelField(labelRect, label);
		EditorGUI.SelectableLabel(fieldRect, displayedPath, EditorStyles.textField);

		GUIContent buttonContent = new GUIContent("Browse", "Select a folder within Assets/.");
		if (GUI.Button(buttonRect, buttonContent))
		{
			openDialog = true;
			propToUpdate = property;
			EditorApplication.delayCall += OpenFolderPanel;
		}

		EditorGUI.EndProperty();

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

		string initialPath = Application.dataPath;
		string prefKey = $"FolderPath_Displayed_{propToUpdate.serializedObject.targetObject.GetInstanceID()}_{propToUpdate.propertyPath}";
		string storedDisplayedPath = EditorPrefs.GetString(prefKey, "");
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

				string relativePath = fullRelativePath.TrimStart('/').TrimEnd('/') + "/";
				if (relativePath == "/" || fullRelativePath == "/")
				{
					propToUpdate.stringValue = "";
					displayedPath = potentialDisplayedPath;
					Debug.Log($"[FolderPathDrawer] Set path to root for {propToUpdate.name}: {propToUpdate.stringValue}");
				}
				else
				{
					propToUpdate.stringValue = relativePath;
					displayedPath = potentialDisplayedPath;
					Debug.Log($"[FolderPathDrawer] Set path to Assets-relative for {propToUpdate.name}: {propToUpdate.stringValue}");
				}
				EditorPrefs.SetString(prefKey, displayedPath);
				propToUpdate.serializedObject.ApplyModifiedProperties();
				EditorUtility.SetDirty(propToUpdate.serializedObject.targetObject);
				lastSelectedPath = selectedPath;
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
			Debug.Log($"[FolderPathDrawer] Folder selection cancelled or invalid for {propToUpdate.name}.");
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