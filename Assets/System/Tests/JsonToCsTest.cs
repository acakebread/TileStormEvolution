using UnityEngine;

public class JsonToCsTest : MonoBehaviour
{
	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
        ""name"": ""John"",
        ""age"": 30,
        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
    }";

	void Awake()
	{
		// Initialize JsonInspectorUtility
		var utility = gameObject.AddComponent<JsonInspectorUtility>();

		// Set JSON input and get deserialized data
		utility.SetJsonInput(jsonInput);
	}
}



//using System.Collections.Generic;
//using UnityEngine;

//public class JsonToCsTest : MonoBehaviour
//{
//	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
//        ""name"": ""John"",
//        ""age"": 30,
//        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
//    }";
//	//[SerializeField, TextArea(5, 20), Header("C# Deserialization Code (Read-Only)")] private string cSharpDeserializationCode = "";

//	private JsonInspectorUtility utility;

//	void Awake()
//	{
//		// Initialize JsonInspectorUtility
//		utility = gameObject.AddComponent<JsonInspectorUtility>();

//		// Set JSON input and get deserialized data
//		utility.SetJsonInput(jsonInput);
//		//cSharpDeserializationCode = utility.cSharpRepresentation; // Access via property or method if needed

//		//// Test deserialization
//		//if (utility.Data != null)
//		//{
//		//	Debug.Log($"Deserialized data: {JsonTocs.ToJson(utility.Data)}");
//		//	if (utility.Data is Dictionary<string, object> data)
//		//	{
//		//		Debug.Log($"Name: {data.GetValueOrDefault("name")}"); // John
//		//		if (data.GetValueOrDefault("address") is Dictionary<string, object> address)
//		//		{
//		//			Debug.Log($"City: {address.GetValueOrDefault("city")}"); // New York
//		//		}
//		//	}
//		//}
//		//else
//		//{
//		//	Debug.LogError("Failed to deserialize JSON");
//		//}
//	}
//}



//using UnityEngine;
//using System.Collections.Generic;
//using System.Text;

//public class JsonToCsTest : MonoBehaviour
//{
//	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
//        ""name"": ""John"",
//        ""age"": 30,
//        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
//    }";
//	[SerializeField, TextArea(5, 20), Header("C# Deserialization Code (Read-Only)")] private string cSharpDeserializationCode = "";

//	private Dictionary<string, object> data; // Runtime data, not Inspector-visible

//	void Awake()
//	{
//		// Deserialize to a dictionary
//		data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInput);
//		if (data != null)
//		{
//			// Demonstrate dynamic access
//			Debug.Log($"Name: {data.GetValueOrDefault("name")}"); // John
//			if (data.GetValueOrDefault("address") is Dictionary<string, object> address)
//			{
//				Debug.Log($"City: {address.GetValueOrDefault("city")}"); // New York
//			}

//			// Generate deserialization code
//			cSharpDeserializationCode = GenerateDeserializationCode(data, jsonInput);
//		}
//		else
//		{
//			Debug.LogError("Failed to deserialize JSON");
//			cSharpDeserializationCode = "Error: Failed to deserialize JSON";
//		}

//		// Serialize back
//		string serialized = JsonTocs.ToJson(data);
//		Debug.Log($"Serialized: {serialized}");
//	}

//	// Generate C# code snippet for deserialization and anonymous type creation
//	private string GenerateDeserializationCode(Dictionary<string, object> data, string jsonString)
//	{
//		var sb = new StringBuilder();
//		sb.Append("using System.Collections.Generic;\n");
//		sb.Append("using UnityEngine;\n");
//		sb.Append($"string jsonString = @\"{EscapeJsonString(jsonString)}\";\n");
//		sb.Append("try\n");
//		sb.Append("{\n");
//		sb.Append("  var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);\n");
//		sb.Append("  if (data != null)\n");
//		sb.Append("  {\n");
//		sb.Append("    // Anonymous type template\n");
//		sb.Append($"    var template = {GenerateAnonymousTemplate(data, 2)};\n");
//		sb.Append("\n");
//		sb.Append("    // Create anonymous type with deserialized values\n");
//		sb.Append($"    var result = {GenerateAnonymousResult(data, 2)};\n");
//		sb.Append("\n");
//		sb.Append("    // Type-safe access example\n");
//		foreach (var pair in data)
//		{
//			if (pair.Value is Dictionary<string, object> nestedDict)
//			{
//				foreach (var nestedPair in nestedDict)
//				{
//					sb.Append($"    Debug.Log($\"{nestedPair.Key}: {{result.{pair.Key}.{nestedPair.Key}}}\"); // {nestedPair.Value}\n");
//				}
//			}
//			else
//			{
//				sb.Append($"    Debug.Log($\"{pair.Key}: {{result.{pair.Key}}}\"); // {pair.Value}\n");
//			}
//		}
//		sb.Append("  }\n");
//		sb.Append("  else\n");
//		sb.Append("  {\n");
//		sb.Append("    Debug.LogError(\"Failed to deserialize JSON\");\n");
//		sb.Append("  }\n");
//		sb.Append("}\n");
//		sb.Append("catch (System.Exception e)\n");
//		sb.Append("{\n");
//		sb.Append("  Debug.LogError($\"Deserialization error: {e.Message}\");\n");
//		sb.Append("}\n");
//		return sb.ToString();
//	}

