using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Globalization;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(DynamicProperties))]
public class DynamicPropertiesEditor : Editor
{
	[InitializeOnLoadMethod]
	private static void InitializeOnLoad()
	{
		if (EditorApplication.isPlaying || EditorApplication.isCompiling)
		{
			return;
		}

		EditorApplication.delayCall += () =>
		{
			bool needsDirty = false;
			foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
			{
				needsDirty |= EnsureTextComponentOnDynamicProperties(go);
			}
			if (needsDirty)
			{
				EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
			}
		};

		PrefabStage.prefabStageOpened += stage =>
		{
			if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
			{
				if (EnsureTextComponentOnDynamicProperties(stage.prefabContentsRoot))
				{
					EditorSceneManager.MarkSceneDirty(stage.scene);
				}
			}
		};
	}

	private static bool EnsureTextComponentOnDynamicProperties(GameObject go)
	{
		if (go == null)
		{
			return false;
		}
		bool needsDirty = false;
		var components = go.GetComponentsInChildren<DynamicProperties>(true);
		if (components.Length > 1)
		{
			Debug.LogWarning($"GameObject '{go.name}' has multiple DynamicProperties components, which is not allowed due to [DisallowMultipleComponent]. Please remove extra components.");
		}
		foreach (var component in components)
		{
			if (component == null || component.gameObject == null)
			{
				continue;
			}

			bool componentChanged = false;
			var textComponent = component.gameObject.GetComponent<Text>();

			if (textComponent == null)
			{
				Undo.RegisterCompleteObjectUndo(component.gameObject, "Configure DynamicProperties Components");
				textComponent = component.gameObject.AddComponent<Text>();
				if (textComponent == null)
				{
					continue;
				}
				componentChanged = true;
			}

			if (textComponent.enabled)
			{
				textComponent.enabled = false;
				componentChanged = true;
			}
			if (string.IsNullOrEmpty(textComponent.text))
			{
				textComponent.text = "{\"Properties\":[]}";
				componentChanged = true;
			}
			if (textComponent.hideFlags != (HideFlags.HideInInspector | HideFlags.NotEditable))
			{
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				componentChanged = true;
			}

			if (componentChanged)
			{
				component.InitializeTextComponent();
				component.LoadProperties();
				component.SaveProperties();
				EditorUtility.SetDirty(textComponent);
				EditorUtility.SetDirty(component);
				needsDirty = true;
			}
		}
		return needsDirty;
	}

