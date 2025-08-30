using System.Text;
using System.Linq;
using System.Collections.Generic;

public static class jsontocs_usagegenerator
{
	public static string GenerateDeserializationCode(object data, string jsonString)
	{
		if (data is Dictionary<string, object> dict)
		{
			return GenerateDeserializationCodeForDictionary(dict, jsonString);
		}
		else if (data is List<object> list)
		{
			return GenerateDeserializationCodeForArray(list, jsonString);
		}
		else if (data is object[] array)
		{
			return GenerateDeserializationCodeForArray(array.ToList(), jsonString);
		}
		else
		{
			return $"Error: Unsupported JSON root type - {data?.GetType().Name ?? "null"}";
		}
	}

	private static string GenerateDeserializationCodeForDictionary(Dictionary<string, object> data, string jsonString)
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
					bool isNestedNullable = nestedPair.Value is string || nestedPair.Value is object[] || nestedPair.Value is Dictionary<string, object> || nestedPair.Value == null;
					string nestedNullCheck = isNestedNullable ? $" && result.{pair.Key}.{nestedPair.Key} != null" : "";
					sb.Append($"                if (result.{pair.Key} != null{nestedNullCheck}) Debug.Log($\"{pair.Key}.{nestedPair.Key}: {{result.{pair.Key}.{nestedPair.Key}}}\"); // {nestedPair.Value}\n");
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
								bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
								string nullCheck = isNullableType ? $" && result.{pair.Key}[{i}].{key} != null" : "";
								if (value is object[] subArray)
								{
									sb.Append($"                if (result.{pair.Key}.Length > {i}{nullCheck}) Debug.Log($\"{pair.Key}[{i}].{key}: {{string.Join(\", \", result.{pair.Key}[{i}].{key})}}\"); // {JsonTocs.ToJson(value)}\n");
								}
								else if (value is Dictionary<string, object> detailsDict)
								{
									foreach (var detailPair in detailsDict)
									{
										bool isDetailNullable = detailPair.Value is string || detailPair.Value is object[] || detailPair.Value is Dictionary<string, object> || detailPair.Value == null;
										string detailNullCheck = isDetailNullable ? $" && result.{pair.Key}[{i}].{key}.{detailPair.Key} != null" : "";
										sb.Append($"                if (result.{pair.Key}.Length > {i}{nullCheck}{detailNullCheck}) Debug.Log($\"{pair.Key}[{i}].{key}.{detailPair.Key}: {{result.{pair.Key}[{i}].{key}.{detailPair.Key}}}\"); // {detailPair.Value}\n");
									}
								}
								else
								{
									sb.Append($"                if (result.{pair.Key}.Length > {i}{nullCheck}) Debug.Log($\"{pair.Key}[{i}].{key}: {{result.{pair.Key}[{i}].{key}}}\"); // {value ?? "null"}\n");
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
				bool isNullableType = pair.Value is string || pair.Value is object[] || pair.Value is Dictionary<string, object> || pair.Value == null;
				string nullCheck = isNullableType ? $" && result.{pair.Key} != null" : "";
				sb.Append($"                if (true{nullCheck}) Debug.Log($\"{pair.Key}: {{result.{pair.Key}}}\"); // {pair.Value ?? "null"}\n");
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

	private static string GenerateDeserializationCodeForArray(List<object> data, string jsonString)
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
							bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
							string nullCheck = isNullableType ? $" && result[{i}].{key} != null" : "";
							if (value is object[] subArray)
							{
								sb.Append($"                if (result.Length > {i}{nullCheck}) Debug.Log($\"[{i}].{key}: {{string.Join(\", \", result[{i}].{key})}}\"); // {JsonTocs.ToJson(value)}\n");
							}
							else if (value is Dictionary<string, object> nestedDict)
							{
								foreach (var nestedPair in nestedDict)
								{
									bool isNestedNullable = nestedPair.Value is string || nestedPair.Value is object[] || nestedPair.Value is Dictionary<string, object> || nestedPair.Value == null;
									string nestedNullCheck = isNestedNullable ? $" && result[{i}].{key}.{nestedPair.Key} != null" : "";
									sb.Append($"                if (result.Length > {i}{nullCheck}{nestedNullCheck}) Debug.Log($\"[{i}].{key}.{nestedPair.Key}: {{result[{i}].{key}.{nestedPair.Key}}}\"); // {nestedPair.Value}\n");
								}
							}
							else
							{
								sb.Append($"                if (result.Length > {i}{nullCheck}) Debug.Log($\"[{i}].{key}: {{result[{i}].{key}}}\"); // {value ?? "null"}\n");
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
		else
		{
			sb.Append("                var result = data.ToArray();\n");
			sb.Append("                // Display deserialized values\n");
			for (int i = 0; i < data.Count; i++)
			{
				sb.Append($"                if (result.Length > {i}) Debug.Log($\"[{i}]: {{result[{i}]}}\"); // {JsonTocs.ToJson(data[i])}\n");
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

	private static HashSet<string> GetAllDictionaryKeys(IEnumerable<Dictionary<string, object>> dicts)
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

	private static Dictionary<string, object> CreateTemplateDictionary(IEnumerable<Dictionary<string, object>> dicts)
	{
		var templateDict = new Dictionary<string, object>();
		foreach (var dict in dicts)
		{
			foreach (var kvp in dict)
			{
				if (!templateDict.ContainsKey(kvp.Key))
				{
					templateDict[kvp.Key] = kvp.Value;
				}
				else if (kvp.Value != null && templateDict[kvp.Key] == null)
				{
					templateDict[kvp.Key] = kvp.Value;
				}
				else if (kvp.Value is Dictionary<string, object> newDict && templateDict[kvp.Key] is Dictionary<string, object> existingDict)
				{
					var mergedDict = new Dictionary<string, object>(existingDict);
					foreach (var nestedKvp in newDict)
					{
						if (!mergedDict.ContainsKey(nestedKvp.Key))
						{
							mergedDict[nestedKvp.Key] = nestedKvp.Value;
						}
					}
					templateDict[kvp.Key] = mergedDict;
				}
				else if (kvp.Value is object[] newArray && templateDict[kvp.Key] is object[] existingArray)
				{
					if (newArray.Length > 0 && existingArray.Length == 0)
					{
						templateDict[kvp.Key] = newArray;
					}
				}
			}
		}
		return templateDict;
	}

	private static string GenerateAnonymousTemplate(object data, int indentLevel = 0)
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

	private static string GenerateAnonymousResult(object data, int indentLevel = 0, string dictName = "data")
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

	private static string GenerateNestedDictionaryResult(Dictionary<string, object> nestedDict, string parentKey, int indentLevel, string dictName)
	{
		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);
		sb.Append($"{indent}({dictName}.ContainsKey(\"{parentKey}\") ? new\n{indent}{{\n");
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
		sb.Append($"\n{indent}}} : {GenerateAnonymousTemplate(nestedDict, indentLevel)})");
		return sb.ToString();
	}

	private static string GenerateArrayResult(string key, IEnumerable<object> arr, int indentLevel, string dictName)
	{
		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);
		var items = arr.ToList();
		if (items.FirstOrDefault() is Dictionary<string, object> firstDict)
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

	private static string EscapeJsonString(string json)
	{
		return json.Replace("\"", "\"\"");
	}
}