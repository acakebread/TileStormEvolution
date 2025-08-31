using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DynamicProperties))]
public class DynamicPropertiesEditor : Editor
{
	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		var component = (DynamicProperties)target;
		var serializedPropertiesProp = serializedObject.FindProperty("serializedProperties");
		DynamicPropertiesData data;

		// Deserialize JSON
		try
		{
			data = JsonUtility.FromJson<DynamicPropertiesData>(serializedPropertiesProp.stringValue);
			if (data == null || data.Properties == null)
			{
				data = new DynamicPropertiesData();
				serializedPropertiesProp.stringValue = JsonUtility.ToJson(data);
				serializedObject.ApplyModifiedProperties();
			}
		}
		catch (System.Exception e)
		{
			EditorGUILayout.HelpBox($"Failed to parse JSON: {e.Message}. Resetting to empty data.", MessageType.Error);
			data = new DynamicPropertiesData();
			serializedPropertiesProp.stringValue = JsonUtility.ToJson(data);
			serializedObject.ApplyModifiedProperties();
		}

		EditorGUILayout.LabelField("Dynamic Properties", EditorStyles.boldLabel);

		// Show warning in play mode
		if (EditorApplication.isPlaying)
		{
			EditorGUILayout.HelpBox("Changes made in play mode are not persisted after exiting play mode.", MessageType.Info);
		}

		bool propertiesChanged = false;
		var existingNames = new HashSet<string>();

		// Display and edit properties
		for (int i = 0; i < data.Properties.Count; i++)
		{
			var prop = data.Properties[i];
			EditorGUILayout.BeginHorizontal();

			// Reduce label width and constrain field sizes
			EditorGUIUtility.labelWidth = 50f;

			EditorGUI.BeginChangeCheck();
			string newName = EditorGUILayout.TextField("Name", prop.Name, GUILayout.Width(150f));
			if (EditorGUI.EndChangeCheck())
			{
				if (string.IsNullOrEmpty(newName))
				{
					EditorGUILayout.HelpBox("Name cannot be empty.", MessageType.Error);
				}
				else if (existingNames.Contains(newName) && newName != prop.Name)
				{
					EditorGUILayout.HelpBox($"Name '{newName}' is already used.", MessageType.Error);
				}
				else
				{
					prop.Name = newName;
					propertiesChanged = true;
				}
			}
			existingNames.Add(prop.Name);

			EditorGUI.BeginChangeCheck();
			PropertyType newType = (PropertyType)EditorGUILayout.EnumPopup(prop.Type, GUILayout.Width(100f));
			if (EditorGUI.EndChangeCheck())
			{
				prop.Type = newType;
				propertiesChanged = true;
			}

			switch (prop.Type)
			{
				case PropertyType.Float:
					EditorGUI.BeginChangeCheck();
					float newFloatValue = EditorGUILayout.FloatField(prop.FloatValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						prop.FloatValue = newFloatValue;
						propertiesChanged = true;
					}
					break;
				case PropertyType.Int:
					EditorGUI.BeginChangeCheck();
					int newIntValue = EditorGUILayout.IntField(prop.IntValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						prop.IntValue = newIntValue;
						propertiesChanged = true;
					}
					break;
				case PropertyType.String:
					EditorGUI.BeginChangeCheck();
					string newStringValue = EditorGUILayout.TextField(prop.StringValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						prop.StringValue = newStringValue;
						propertiesChanged = true;
					}
					break;
				case PropertyType.Bool:
					EditorGUI.BeginChangeCheck();
					bool newBoolValue = EditorGUILayout.Toggle(prop.BoolValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						prop.BoolValue = newBoolValue;
						propertiesChanged = true;
					}
					break;
			}

			if (GUILayout.Button("Remove", GUILayout.Width(60f)))
			{
				data.Properties.RemoveAt(i);
				i--;
				propertiesChanged = true;
			}
			EditorGUILayout.EndHorizontal();
		}

		if (GUILayout.Button("Add Property"))
		{
			string newName = "NewProperty" + (data.Properties.Count + 1);
			while (existingNames.Contains(newName))
			{
				newName = "NewProperty" + (data.Properties.Count + 1);
			}
			data.Properties.Add(new DynamicProperty
			{
				Name = newName,
				Type = PropertyType.Float,
				FloatValue = 0f,
				IntValue = 0,
				StringValue = "",
				BoolValue = false
			});
			propertiesChanged = true;
		}

		// Serialize back to JSON only if properties changed
		if (propertiesChanged)
		{
			serializedPropertiesProp.stringValue = JsonUtility.ToJson(data);
			component.SaveProperties();
			serializedObject.ApplyModifiedProperties();
			EditorUtility.SetDirty(target);
		}
		else
		{
			serializedObject.ApplyModifiedProperties();
		}
	}
}