//	// Generate anonymous type template with default values
//	private string GenerateAnonymousTemplate(object data, int indentLevel = 0)
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
//				string defaultValue = pair.Value is Dictionary<string, object> ? GenerateAnonymousTemplate(pair.Value, indentLevel + 1) : pair.Value is IEnumerable<object> ? "new int[0]" : pair.Value is string ? "\"\"" : "0";
//				sb.Append($"{indent}  {pair.Key} = {defaultValue},\n");
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
//				string defaultValue = array[i] is Dictionary<string, object> ? GenerateAnonymousTemplate(array[i], indentLevel + 1) : array[i] is IEnumerable<object> ? "new int[0]" : array[i] is string ? "\"\"" : "0";
//				sb.Append($"{indent}  {defaultValue},\n");
//			}
//			if (array.Length > 0)
//			{
//				sb.Length -= 2; // Remove last comma
//				sb.Append("\n");
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle primitives
//		if (data is string)
//		{
//			return "\"\"";
//		}
//		return "0";
//	}

//	// Generate anonymous type with deserialized values
//	private string GenerateAnonymousResult(object data, int indentLevel = 0, string parentKey = "")
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
//				string value = GenerateValueExpression(pair.Key, pair.Value, indentLevel + 1, parentKey);
//				sb.Append($"{indent}  {pair.Key} = {value},\n");
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
//				sb.Append($"{indent}  {GenerateValueExpression($"[{i}]", array[i], indentLevel + 1, parentKey)},\n");
//			}
//			if (array.Length > 0)
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

//	// Generate value expression for dictionary access
//	private string GenerateValueExpression(string key, object value, int indentLevel, string parentKey)
//	{
//		string fullKey = string.IsNullOrEmpty(parentKey) ? key : $"{parentKey}.{key}";
//		if (value is Dictionary<string, object>)
//		{
//			return GenerateAnonymousResult(value, indentLevel, fullKey);
//		}
//		if (value is object[] array)
//		{
//			return GenerateAnonymousResult(value, indentLevel, fullKey);
//		}
//		if (value is double || value is float || value is long || value is int)
//		{
//			return string.IsNullOrEmpty(parentKey)
//				? $"data[\"{key}\"] is double ? (int)(double)data[\"{key}\"] : data[\"{key}\"] is long ? (int)(long)data[\"{key}\"] : (int)data[\"{key}\"]"
//				: $"(int)((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"]";
//		}
//		if (value is bool)
//		{
//			return string.IsNullOrEmpty(parentKey)
//				? $"(bool)data[\"{key}\"]"
//				: $"(bool)((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"]";
//		}
//		if (value is string)
//		{
//			return string.IsNullOrEmpty(parentKey)
//				? $"data[\"{key}\"].ToString()"
//				: $"((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"].ToString()";
//		}
//		return string.IsNullOrEmpty(parentKey)
//			? $"data[\"{key}\"]?.ToString() ?? \"null\""
//			: $"((Dictionary<string, object>)data[\"{parentKey}\"])[\"{key}\"]?.ToString() ?? \"null\"";
//	}

//	// Escape JSON string for C# verbatim string literal
//	private string EscapeJsonString(string json)
//	{
//		return json.Replace("\"", "\"\"");
//	}
//}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Text;

//public class JsonToCsTest : MonoBehaviour
//{
//	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
//        ""name"": ""John"",
//        ""age"": 30,
//        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
//}";
//	[SerializeField, TextArea(5, 20), Header("C# Deserialization Code (Read-Only)")] private string cSharpDeserializationCode = "";

//	private Dictionary<string, object> data; // Runtime data, not Inspector-visible

//	void Awake()
//	{
//		// Deserialize to a dictionary
//		data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInput);
//		if (data != null)
//		{
//			// Demonstrate dynamic access
//			Debug.Log($"Name: {data.GetValueOrDefault("name")}"); // John
//			if (data.GetValueOrDefault("address") is Dictionary<string, object> address)
//			{
//				Debug.Log($"City: {address.GetValueOrDefault("city")}"); // New York
//			}

