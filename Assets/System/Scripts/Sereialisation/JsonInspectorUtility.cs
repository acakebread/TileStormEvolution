using System.Collections.Generic;
using UnityEngine;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class JsonInspectorUtility : MonoBehaviour
{
	[SerializeField, TextArea(5, 20), Header("C# Deserialization Code (Read-Only")]
	private string cSharpRepresentation = "";
	public string CSharpRepresentation => cSharpRepresentation; // Read-only property for editor access
	private string jsonInputInternal = "{}"; // Private, used for deserialization

	public object Data { get; private set; } // Runtime data, not Inspector-visible

	public void SetJsonInput(string json)
	{
		jsonInputInternal = json;
		OnValidate(); // Trigger deserialization and update display
	}

	void OnValidate()
	{
		Data = null;
		cSharpRepresentation = "No data deserialized";

		if (string.IsNullOrEmpty(jsonInputInternal))
		{
			return;
		}

		try
		{
			// Always deserialize to dictionary for anonymous type approach
			Data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInputInternal);
			cSharpRepresentation = GenerateDeserializationCode((Dictionary<string, object>)Data, jsonInputInternal);
		}
		catch (System.Exception e)
		{
			cSharpRepresentation = $"Error: Failed to deserialize JSON - {e.Message}";
			Data = null;
		}
	}

	void Start()
	{
		OnValidate();
		if (Data != null)
		{
			Debug.Log($"Deserialized data: {JsonTocs.ToJson(Data)}");
		}
	}

	// Generate complete C# class for deserialization
	private string GenerateDeserializationCode(Dictionary<string, object> data, string jsonString)
	{
		var sb = new StringBuilder();
		sb.Append("using System.Collections.Generic;\n");
		sb.Append("using UnityEngine;\n");
		sb.Append("\n");
		sb.Append("public class JsonDeserializationExample : MonoBehaviour\n");
		sb.Append("{\n");
		sb.Append("    void Start()\n");
		sb.Append("    {\n");
		sb.Append($"        string jsonString = @\"{EscapeJsonString(jsonString)}\";\n");
		sb.Append("        try\n");
		sb.Append("        {\n");
		sb.Append("            var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);\n");
		sb.Append("            if (data != null)\n");
		sb.Append("            {\n");
		sb.Append("                // Anonymous type template\n");
		sb.Append($"                var template = {GenerateAnonymousTemplate(data, 4)};\n");
		sb.Append("\n");
		sb.Append("                // Create anonymous type with deserialized values\n");
		sb.Append($"                var result = {GenerateAnonymousResult(data, 4)};\n");
		sb.Append("\n");
		sb.Append("                // Type-safe access example\n");
		foreach (var pair in data)
		{
			if (pair.Value is Dictionary<string, object> nestedDict)
			{
				foreach (var nestedPair in nestedDict)
				{
					sb.Append($"                Debug.Log($\"{nestedPair.Key}: {{result.{pair.Key}.{nestedPair.Key}}}\"); // {nestedPair.Value}\n");
				}
			}
			else
			{
				sb.Append($"                Debug.Log($\"{pair.Key}: {{result.{pair.Key}}}\"); // {pair.Value}\n");
			}
		}
		sb.Append("            }\n");
		sb.Append("            else\n");
		sb.Append("            {\n");
		sb.Append("                Debug.LogError(\"Failed to deserialize JSON\");\n");
		sb.Append("            }\n");
		sb.Append("        }\n");
		sb.Append("        catch (System.Exception e)\n");
		sb.Append("        {\n");
		sb.Append("            Debug.LogError($\"Deserialization error: {e.Message}\");\n");
		sb.Append("        }\n");
		sb.Append("    }\n");
		sb.Append("}\n");
		return sb.ToString();
	}

	// Generate anonymous type template with default values
	private string GenerateAnonymousTemplate(object data, int indentLevel = 0)
	{
		if (data == null) return "null";

		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);

		// Handle dictionaries (JSON objects)
		if (data is Dictionary<string, object> dict)
		{
			sb.Append($"{indent}new\n");
			sb.Append($"{indent}{{\n");
			foreach (var pair in dict)
			{
				string defaultValue = pair.Value is Dictionary<string, object> ? GenerateAnonymousTemplate(pair.Value, indentLevel + 1) : pair.Value is IEnumerable<object> ? "new int[0]" : pair.Value is string ? "\"\"" : "0";
				sb.Append($"{indent}  {pair.Key} = {defaultValue},\n");
			}
			if (dict.Count > 0)
			{
				sb.Length -= 2; // Remove last comma
				sb.Append("\n");
			}
			sb.Append($"{indent}}}");
			return sb.ToString();
		}

		// Handle arrays
		if (data is object[] array)
		{
			sb.Append($"{indent}new []\n");
			sb.Append($"{indent}{{\n");
			for (int i = 0; i < array.Length; i++)
			{
				string defaultValue = array[i] is Dictionary<string, object> ? GenerateAnonymousTemplate(array[i], indentLevel + 1) : array[i] is IEnumerable<object> ? "new int[0]" : array[i] is string ? "\"\"" : "0";
				sb.Append($"{indent}  {defaultValue},\n");
			}
			if (array.Length > 0)
			{
				sb.Length -= 2; // Remove last comma
				sb.Append("\n");
			}
			sb.Append($"{indent}}}");
			return sb.ToString();
		}

		// Handle primitives
		if (data is string)
		{
			return "\"\"";
		}
		return "0";
	}

	// Generate anonymous type with deserialized values
	private string GenerateAnonymousResult(object data, int indentLevel = 0, string parentKey = "")
	{
		if (data == null) return "null";

		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);

		// Handle dictionaries (JSON objects)
		if (data is Dictionary<string, object> dict)
		{
			sb.Append($"{indent}new\n");
			sb.Append($"{indent}{{\n");
			foreach (var pair in dict)
			{
				string value = GenerateValueExpression(pair.Key, pair.Value, indentLevel + 1, parentKey);
				sb.Append($"{indent}  {pair.Key} = {value},\n");
			}
			if (dict.Count > 0)
			{
				sb.Length -= 2; // Remove last comma
				sb.Append("\n");
			}
			sb.Append($"{indent}}}");
			return sb.ToString();
		}

		// Handle arrays
		if (data is object[] array)
		{
			sb.Append($"{indent}new []\n");
			sb.Append($"{indent}{{\n");
			for (int i = 0; i < array.Length; i++)
			{
				sb.Append($"{indent}  {GenerateValueExpression($"[{i}]", array[i], indentLevel + 1, parentKey)},\n");
			}
			if (array.Length > 0)
			{
				sb.Length -= 2; // Remove last comma
				sb.Append("\n");
			}
			sb.Append($"{indent}}}");
			return sb.ToString();
		}

		// Handle primitives
		if (data is string str)
		{
			return $"\"{str}\"";
		}
		return data.ToString();
	}

	// Generate value expression for dictionary access
	private string GenerateValueExpression(string key, object value, int indentLevel, string parentKey)
	{
		string fullKey = string.IsNullOrEmpty(parentKey) ? key : $"{parentKey}.{key}";
		if (value is Dictionary<string, object>)
		{
			return GenerateAnonymousResult(value, indentLevel, fullKey);
		}
		if (value is object[] array)
		{
			return GenerateAnonymousResult(value, indentLevel, fullKey);
		}
		if (value is double || value is float || value is long || value is int)
		{
			return string.IsNullOrEmpty(parentKey)
				? $"data[\"{key}\"] is double ? (int)(double)data[\"{key}\"] : data[\"{key}\"] is long ? (int)(long)data[\"{key}\"] : (int)data[\"{key}\"]"
				: $"(int)((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"]";
		}
		if (value is bool)
		{
			return string.IsNullOrEmpty(parentKey)
				? $"(bool)data[\"{key}\"]"
				: $"(bool)((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"]";
		}
		if (value is string)
		{
			return string.IsNullOrEmpty(parentKey)
				? $"data[\"{key}\"].ToString()"
				: $"((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"].ToString()";
		}
		return string.IsNullOrEmpty(parentKey)
			? $"data[\"{key}\"]?.ToString() ?? \"null\""
			: $"((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"]?.ToString() ?? \"null\"";
	}

	// Escape JSON string for C# verbatim string literal
	private string EscapeJsonString(string json)
	{
		return json.Replace("\"", "\"\"");
	}
}

