using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class JsonInputDialog : EditorWindow
{
	private string jsonInput = "";
	private string jsonError = "";
	private DynamicProperties component;
	private Text textComponent;
	private SerializedObject textSerializedObject;

	public static void ShowDialog(DynamicProperties component, Text textComponent, SerializedObject textSerializedObject)
	{
		var window = GetWindow<JsonInputDialog>("Inject JSON Config");
		window.component = component;
		window.textComponent = textComponent;
		window.textSerializedObject = textSerializedObject;
		window.jsonInput = "";
		window.jsonError = "";
		window.Show();
	}

	private void OnGUI()
	{
		EditorGUILayout.LabelField("Inject JSON Config", EditorStyles.boldLabel);
		EditorGUILayout.HelpBox("Paste JSON like: {\"myfloat\": 1.245, \"myint\": 6, \"mystring\": \"hello\", \"myflag\": \"false\"}", MessageType.Info);

		jsonInput = EditorGUILayout.TextArea(jsonInput, GUILayout.Height(100), GUILayout.Width(position.width - 20));

		if (!string.IsNullOrEmpty(jsonError))
		{
			EditorGUILayout.HelpBox(jsonError, MessageType.Error);
		}

		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Inject", GUILayout.Width(100)))
		{
			Undo.RecordObject(textComponent, "Inject JSON Config");
			jsonError = "";
			try
			{
				var jsonDict = DynamicPropertiesEditor.MiniJSON.Deserialize(jsonInput) as System.Collections.Generic.Dictionary<string, object>;
				if (jsonDict == null)
				{
					jsonError = "Invalid JSON: Must be a valid JSON object (e.g., {\"key\": value}). Check for syntax errors.";
				}
				else
				{
					var newData = new DynamicPropertiesData();
					var existingNames = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
					foreach (var kvp in jsonDict)
					{
						string name = kvp.Key;
						if (string.IsNullOrEmpty(name) || existingNames.Contains(name))
						{
							jsonError = $"Invalid or duplicate property name '{name}'.";
							break;
						}
						existingNames.Add(name);

						var value = kvp.Value;
						DynamicProperty prop = new DynamicProperty { Name = name };
						if (value is double d)
						{
							if (d % 1 == 0 && d >= int.MinValue && d <= int.MaxValue)
							{
								prop.Type = PropertyType.Int;
								prop.IntValue = (int)d;
							}
							else
							{
								prop.Type = PropertyType.Float;
								prop.FloatValue = (float)d;
							}
						}
						else if (value is int i)
						{
							prop.Type = PropertyType.Int;
							prop.IntValue = i;
						}
						else if (value is string s)
						{
							if (s.ToLower() == "true" || s.ToLower() == "false")
							{
								prop.Type = PropertyType.Bool;
								prop.BoolValue = s.ToLower() == "true";
							}
							else
							{
								prop.Type = PropertyType.String;
								prop.StringValue = s;
							}
						}
						else if (value is bool b)
						{
							prop.Type = PropertyType.Bool;
							prop.BoolValue = b;
						}
						else
						{
							jsonError = $"Unsupported value type for property '{name}': {value?.GetType().Name ?? "null"}.";
							break;
						}
						newData.Properties.Add(prop);
					}
					if (string.IsNullOrEmpty(jsonError))
					{
						component.SetData(newData);
						EditorUtility.SetDirty(textComponent);
						EditorUtility.SetDirty(component);
						Close();
					}
				}
			}
			catch (System.Exception e)
			{
				jsonError = $"Failed to parse JSON: {e.Message}. Ensure the JSON is well-formed and contains valid key-value pairs.";
			}
		}
		if (GUILayout.Button("Cancel", GUILayout.Width(100)))
		{
			Close();
		}
		EditorGUILayout.EndHorizontal();
	}
}