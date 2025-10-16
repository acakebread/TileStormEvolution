using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using MassiveHadronLtd;

public class JsonInputDialog : EditorWindow
{
	private string dialogInput = "<input json here>";
	private string jsonError = "";
	private DynamicProperties component;
	private Text textComponent;
	private SerializedObject textSerializedObject;
	private Vector2 scrollPosition;
	private bool isPlaceholder = true;
	private readonly string textAreaControlName = "JsonInputTextArea";

	private void OnEnable()
	{
		dialogInput = "<input json here>";
		jsonError = "";
		scrollPosition = Vector2.zero;
		isPlaceholder = true;
	}

	private void OnFocus()
	{
		dialogInput = "<input json here>";
		jsonError = "";
		scrollPosition = Vector2.zero;
		isPlaceholder = true;
	}

	public static void ShowDialog(DynamicProperties component, Text textComponent, SerializedObject textSerializedObject)
	{
		if (HasOpenInstances<JsonInputDialog>())
		{
			GetWindow<JsonInputDialog>().Close();
		}
		var window = GetWindow<JsonInputDialog>("Inject JSON");
		window.component = component;
		window.textComponent = textComponent;
		window.textSerializedObject = textSerializedObject;
		window.dialogInput = "<input json here>";
		window.jsonError = "";
		window.scrollPosition = Vector2.zero;
		window.isPlaceholder = true;
		window.Show();
	}

	private void OnGUI()
	{
		// Ensure no unexpected content overrides placeholder
		if (!isPlaceholder && dialogInput == "<input json here>")
		{
			dialogInput = "";
			isPlaceholder = false;
		}

		GUILayout.Label("Paste JSON below:", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("Example: {\"string_property\": \"string_value\"}", MessageType.Info);

		// Set control name for the text area to detect focus
		GUI.SetNextControlName(textAreaControlName);
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));