#if UNITY_EDITOR
[CustomEditor(typeof(JsonInspectorUtility))]
public class JsonInspectorUtilityEditor : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		JsonInspectorUtility utility = (JsonInspectorUtility)target;
		if (GUILayout.Button("Copy Script"))
		{
			GUIUtility.systemCopyBuffer = utility.CSharpRepresentation;
			Debug.Log("Deserialization script copied to clipboard");
		}
	}
}
#endif

//using System.Collections.Generic;
//using UnityEngine;
//using System.Reflection;
//using System.Text;

//public class JsonInspectorUtility : MonoBehaviour
//{
//	[SerializeField, TextArea(5, 20), Header("C# Representation (Read-Only)")] private string cSharpRepresentation = "";
//	[SerializeField] public string targetType = ""; // Editable in Inspector
//	private string jsonInputInternal = "{}"; // Private, used for deserialization

//	public object Data { get; private set; } // Runtime data, not Inspector-visible

//	private static readonly Dictionary<string, System.Type> _typeMap = new Dictionary<string, System.Type>();

//	public static void RegisterType(string typeName, System.Type type)
//	{
//		_typeMap[typeName] = type;
//	}

//	public void SetJsonInput(string json)
//	{
//		jsonInputInternal = json;
//		OnValidate(); // Trigger deserialization and update display
//	}

