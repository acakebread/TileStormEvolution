using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Globalization;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DisallowMultipleComponent]
public class PortableDynamicProperties : MonoBehaviour
{
	// Runtime: Property data structure
	[System.Serializable]
	public enum PropertyType { Float, Int, String, Bool }

	[System.Serializable]
	public class DynamicProperty
	{
		public string Name;
		public PropertyType Type;
		public string Value; // Single string field to store the value

		public bool TryGetFloat(out float value)
		{
			value = default;
			return Type == PropertyType.Float && float.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}

		public bool TryGetInt(out int value)
		{
			value = default;
			return Type == PropertyType.Int && int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		public bool TryGetString(out string value)
		{
			value = default;
			if (Type == PropertyType.String)
			{
				value = Value ?? "";
				return true;
			}
			return false;
		}

		public bool TryGetBool(out bool value)
		{
			value = default;
			return Type == PropertyType.Bool && bool.TryParse(Value, out value);
		}
	}

	[System.Serializable]
	public class DynamicPropertiesData
	{
		public List<DynamicProperty> Properties = new List<DynamicProperty>();
	}

	// Runtime: Data management
	private class DataManager
	{
		private readonly DynamicPropertiesData _data;
		private readonly Dictionary<string, DynamicProperty> propertyMap;

		public DataManager(string json)
		{
			propertyMap = new Dictionary<string, DynamicProperty>(StringComparer.OrdinalIgnoreCase);
			_data = string.IsNullOrEmpty(json) ? new DynamicPropertiesData() : JsonUtility.FromJson<DynamicPropertiesData>(json);
			if (_data?.Properties != null)
			{
				foreach (var prop in _data.Properties)
				{
					if (!string.IsNullOrEmpty(prop.Name) && !propertyMap.ContainsKey(prop.Name))
					{
						// Validate Value format
						switch (prop.Type)
						{
							case PropertyType.Float:
								if (!float.TryParse(prop.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
									prop.Value = "0";
								break;
							case PropertyType.Int:
								if (!int.TryParse(prop.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
									prop.Value = "0";
								break;
							case PropertyType.String:
								prop.Value = prop.Value ?? "";
								break;
							case PropertyType.Bool:
								if (!bool.TryParse(prop.Value, out _))
									prop.Value = "false";
								break;
						}
						propertyMap.Add(prop.Name, prop);
					}
				}
			}
		}

		public DynamicPropertiesData data => _data;
		public IReadOnlyList<DynamicProperty> Properties => _data.Properties.AsReadOnly();

		public IEnumerable<DynamicProperty> GetPropertiesByType(PropertyType type)
		{
			return _data.Properties.FindAll(p => p.Type == type);
		}

		public void AddFloat(string name, float value)
		{
			if (string.IsNullOrEmpty(name)) return;
			string valueStr = value.ToString(CultureInfo.InvariantCulture);
			if (propertyMap.ContainsKey(name))
			{
				var prop = propertyMap[name];
				prop.Type = PropertyType.Float;
				prop.Value = valueStr;
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.Float, Value = valueStr };
				_data.Properties.Add(prop);
				propertyMap.Add(name, prop);
			}
		}

		public void AddInt(string name, int value)
		{
			if (string.IsNullOrEmpty(name)) return;
			string valueStr = value.ToString(CultureInfo.InvariantCulture);
			if (propertyMap.ContainsKey(name))
			{
				var prop = propertyMap[name];
				prop.Type = PropertyType.Int;
				prop.Value = valueStr;
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.Int, Value = valueStr };
				_data.Properties.Add(prop);
				propertyMap.Add(name, prop);
			}
		}

		public void AddString(string name, string value)
		{
			if (string.IsNullOrEmpty(name)) return;
			if (propertyMap.ContainsKey(name))
			{
				var prop = propertyMap[name];
				prop.Type = PropertyType.String;
				prop.Value = value ?? "";
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.String, Value = value ?? "" };
				_data.Properties.Add(prop);
				propertyMap.Add(name, prop);
			}
		}

		public void AddBool(string name, bool value)
		{
			if (string.IsNullOrEmpty(name)) return;
			string valueStr = value.ToString().ToLowerInvariant();
			if (propertyMap.ContainsKey(name))
			{
				var prop = propertyMap[name];
				prop.Type = PropertyType.Bool;
				prop.Value = valueStr;
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.Bool, Value = valueStr };
				_data.Properties.Add(prop);
				propertyMap.Add(name, prop);
			}
		}

