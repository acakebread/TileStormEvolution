using UnityEngine;
using UnityEditor;
using System;

namespace ClassicTilestorm
{
	[CustomPropertyDrawer(typeof(FolderPathAttribute))]
	public class FolderPathDrawer : PropertyDrawer
	{
		private static bool openDialog = false;
		private static SerializedProperty propToUpdate;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			float labelWidth = EditorGUIUtility.labelWidth;
			float fieldWidth = position.width - labelWidth - 60f;

			Rect labelRect = new Rect(position.x, position.y, labelWidth, position.height);
			Rect fieldRect = new Rect(position.x + labelWidth, position.y, fieldWidth, position.height);
			Rect buttonRect = new Rect(position.x + labelWidth + fieldWidth + 5f, position.y, 55f, position.height);

			EditorGUI.LabelField(labelRect, label);
			EditorGUI.SelectableLabel(fieldRect, property.stringValue, EditorStyles.textField);

			if (GUI.Button(buttonRect, "Browse"))
			{
				openDialog = true;
				propToUpdate = property;
				// Register callback to open folder panel outside OnGUI
				EditorApplication.delayCall += OpenFolderPanel;
			}

			EditorGUI.EndProperty();
		}

		private void OpenFolderPanel()
		{
			if (!openDialog || propToUpdate == null)
				return;

			openDialog = false;

			string selectedPath = EditorUtility.OpenFolderPanel("Select Folder", Application.dataPath, "");

			if (!string.IsNullOrEmpty(selectedPath))
			{
				string normalizedPath = selectedPath.Replace('\\', '/');
				int resourcesIndex = normalizedPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);

				if (resourcesIndex >= 0)
				{
					string relativePath = normalizedPath.Substring(resourcesIndex + "/Resources/".Length);
					relativePath = relativePath.TrimEnd('/') + "/";
					propToUpdate.stringValue = relativePath;
					propToUpdate.serializedObject.ApplyModifiedProperties();

					Debug.Log($"[FolderPathDrawer] Set path to Resources-relative: {propToUpdate.stringValue}");
				}
				else
				{
					Debug.LogWarning("Selected folder is not inside a Resources folder. Resources.Load will not work. " + selectedPath);
				}
			}

			// Repaint inspector after property change
			if (propToUpdate != null && propToUpdate.serializedObject != null)
			{
				EditorWindow window = EditorWindow.focusedWindow;
				if (window != null) window.Repaint();
			}

			propToUpdate = null;
		}
	}
}