//	void OnValidate()
//	{
//		Data = null;
//		cSharpRepresentation = "No data deserialized";

//		if (string.IsNullOrEmpty(jsonInputInternal))
//		{
//			return;
//		}

//		try
//		{
//			if (!string.IsNullOrEmpty(targetType) && _typeMap.TryGetValue(targetType, out var type))
//			{
//				// Use reflection to call JsonTocs.FromJson<T>
//				var method = typeof(JsonTocs).GetMethod(nameof(JsonTocs.FromJson)).MakeGenericMethod(type);
//				Data = method.Invoke(null, new object[] { jsonInputInternal });
//			}
//			else
//			{
//				// Fallback to dictionary for dynamic JSON
//				Data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInputInternal);
//			}

//			// Generate anonymous type display
//			cSharpRepresentation = MarkupAsCS(Data);
//		}
//		catch (System.Exception e)
//		{
//			cSharpRepresentation = $"Error: Failed to deserialize JSON - {e.Message}";
//			Data = null;
//		}
//	}

//	void Start()
//	{
//		OnValidate();
//		if (Data != null)
//		{
//			Debug.Log($"Deserialized data: {JsonTocs.ToJson(Data)}");
//		}
//	}

//	// Format data as C# anonymous type expression
//	private string MarkupAsCS(object data, int indentLevel = 0)
//	{
//		if (data == null) return "null";

//		var sb = new StringBuilder();
//		string indent = new string(' ', indentLevel * 2);

//		// Handle dictionaries (JSON objects)
//		if (data is Dictionary<string, object> dict)
//		{
//			sb.Append($"{indent}new\n");
//			sb.Append($"{indent}{{\n");
//			foreach (var pair in dict)
//			{
//				sb.Append($"{indent}  {pair.Key} = {MarkupAsCS(pair.Value, indentLevel + 1)},\n");
//			}
//			if (dict.Count > 0)
//			{
//				sb.Length -= 2; // Remove last comma
//				sb.Append("\n");
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle arrays
//		if (data is object[] array)
//		{
//			sb.Append($"{indent}new []\n");
//			sb.Append($"{indent}{{\n");
//			for (int i = 0; i < array.Length; i++)
//			{
//				sb.Append($"{indent}  {MarkupAsCS(array[i], indentLevel + 1)},\n");
//			}
//			if (array.Length > 0)
//			{
//				sb.Length -= 2; // Remove last comma
//				sb.Append("\n");
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle custom objects (like User)
//		if (data.GetType().IsClass && data.GetType() != typeof(string))
//		{
//			sb.Append($"{indent}new\n");
//			sb.Append($"{indent}{{\n");
//			var properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
//			foreach (var prop in properties)
//			{
//				if (prop.CanRead)
//				{
//					var value = prop.GetValue(data);
//					sb.Append($"{indent}  {prop.Name} = {MarkupAsCS(value, indentLevel + 1)},\n");
//				}
//			}
//			if (properties.Length > 0)
//			{
//				sb.Length -= 2; // Remove last comma
//				sb.Append("\n");
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle primitives
//		if (data is string str)
//		{
//			return $"\"{str}\"";
//		}
//		return data.ToString();
//	}
//}