//			// Generate deserialization code
//			cSharpDeserializationCode = GenerateDeserializationCode(data);
//		}
//		else
//		{
//			Debug.LogError("Failed to deserialize JSON");
//			cSharpDeserializationCode = "Error: Failed to deserialize JSON";
//		}

//		// Serialize back
//		string serialized = JsonTocs.ToJson(data);
//		Debug.Log($"Serialized: {serialized}");
//	}

//	// Generate C# code snippet for deserialization and anonymous type creation
//	private string GenerateDeserializationCode(Dictionary<string, object> data)
//	{
//		var sb = new StringBuilder();
//		sb.Append("string jsonString = @\"{ ... }\"; // Your JSON string\n");
//		sb.Append("var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);\n");
//		sb.Append("if (data != null)\n");
//		sb.Append("{\n");
//		sb.Append("  // Anonymous type template\n");
//		sb.Append($"  var template = {GenerateAnonymousTemplate(data, 1)};\n");
//		sb.Append("\n");
//		sb.Append("  // Create anonymous type with deserialized values\n");
//		sb.Append($"  var result = {GenerateAnonymousResult(data, 1)};\n");
//		sb.Append("\n");
//		sb.Append("  // Type-safe access example\n");
//		sb.Append($"  Debug.Log($\"Name: {{result.name}}\"); // {data.GetValueOrDefault("name")}\n");
//		if (data.GetValueOrDefault("address") is Dictionary<string, object> address)
//		{
//			sb.Append($"  Debug.Log($\"City: {{result.address.city}}\"); // {address.GetValueOrDefault("city")}\n");
//		}
//		sb.Append("}\n");
//		sb.Append("else\n");
//		sb.Append("{\n");
//		sb.Append("  Debug.LogError(\"Failed to deserialize JSON\");\n");
//		sb.Append("}");
//		return sb.ToString();
//	}

//	// Generate anonymous type template with default values
//	private string GenerateAnonymousTemplate(object data, int indentLevel = 0)
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
//				string defaultValue = pair.Value is string ? "\"\"" : pair.Value is IEnumerable<object> ? "new int[0]" : "0";
//				sb.Append($"{indent}  {pair.Key} = {defaultValue},\n");
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
//				string defaultValue = array[i] is string ? "\"\"" : array[i] is IEnumerable<object> ? "new int[0]" : "0";
//				sb.Append($"{indent}  {defaultValue},\n");
//			}
//			if (array.Length > 0)
//			{
//				sb.Length -= 2; // Remove last comma
//				sb.Append("\n");
//			}
//			sb.Append($"{indent}}}");
//			return sb.ToString();
//		}

//		// Handle primitives
//		if (data is string)
//		{
//			return "\"\"";
//		}
//		return "0";
//	}

//	// Generate anonymous type with deserialized values
//	private string GenerateAnonymousResult(object data, int indentLevel = 0)
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
//				string value = GenerateValueExpression(pair.Key, pair.Value, indentLevel + 1);
//				sb.Append($"{indent}  {pair.Key} = {value},\n");
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
//				sb.Append($"{indent}  {GenerateValueExpression($"[{i}]", array[i], indentLevel + 1)},\n");
//			}
//			if (array.Length > 0)
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

//	// Generate value expression for dictionary access
//	private string GenerateValueExpression(string key, object value, int indentLevel)
//	{
//		if (value is Dictionary<string, object>)
//		{
//			return GenerateAnonymousResult(value, indentLevel);
//		}
//		if (value is object[] array)
//		{
//			return GenerateAnonymousResult(value, indentLevel);
//		}
//		if (value is string)
//		{
//			return $"data[\"{key}\"].ToString()";
//		}
//		if (value is double || value is float)
//		{
//			return $"(float)data[\"{key}\"]";
//		}
//		if (value is long || value is int)
//		{
//			return $"(int)data[\"{key}\"]";
//		}
//		if (value is bool)
//		{
//			return $"(bool)data[\"{key}\"]";
//		}
//		return $"data[\"{key}\"]";
//	}
//}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Text;

//public class JsonToCsTest : MonoBehaviour
//{
//	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
//        ""name"": ""John"",
//        ""age"": 30,
//        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
//    }";
//	[SerializeField, TextArea(5, 20), Header("C# Deserialization Code (Read-Only)")] private string cSharpDeserializationCode = "";

//	private Dictionary<string, object> data; // Runtime data, not Inspector-visible

//	void Awake()
//	{
//		// Deserialize to a dictionary
//		data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInput);
//		if (data != null)
//		{
//			// Demonstrate dynamic access
//			Debug.Log($"Name: {data.GetValueOrDefault("name")}"); // John
//			if (data.GetValueOrDefault("address") is Dictionary<string, object> address)
//			{
//				Debug.Log($"City: {address.GetValueOrDefault("city")}"); // New York
//			}