		public bool RemoveProperty(string name)
		{
			if (string.IsNullOrEmpty(name) || !propertyMap.ContainsKey(name)) return false;
			var prop = propertyMap[name];
			propertyMap.Remove(name);
			return _data.Properties.Remove(prop);
		}

		public bool HasFloat(string name)
		{
			return propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Float;
		}

		public bool TryGetFloat(string name, out float value)
		{
			value = default;
			return propertyMap.ContainsKey(name) && propertyMap[name].TryGetFloat(out value);
		}

		public float GetFloat(string name)
		{
			return TryGetFloat(name, out float value) ? value : throw new KeyNotFoundException($"Float property '{name}' not found.");
		}

		public bool HasInt(string name)
		{
			return propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Int;
		}

		public bool TryGetInt(string name, out int value)
		{
			value = default;
			return propertyMap.ContainsKey(name) && propertyMap[name].TryGetInt(out value);
		}

		public int GetInt(string name)
		{
			return TryGetInt(name, out int value) ? value : throw new KeyNotFoundException($"Int property '{name}' not found.");
		}

		public bool HasString(string name)
		{
			return propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.String;
		}

		public bool TryGetString(string name, out string value)
		{
			value = default;
			return propertyMap.ContainsKey(name) && propertyMap[name].TryGetString(out value);
		}

		public string GetString(string name)
		{
			return TryGetString(name, out string value) ? value : throw new KeyNotFoundException($"String property '{name}' not found.");
		}

		public bool HasBool(string name)
		{
			return propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Bool;
		}

		public bool TryGetBool(string name, out bool value)
		{
			value = default;
			return propertyMap.ContainsKey(name) && propertyMap[name].TryGetBool(out value);
		}

		public bool GetBool(string name)
		{
			return TryGetBool(name, out bool value) ? value : throw new KeyNotFoundException($"Bool property '{name}' not found.");
		}

		public string SaveToJson()
		{
			string json = JsonUtility.ToJson(_data, true);
			return json;
		}
	}

	// Runtime: Component logic
	private Text textComponent;
	private DataManager dataManager;

	public DynamicPropertiesData Data => dataManager?.data;

	private void Awake()
	{
		InitializeTextComponent();
		LoadProperties();
	}

	private void OnValidate()
	{
		// Avoid modifications in OnValidate to prevent dirtying
		if (!Application.isPlaying && textComponent == null)
		{
			textComponent = gameObject.GetComponent<Text>();
			if (textComponent != null && string.IsNullOrEmpty(textComponent.text))
			{
#if UNITY_EDITOR
				EditorApplication.delayCall += () =>
				{
					if (textComponent != null && string.IsNullOrEmpty(textComponent.text))
					{
						textComponent.text = "{\"Properties\":[]}";
					}
				};
#endif
			}
		}
	}

	public void InitializeTextComponent()
	{
		bool needsDirty = false;

		if (textComponent == null)
		{
			textComponent = gameObject.GetComponent<Text>();
			if (textComponent == null)
			{
#if UNITY_EDITOR
				Undo.RegisterCompleteObjectUndo(gameObject, "Add Text Component");
#endif
				textComponent = gameObject.AddComponent<Text>();
				if (textComponent == null)
				{
					return;
				}
				needsDirty = true;
			}
		}

		// Only modify Text component properties if necessary
		if (textComponent.enabled)
		{
			textComponent.enabled = false;
			needsDirty = true;
		}
		if (string.IsNullOrEmpty(textComponent.text))
		{
			textComponent.text = "{\"Properties\":[]}";
			needsDirty = true;
		}
		if (textComponent.hideFlags != (HideFlags.HideInInspector | HideFlags.NotEditable))
		{
			textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
			needsDirty = true;
		}

		// Handle Canvas component
		Canvas canvas = gameObject.GetComponent<Canvas>();
		if (canvas != null)
		{
			if (canvas.enabled)
			{
				canvas.enabled = false;
				needsDirty = true;
			}
			if (canvas.hideFlags != (HideFlags.HideInInspector | HideFlags.NotEditable))
			{
				canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				needsDirty = true;
			}
			// Only remove Canvas if it has no necessary components
			if (canvas.GetComponent<CanvasScaler>() == null && canvas.GetComponent<GraphicRaycaster>() == null)
			{
#if UNITY_EDITOR
				Undo.RegisterCompleteObjectUndo(canvas, "Remove Canvas Component");
#endif
				DestroyImmediate(canvas);
				needsDirty = true;
			}
		}

		// Handle CanvasRenderer component
		CanvasRenderer canvasRenderer = gameObject.GetComponent<CanvasRenderer>();
		if (canvasRenderer != null)
		{
			if (canvasRenderer.hideFlags != (HideFlags.HideInInspector | HideFlags.NotEditable))
			{
				canvasRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				needsDirty = true;
			}
		}

		if (needsDirty)
		{
#if UNITY_EDITOR
			EditorUtility.SetDirty(this);
			if (textComponent != null)
			{
				EditorUtility.SetDirty(textComponent);
			}
#endif
		}
	}

