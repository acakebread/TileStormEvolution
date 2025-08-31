using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using UnityEngine;

public static class jsontocs_usagegenerator
{
	private static string SanitizeKey(string key)
	{
		if (string.IsNullOrEmpty(key)) return "_";
		string sanitized = Regex.Replace(key, @"[^a-zA-Z0-9_]", "_");
		if (char.IsDigit(sanitized[0]))
		{
			sanitized = "_" + sanitized;
		}
		return sanitized;
	}

	private static string EscapeCommentValue(object value)
	{
		if (value == null) return "null";
		string stringValue = JsonTocs.ToJson(value);
		stringValue = Regex.Replace(stringValue, @"[\n\r\t{}]{}", m => m.Value switch
		{
			"\n" => "\\n",
			"\r" => "\\r",
			"\t" => "\\t",
			"{" => "{",
			"}" => "}",
			_ => m.Value
		});
		const int maxLength = 100;
		if (stringValue.Length > maxLength)
		{
			stringValue = stringValue.Substring(0, maxLength - 3) + "...";
		}
		return stringValue;
	}

	private static string EscapeJsonString(string json)
	{
		if (string.IsNullOrEmpty(json)) return "";
		return json.Replace("\"", "\"\"").Replace("\r\n", "\n");
	}

	private static string EscapeInterpolatedString(string input)
	{
		return input.Replace("{", "{{").Replace("}", "}}");
	}

	public static string GenerateDeserializationCode(object data, string jsonString)
	{
		try
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
		catch (Exception e)
		{
			return $"Error: Failed to generate deserialization code - {e.Message}";
		}
	}