//			// Generate deserialization code
//			cSharpDeserializationCode = GenerateDeserializationCode(data);
//		}
//		else
//		{
//			Debug.LogError("Failed to deserialize JSON");
//			cSharpDeserializationCode = "Error: Failed to deserialize JSON";
//		}

//		// Serialize back
//		string serialized = JsonTocs.ToJson(data);
//		Debug.Log($"Serialized: {serialized}");
//	}

//	// Generate C# code snippet for deserialization and access
//	private string GenerateDeserializationCode(Dictionary<string, object> data)
//	{
//		var sb = new StringBuilder();
//		sb.Append("var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);\n");
//		sb.Append("if (data != null)\n");
//		sb.Append("{\n");
//		sb.Append("  // Example anonymous type template\n");
//		sb.Append($"  var template = {GenerateAnonymousTemplate(data, 1)};\n");
//		sb.Append("\n");
//		sb.Append("  // Dynamic access example\n");
//		sb.Append($"  Debug.Log($\"Name: {{data[\"name\"]}}\"); // {data.GetValueOrDefault("name")}\n");
//		if (data.GetValueOrDefault("address") is Dictionary<string, object> address)
//		{
//			sb.Append($"  Debug.Log($\"City: {{(data[\"address\"] as Dictionary<string, object>)[\"city\"]}}\"); // {address.GetValueOrDefault("city")}\n");
//		}
//		sb.Append("}\n");
//		sb.Append("else\n");
//		sb.Append("{\n");
//		sb.Append("  Debug.LogError(\"Failed to deserialize JSON\");\n");
//		sb.Append("}");
//		return sb.ToString();
//	}

//	// Generate anonymous type template
//	private string GenerateAnonymousTemplate(object data, int indentLevel = 0)
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
//				string value = GenerateAnonymousTemplate(pair.Value, indentLevel + 1);
//				sb.Append($"{indent}  {pair.Key} = {value},\n");
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
//				sb.Append($"{indent}  {GenerateAnonymousTemplate(array[i], indentLevel + 1)},\n");
//			}
//			if (array.Length > 0)
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

//using UnityEngine;
//using System.Collections.Generic;
//using System.Text;

//public class JsonToCsTest : MonoBehaviour
//{
//	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
//        ""name"": ""John"",
//        ""age"": 30,
//        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
//    }";
//	[SerializeField, TextArea(5, 20), Header("C# Anonymous Type (Read-Only)")] private string cSharpRepresentation = "";

//	private Dictionary<string, object> data; // Runtime data, not Inspector-visible

//	void Awake()
//	{
//		// Deserialize to a dictionary
//		data = JsonTocs.FromJson<Dictionary<string, object>>(jsonInput);
//		if (data != null)
//		{
//			// Demonstrate dynamic access
//			Debug.Log($"Name: {data.GetValueOrDefault("name")}"); // John
//			if (data.GetValueOrDefault("address") is Dictionary<string, object> address)
//			{
//				Debug.Log($"City: {address.GetValueOrDefault("city")}"); // New York
//			}

//			// Generate anonymous type display
//			cSharpRepresentation = MarkupAsCS(data);
//		}
//		else
//		{
//			Debug.LogError("Failed to deserialize JSON");
//			cSharpRepresentation = "Error: Failed to deserialize JSON";
//		}

//		// Serialize back
//		string serialized = JsonTocs.ToJson(data);
//		Debug.Log($"Serialized: {serialized}");
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

//		// Handle primitives
//		if (data is string str)
//		{
//			return $"\"{str}\"";
//		}
//		return data.ToString();
//	}
//}

//using UnityEngine;

//public class JsonToCsTest : MonoBehaviour
//{
//	[System.Serializable]
//	public class User
//	{
//		public string Name { get; set; }
//		public int Age { get; set; }
//		public Address Address { get; set; }
//	}

//	[System.Serializable]
//	public class Address
//	{
//		public string Street { get; set; }
//		public string City { get; set; }
//	}

//	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
//        ""name"": ""John"",
//        ""age"": 30,
//        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
//    }";
//	[SerializeField] private User user; // Displayed in Inspector

//	void Awake()
//	{
//		// Deserialize to a typed object
//		user = JsonTocs.FromJson<User>(jsonInput);
//		if (user != null)
//		{
//			Debug.Log($"Name: {user.Name}"); // John
//			Debug.Log($"City: {user.Address?.City}"); // New York
//		}
//		else
//		{
//			Debug.LogError("Failed to deserialize JSON");
//		}

//		// Serialize back
//		string serialized = JsonTocs.ToJson(user);
//		Debug.Log($"Serialized: {serialized}");
//	}
//}