	public void LoadProperties()
	{
		if (textComponent == null)
		{
			InitializeTextComponent();
			if (textComponent == null)
			{
				dataManager = new DataManager("{\"Properties\":[]}");
				return;
			}
		}
		dataManager = new DataManager(textComponent.text);
	}

	public void SaveProperties()
	{
		if (textComponent == null)
		{
			InitializeTextComponent();
			if (textComponent == null)
			{
				return;
			}
		}
		if (dataManager == null)
		{
			LoadProperties();
			if (dataManager == null)
			{
				return;
			}
		}
		string newJson = dataManager.SaveToJson();
		if (textComponent.text != newJson)
		{
			textComponent.text = newJson;
#if UNITY_EDITOR
			EditorUtility.SetDirty(textComponent);
			EditorUtility.SetDirty(this);
#endif
		}
	}

	public DynamicPropertiesData GetData()
	{
		if (dataManager == null)
		{
			LoadProperties();
		}
		return dataManager?.data ?? new DynamicPropertiesData();
	}

	public void SetData(DynamicPropertiesData newData)
	{
		if (newData == null)
		{
			newData = new DynamicPropertiesData();
		}
		dataManager = new DataManager(JsonUtility.ToJson(newData));
		SaveProperties();
	}

	// Delegate to DataManager
	public IReadOnlyList<DynamicProperty> Properties => dataManager?.Properties ?? new List<DynamicProperty>().AsReadOnly();
	public IEnumerable<DynamicProperty> GetPropertiesByType(PropertyType type)
	{
		return dataManager?.GetPropertiesByType(type) ?? Enumerable.Empty<DynamicProperty>();
	}
	public void AddFloat(string name, float value)
	{
		dataManager?.AddFloat(name, value);
	}
	public void AddInt(string name, int value)
	{
		dataManager?.AddInt(name, value);
	}
	public void AddString(string name, string value)
	{
		dataManager?.AddString(name, value);
	}
	public void AddBool(string name, bool value)
	{
		dataManager?.AddBool(name, value);
	}
	public bool RemoveProperty(string name)
	{
		return dataManager?.RemoveProperty(name) ?? false;
	}
	public bool HasFloat(string name)
	{
		return dataManager?.HasFloat(name) ?? false;
	}
	public bool TryGetFloat(string name, out float value)
	{
		return dataManager?.TryGetFloat(name, out value) ?? ((value = default) == default);
	}
	public float GetFloat(string name)
	{
		return dataManager?.GetFloat(name) ?? throw new KeyNotFoundException($"Float property '{name}' not found.");
	}
	public bool HasInt(string name)
	{
		return dataManager?.HasInt(name) ?? false;
	}
	public bool TryGetInt(string name, out int value)
	{
		return dataManager?.TryGetInt(name, out value) ?? ((value = default) == default);
	}
	public int GetInt(string name)
	{
		return dataManager?.GetInt(name) ?? throw new KeyNotFoundException($"Int property '{name}' not found.");
	}
	public bool HasString(string name)
	{
		return dataManager?.HasString(name) ?? false;
	}
	public bool TryGetString(string name, out string value)
	{
		return dataManager?.TryGetString(name, out value) ?? ((value = default) == default);
	}
	public string GetString(string name)
	{
		return dataManager?.GetString(name) ?? throw new KeyNotFoundException($"String property '{name}' not found.");
	}
	public bool HasBool(string name)
	{
		return dataManager?.HasBool(name) ?? false;
	}
	public bool TryGetBool(string name, out bool value)
	{
		return dataManager?.TryGetBool(name, out value) ?? ((value = default) == default);
	}
	public bool GetBool(string name)
	{
		return dataManager?.GetBool(name) ?? throw new KeyNotFoundException($"Bool property '{name}' not found.");
	}

#if UNITY_EDITOR
	// Editor: JSON Input Dialog
	private class JsonInputDialog : EditorWindow
	{
		private string jsonInput = "";
		private string jsonError = "";
		private PortableDynamicProperties component;
		private Text textComponent;
		private SerializedObject textSerializedObject;
		private Vector2 scrollPosition;

