using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class JsonInspectorUtility : MonoBehaviour
{
	[SerializeField, TextArea(5, 20), Header("C# Deserialization Code (Read-Only)")]
	private string cSharpRepresentation = "";
	public string CSharpRepresentation => cSharpRepresentation;
	private string jsonInputInternal = "{}";
	public object Data { get; private set; }

	public void SetJsonInput(string json)
	{
		jsonInputInternal = json;
		OnValidate();
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
			Data = JsonTocs.FromJson<object>(jsonInputInternal);
			if (Data != null)
			{
				if (Data is Dictionary<string, object> dict)
				{
					cSharpRepresentation = GenerateDeserializationCodeForDictionary(dict, jsonInputInternal);
				}
				else if (Data is List<object> list)
				{
					cSharpRepresentation = GenerateDeserializationCodeForArray(list, jsonInputInternal);
				}
				else
				{
					cSharpRepresentation = $"Error: Unsupported JSON root type - {Data.GetType().Name}";
				}
			}
			else
			{
				cSharpRepresentation = "Error: Failed to deserialize JSON - null result";
			}
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

	private string GenerateDeserializationCodeForDictionary(Dictionary<string, object> data, string jsonString)
	{
		var sb = new StringBuilder();
		sb.Append("using System.Collections.Generic;\n");
		sb.Append("using UnityEngine;\n");
		sb.Append("using System.Linq;\n");
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

		sb.Append($"                var template = {GenerateAnonymousTemplate(data, 4)};\n");
		sb.Append("\n");
		sb.Append($"                var result = {GenerateAnonymousResult(data, 4)};\n");
		sb.Append("\n");
		sb.Append("                // Display deserialized values\n");

		foreach (var pair in data)
		{
			if (pair.Value is Dictionary<string, object> nestedDict)
			{
				foreach (var nestedPair in nestedDict)
				{
					sb.Append($"                Debug.Log($\"{pair.Key}.{nestedPair.Key}: {{result.{pair.Key}.{nestedPair.Key}}}\"); // {nestedPair.Value}\n");
				}
			}
			else if (pair.Value is object[] array)
			{
				var allKeys = GetAllDictionaryKeys(array.OfType<Dictionary<string, object>>());
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i] is Dictionary<string, object> memberDict)
					{
						foreach (var key in allKeys)
						{
							if (memberDict.ContainsKey(key))
							{
								var value = memberDict[key];
								if (value is object[] subArray)
								{
									sb.Append($"                if (result.{pair.Key}.Length > {i} && result.{pair.Key}[{i}].{key} != null) Debug.Log($\"{pair.Key}[{i}].{key}: {{string.Join(\", \", result.{pair.Key}[{i}].{key})}}\"); // {JsonTocs.ToJson(value)}\n");
								}
								else if (value is Dictionary<string, object> detailsDict)
								{
									foreach (var detailPair in detailsDict)
									{
										sb.Append($"                if (result.{pair.Key}.Length > {i} && result.{pair.Key}[{i}].{key} != null) Debug.Log($\"{pair.Key}[{i}].{key}.{detailPair.Key}: {{result.{pair.Key}[{i}].{key}.{detailPair.Key}}}\"); // {detailPair.Value}\n");
									}
								}
								else
								{
									sb.Append($"                if (result.{pair.Key}.Length > {i}) Debug.Log($\"{pair.Key}[{i}].{key}: {{result.{pair.Key}[{i}].{key}}}\"); // {value ?? "null"}\n");
								}
							}
						}
					}
					else
					{
						sb.Append($"                if (result.{pair.Key}.Length > {i}) Debug.Log($\"{pair.Key}[{i}]: {{result.{pair.Key}[{i}]}}\"); // {JsonTocs.ToJson(array[i])}\n");
					}
				}
			}
			else
			{
				sb.Append($"                Debug.Log($\"{pair.Key}: {{result.{pair.Key}}}\"); // {pair.Value ?? "null"}\n");
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

	private string GenerateDeserializationCodeForArray(List<object> data, string jsonString)
	{
		var sb = new StringBuilder();
		sb.Append("using System.Collections.Generic;\n");
		sb.Append("using UnityEngine;\n");
		sb.Append("using System.Linq;\n");
		sb.Append("\n");
		sb.Append("public class JsonDeserializationExample : MonoBehaviour\n");
		sb.Append("{\n");
		sb.Append("    void Start()\n");
		sb.Append("    {\n");
		sb.Append($"        string jsonString = @\"{EscapeJsonString(jsonString)}\";\n");
		sb.Append("        try\n");
		sb.Append("        {\n");
		sb.Append("            var data = JsonTocs.FromJson<List<object>>(jsonString);\n");
		sb.Append("            if (data != null)\n");
		sb.Append("            {\n");

		var dictList = data.OfType<Dictionary<string, object>>().ToList();
		if (dictList.Any())
		{
			var templateDict = CreateTemplateDictionary(dictList);
			sb.Append($"                var template = new[] {{ {GenerateAnonymousTemplate(templateDict, 4)} }};\n");
			sb.Append("\n");
			sb.Append($"                var result = ((List<object>)data).Select((item, i) =>\n");
			sb.Append("                {\n");
			sb.Append($"                    var dict = (Dictionary<string, object>)item;\n");
			sb.Append($"                    return {GenerateAnonymousResult(templateDict, 5, "dict")};\n");
			sb.Append("                }).ToArray();\n");
			sb.Append("\n");
			sb.Append("                // Display deserialized values\n");

			var allKeys = GetAllDictionaryKeys(dictList);
			for (int i = 0; i < data.Count; i++)
			{
				if (data[i] is Dictionary<string, object> memberDict)
				{
					foreach (var key in allKeys)
					{
						if (memberDict.ContainsKey(key))
						{
							var value = memberDict[key];
							if (value is object[] subArray)
							{
								sb.Append($"                if (result.Length > {i} && result[{i}].{key} != null) Debug.Log($\"[{i}].{key}: {{string.Join(\", \", result[{i}].{key})}}\"); // {JsonTocs.ToJson(value)}\n");
							}
							else if (value is Dictionary<string, object> nestedDict)
							{
								foreach (var nestedPair in nestedDict)
								{
									sb.Append($"                if (result.Length > {i} && result[{i}].{key} != null) Debug.Log($\"[{i}].{key}.{nestedPair.Key}: {{result[{i}].{key}.{nestedPair.Key}}}\"); // {nestedPair.Value}\n");
								}
							}
							else
							{
								sb.Append($"                if (result.Length > {i}) Debug.Log($\"[{i}].{key}: {{result[{i}].{key}}}\"); // {value ?? "null"}\n");
							}
						}
					}
				}
				else
				{
					sb.Append($"                if (result.Length > {i}) Debug.Log($\"[{i}]: {{result[{i}]}}\"); // {JsonTocs.ToJson(data[i])}\n");
				}
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

	private HashSet<string> GetAllDictionaryKeys(IEnumerable<Dictionary<string, object>> dicts)
	{
		var keys = new HashSet<string>();
		foreach (var dict in dicts)
		{
			foreach (var key in dict.Keys)
			{
				keys.Add(key);
			}
		}
		return keys;
	}

	private Dictionary<string, object> CreateTemplateDictionary(IEnumerable<Dictionary<string, object>> dicts)
	{
		var templateDict = new Dictionary<string, object>();
		foreach (var dict in dicts)
		{
			foreach (var kvp in dict)
			{
				if (!templateDict.ContainsKey(kvp.Key) || (kvp.Value is Dictionary<string, object> || kvp.Value is object[]))
				{
					templateDict[kvp.Key] = kvp.Value;
				}
				else if (templateDict.ContainsKey(kvp.Key) && templateDict[kvp.Key] == null)
				{
					templateDict[kvp.Key] = kvp.Value;
				}
			}
		}
		return templateDict;
	}

	private string GenerateAnonymousTemplate(object data, int indentLevel = 0)
	{
		if (data == null) return "(object)null";

		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);

		if (data is Dictionary<string, object> dict)
		{
			sb.Append($"{indent}new\n{indent}{{\n");
			foreach (var pair in dict)
			{
				string defaultValue = pair.Value switch
				{
					Dictionary<string, object> => GenerateAnonymousTemplate(pair.Value, indentLevel + 1),
					IEnumerable<object> arr => arr.Any() && arr.FirstOrDefault() is Dictionary<string, object> ? $"new[] {{ {GenerateAnonymousTemplate(arr.FirstOrDefault(), indentLevel + 1)} }}" : (arr.Any() && arr.FirstOrDefault() is string ? "new string[0]" : "new int[0]"),
					string => "\"\"",
					bool => "false",
					null => "(object)null",
					_ => "0"
				};
				sb.Append($"{indent}  {pair.Key} = {defaultValue},\n");
			}
			if (dict.Count > 0) sb.Length -= 2;
			sb.Append("\n" + indent + "}");
			return sb.ToString();
		}

		return data switch
		{
			string => "\"\"",
			bool => "false",
			null => "(object)null",
			_ => "0"
		};
	}

	private string GenerateAnonymousResult(object data, int indentLevel = 0, string dictName = "data")
	{
		if (data == null) return "(object)null";

		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);

		if (data is Dictionary<string, object> dict)
		{
			sb.Append($"{indent}new\n{indent}{{\n");
			foreach (var pair in dict)
			{
				string valueExpr = pair.Value switch
				{
					Dictionary<string, object> nestedDict => GenerateNestedDictionaryResult(nestedDict, pair.Key, indentLevel + 1, dictName),
					IEnumerable<object> arr => GenerateArrayResult(pair.Key, arr, indentLevel + 1, dictName),
					string => $"{dictName}.ContainsKey(\"{pair.Key}\") ? {dictName}[\"{pair.Key}\"].ToString() : \"\"",
					bool => $"{dictName}.ContainsKey(\"{pair.Key}\") ? (bool){dictName}[\"{pair.Key}\"] : false",
					long or int or double or float => $"{dictName}.ContainsKey(\"{pair.Key}\") ? System.Convert.ToInt32({dictName}[\"{pair.Key}\"]) : 0",
					null => "(object)null",
					_ => $"{dictName}.ContainsKey(\"{pair.Key}\") ? {dictName}[\"{pair.Key}\"] : (object)null"
				};
				sb.Append($"{indent}  {pair.Key} = {valueExpr},\n");
			}
			if (dict.Count > 0) sb.Length -= 2;
			sb.Append("\n" + indent + "}");
			return sb.ToString();
		}

		return data switch
		{
			string => $"\"{data}\"",
			bool => data.ToString().ToLower(),
			long or int or double or float => $"System.Convert.ToInt32({dictName})",
			null => "(object)null",
			_ => dictName
		};
	}

	private string GenerateNestedDictionaryResult(Dictionary<string, object> nestedDict, string parentKey, int indentLevel, string dictName)
	{
		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);
		sb.Append($"{indent}new\n{indent}{{\n");
		foreach (var pair in nestedDict)
		{
			string valueExpr = pair.Value switch
			{
				Dictionary<string, object> subDict => GenerateNestedDictionaryResult(subDict, $"{parentKey}.{pair.Key}", indentLevel + 1, dictName),
				IEnumerable<object> arr => GenerateArrayResult($"{parentKey}.{pair.Key}", arr, indentLevel + 1, dictName),
				string => $"{dictName}.ContainsKey(\"{parentKey}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"]).ContainsKey(\"{pair.Key}\") ? ((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{pair.Key}\"].ToString() : \"\"",
				bool => $"{dictName}.ContainsKey(\"{parentKey}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"]).ContainsKey(\"{pair.Key}\") ? (bool)((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{pair.Key}\"] : false",
				long or int or double or float => $"{dictName}.ContainsKey(\"{parentKey}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"]).ContainsKey(\"{pair.Key}\") ? System.Convert.ToInt32(((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{pair.Key}\"]) : 0",
				null => "(object)null",
				_ => $"{dictName}.ContainsKey(\"{parentKey}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"]).ContainsKey(\"{pair.Key}\") ? ((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{pair.Key}\"] : (object)null"
			};
			sb.Append($"{indent}  {pair.Key} = {valueExpr},\n");
		}
		if (nestedDict.Count > 0) sb.Length -= 2;
		sb.Append("\n" + indent + "}");
		return sb.ToString();
	}

	private string GenerateArrayResult(string key, IEnumerable<object> arr, int indentLevel, string dictName)
	{
		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);
		var items = arr.ToList();
		if (items.FirstOrDefault() is Dictionary<string, object>)
		{
			var dictList = items.OfType<Dictionary<string, object>>().ToList();
			var templateDict = CreateTemplateDictionary(dictList);
			sb.Append($"{indent}({dictName}.ContainsKey(\"{key}\") ? ((object[]){dictName}[\"{key}\"]).Select((item, i) =>\n");
			sb.Append(indent + "{\n");
			sb.Append($"{indent}  var subDict = (Dictionary<string, object>)item;\n");
			sb.Append($"{indent}  return new\n{indent}  {{\n");
			foreach (var pair in templateDict)
			{
				string valueExpr = pair.Value switch
				{
					Dictionary<string, object> nestedDict => GenerateNestedDictionaryResult(nestedDict, pair.Key, indentLevel + 1, "subDict"),
					IEnumerable<object> subArr => subArr.Any() && subArr.FirstOrDefault() is string ? $"subDict.ContainsKey(\"{pair.Key}\") ? ((object[])subDict[\"{pair.Key}\"]).Select(item => item.ToString()).ToArray() : new string[0]" : $"subDict.ContainsKey(\"{pair.Key}\") ? ((object[])subDict[\"{pair.Key}\"]).Select(item => System.Convert.ToInt32(item)).ToArray() : new int[0]",
					string => $"subDict.ContainsKey(\"{pair.Key}\") ? subDict[\"{pair.Key}\"].ToString() : \"\"",
					bool => $"subDict.ContainsKey(\"{pair.Key}\") ? (bool)subDict[\"{pair.Key}\"] : false",
					long or int or double or float => $"subDict.ContainsKey(\"{pair.Key}\") ? System.Convert.ToInt32(subDict[\"{pair.Key}\"]) : 0",
					null => "(object)null",
					_ => $"subDict.ContainsKey(\"{pair.Key}\") ? subDict[\"{pair.Key}\"] : (object)null"
				};
				sb.Append($"{indent}    {pair.Key} = {valueExpr},\n");
			}
			if (templateDict.Count > 0) sb.Length -= 2;
			sb.Append($"\n{indent}  }};\n");
			sb.Append(indent + "}).ToArray() : new[] { new { ");
			foreach (var pair in templateDict)
			{
				string defaultValue = pair.Value switch
				{
					Dictionary<string, object> nestedDict => GenerateAnonymousTemplate(nestedDict, 0),
					IEnumerable<object> subArr => subArr.Any() && subArr.FirstOrDefault() is string ? "new string[0]" : "new int[0]",
					string => "\"\"",
					bool => "false",
					null => "(object)null",
					_ => "0"
				};
				sb.Append($"{pair.Key} = {defaultValue}, ");
			}
			if (templateDict.Count > 0) sb.Length -= 2;
			sb.Append(" } })");
		}
		else
		{
			sb.Append($"{indent}({dictName}.ContainsKey(\"{key}\") ? ((object[]){dictName}[\"{key}\"]).Select(item => System.Convert.ToInt32(item)).ToArray() : new int[0])");
		}
		return sb.ToString();
	}

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
		JsonInspectorUtility utility = (JsonInspectorUtility)target;

		// JSON Input at the top
		EditorGUILayout.LabelField("JSON Input", EditorStyles.boldLabel);
		string jsonInput = EditorGUILayout.TextArea(utility.GetFieldValue<string>("jsonInputInternal"), GUILayout.Height(100));
		if (GUI.changed)
		{
			utility.SetJsonInput(jsonInput);
		}

		// C# Deserialization Code (Read-Only)
		EditorGUILayout.LabelField("C# Deserialization Code (Read-Only)", EditorStyles.boldLabel);
		EditorGUILayout.TextArea(utility.CSharpRepresentation, GUILayout.Height(200));

		// Copy Script button below the deserialization code
		if (GUILayout.Button("Copy Script"))
		{
			GUIUtility.systemCopyBuffer = utility.CSharpRepresentation;
			Debug.Log("Deserialization script copied to clipboard");
		}
	}
}

static class EditorExtensions
{
	public static T GetFieldValue<T>(this Object obj, string fieldName)
	{
		return (T)obj.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(obj);
	}
}
#endif