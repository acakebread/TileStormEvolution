using UnityEditor;
using UnityEngine;
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
		window.minSize = new Vector2(300, 120);
		window.maxSize = new Vector2(300, 120);
		window.Show();
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("Remove All Properties", EditorStyles.boldLabel);
		EditorGUILayout.Space();
		EditorGUILayout.HelpBox("Are you sure you want to remove all properties? This cannot be undone.", MessageType.Warning);

		EditorGUILayout.Space();

		GUIStyle removeButtonStyle = new GUIStyle(EditorStyles.miniButton)
		{
			normal = { background = MakeTex(2, 2, new Color(1.0f, 0.0f, 0.0f)), textColor = Color.white },
			hover = { background = MakeTex(2, 2, new Color(1.0f, 0.2f, 0.2f)), textColor = Color.white },
			active = { background = MakeTex(2, 2, new Color(0.8f, 0.0f, 0.0f)), textColor = Color.white },
			fixedHeight = 20f,
			fontSize = 10,
			padding = new RectOffset(2, 2, 2, 2)
		};

		GUIStyle cancelButtonStyle = new GUIStyle(EditorStyles.miniButton)
		{
			fixedHeight = 20f,
			fontSize = 10,
			padding = new RectOffset(2, 2, 2, 2)
		};

		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("Remove", removeButtonStyle, GUILayout.Width(100)))
		{
			Undo.RecordObject(textComponent, "Remove All Properties");
			var data = component.GetData();
			data.Properties.Clear();
			textSerializedObject.Update();
			textSerializedObject.FindProperty("m_Text").stringValue = JsonUtility.ToJson(data);
			textSerializedObject.ApplyModifiedProperties();
			component.SetData(data);
			EditorUtility.SetDirty(textComponent);
			EditorUtility.SetDirty(component);
			Close();
		}
		if (GUILayout.Button("Cancel", cancelButtonStyle, GUILayout.Width(100)))
		{
			Close();
		}
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
	}

	// Helper method to create a colored texture for button background
	private static Texture2D MakeTex(int width, int height, Color col)
	{
		Color[] pix = new Color[width * height];
		for (int i = 0; i < pix.Length; i++)
		{
			pix[i] = col;
		}
		Texture2D result = new Texture2D(width, height);
		result.SetPixels(pix);
		result.Apply();
		return result;
	}
}