		// Use a custom style to visually distinguish placeholder text
		GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea)
		{
			fontStyle = isPlaceholder ? FontStyle.Italic : FontStyle.Normal,
			normal = { textColor = isPlaceholder ? Color.gray : Color.black }
		};

		string newInput = EditorGUILayout.TextArea(dialogInput, textAreaStyle, GUILayout.ExpandHeight(true));

		// Check if the text area is focused
		bool isTextAreaFocused = GUI.GetNameOfFocusedControl() == textAreaControlName;

		if (isTextAreaFocused && isPlaceholder)
		{
			dialogInput = "";
			isPlaceholder = false;
		}
		else if (newInput != dialogInput)
		{
			// User typed or modified the text
			if (isPlaceholder && newInput != "<input json here>")
			{
				isPlaceholder = false;
			}
			dialogInput = newInput;
		}

		EditorGUILayout.EndScrollView();

		if (!string.IsNullOrEmpty(jsonError))
		{
			EditorGUILayout.HelpBox(jsonError, MessageType.Error);
		}

		GUILayout.Space(10);

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Validate and Apply"))
		{
			if (string.IsNullOrEmpty(dialogInput.Trim()) || dialogInput == "<input json here>")
			{
				jsonError = "Please enter valid JSON.";
			}
			else
			{
				Undo.RecordObject(textComponent, "Inject JSON Data");
				jsonError = "";
				try
				{
					var parsed = MiniJSON.Deserialize(dialogInput);
					if (parsed is Dictionary<string, object> dict)
					{
						DynamicPropertiesData newData = new DynamicPropertiesData();
						var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
						foreach (var kvp in dict)
						{
							string name = kvp.Key;
							if (string.IsNullOrEmpty(name) || existingNames.Contains(name))
							{
								jsonError = $"Invalid or duplicate property name '{name}'.";
								break;
							}
							existingNames.Add(name);

							object value = kvp.Value;
							string type;
							string stringValue;

							if (value is double d)
							{
								if (d % 1 == 0 && d >= int.MinValue && d <= int.MaxValue)
								{
									type = "int";
									stringValue = ((int)d).ToString(CultureInfo.InvariantCulture);
								}
								else
								{
									type = "float";
									stringValue = ((float)d).ToString(CultureInfo.InvariantCulture);
								}
							}
							else if (value is int i)
							{
								type = "int";
								stringValue = i.ToString(CultureInfo.InvariantCulture);
							}
							else if (value is bool b)
							{
								type = "bool";
								stringValue = b.ToString().ToLowerInvariant();
							}
							else if (value is string str)
							{
								if (str.ToLower() == "true" || str.ToLower() == "false")
								{
									type = "bool";
									stringValue = str.ToLower();
								}
								else
								{
									type = "string";
									stringValue = str;
								}
							}
							else
							{
								jsonError = $"Unsupported JSON value type for key: {name}";
								break;
							}

							newData.Properties.Add(new DynamicProperty
							{
								Name = name,
								Type = type,
								Value = stringValue
							});
						}

						if (string.IsNullOrEmpty(jsonError))
						{
							component.SetData(newData);
							textSerializedObject.FindProperty("m_Text").stringValue = JsonUtility.ToJson(newData);
							textSerializedObject.ApplyModifiedProperties();
							EditorUtility.SetDirty(textComponent);
							EditorUtility.SetDirty(component);
							Close();
						}
					}
					else
					{
						jsonError = "Invalid JSON format: Expected an object (e.g., {\"key\": value}).";
					}
				}
				catch (Exception ex)
				{
					jsonError = $"Failed to parse JSON: {ex.Message}";
				}
			}
		}

		if (GUILayout.Button("Cancel"))
		{
			Close();
		}
		EditorGUILayout.EndHorizontal();
	}

	private static class MiniJSON
	{
		public static object Deserialize(string json)
		{
			if (string.IsNullOrEmpty(json)) return null;
			index = 0;
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
			i++;
			while (i < json.Length)
			{
				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null;
				if (json[i] == '}') { i++; return dict; }

				string key = ParseString(json, ref i);
				if (key == null) return null;
				SkipWhitespace(json, ref i);
				if (i >= json.Length || json[i] != ':') return null;
				i++;

				object value = ParseValue(json, ref i);
				if (value == null && i < json.Length) return null;
				dict[key] = value;

				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null;
				if (json[i] == '}') { i++; return dict; }
				if (json[i] != ',') return null;
				i++;
			}
			return null;
		}

		private static List<object> ParseArray(string json, ref int i)
		{
			var array = new List<object>();
			i++;
			while (i < json.Length)
			{
				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null;
				if (json[i] == ']') { i++; return array; }

				object value = ParseValue(json, ref i);
				if (value == null && i < json.Length) return null;
				array.Add(value);

				SkipWhitespace(json, ref i);
				if (i >= json.Length) return null;
				if (json[i] == ']') { i++; return array; }
				if (json[i] != ',') return null;
				i++;
			}
			return null;
		}

		private static string ParseString(string json, ref int i)
		{
			var sb = new StringBuilder();
			i++;
			while (i < json.Length)
			{
				char c = json[i];
				if (c == '"') { i++; return sb.ToString(); }
				if (c == '\\')
				{
					i++;
					if (i >= json.Length) return null;
					char next = json[i];
					if (next == '"' || next == '\\' || next == '/') sb.Append(next);
					else if (next == 'n') sb.Append('\n');
					else if (next == 't') sb.Append('\t');
					else if (next == 'r') sb.Append('\r');
					else return null;
					i++;
				}
				else
				{
					sb.Append(c);
					i++;
				}
			}
			return null;
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
				if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
					return result;
				return null;
			}
			else
			{
				if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
					return result;
				return null;
			}
		}

		private static void SkipWhitespace(string json, ref int i)
		{
			while (i < json.Length && char.IsWhiteSpace(json[i]))
				i++;
		}
	}
}