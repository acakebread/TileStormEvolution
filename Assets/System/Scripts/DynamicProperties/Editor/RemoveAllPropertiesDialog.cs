using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class RemoveAllPropertiesDialog : EditorWindow
{
	private DynamicProperties component;
	private Text textComponent;
	private SerializedObject textSerializedObject;

	public static void ShowDialog(DynamicProperties component, Text textComponent, SerializedObject textSerializedObject)
	{
		var window = GetWindow<RemoveAllPropertiesDialog>("Confirm Remove All Properties");
		window.component = component;
		window.textComponent = textComponent;
		window.textSerializedObject = textSerializedObject;
		window.Show();
	}

	private void OnGUI()
	{
		GUILayout.Label("Are you sure you want to remove all properties?", EditorStyles.boldLabel);
		GUILayout.Label("This action cannot be undone.");

		GUILayout.Space(10);

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Remove All"))
		{
			Undo.RecordObject(textComponent, "Remove All Properties");
			DynamicPropertiesData emptyData = new DynamicPropertiesData();
			component.SetData(emptyData);
			textSerializedObject.FindProperty("m_Text").stringValue = JsonUtility.ToJson(emptyData);
			textSerializedObject.ApplyModifiedProperties();
			EditorUtility.SetDirty(textComponent);
			EditorUtility.SetDirty(component);
			Close();
		}
		if (GUILayout.Button("Cancel"))
		{
			Close();
		}
		EditorGUILayout.EndHorizontal();
	}
}