		// MiniJSON parser, moved to JsonInputDialog as it's only used here
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

		public static void ShowDialog(PortableDynamicProperties component, Text textComponent, SerializedObject textSerializedObject)
		{
			var window = GetWindow<JsonInputDialog>("Inject JSON Config");
			window.component = component;
			window.textComponent = textComponent;
			window.textSerializedObject = textSerializedObject;
			window.jsonInput = textComponent.text;
			window.jsonError = "";
			window.Show();
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Inject JSON Config", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Paste JSON like: {\"myfloat\": 1.245, \"myint\": 6, \"mystring\": \"hello\", \"myflag\": true}", MessageType.Info);

			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
			jsonInput = EditorGUILayout.TextArea(jsonInput, GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

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
					var jsonDict = MiniJSON.Deserialize(jsonInput) as Dictionary<string, object>;
					if (jsonDict == null)
					{
						jsonError = "Invalid JSON: Must be a valid JSON object (e.g., {\"key\": value}). Check for syntax errors.";
					}
					else
					{
						var newData = new DynamicPropertiesData();
						var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
									prop.Value = ((int)d).ToString(CultureInfo.InvariantCulture);
								}
								else
								{
									prop.Type = PropertyType.Float;
									prop.Value = ((float)d).ToString(CultureInfo.InvariantCulture);
								}
							}
							else if (value is int i)
							{
								prop.Type = PropertyType.Int;
								prop.Value = i.ToString(CultureInfo.InvariantCulture);
							}
							else if (value is string s)
							{
								if (s.ToLower() == "true" || s.ToLower() == "false")
								{
									prop.Type = PropertyType.Bool;
									prop.Value = s.ToLower();
								}
								else
								{
									prop.Type = PropertyType.String;
									prop.Value = s;
								}
							}
							else if (value is bool b)
							{
								prop.Type = PropertyType.Bool;
								prop.Value = b.ToString().ToLowerInvariant();
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
							textSerializedObject.FindProperty("m_Text").stringValue = JsonUtility.ToJson(newData);
							textSerializedObject.ApplyModifiedProperties();
							EditorUtility.SetDirty(textComponent);
							EditorUtility.SetDirty(component);
							Close();
						}
					}
				}
				catch (Exception e)
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

	// Editor: Remove All Properties Dialog
	private class RemoveAllPropertiesDialog : EditorWindow
	{
		private PortableDynamicProperties component;
		private Text textComponent;
		private SerializedObject textSerializedObject;

		public static void ShowDialog(PortableDynamicProperties component, Text textComponent, SerializedObject textSerializedObject)
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

	// Editor: Custom Inspector
	[CustomEditor(typeof(PortableDynamicProperties))]
	private class PortableDynamicPropertiesEditor : Editor
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
			var components = go.GetComponentsInChildren<PortableDynamicProperties>(true);
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
					Undo.RegisterCompleteObjectUndo(component.gameObject, "Configure PortableDynamicProperties Components");
					textComponent = component.gameObject.AddComponent<Text>();
					if (textComponent == null)
					{
						continue;
					}
					componentChanged = true;
				}

				// Only configure Text component if necessary
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
			var component = (PortableDynamicProperties)target;
			if (component == null || component.gameObject == null)
			{
				EditorGUILayout.HelpBox("PortableDynamicProperties component or its GameObject is null. Please re-add the component to a valid GameObject.", MessageType.Error);
				return;
			}

			// Ensure Text component is initialized
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
#endif
}