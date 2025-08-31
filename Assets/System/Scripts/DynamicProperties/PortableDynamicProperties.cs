using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

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
		public float FloatValue;
		public int IntValue;
		public string StringValue;
		public bool BoolValue;
	}

	[System.Serializable]
	public class DynamicPropertiesData
	{
		public List<DynamicProperty> Properties = new List<DynamicProperty>();
	}

	// Runtime: Data management (formerly DynamicPropertiesDataManager)
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
						propertyMap.Add(prop.Name, prop);
					}
				}
			}
		}

		public DynamicPropertiesData data => _data;
		public IReadOnlyList<DynamicProperty> Properties => _data.Properties.AsReadOnly();

		public IEnumerable<DynamicProperty> GetPropertiesByType(PropertyType type) =>
			_data.Properties.FindAll(p => p.Type == type);

		public void AddFloat(string name, float value)
		{
			if (string.IsNullOrEmpty(name)) return;
			if (propertyMap.ContainsKey(name))
			{
				var prop = propertyMap[name];
				prop.Type = PropertyType.Float;
				prop.FloatValue = value;
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.Float, FloatValue = value };
				_data.Properties.Add(prop);
				propertyMap.Add(name, prop);
			}
		}

		public void AddInt(string name, int value)
		{
			if (string.IsNullOrEmpty(name)) return;
			if (propertyMap.ContainsKey(name))
			{
				var prop = propertyMap[name];
				prop.Type = PropertyType.Int;
				prop.IntValue = value;
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.Int, IntValue = value };
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
				prop.StringValue = value ?? "";
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.String, StringValue = value ?? "" };
				_data.Properties.Add(prop);
				propertyMap.Add(name, prop);
			}
		}

		public void AddBool(string name, bool value)
		{
			if (string.IsNullOrEmpty(name)) return;
			if (propertyMap.ContainsKey(name))
			{
				var prop = propertyMap[name];
				prop.Type = PropertyType.Bool;
				prop.BoolValue = value;
			}
			else
			{
				var prop = new DynamicProperty { Name = name, Type = PropertyType.Bool, BoolValue = value };
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

		public bool HasFloat(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Float;
		public bool TryGetFloat(string name, out float value)
		{
			value = default;
			if (propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Float)
			{
				value = propertyMap[name].FloatValue;
				return true;
			}
			return false;
		}
		public float GetFloat(string name) => HasFloat(name) ? propertyMap[name].FloatValue : throw new KeyNotFoundException($"Float property '{name}' not found.");

		public bool HasInt(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Int;
		public bool TryGetInt(string name, out int value)
		{
			value = default;
			if (propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Int)
			{
				value = propertyMap[name].IntValue;
				return true;
			}
			return false;
		}
		public int GetInt(string name) => HasInt(name) ? propertyMap[name].IntValue : throw new KeyNotFoundException($"Int property '{name}' not found.");

		public bool HasString(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.String;
		public bool TryGetString(string name, out string value)
		{
			value = default;
			if (propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.String)
			{
				value = propertyMap[name].StringValue;
				return true;
			}
			return false;
		}
		public string GetString(string name) => HasString(name) ? propertyMap[name].StringValue : throw new KeyNotFoundException($"String property '{name}' not found.");

		public bool HasBool(string name) => propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Bool;
		public bool TryGetBool(string name, out bool value)
		{
			value = default;
			if (propertyMap.ContainsKey(name) && propertyMap[name].Type == PropertyType.Bool)
			{
				value = propertyMap[name].BoolValue;
				return true;
			}
			return false;
		}
		public bool GetBool(string name) => HasBool(name) ? propertyMap[name].BoolValue : throw new KeyNotFoundException($"Bool property '{name}' not found.");

		public string SaveToJson() => JsonUtility.ToJson(_data);
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
		if (!Application.isPlaying)
		{
			InitializeTextComponent();
			LoadProperties();
		}
	}

	public void InitializeTextComponent()
	{
		if (textComponent == null)
		{
			textComponent = gameObject.GetComponent<Text>();
			if (textComponent == null)
			{
				textComponent = gameObject.AddComponent<Text>();
				if (textComponent == null)
				{
					Debug.LogWarning($"Failed to add Text component to {gameObject.name}.");
					return;
				}
				textComponent.enabled = false;
				textComponent.text = "{\"Properties\":[]}";
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				// Configure RectTransform to mimic Transform
				RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
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
			}
			else
			{
				// Configure existing Text component
				textComponent.enabled = false;
				if (string.IsNullOrEmpty(textComponent.text))
				{
					textComponent.text = "{\"Properties\":[]}";
				}
				textComponent.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
				// Configure existing RectTransform
				RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
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
			}

			// Configure Canvas if present
			Canvas canvas = gameObject.GetComponent<Canvas>();
			if (canvas != null)
			{
				canvas.enabled = false;
				canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
			}

			// Configure CanvasRenderer if present
			CanvasRenderer canvasRenderer = gameObject.GetComponent<CanvasRenderer>();
			if (canvasRenderer != null)
			{
				canvasRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
			}
		}
	}

	public void LoadProperties()
	{
		if (textComponent == null)
		{
			InitializeTextComponent();
			if (textComponent == null)
			{
				Debug.LogWarning($"Text component could not be initialized for {gameObject.name}.");
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
			Debug.LogWarning($"Text component is missing in SaveProperties for {gameObject.name}. Reinitializing...");
			InitializeTextComponent();
			if (textComponent == null)
			{
				Debug.LogWarning($"Failed to reinitialize Text component for {gameObject.name}. Cannot save properties.");
				return;
			}
		}
		if (dataManager == null)
		{
			Debug.LogWarning($"DataManager is null in SaveProperties for {gameObject.name}. Reinitializing...");
			LoadProperties();
			if (dataManager == null)
			{
				Debug.LogWarning($"Failed to initialize DataManager for {gameObject.name}. Cannot save properties.");
				return;
			}
		}
		textComponent.text = dataManager.SaveToJson();
#if UNITY_EDITOR
		EditorUtility.SetDirty(textComponent);
		EditorUtility.SetDirty(this);
#endif
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
			Debug.LogWarning($"SetData received null data for {gameObject.name}. Initializing empty data.");
			newData = new DynamicPropertiesData();
		}
		dataManager = new DataManager(JsonUtility.ToJson(newData));
		SaveProperties();
	}

	// Delegate to DataManager
	public IReadOnlyList<DynamicProperty> Properties => dataManager?.Properties ?? new List<DynamicProperty>().AsReadOnly();
	public IEnumerable<DynamicProperty> GetPropertiesByType(PropertyType type) => dataManager?.GetPropertiesByType(type) ?? Enumerable.Empty<DynamicProperty>();
	public void AddFloat(string name, float value) => dataManager?.AddFloat(name, value);
	public void AddInt(string name, int value) => dataManager?.AddInt(name, value);
	public void AddString(string name, string value) => dataManager?.AddString(name, value);
	public void AddBool(string name, bool value) => dataManager?.AddBool(name, value);
	public bool RemoveProperty(string name) => dataManager?.RemoveProperty(name) ?? false;
	public bool HasFloat(string name) => dataManager?.HasFloat(name) ?? false;
	public bool TryGetFloat(string name, out float value) => dataManager?.TryGetFloat(name, out value) ?? (value = default) == default;
	public float GetFloat(string name) => dataManager?.GetFloat(name) ?? throw new KeyNotFoundException($"Float property '{name}' not found.");
	public bool HasInt(string name) => dataManager?.HasInt(name) ?? false;
	public bool TryGetInt(string name, out int value) => dataManager?.TryGetInt(name, out value) ?? (value = default) == default;
	public int GetInt(string name) => dataManager?.GetInt(name) ?? throw new KeyNotFoundException($"Int property '{name}' not found.");
	public bool HasString(string name) => dataManager?.HasString(name) ?? false;
	public bool TryGetString(string name, out string value) => dataManager?.TryGetString(name, out value) ?? (value = default) == default;
	public string GetString(string name) => dataManager?.GetString(name) ?? throw new KeyNotFoundException($"String property '{name}' not found.");
	public bool HasBool(string name) => dataManager?.HasBool(name) ?? false;
	public bool TryGetBool(string name, out bool value) => dataManager?.TryGetBool(name, out value) ?? (value = default) == default;
	public bool GetBool(string name) => dataManager?.GetBool(name) ?? throw new KeyNotFoundException($"Bool property '{name}' not found.");

#if UNITY_EDITOR
	// Editor: MiniJSON parser
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
				if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result))
					return result;
				return null;
			}
			else
			{
				if (int.TryParse(numStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int result))
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

	// Editor: JSON Input Dialog
	private class JsonInputDialog : EditorWindow
	{
		private string jsonInput = "";
		private string jsonError = "";
		private PortableDynamicProperties component;
		private Text textComponent;
		private SerializedObject textSerializedObject;

		public static void ShowDialog(PortableDynamicProperties component, Text textComponent, SerializedObject textSerializedObject)
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
			PrefabStage.prefabStageOpened += stage =>
			{
				if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
				{
					EnsureTextComponentOnDynamicProperties(stage.prefabContentsRoot);
				}
			};
			EditorApplication.hierarchyChanged += () =>
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
			var components = go.GetComponentsInChildren<PortableDynamicProperties>(true);
			foreach (var component in components)
			{
				if (component == null || component.gameObject == null)
				{
					Debug.LogWarning($"PortableDynamicProperties component or its GameObject is null on {go.name}.");
					continue;
				}
				Undo.RegisterCompleteObjectUndo(component.gameObject, "Configure PortableDynamicProperties Components");
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

				// Configure Canvas if present
				Canvas canvas = component.gameObject.GetComponent<Canvas>();
				if (canvas != null)
				{
					canvas.enabled = false;
					canvas.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
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
				EditorUtility.SetDirty(textComponent);
				EditorUtility.SetDirty(component);
			}
		}

		public override void OnInspectorGUI()
		{
			var component = (PortableDynamicProperties)target;
			if (component == null || component.gameObject == null)
			{
				EditorGUILayout.HelpBox("PortableDynamicProperties component or its GameObject is null. Please re-add the component to a valid GameObject.", MessageType.Error);
				return;
			}

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
					FloatValue = 0f,
					IntValue = 0,
					StringValue = "",
					BoolValue = false
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