//using System.Collections.Generic;
//using UnityEngine;
//using System.Reflection;
//using System.Text;

//public class JsonInspectorUtility : MonoBehaviour
//{
//	[SerializeField, TextArea(5, 20), Header("C# Representation (Read-Only)")] private string cSharpRepresentation = "";
//	[SerializeField] public string targetType = ""; // Editable in Inspector
//	private string jsonInputInternal = "{}"; // Private, used for deserialization

//	public object Data { get; private set; } // Runtime data, not Inspector-visible

//	private static readonly Dictionary<string, System.Type> _typeMap = new Dictionary<string, System.Type>();

//	public static void RegisterType(string typeName, System.Type type)
//	{
//		_typeMap[typeName] = type;
//	}

//	public void SetJsonInput(string json)
//	{
//		jsonInputInternal = json;
//		OnValidate(); // Trigger deserialization and update display
//	}

//	void OnValidate()
//	{
//		Data = null;
//		cSharpRepresentation = "No data deserialized";

//		if (string.IsNullOrEmpty(jsonInputInternal))
//		{
//			return;
//		}

//		try
//		{
//			if (!string.IsNullOrEmpty(targetType) && _typeMap.TryGetValue(targetType, out var type))
//			{
//				// Use reflection to call JsonTocs.FromJson<T>
//				var method = typeof(JsonTocs).GetMethod(nameof(JsonTocs.FromJson)).MakeGenericMethod(type);
//				Data = method.Invoke(null, new object[] { jsonInputInternal });
//			}
//			else
//			{
//				// Fallback to dictionary for dynamic JSON
//				Data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInputInternal);
//			}

//			// Generate C#-like display
//			cSharpRepresentation = MarkupAsCS(Data);
//		}
//		catch (System.Exception e)
//		{
//			cSharpRepresentation = $"Error: Failed to deserialize JSON - {e.Message}";
//			Data = null;
//		}
//	}

//	void Start()
//	{
//		OnValidate();
//		if (Data != null)
//		{
//			Debug.Log($"Deserialized data: {JsonTocs.ToJson(Data)}");
//		}
//	}

//	// Format data as valid C# class declaration
//	private string MarkupAsCS(object data, int indentLevel = 0, string className = null)
//	{
//		if (data == null) return "null";

//		var sb = new StringBuilder();
//		string indent = new string(' ', indentLevel * 2);

//		// Handle dictionaries (JSON objects)
//		if (data is Dictionary<string, object> dict)
//		{
//			string typeName = indentLevel == 0 ? (string.IsNullOrEmpty(className) ? (string.IsNullOrEmpty(targetType) ? "Dynamic" : targetType) : "Dynamic") : "Dynamic";
//			sb.Append($"{indent}public class {typeName}\n");
//			sb.Append($"{indent}{{\n");
//			foreach (var pair in dict)
//			{
//				sb.Append($"{indent}  public {GetCSType(pair.Value)} {pair.Key} = {MarkupAsCS(pair.Value, indentLevel + 1, pair.Key)};\n");
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle arrays
//		if (data is object[] array)
//		{
//			sb.Append($"{indent}public {GetCSType(array.Length > 0 ? array[0] : null)}[] Array = new {GetCSType(array.Length > 0 ? array[0] : null)}[{array.Length}]\n");
//			sb.Append($"{indent}{{\n");
//			for (int i = 0; i < array.Length; i++)
//			{
//				sb.Append($"{indent}  [{i}] = {MarkupAsCS(array[i], indentLevel + 1)};\n");
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle custom objects (like User)
//		if (data.GetType().IsClass && data.GetType() != typeof(string))
//		{
//			string typeName = indentLevel == 0 ? (string.IsNullOrEmpty(className) ? data.GetType().Name : className) : data.GetType().Name;
//			sb.Append($"{indent}public class {typeName}\n");
//			sb.Append($"{indent}{{\n");
//			var properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
//			foreach (var prop in properties)
//			{
//				if (prop.CanRead)
//				{
//					var value = prop.GetValue(data);
//					sb.Append($"{indent}  public {GetCSType(value)} {prop.Name} = {MarkupAsCS(value, indentLevel + 1, prop.Name)};\n");
//				}
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle primitives
//		if (data is string str)
//		{
//			return $"\"{str}\"";
//		}
//		return data.ToString();
//	}