	public override void OnInspectorGUI()
	{
		var component = (DynamicProperties)target;
		if (component == null || component.gameObject == null)
		{
			EditorGUILayout.HelpBox("DynamicProperties component or its GameObject is null. Please re-add the component to a valid GameObject.", MessageType.Error);
			return;
		}

		var components = component.gameObject.GetComponents<DynamicProperties>();
		if (components.Length > 1)
		{
			EditorGUILayout.HelpBox($"Multiple DynamicProperties components detected on '{component.gameObject.name}'. This is not allowed due to [DisallowMultipleComponent]. Please remove extra components.", MessageType.Error);
			if (GUILayout.Button("Remove Extra Components"))
			{
				Undo.RecordObject(component.gameObject, "Remove Extra DynamicProperties");
				for (int i = 1; i < components.Length; i++)
				{
					DestroyImmediate(components[i]);
				}
				EditorUtility.SetDirty(component.gameObject);
				return;
			}
		}

		component.InitializeTextComponent();
		var textComponent = component.gameObject.GetComponent<Text>();
		if (textComponent == null)
		{
			EditorGUILayout.HelpBox("Text component is missing. It will be added automatically on next editor update.", MessageType.Warning);
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
		var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		GUIStyle removeButtonStyle = new GUIStyle(EditorStyles.miniButton)
		{
			normal = { background = MakeTex(2, 2, new Color(0.7f, 0.2f, 0.2f)), textColor = Color.white },
			hover = { background = MakeTex(2, 2, new Color(0.8f, 0.3f, 0.3f)), textColor = Color.white },
			active = { background = MakeTex(2, 2, new Color(0.6f, 0.1f, 0.1f)), textColor = Color.white }
		};

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
				switch (newType)
				{
					case PropertyType.Float:
						prop.Value = "0";
						break;
					case PropertyType.Int:
						prop.Value = "0";
						break;
					case PropertyType.String:
						prop.Value = "";
						break;
					case PropertyType.Bool:
						prop.Value = "false";
						break;
				}
				propertiesChanged = true;
			}

			switch (prop.Type)
			{
				case PropertyType.Float:
					EditorGUI.BeginChangeCheck();
					float floatValue = prop.TryGetFloat(out float f) ? f : 0f;
					float newFloatValue = EditorGUILayout.FloatField(floatValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(textComponent, "Change Float Value");
						prop.Value = newFloatValue.ToString(CultureInfo.InvariantCulture);
						propertiesChanged = true;
					}
					break;
				case PropertyType.Int:
					EditorGUI.BeginChangeCheck();
					int intValue = prop.TryGetInt(out int i2) ? i2 : 0;
					int newIntValue = EditorGUILayout.IntField(intValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(textComponent, "Change Int Value");
						prop.Value = newIntValue.ToString(CultureInfo.InvariantCulture);
						propertiesChanged = true;
					}
					break;
				case PropertyType.String:
					EditorGUI.BeginChangeCheck();
					string stringValue = prop.TryGetString(out string s) ? s : "";
					string newStringValue = EditorGUILayout.TextField(stringValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(textComponent, "Change String Value");
						prop.Value = newStringValue;
						propertiesChanged = true;
					}
					break;
				case PropertyType.Bool:
					EditorGUI.BeginChangeCheck();
					bool boolValue = prop.TryGetBool(out bool b) ? b : false;
					bool newBoolValue = EditorGUILayout.Toggle(boolValue, GUILayout.Width(100f));
					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(textComponent, "Change Bool Value");
						prop.Value = newBoolValue.ToString().ToLowerInvariant();
						propertiesChanged = true;
					}
					break;
			}

			if (GUILayout.Button("Remove", removeButtonStyle, GUILayout.Width(60f)))
			{
				Undo.RecordObject(textComponent, "Remove Property");
				data.Properties.RemoveAt(i);
				i--;
				propertiesChanged = true;
			}
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.Space();

		GUIStyle addButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
		{
			fixedHeight = 24f,
			margin = new RectOffset(5, 5, 2, 2),
			normal = { background = MakeTex(2, 2, new Color(0.2f, 0.7f, 0.2f)), textColor = Color.white },
			hover = { background = MakeTex(2, 2, new Color(0.3f, 0.8f, 0.3f)), textColor = Color.white },
			active = { background = MakeTex(2, 2, new Color(0.1f, 0.6f, 0.1f)), textColor = Color.white }
		};
		if (GUILayout.Button("Add Property", addButtonStyle))
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
				Value = "0"
			});
			propertiesChanged = true;
		}

		EditorGUILayout.Space();

		GUIStyle secondaryButtonStyle = new GUIStyle(EditorStyles.miniButton)
		{
			fixedHeight = 16f,
			fontSize = 10,
			padding = new RectOffset(2, 2, 2, 2)
		};
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("Inject JSON", secondaryButtonStyle, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 10)))
		{
			JsonInputDialog.ShowDialog(component, textComponent, textSerializedObject);
		}
		if (GUILayout.Button("Remove All Properties", secondaryButtonStyle, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 10)))
		{
			RemoveAllPropertiesDialog.ShowDialog(component, textComponent, textSerializedObject);
		}
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();

		if (propertiesChanged)
		{
			serializedPropertiesProp.stringValue = JsonUtility.ToJson(data);
			textSerializedObject.ApplyModifiedProperties();
			component.SetData(data);
			EditorUtility.SetDirty(textComponent);
			EditorUtility.SetDirty(component);
			Repaint();
		}
		else
		{
			textSerializedObject.ApplyModifiedProperties();
		}
	}

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