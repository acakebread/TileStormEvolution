using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Text;

[CustomEditor(typeof(DynamicProperties))]
public class DynamicPropertiesEditor : Editor
{
	// Embedded MiniJSON parser (based on public domain MiniJSON by Calvin Rien)
	public static class MiniJSON
	{
		public static object Deserialize(string json)
		{
			if (string.IsNullOrEmpty(json)) return null;
			index = 0; // Reset index for each deserialization
			return ParseValue(json, ref index);
		}

		private static int index;
		private static object ParseValue(string json, ref int i)
		{
			SkipWhitespace(json, ref i);
			if (i >= json.Length) return null;

			char c = json[i];
			if (c == '{') return ParseObject(json, ref i);
			if (c == '[') return ParseArray(json, ref i);
			if (c == '"') return ParseString(json, ref i);
			if (c == '-' || char.IsDigit(c)) return ParseNumber(json, ref i);
			if (i + 3 < json.Length && json.Substring(i, 4).ToLower() == "true") { i += 4; return true; }
			if (i + 4 < json.Length && json.Substring(i, 5).ToLower() == "false") { i += 5; return false; }
			if (i + 3 < json.Length && json.Substring(i, 4).ToLower() == "null") { i += 4; return null; }
			return null;
		}

		private static Dictionary<string, object> ParseObject(string json, ref int i)
		{
			var dict = new Dictionary<string, object>();
			i++; // Skip '{'
			while (i < json.Length)
			{
				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null; // Invalid JSON
				if (json[i] == '}') { i++; return dict; }

				string key = ParseString(json, ref i);
				if (key == null) return null; // Invalid key

				SkipWhitespace(json, ref i);
				if (i >= json.Length || json[i] != ':') return null; // Missing colon
				i++; // Skip ':'

				object value = ParseValue(json, ref i);
				if (value == null && i < json.Length) return null; // Invalid value
				dict[key] = value;

				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null; // Missing closing brace
				if (json[i] == '}') { i++; return dict; }
				if (json[i] != ',') return null; // Missing comma
				i++;
			}
			return null; // Unclosed object
		}

		private static List<object> ParseArray(string json, ref int i)
		{
			var array = new List<object>();
			i++; // Skip '['
			while (i < json.Length)
			{
				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null; // Invalid JSON
				if (json[i] == ']') { i++; return array; }

				object value = ParseValue(json, ref i);
				if (value == null && i < json.Length) return null; // Invalid value
				array.Add(value);

				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null; // Missing closing bracket
				if (json[i] == ']') { i++; return array; }
				if (json[i] != ',') return null; // Missing comma
				i++;
			}
			return null; // Unclosed array
		}

		private static string ParseString(string json, ref int i)
		{
			var sb = new StringBuilder();
			i++; // Skip '"'
			while (i < json.Length)
			{
				char c = json[i];
				if (c == '"') { i++; return sb.ToString(); }
				if (c == '\\')
				{
					i++;
					if (i >= json.Length) return null; // Invalid escape
					char next = json[i];
					if (next == '"' || next == '\\' || next == '/') sb.Append(next);
					else if (next == 'n') sb.Append('\n');
					else if (next == 't') sb.Append('\t');
					else if (next == 'r') sb.Append('\r');
					else return null; // Unsupported escape sequence
					i++;
				}
				else
				{
					sb.Append(c);
					i++;
				}
			}
			return null; // Unclosed string
		}

		private static object ParseNumber(string json, ref int i)
		{
			var sb = new StringBuilder();
			while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '.' || json[i] == 'e' || json[i] == 'E'))
			{
				sb.Append(json[i]);
				i++;
			}
			string numStr = sb.ToString();
			if (string.IsNullOrEmpty(numStr)) return null;
			if (numStr.Contains(".") || numStr.Contains("e") || numStr.Contains("E"))
			{
				if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result))
					return result;
				return null; // Invalid number
			}
			else
			{
				if (int.TryParse(numStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int result))
					return result;
				return null; // Invalid number
			}
		}

		private static void SkipWhitespace(string json, ref int i)
		{
			while (i < json.Length && char.IsWhiteSpace(json[i]))
				i++;
		}
	}

	[InitializeOnLoadMethod]
	private static void InitializeOnLoad()
	{
		EditorApplication.delayCall += () =>
		{
			if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
			{
				foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
				{
					EnsureTextComponentOnDynamicProperties(go);
				}
			}
		};
		UnityEditor.SceneManagement.PrefabStage.prefabStageOpened += stage =>
		{
			if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
			{
				EnsureTextComponentOnDynamicProperties(stage.prefabContentsRoot);
			}
		};
		UnityEditor.EditorApplication.hierarchyChanged += () =>
		{
			if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
			{
				foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
				{
					EnsureTextComponentOnDynamicProperties(go);
				}
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
			Undo.RegisterCompleteObjectUndo(component.gameObject, "Configure DynamicProperties Components");
			var textComponent = component.gameObject.GetComponent<Text>();
			if (textComponent == null)
			{
				textComponent = component.gameObject.AddComponent<Text>();
				if (textComponent == null)
				{
					Debug.LogWarning($"Failed to add Text component to {component.gameObject.name}.");
					continue;
				}
			}
			// Configure Text component
			textComponent.enabled = false;
			if (string.IsNullOrEmpty(textComponent.text))
			{
				textComponent.text = "{\"Properties\":[]}";
			}
			textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;

			// Configure RectTransform to mimic Transform
			RectTransform rectTransform = component.gameObject.GetComponent<RectTransform>();
			if (rectTransform != null)
			{
				rectTransform.localPosition = Vector3.zero;
				rectTransform.localScale = Vector3.one;
				rectTransform.localRotation = Quaternion.identity;
				rectTransform.anchorMin = Vector2.zero;
				rectTransform.anchorMax = Vector2.one;
				rectTransform.anchoredPosition = Vector2.zero;
				rectTransform.sizeDelta = Vector2.zero;
			}

			// Remove Canvas if it has no necessary components
			Canvas canvas = component.gameObject.GetComponent<Canvas>();
			if (canvas != null)
			{
				canvas.enabled = false;
				canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				if (canvas.GetComponent<CanvasScaler>() == null && canvas.GetComponent<GraphicRaycaster>() == null)
				{
					DestroyImmediate(canvas);
				}
			}

			// Configure CanvasRenderer if present
			CanvasRenderer canvasRenderer = component.gameObject.GetComponent<CanvasRenderer>();
			if (canvasRenderer != null)
			{
				canvasRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
			}

			component.InitializeTextComponent();
			component.LoadProperties();
			component.SaveProperties();
			UnityEditor.EditorUtility.SetDirty(textComponent);
			UnityEditor.EditorUtility.SetDirty(component);
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

		// Ensure Text component is initialized
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

		// Property List UI
		bool propertiesChanged = false;
		var existingNames2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// Style for individual Remove buttons (dull red)
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
				else if (existingNames2.Contains(newName) && !string.Equals(newName, prop.Name, StringComparison.OrdinalIgnoreCase))
				{
					EditorGUILayout.HelpBox($"Name '{newName}' is already used.", MessageType.Error);
				}
				else
				{
					prop.Name = newName;
					propertiesChanged = true;
				}
			}
			existingNames2.Add(prop.Name);

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
						Undo.RecordObject(textComponent, "Change Int Value");
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

		// Add Property Button (Full width, taller, green)
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
			while (existingNames2.Contains(newName))
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

		EditorGUILayout.Space();

		// Secondary Buttons (Inject JSON and Remove All Properties, 50/50 width)
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