//	// Helper to map object types to C#-like type names
//	private string GetCSType(object value)
//	{
//		if (value == null) return "object";
//		var type = value.GetType();
//		if (type == typeof(string)) return "string";
//		if (type == typeof(int) || type == typeof(long)) return "int";
//		if (type == typeof(float) || type == typeof(double)) return "float";
//		if (type == typeof(bool)) return "bool";
//		if (type.IsArray) return $"{GetCSType(type.GetElementType())}[]";
//		if (type.IsClass) return type.Name;
//		return "object";
//	}
//}


//using System.Collections.Generic;
//using UnityEngine;

//[System.Serializable]
//public class JsonInspectorData
//{
//	[SerializeField] public List<string> Keys = new List<string>();
//	[SerializeField] public List<string> Values = new List<string>(); // Display as strings for Inspector
//}

//public class JsonInspectorUtility : MonoBehaviour
//{
//	[SerializeField, TextArea(3, 10)] public string jsonInput = "{}";
//	[SerializeField] public string targetType = ""; // Editable in Inspector
//	[SerializeField] private JsonInspectorData dataWrapper = new JsonInspectorData();

//	public object Data { get; private set; } // Runtime data, not Inspector-visible

//	private static readonly Dictionary<string, System.Type> _typeMap = new Dictionary<string, System.Type>();

//	public static void RegisterType(string typeName, System.Type type)
//	{
//		_typeMap[typeName] = type;
//	}

//	void OnValidate()
//	{
//		if (dataWrapper == null)
//		{
//			dataWrapper = new JsonInspectorData();
//		}

//		dataWrapper.Keys.Clear();
//		dataWrapper.Values.Clear();
//		Data = null;

//		if (string.IsNullOrEmpty(jsonInput))
//		{
//			return;
//		}

//		try
//		{
//			if (!string.IsNullOrEmpty(targetType) && _typeMap.TryGetValue(targetType, out var type))
//			{
//				// Use reflection to call JsonTocs.FromJson<T>
//				var method = typeof(JsonTocs).GetMethod(nameof(JsonTocs.FromJson)).MakeGenericMethod(type);
//				Data = method.Invoke(null, new object[] { jsonInput });
//			}
//			else
//			{
//				// Fallback to dictionary for dynamic JSON
//				Data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInput);
//			}

//			// Populate Inspector data
//			if (Data is Dictionary<string, object> dict)
//			{
//				foreach (var pair in dict)
//				{
//					dataWrapper.Keys.Add(pair.Key);
//					dataWrapper.Values.Add(JsonTocs.ToJson(pair.Value));
//				}
//			}
//			else if (Data != null)
//			{
//				// Convert object to dictionary for Inspector display
//				var dict2 = JsonTocs.ToJson(Data);
//				var tempDict = JsonTocs.FromJson<Dictionary<string, object>>(dict2);
//				foreach (var pair in tempDict)
//				{
//					dataWrapper.Keys.Add(pair.Key);
//					dataWrapper.Values.Add(JsonTocs.ToJson(pair.Value));
//				}
//			}
//		}
//		catch (System.Exception e)
//		{
//			Debug.LogWarning($"Failed to deserialize JSON in {name}: {e.Message}");
//			Data = null;
//		}
//	}

//	void Start()
//	{
//		OnValidate();
//		if (Data != null)
//		{
//			Debug.Log($"Deserialized data: {JsonTocs.ToJson(Data)}");
//		}
//	}
//}