	private static string GenerateDeserializationCodeForDictionary(Dictionary<string, object> data, string jsonString)
	{
		var sb = new StringBuilder();
		sb.Append("using System;\n");
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

		var keyMapping = CreateKeyMapping(data.Keys);
		sb.Append($"                var template = {GenerateAnonymousTemplate(data, keyMapping, 4)};\n");
		sb.Append("\n");
		sb.Append($"                var result = {GenerateAnonymousResult(data, keyMapping, 4)};\n");
		sb.Append("\n");
		sb.Append("                // Display deserialized values\n");

		foreach (var pair in data.OrderBy(p => p.Key))
		{
			if (pair.Value is object[] array1 && array1.Any() && array1[0] is Dictionary<string, object>)
			{
				var subDicts = array1.OfType<Dictionary<string, object>>().ToList();
				var subKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(subDicts));
				for (int i = 0; i < array1.Length; i++)
				{
					if (array1[i] is Dictionary<string, object> itemDict)
					{
						var itemKeyMapping = CreateKeyMapping(itemDict.Keys);
						foreach (var subKey in itemKeyMapping.OrderBy(kv => kv.Key))
						{
							if (itemDict.ContainsKey(subKey.Key))
							{
								var value = itemDict[subKey.Key];
								bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
								string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)} != null" : "";
								string path = EscapeInterpolatedString($"{pair.Key}[{i}].{subKey.Key}");
								string valueAccess = $"result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}";
								if (value is object[] subArray)
								{
									sb.Append($"                if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", {valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
								}
								else
								{
									sb.Append($"                if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
								}
							}
						}
					}
					if (array1[i] is Dictionary<string, object> nestedDict && nestedDict.ContainsKey("Items") && nestedDict["Items"] is object[] itemsArray && itemsArray.Any() && itemsArray[0] is Dictionary<string, object>)
					{
						var nestedSubDicts = itemsArray.OfType<Dictionary<string, object>>().ToList();
						var nestedSubKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(nestedSubDicts));
						for (int j = 0; j < itemsArray.Length; j++)
						{
							if (itemsArray[j] is Dictionary<string, object> nestedItemDict)
							{
								var nestedItemKeyMapping = CreateKeyMapping(nestedItemDict.Keys);
								foreach (var subKey in nestedItemKeyMapping.OrderBy(kv => kv.Key))
								{
									if (nestedItemDict.ContainsKey(subKey.Key))
									{
										var value = nestedItemDict[subKey.Key];
										bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
										string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)}[{i}].Items[{j}].{SanitizeKey(subKey.Key)} != null" : "";
										string path = EscapeInterpolatedString($"{pair.Key}[{i}].Items[{j}].{subKey.Key}");
										string valueAccess = $"result.{SanitizeKey(pair.Key)}[{i}].Items[{j}].{SanitizeKey(subKey.Key)}";
										if (value is object[] subArray)
										{
											sb.Append($"                if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i} && result.{SanitizeKey(pair.Key)}[{i}].Items != null && result.{SanitizeKey(pair.Key)}[{i}].Items.Length > {j}{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", {valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
										}
										else
										{
											sb.Append($"                if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i} && result.{SanitizeKey(pair.Key)}[{i}].Items != null && result.{SanitizeKey(pair.Key)}[{i}].Items.Length > {j}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
										}
									}
								}
							}
						}
					}
				}
			}
			else if (pair.Value is Dictionary<string, object> nestedDict)
			{
				var nestedKeyMapping = CreateKeyMapping(nestedDict.Keys);
				foreach (var subKey in nestedKeyMapping.OrderBy(kv => kv.Key))
				{
					var value = nestedDict[subKey.Key];
					bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
					string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)} != null" : "";
					string path = EscapeInterpolatedString($"{pair.Key}.{subKey.Key}");
					string valueAccess = $"result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}";
					if (value is object[] subArray)
					{
						sb.Append($"                if (result.{SanitizeKey(pair.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", {valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
					}
					else
					{
						sb.Append($"                if (result.{SanitizeKey(pair.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
					}
				}
			}
			else
			{
				bool isNullableType = pair.Value is string || pair.Value is object[] || pair.Value is Dictionary<string, object> || pair.Value == null;
				string nullCheck = isNullableType ? $"result.{SanitizeKey(pair.Key)} != null" : "true";
				string path = EscapeInterpolatedString(pair.Key);
				string valueAccess = $"result.{SanitizeKey(pair.Key)}";
				if (pair.Value is object[] array2)
				{
					sb.Append($"                if ({nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", {valueAccess})}}\"); // {EscapeCommentValue(pair.Value)}\n");
				}
				else
				{
					sb.Append($"                if ({nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(pair.Value)}\n");
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

	private static string GenerateDeserializationCodeForArray(List<object> data, string jsonString)
	{
		var sb = new StringBuilder();
		sb.Append("using System;\n");
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
			var allKeys = GetAllDictionaryKeys(dictList);
			var keyMapping = CreateKeyMapping(allKeys);
			var templateDict = CreateTemplateDictionary(dictList);
			sb.Append($"                var template = new[] {{ {GenerateAnonymousTemplate(templateDict, keyMapping, 4)} }};\n");
			sb.Append("\n");
			sb.Append($"                var result = ((List<object>)data).Select((item, i) =>\n");
			sb.Append("                {\n");
			sb.Append($"                    var subDict = (Dictionary<string, object>)item;\n");
			sb.Append($"                    return {GenerateAnonymousResult(templateDict, keyMapping, 5, "subDict")};\n");
			sb.Append("                }).ToArray();\n");
			sb.Append("\n");
			sb.Append("                // Display deserialized values\n");

			for (int i = 0; i < data.Count; i++)
			{
				if (data[i] is Dictionary<string, object> memberDict)
				{
					var itemKeyMapping = CreateKeyMapping(memberDict.Keys);
					foreach (var key in itemKeyMapping.OrderBy(kv => kv.Key))
					{
						if (memberDict.ContainsKey(key.Key))
						{
							var value = memberDict[key.Key];
							bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
							string nullCheck = isNullableType ? $" && result[{i}].{SanitizeKey(key.Key)} != null" : "";
							string path = EscapeInterpolatedString($"[{i}].{key.Key}");
							string valueAccess = $"result[{i}].{SanitizeKey(key.Key)}";
							if (value is object[] subArray)
							{
								sb.Append($"                if (result.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", {valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
							}
							else
							{
								sb.Append($"                if (result.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
							}
						}
					}
				}
			}
		}
		else
		{
			sb.Append("                var result = data.ToArray();\n");
			sb.Append("                // Display deserialized values\n");
			for (int i = 0; i < data.Count; i++)
			{
				string path = EscapeInterpolatedString($"[{i}]");
				string valueAccess = $"result[{i}]";
				bool isNullableType = data[i] is string || data[i] is object[] || data[i] is Dictionary<string, object> || data[i] == null;
				string nullCheck = isNullableType ? $" && result[{i}] != null" : "";
				sb.Append($"                if (result.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(data[i])}\n");
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
		var allKeys = GetAllDictionaryKeys(dicts);
		foreach (var key in allKeys.OrderBy(k => k))
		{
			var values = dicts.Where(d => d.ContainsKey(key)).Select(d => d[key]).ToList();
			if (values.Any())
			{
				var firstNonNull = values.FirstOrDefault(v => v != null);
				if (firstNonNull is Dictionary<string, object> nestedDict)
				{
					templateDict[key] = CreateTemplateDictionary(new[] { nestedDict });
				}
				else if (firstNonNull is object[] array && array.Any() && array[0] is Dictionary<string, object> firstDict)
				{
					var subDicts = array.OfType<Dictionary<string, object>>().ToList();
					if (subDicts.Any())
					{
						var subTemplate = CreateTemplateDictionary(subDicts);
						templateDict[key] = new[] { subTemplate };
					}
					else
					{
						templateDict[key] = new object[0];
					}
				}
				else
				{
					templateDict[key] = null;
				}
			}
			else
			{
				templateDict[key] = null;
			}
		}
		return templateDict;
	}

	private static Dictionary<string, string> CreateKeyMapping(IEnumerable<string> keys)
	{
		var mapping = new Dictionary<string, string>();
		foreach (var key in keys.OrderBy(k => k))
		{
			mapping[key] = SanitizeKey(key);
		}
		return mapping;
	}

	private static string GenerateAnonymousTemplate(object data, Dictionary<string, string> keyMapping, int indentLevel = 0)
	{
		if (data == null) return "(object)null";

		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);

		if (data is Dictionary<string, object> dict)
		{
			sb.Append($"{indent}new\n{indent}{{\n");
			foreach (var key in keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
			{
				string defaultValue = dict.ContainsKey(key) && dict[key] != null ? dict[key] switch
				{
					Dictionary<string, object> nestedDict => GenerateAnonymousTemplate(nestedDict, CreateKeyMapping(nestedDict.Keys), indentLevel + 1),
					object[] arr => arr.Any() && arr[0] is Dictionary<string, object> firstDict ? $"new[] {{ {GenerateAnonymousTemplate(firstDict, CreateKeyMapping(firstDict.Keys), indentLevel + 1)} }}" : "new object[0]",
					_ => "(object)null"
				} : "(object)null";
				sb.Append($"{indent}  {keyMapping[key]} = {defaultValue},\n");
			}
			if (keyMapping.Count > 0) sb.Length -= 2;
			sb.Append("\n" + indent + "}");
			return sb.ToString();
		}

		return "(object)null";
	}

	private static string GenerateAnonymousResult(object data, Dictionary<string, string> keyMapping, int indentLevel = 0, string dictName = "data")
	{
		if (data == null) return "(object)null";

		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);

		if (data is Dictionary<string, object> dict)
		{
			sb.Append($"{indent}new\n{indent}{{\n");
			foreach (var key in keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
			{
				string valueExpr = dict.ContainsKey(key) && dict[key] != null ? dict[key] switch
				{
					Dictionary<string, object> nestedDict => GenerateNestedDictionaryResult(nestedDict, key, CreateKeyMapping(nestedDict.Keys), indentLevel + 1, dictName),
					object[] arr => GenerateArrayResult(key, arr, CreateKeyMapping(GetAllDictionaryKeys(arr.OfType<Dictionary<string, object>>())), indentLevel + 1, dictName, dict),
					_ => $"{dictName}.ContainsKey(\"{key}\") ? {dictName}[\"{key}\"] : (object)null"
				} : $"{dictName}.ContainsKey(\"{key}\") ? {dictName}[\"{key}\"] : (object)null";
				sb.Append($"{indent}  {keyMapping[key]} = {valueExpr},\n");
			}
			if (keyMapping.Count > 0) sb.Length -= 2;
			sb.Append("\n" + indent + "}");
			return sb.ToString();
		}

		return $"{dictName} ?? (object)null";
	}

	private static string GenerateNestedDictionaryResult(Dictionary<string, object> nestedDict, string parentKey, Dictionary<string, string> keyMapping, int indentLevel, string dictName)
	{
		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);
		sb.Append($"{indent}({dictName}.ContainsKey(\"{parentKey}\") ? new\n{indent}{{\n");
		foreach (var key in keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
		{
			string valueExpr = nestedDict.ContainsKey(key) ? $"{dictName}.ContainsKey(\"{parentKey}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"]).ContainsKey(\"{key}\") ? ((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{key}\"] : (object)null" : "(object)null";
			sb.Append($"{indent}  {keyMapping[key]} = {valueExpr},\n");
		}
		if (keyMapping.Count > 0) sb.Length -= 2;
		sb.Append($"\n{indent}}} : new\n{indent}{{\n");
		foreach (var key in keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
		{
			string defaultValue = nestedDict.ContainsKey(key) && nestedDict[key] != null ? nestedDict[key] switch
			{
				Dictionary<string, object> => GenerateAnonymousTemplate(nestedDict[key], CreateKeyMapping(((Dictionary<string, object>)nestedDict[key]).Keys), indentLevel + 1),
				object[] arr => arr.Any() && arr[0] is Dictionary<string, object> firstDict ? $"new[] {{ {GenerateAnonymousTemplate(firstDict, CreateKeyMapping(firstDict.Keys), indentLevel + 1)} }}" : "new object[0]",
				_ => "(object)null"
			} : "(object)null";
			sb.Append($"{indent}  {keyMapping[key]} = {defaultValue},\n");
		}
		if (keyMapping.Count > 0) sb.Length -= 2;
		sb.Append($"\n{indent}}})");
		return sb.ToString();
	}

	private static string GenerateArrayResult(string key, IEnumerable<object> arr, Dictionary<string, string> keyMapping, int indentLevel, string dictName, Dictionary<string, object> parentDict)
	{
		var sb = new StringBuilder();
		string indent = new string(' ', indentLevel * 2);
		var items = arr.ToList();
		if (items.FirstOrDefault() is Dictionary<string, object> firstDict)
		{
			var dictList = items.OfType<Dictionary<string, object>>().ToList();
			var allKeys = keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key).ToList();
			var itemTemplate = firstDict != null ? GenerateAnonymousTemplate(firstDict, keyMapping, indentLevel + 2) : $"new {{ {string.Join(", ", allKeys.Select(k => $"{keyMapping[k]} = (object)null"))} }}";
			sb.Append($"{indent}({dictName}.ContainsKey(\"{key}\") ? ((object[]){dictName}[\"{key}\"]).Select((item, j) =>\n");
			sb.Append(indent + "{\n");
			sb.Append($"{indent}  var subDict = (Dictionary<string, object>)item;\n");
			sb.Append($"{indent}  return new\n{indent}  {{\n");
			foreach (var subKey in allKeys)
			{
				string valueExpr = $"subDict.ContainsKey(\"{subKey}\") ? subDict[\"{subKey}\"] : (object)null";
				sb.Append($"{indent}    {keyMapping[subKey]} = {valueExpr},\n");
			}
			if (allKeys.Count > 0) sb.Length -= 2;
			sb.Append($"\n{indent}  }};\n");
			sb.Append(indent + "}).ToArray() : new[] { " + itemTemplate + " })");
		}
		else
		{
			sb.Append($"{indent}({dictName}.ContainsKey(\"{key}\") ? ((object[]){dictName}[\"{key}\"]).Select(item => item).ToArray() : new object[0])");
		}
		return sb.ToString();
	}
}