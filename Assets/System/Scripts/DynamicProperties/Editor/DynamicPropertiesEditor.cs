using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(DynamicProperties))]
public class DynamicPropertiesEditor : Editor
{
	[InitializeOnLoadMethod]
	private static void InitializeOnLoad()
	{
		// Ensure Text component is added early in Editor lifecycle
		EditorApplication.delayCall += () =>
		{
			foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
			{
				EnsureTextComponentOnDynamicProperties(go);
			}
		};
		UnityEditor.SceneManagement.PrefabStage.prefabStageOpened += stage =>
		{
			EnsureTextComponentOnDynamicProperties(stage.prefabContentsRoot);
		};
		UnityEditor.EditorApplication.hierarchyChanged += () =>
		{
			foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
			{
				EnsureTextComponentOnDynamicProperties(go);
			}
		};
	}

	private static void EnsureTextComponentOnDynamicProperties(GameObject go)
	{
		if (go == null)
		{
			Debug.LogWarning("GameObject is null in EnsureTextComponentOnDynamicProperties.");
			return;
		}
		var components = go.GetComponentsInChildren<DynamicProperties>(true);
		foreach (var component in components)
		{
			if (component == null || component.gameObject == null)
			{
				Debug.LogWarning($"DynamicProperties component or its GameObject is null on {go.name}.");
				continue;
			}
			var textComponent = component.gameObject.GetComponent<Text>();
			if (textComponent == null)
			{
				Undo.RegisterCompleteObjectUndo(component.gameObject, "Add Text Component");
				textComponent = component.gameObject.AddComponent<Text>();
				textComponent.enabled = false;
				textComponent.text = "{\"Properties\":[]}";
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				Canvas canvas = component.gameObject.GetComponent<Canvas>();
				if (canvas != null)
				{
					canvas.enabled = false;
					canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				}
				CanvasRenderer canvasRenderer = component.gameObject.GetComponent<CanvasRenderer>();
				if (canvasRenderer != null)
				{
					canvasRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				}
				component.InitializeTextComponent();
				component.SaveProperties();
				UnityEditor.EditorUtility.SetDirty(textComponent);
				UnityEditor.EditorUtility.SetDirty(component);
			}
		}
	}

	public override void OnInspectorGUI()
	{
		var component = (DynamicProperties)target;
		if (component == null || component.gameObject == null)
		{
			EditorGUILayout.HelpBox("DynamicProperties component or its GameObject is null. Please re-add the component to a valid GameObject.", MessageType.Error);
			return;
		}

		// Ensure Text component is initialized before accessing GetData
		component.InitializeTextComponent();
		var textComponent = component.gameObject.GetComponent<Text>();
		if (textComponent == null)
		{
			EditorGUILayout.HelpBox("Failed to initialize Text component. Please add it manually.", MessageType.Error);
			return;
		}

		var textSerializedObject = new SerializedObject(textComponent);
		var serializedPropertiesProp = textSerializedObject.FindProperty("m_Text");
		if (serializedPropertiesProp == null)
		{
			EditorGUILayout.HelpBox("Text component does not have a text field.", MessageType.Error);
			return;
		}

		textSerializedObject.Update();

		DynamicPropertiesData data = component.GetData();

		EditorGUILayout.LabelField("Dynamic Properties", EditorStyles.boldLabel);

		if (EditorApplication.isPlaying)
		{
			EditorGUILayout.HelpBox("Changes made in play mode are not persisted after exiting play mode.", MessageType.Info);
		}

		bool propertiesChanged = false;
		var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Case-insensitive

		for (int i = 0; i < data.Properties.Count; i++)
		{
			var prop = data.Properties[i];
			EditorGUILayout.BeginHorizontal();

			EditorGUIUtility.labelWidth = 50f;

			EditorGUI.BeginChangeCheck();
			string newName = EditorGUILayout.TextField("Name", prop.Name, GUILayout.Width(150f));
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(textComponent, "Change Property Name");
				if (string.IsNullOrEmpty(newName))
				{
					EditorGUILayout.HelpBox("Name cannot be empty.", MessageType.Error);
				}
				else if (existingNames.Contains(newName) && !string.Equals(newName, prop.Name, StringComparison.OrdinalIgnoreCase))
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
				Undo.RecordObject(textComponent, "Change Property Type");
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
						Undo.RecordObject(textComponent, "Change Float Value");
						prop.FloatValue = newFloatValue;
						propertiesChanged = true;
					}
					break;
				case PropertyType.Int:
					EditorGUI.BeginChangeCheck();
					int newIntValue = EditorGUILayout.IntField(prop.IntValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(textComponent, "Change Float Value");
						prop.IntValue = newIntValue;
						propertiesChanged = true;
					}
					break;
				case PropertyType.String:
					EditorGUI.BeginChangeCheck();
					string newStringValue = EditorGUILayout.TextField(prop.StringValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(textComponent, "Change String Value");
						prop.StringValue = newStringValue;
						propertiesChanged = true;
					}
					break;
				case PropertyType.Bool:
					EditorGUI.BeginChangeCheck();
					bool newBoolValue = EditorGUILayout.Toggle(prop.BoolValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(textComponent, "Change Bool Value");
						prop.BoolValue = newBoolValue;
						propertiesChanged = true;
					}
					break;
			}

			if (GUILayout.Button("Remove", GUILayout.Width(60f)))
			{
				Undo.RecordObject(textComponent, "Remove Property");
				data.Properties.RemoveAt(i);
				i--;
				propertiesChanged = true;
			}
			EditorGUILayout.EndHorizontal();
		}

		if (GUILayout.Button("Add Property"))
		{
			Undo.RecordObject(textComponent, "Add Property");
			string newName = "NewProperty" + (data.Properties.Count + 1);
			int attempt = 1;
			while (existingNames.Contains(newName))
			{
				newName = "NewProperty" + (data.Properties.Count + 1 + attempt);
				attempt++;
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

		if (propertiesChanged)
		{
			serializedPropertiesProp.stringValue = JsonUtility.ToJson(data);
			textSerializedObject.ApplyModifiedProperties();
			component.SetData(data); // Syncs data, propertyMap, and saves
			EditorUtility.SetDirty(textComponent);
			EditorUtility.SetDirty(component);
			Repaint();
		}
		else
		{
			textSerializedObject.ApplyModifiedProperties();
		}
	}
}