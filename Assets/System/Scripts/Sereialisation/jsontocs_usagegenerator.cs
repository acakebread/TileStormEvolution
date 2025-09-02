using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class jsontocs_usagegenerator
{
	private static string SanitizeKey(string key) => string.IsNullOrEmpty(key) ? "_" : "_" + Regex.Replace(key, @"[^a-zA-Z0-9_]", "_");

	private static string EscapeCommentValue(object value)
	{
		if (value == null) return "null";

		string result = value.ToString();

		// Handle different types specifically if needed
		if (value is object[] array)
		{
			result = $"[{string.Join(", ", array.Select(x => EscapeCommentValue(x)))}]";
		}
		else if (value is Dictionary<string, object> dict)
		{
			result = "{" + string.Join(", ", dict.Select(kv => $"{kv.Key}: {EscapeCommentValue(kv.Value)}")) + "}";
		}

		// Replace special characters with underscore
		return Regex.Replace(result, @"[\n\r\t\""\']+|\\.", "_");
	}

	private static string EscapeJsonString(string json) => string.IsNullOrEmpty(json) ? json : json.Replace("\"", "\"\"");

	private static string EscapeInterpolatedString(string input) => input.Replace("{", "{{").Replace("}", "}}");

	public static string GenerateDeserializationCode(object data, string jsonString)
	{
		try
		{
			if (data is Dictionary<string, object> dict)
			{
				return CodeFormatter.FormatCode(GenerateDeserializationCodeForDictionary(dict, jsonString));
			}
			else if (data is List<object> list)
			{
				return CodeFormatter.FormatCode(GenerateDeserializationCodeForArray(list, jsonString));
			}
			else if (data is object[] array)
			{
				return CodeFormatter.FormatCode(GenerateDeserializationCodeForArray(array.ToList(), jsonString));
			}
			else
			{
				return CodeFormatter.FormatCode($"Error: Unsupported JSON root type - {data?.GetType().Name ?? "null"}");
			}
		}
		catch (Exception e)
		{
			return CodeFormatter.FormatCode($"Error: Failed to generate deserialization code - {e.Message}");
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
		sb.Append("void Start()\n");
		sb.Append("{\n");
		sb.Append($"string jsonString = @\"{EscapeJsonString(jsonString)}\";\n"); //ToDo remove whitespace at end of jsonString before injecting it
		sb.Append("try\n");
		sb.Append("{\n");
		sb.Append("var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);\n");
		sb.Append("if (data != null)\n");
		sb.Append("{\n");

		var keyMapping = CreateKeyMapping(data.Keys);
		sb.Append($"var template = {GenerateAnonymousTemplate(data, keyMapping)};\n");
		sb.Append("\n");
		sb.Append($"var result = {GenerateAnonymousResult(data, keyMapping)};\n");
		sb.Append("\n");
		sb.Append("// Display deserialized values\n");

		foreach (var pair in data.OrderBy(p => p.Key))
		{
			if (pair.Value is Dictionary<string, object> nestedDict)
			{
				var nestedKeyMapping = CreateKeyMapping(nestedDict.Keys);
				foreach (var subKey in nestedKeyMapping.OrderBy(kv => kv.Key))
				{
					var value = nestedDict[subKey.Key];
					bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
					string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)} != null" : "";
					string path = EscapeInterpolatedString($"{pair.Key}.{subKey.Key}");
					string valueAccess = $"result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}";
					if (value is object[] subArray && (subArray.Any() && subArray[0] is Dictionary<string, object> || !subArray.Any()))
					{
						var subDicts = subArray.OfType<Dictionary<string, object>>().ToList();
						var subNestedKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(subDicts));
						if (subDicts.Any())
						{
							for (int i = 0; i < subArray.Length; i++)
							{
								if (subArray[i] is Dictionary<string, object> itemDict)
								{
									foreach (var subNestedKey in subNestedKeyMapping.OrderBy(kv => kv.Key))
									{
										if (itemDict.ContainsKey(subNestedKey.Key))
										{
											var subValue = itemDict[subNestedKey.Key];
											bool isSubNullable = subValue is string || subValue is object[] || subValue is Dictionary<string, object> || subValue == null;
											string subNullCheck = isSubNullable ? $" && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}[{i}].{SanitizeKey(subNestedKey.Key)} != null" : "";
											string subPath = EscapeInterpolatedString($"{pair.Key}.{subKey.Key}[{i}].{subNestedKey.Key}");
											string subValueAccess = $"result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}[{i}].{SanitizeKey(subNestedKey.Key)}";
											if (subValue is object[] subSubArray)
											{
												sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)} != null && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}.Length > {i}{subNullCheck}) Debug.Log($\"{subPath} : {{string.Join(\", \", (object[]){subValueAccess})}}\"); // {EscapeCommentValue(subValue)}\n");
											}
											else
											{
												sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)} != null && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}.Length > {i}{subNullCheck}) Debug.Log($\"{subPath} : {{ {subValueAccess} }}\"); // {EscapeCommentValue(subValue)}\n");
											}
										}
									}
								}
							}
						}
						else
						{
							sb.Append($"if (result.{SanitizeKey(pair.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", (object[]){valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
						}
					}
					else if (value is Dictionary<string, object> subDict)
					{
						var subNestedKeyMapping = CreateKeyMapping(subDict.Keys);
						foreach (var subNestedKey in subNestedKeyMapping.OrderBy(kv => kv.Key))
						{
							var subValue = subDict[subNestedKey.Key];
							bool isSubNullable = subValue is string || subValue is object[] || subValue is Dictionary<string, object> || subValue == null;
							string subNullCheck = isSubNullable ? $" && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}.{SanitizeKey(subNestedKey.Key)} != null" : "";
							string subPath = EscapeInterpolatedString($"{pair.Key}.{subKey.Key}.{subNestedKey.Key}");
							string subValueAccess = $"result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)}.{SanitizeKey(subNestedKey.Key)}";
							if (subValue is object[] subSubArray)
							{
								sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)} != null{subNullCheck}) Debug.Log($\"{subPath} : {{string.Join(\", \", (object[]){subValueAccess})}}\"); // {EscapeCommentValue(subValue)}\n");
							}
							else
							{
								sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.{SanitizeKey(subKey.Key)} != null{subNullCheck}) Debug.Log($\"{subPath} : {{ {subValueAccess} }}\"); // {EscapeCommentValue(subValue)}\n");
							}
						}
					}
					else
					{
						sb.Append($"if (result.{SanitizeKey(pair.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
					}
				}
			}
			else if (pair.Value is object[] array1 && (array1.Any() && array1[0] is Dictionary<string, object> || !array1.Any()))
			{
				var subDicts = array1.OfType<Dictionary<string, object>>().ToList();
				var subKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(subDicts));
				for (int i = 0; i < array1.Length; i++)
				{
					if (array1[i] is Dictionary<string, object> itemDict)
					{
						foreach (var subKey in itemDict.OrderBy(kv => kv.Key))
						{
							if (itemDict[subKey.Key] is Dictionary<string, object> nestedDict2)
							{
								var nestedKeyMapping = CreateKeyMapping(nestedDict2.Keys);
								foreach (var nestedKey in nestedKeyMapping.OrderBy(kv => kv.Key))
								{
									if (nestedDict2.ContainsKey(nestedKey.Key))
									{
										var value = nestedDict2[nestedKey.Key];
										bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
										string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}.{SanitizeKey(nestedKey.Key)} != null" : "";
										string path = EscapeInterpolatedString($"{pair.Key}[{i}].{subKey.Key}.{nestedKey.Key}");
										string valueAccess = $"result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}.{SanitizeKey(nestedKey.Key)}";
										if (value is object[] subArray)
										{
											sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i} && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", (object[]){valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
										}
										else
										{
											sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i} && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
										}
									}
								}
							}
							else if (itemDict[subKey.Key] is object[] innerArray && (innerArray.Any() && innerArray[0] is Dictionary<string, object> || !innerArray.Any()))
							{
								var innerDicts = innerArray.OfType<Dictionary<string, object>>().ToList();
								var innerKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(innerDicts));
								for (int j = 0; j < innerArray.Length; j++)
								{
									if (innerArray[j] is Dictionary<string, object> innerDict)
									{
										foreach (var innerKey in innerDict.OrderBy(kv => kv.Key))
										{
											var value = innerDict[innerKey.Key];
											bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
											string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}[{j}].{SanitizeKey(innerKey.Key)} != null" : "";
											string path = EscapeInterpolatedString($"{pair.Key}[{i}].{subKey.Key}[{j}].{innerKey.Key}");
											string valueAccess = $"result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}[{j}].{SanitizeKey(innerKey.Key)}";
											if (value is object[] subArray)
											{
												sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i} && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)} != null && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}.Length > {j}{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", (object[]){valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
											}
											else
											{
												sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i} && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)} != null && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}.Length > {j}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
											}
										}
									}
								}
							}
							else
							{
								var value = itemDict[subKey.Key];
								bool isNullableType = value is string || value is object[] || value is Dictionary<string, object> || value == null;
								string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)} != null" : "";
								string path = EscapeInterpolatedString($"{pair.Key}[{i}].{subKey.Key}");
								string valueAccess = $"result.{SanitizeKey(pair.Key)}[{i}].{SanitizeKey(subKey.Key)}";
								if (value is object[] subArray)
								{
									sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", (object[]){valueAccess})}}\"); // {EscapeCommentValue(value)}\n");
								}
								else
								{
									sb.Append($"if (result.{SanitizeKey(pair.Key)} != null && result.{SanitizeKey(pair.Key)}.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
								}
							}
						}
					}
				}
			}
			else
			{
				bool isNullableType = pair.Value is string || pair.Value is object[] || pair.Value is Dictionary<string, object> || pair.Value == null;
				string nullCheck = isNullableType ? $" && result.{SanitizeKey(pair.Key)} != null" : "";
				string path = EscapeInterpolatedString(pair.Key);
				string valueAccess = $"result.{SanitizeKey(pair.Key)}";
				if (pair.Value is object[] array2)
				{
					sb.Append($"if (result.{SanitizeKey(pair.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{string.Join(\", \", (object[]){valueAccess})}}\"); // {EscapeCommentValue(pair.Value)}\n");
				}
				else
				{
					sb.Append($"if (result.{SanitizeKey(pair.Key)} != null{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(pair.Value)}\n");
				}
			}
		}

		sb.Append("}\n");
		sb.Append("else\n");
		sb.Append("{\n");
		sb.Append("Debug.LogError(\"Failed to deserialize JSON\");\n");
		sb.Append("}\n");
		sb.Append("}\n");
		sb.Append("catch (System.Exception e)\n");
		sb.Append("{\n");
		sb.Append("Debug.LogError($\"Deserialization error: {e.Message}\");\n");
		sb.Append("}\n");
		sb.Append("}\n");
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
		sb.Append("void Start()\n");
		sb.Append("{\n");
		sb.Append($"string jsonString = @\"{EscapeJsonString(jsonString)}\";\n");
		sb.Append("try\n");
		sb.Append("{\n");
		sb.Append("var data = JsonTocs.FromJson<List<object>>(jsonString);\n");
		sb.Append("if (data != null)\n");
		sb.Append("{\n");

		var dictList = data.OfType<Dictionary<string, object>>().ToList();
		if (dictList.Any())
		{
			var allKeys = GetAllDictionaryKeys(dictList);
			var keyMapping = CreateKeyMapping(allKeys);
			var templateDict = CreateTemplateDictionary(dictList);
			sb.Append($"var template = new[] {{ {GenerateAnonymousTemplate(templateDict, keyMapping)} }};\n");
			sb.Append("\n");
			sb.Append($"var result = ((List<object>)data).Select((item, i) =>\n");
			sb.Append("{\n");
			sb.Append($"var subDict = (Dictionary<string, object>)item;\n");
			sb.Append($"return {GenerateAnonymousResult(templateDict, keyMapping, 1, "subDict")};\n");
			sb.Append("}).ToArray();\n");
			sb.Append("\n");
			sb.Append("// Display deserialized values\n");

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
							if (value is object[] subArray && (subArray.Any() && subArray[0] is Dictionary<string, object> || !subArray.Any()))
							{
								var subDicts = subArray.OfType<Dictionary<string, object>>().ToList();
								var subKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(subDicts));
								for (int j = 0; j < subArray.Length; j++)
								{
									if (subArray[j] is Dictionary<string, object> subDict)
									{
										foreach (var subKey in subDict.OrderBy(kv => kv.Key))
										{
											var subValue = subDict[subKey.Key];
											bool isSubNullable = subValue is string || subValue is object[] || subValue is Dictionary<string, object> || subValue == null;
											string subNullCheck = isSubNullable ? $" && result[{i}].{SanitizeKey(key.Key)}[{j}].{SanitizeKey(subKey.Key)} != null" : "";
											string subPath = EscapeInterpolatedString($"[{i}].{key.Key}[{j}].{subKey.Key}");
											string subValueAccess = $"result[{i}].{SanitizeKey(key.Key)}[{j}].{SanitizeKey(subKey.Key)}";
											if (subValue is object[] subSubArray)
											{
												sb.Append($"if (result.Length > {i} && result[{i}].{SanitizeKey(key.Key)} != null && result[{i}].{SanitizeKey(key.Key)}.Length > {j}{subNullCheck}) Debug.Log($\"{subPath} : {{string.Join(\", \", (object[]){subValueAccess})}}\"); // {EscapeCommentValue(subValue)}\n");
											}
											else
											{
												sb.Append($"if (result.Length > {i} && result[{i}].{SanitizeKey(key.Key)} != null && result[{i}].{SanitizeKey(key.Key)}.Length > {j}{subNullCheck}) Debug.Log($\"{subPath} : {{ {subValueAccess} }}\"); // {EscapeCommentValue(subValue)}\n");
											}
										}
									}
								}
							}
							else if (value is Dictionary<string, object> nestedDict)
							{
								var nestedKeyMapping = CreateKeyMapping(nestedDict.Keys);
								foreach (var nestedKey in nestedKeyMapping.OrderBy(kv => kv.Key))
								{
									if (nestedDict.ContainsKey(nestedKey.Key))
									{
										var nestedValue = nestedDict[nestedKey.Key];
										bool isNestedNullable = nestedValue is string || nestedValue is object[] || nestedValue is Dictionary<string, object> || nestedValue == null;
										string nestedNullCheck = isNestedNullable ? $" && result[{i}].{SanitizeKey(key.Key)}.{SanitizeKey(nestedKey.Key)} != null" : "";
										string nestedPath = EscapeInterpolatedString($"[{i}].{key.Key}.{nestedKey.Key}");
										string nestedValueAccess = $"result[{i}].{SanitizeKey(key.Key)}.{SanitizeKey(nestedKey.Key)}";
										if (nestedValue is object[] nestedArray)
										{
											sb.Append($"if (result.Length > {i} && result[{i}].{SanitizeKey(key.Key)} != null{nestedNullCheck}) Debug.Log($\"{nestedPath} : {{string.Join(\", \", (object[]){nestedValueAccess})}}\"); // {EscapeCommentValue(nestedValue)}\n");
										}
										else
										{
											sb.Append($"if (result.Length > {i} && result[{i}].{SanitizeKey(key.Key)} != null{nestedNullCheck}) Debug.Log($\"{nestedPath} : {{ {nestedValueAccess} }}\"); // {EscapeCommentValue(nestedValue)}\n");
										}
									}
								}
							}
							else
							{
								sb.Append($"if (result.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(value)}\n");
							}
						}
					}
				}
				else
				{
					string path = EscapeInterpolatedString($"[{i}]");
					string valueAccess = $"result[{i}]";
					bool isNullableType = data[i] is string || data[i] is object[] || data[i] is Dictionary<string, object> || data[i] == null;
					string nullCheck = isNullableType ? $" && result[{i}] != null" : "";
					sb.Append($"if (result.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(data[i])}\n");
				}
			}
		}
		else
		{
			sb.Append("var result = data.ToArray();\n");
			sb.Append("// Display deserialized values\n");
			for (int i = 0; i < data.Count; i++)
			{
				string path = EscapeInterpolatedString($"[{i}]");
				string valueAccess = $"result[{i}]";
				bool isNullableType = data[i] is string || data[i] is object[] || data[i] is Dictionary<string, object> || data[i] == null;
				string nullCheck = isNullableType ? $" && result[{i}] != null" : "";
				sb.Append($"if (result.Length > {i}{nullCheck}) Debug.Log($\"{path} : {{ {valueAccess} }}\"); // {EscapeCommentValue(data[i])}\n");
			}
		}

		sb.Append("}\n");
		sb.Append("else\n");
		sb.Append("{\n");
		sb.Append("Debug.LogError(\"Failed to deserialize JSON\");\n");
		sb.Append("}\n");
		sb.Append("}\n");
		sb.Append("catch (System.Exception e)\n");
		sb.Append("{\n");
		sb.Append("Debug.LogError($\"Deserialization error: {e.Message}\");\n");
		sb.Append("}\n");
		sb.Append("}\n");
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
				else if (firstNonNull is object[] array && (array.Any() && array[0] is Dictionary<string, object> || !array.Any()))
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

	private static string GenerateAnonymousTemplate(object data, Dictionary<string, string> keyMapping)
	{
		if (data == null) return "(object)null";

		var sb = new StringBuilder();

		if (data is Dictionary<string, object> dict)
		{
			sb.Append("new {");
			if (keyMapping.Any())
			{
				sb.Append("\n");
				foreach (var key in keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
				{
					string defaultValue = dict.ContainsKey(key) && dict[key] != null ? dict[key] switch
					{
						Dictionary<string, object> nestedDict => GenerateAnonymousTemplate(nestedDict, CreateKeyMapping(nestedDict.Keys)),
						object[] arr => arr.Any() && arr[0] is Dictionary<string, object> firstDict
							? $"new[] {{ {GenerateAnonymousTemplate(firstDict, CreateKeyMapping(GetAllDictionaryKeys(arr.OfType<Dictionary<string, object>>())))} }}"
							: "new object[0]",
						_ => "(object)null"
					} : "(object)null";
					sb.Append($"{keyMapping[key]} = {defaultValue},\n");
				}
				if (keyMapping.Count > 0) sb.Length -= 2; // Remove trailing comma
				sb.Append("\n");
			}
			sb.Append("}");
			return sb.ToString();
		}

		return "(object)null";
	}

	private static string GenerateAnonymousResult(object data, Dictionary<string, string> keyMapping, int indentLevel = 0, string dictName = "data")
	{
		if (data == null) return "(object)null";

		var sb = new StringBuilder();

		if (data is Dictionary<string, object> dict)
		{
			sb.Append("new {");
			if (keyMapping.Any())
			{
				sb.Append("\n");
				foreach (var key in keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
				{
					string valueExpr = dict.ContainsKey(key) && dict[key] != null ? dict[key] switch
					{
						Dictionary<string, object> nestedDict => GenerateNestedDictionaryResult(nestedDict, key, CreateKeyMapping(nestedDict.Keys), indentLevel + 1, dictName),
						object[] arr => GenerateArrayResult(key, arr, CreateKeyMapping(GetAllDictionaryKeys(arr.OfType<Dictionary<string, object>>())), indentLevel + 1, dictName, dict),
						_ => $"{dictName}.ContainsKey(\"{key}\") ? {dictName}[\"{key}\"] : (object)null"
					} : $"{dictName}.ContainsKey(\"{key}\") ? {dictName}[\"{key}\"] : (object)null";
					sb.Append($"{keyMapping[key]} = {valueExpr},\n");
				}
				if (keyMapping.Count > 0) sb.Length -= 2;
				sb.Append("\n");
			}
			sb.Append("}");
			return sb.ToString();
		}

		return $"{dictName} ?? (object)null";
	}

	private static string GenerateNestedDictionaryResult(Dictionary<string, object> nestedDict, string parentKey, Dictionary<string, string> keyMapping, int indentLevel, string dictName)
	{
		var sb = new StringBuilder();
		var template = GenerateAnonymousTemplate(nestedDict, keyMapping);

		sb.Append($"({dictName}.ContainsKey(\"{parentKey}\") && {dictName}[\"{parentKey}\"] != null ? new {{");
		if (keyMapping.Any())
		{
			sb.Append("\n");
			foreach (var key in keyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
			{
				string valueExpr;
				if (nestedDict.ContainsKey(key) && nestedDict[key] is Dictionary<string, object> subDict)
				{
					var subKeyMapping = CreateKeyMapping(subDict.Keys);
					valueExpr = GenerateNestedDictionaryResult(subDict, key, subKeyMapping, indentLevel + 1, $"((Dictionary<string, object>){dictName}[\"{parentKey}\"])");
				}
				else if (nestedDict.ContainsKey(key) && nestedDict[key] is object[] arr && (arr.Any() && arr[0] is Dictionary<string, object> || !arr.Any()))
				{
					var dictList = arr.OfType<Dictionary<string, object>>().ToList();
					var innerKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(dictList));
					var itemTemplate = dictList.Any() ? GenerateAnonymousTemplate(CreateTemplateDictionary(dictList), innerKeyMapping) : "new { }";
					valueExpr = $"({dictName}.ContainsKey(\"{parentKey}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"]).ContainsKey(\"{key}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{key}\"] != null ? ((object[])((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{key}\"]).Select((item, j) =>\n";
					valueExpr += "{\n";
					valueExpr += $"var subDict = (Dictionary<string, object>)item;\n";
					valueExpr += $"return new {{";
					if (innerKeyMapping.Any())
					{
						valueExpr += "\n";
						foreach (var innerKey in innerKeyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
						{
							if (dictList.Any() && dictList.Any(d => d.ContainsKey(innerKey) && d[innerKey] is Dictionary<string, object>))
							{
								var nestedSubDict = dictList.First(d => d.ContainsKey(innerKey) && d[innerKey] is Dictionary<string, object>)[innerKey] as Dictionary<string, object>;
								var nestedInnerKeyMapping = CreateKeyMapping(nestedSubDict.Keys);
								valueExpr += $"{innerKeyMapping[innerKey]} = {GenerateNestedDictionaryResult(nestedSubDict, innerKey, nestedInnerKeyMapping, indentLevel + 2, "subDict")},\n";
							}
							else if (dictList.Any() && dictList.Any(d => d.ContainsKey(innerKey) && d[innerKey] is object[] innerArr && (innerArr.Any() && innerArr[0] is Dictionary<string, object> || !innerArr.Any())))
							{
								var innerArr = dictList.First(d => d.ContainsKey(innerKey) && d[innerKey] is object[])[innerKey] as object[];
								var innerDictList = innerArr.OfType<Dictionary<string, object>>().ToList();
								var innerInnerKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(innerDictList));
								var innerTemplate = innerDictList.Any() ? GenerateAnonymousTemplate(CreateTemplateDictionary(innerDictList), innerInnerKeyMapping) : "new { }";
								valueExpr += $"{innerKeyMapping[innerKey]} = subDict.ContainsKey(\"{innerKey}\") && subDict[\"{innerKey}\"] != null ? ((object[])subDict[\"{innerKey}\"]).Select((innerItem, k) =>\n";
								valueExpr += "{\n";
								valueExpr += $"var innerDict = (Dictionary<string, object>)innerItem;\n";
								valueExpr += $"return new {{";
								if (innerInnerKeyMapping.Any())
								{
									valueExpr += "\n";
									foreach (var nestedKey in innerInnerKeyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
									{
										if (innerDictList.Any() && innerDictList.Any(d => d.ContainsKey(nestedKey) && d[nestedKey] is Dictionary<string, object>))
										{
											var nestedInnerDict = innerDictList.First(d => d.ContainsKey(nestedKey) && d[nestedKey] is Dictionary<string, object>)[nestedKey] as Dictionary<string, object>;
											var nestedInnerKeyMapping = CreateKeyMapping(nestedInnerDict.Keys);
											valueExpr += $"{innerInnerKeyMapping[nestedKey]} = {GenerateNestedDictionaryResult(nestedInnerDict, nestedKey, nestedInnerKeyMapping, indentLevel + 3, "innerDict")},\n";
										}
										else
										{
											valueExpr += $"{innerInnerKeyMapping[nestedKey]} = innerDict.ContainsKey(\"{nestedKey}\") ? innerDict[\"{nestedKey}\"] : (object)null,\n";
										}
									}
									if (innerInnerKeyMapping.Count > 0) valueExpr = valueExpr.Substring(0, valueExpr.Length - 2);
									valueExpr += "\n";
								}
								valueExpr += "}";
								valueExpr += ";\n";
								valueExpr += "}).ToArray() : new[] { " + innerTemplate + " },\n";
							}
							else
							{
								valueExpr += $"{innerKeyMapping[innerKey]} = subDict.ContainsKey(\"{innerKey}\") ? subDict[\"{innerKey}\"] : (object)null,\n";
							}
						}
						if (innerKeyMapping.Count > 0) valueExpr = valueExpr.Substring(0, valueExpr.Length - 2);
						valueExpr += "\n";
					}
					valueExpr += "}";
					valueExpr += ";\n";
					valueExpr += "}).ToArray() : new[] { " + itemTemplate + " })";
				}
				else
				{
					valueExpr = $"({dictName}.ContainsKey(\"{parentKey}\") && ((Dictionary<string, object>){dictName}[\"{parentKey}\"]).ContainsKey(\"{key}\") ? ((Dictionary<string, object>){dictName}[\"{parentKey}\"])[\"{key}\"] : (object)null)";
				}
				sb.Append($"{keyMapping[key]} = {valueExpr},\n");
			}
			if (keyMapping.Count > 0) sb.Length -= 2;
			sb.Append("\n");
		}
		sb.Append("} : " + template + ")");
		return sb.ToString();
	}

	private static string GenerateArrayResult(string key, IEnumerable<object> arr, Dictionary<string, string> keyMapping, int indentLevel, string dictName, Dictionary<string, object> parentDict)
	{
		var sb = new StringBuilder();
		var items = arr.ToList();
		if (items.Any() && items[0] is Dictionary<string, object> firstDict || !items.Any())
		{
			var dictList = items.OfType<Dictionary<string, object>>().ToList();
			var allKeys = GetAllDictionaryKeys(dictList);
			var innerKeyMapping = CreateKeyMapping(allKeys);
			var templateDict = CreateTemplateDictionary(dictList);
			var itemTemplate = dictList.Any() ? GenerateAnonymousTemplate(CreateTemplateDictionary(dictList), innerKeyMapping) : "new { }";

			sb.Append($"({dictName}.ContainsKey(\"{key}\") && {dictName}[\"{key}\"] != null ? ((object[]){dictName}[\"{key}\"]).Select((item, j) =>\n");
			sb.Append("{\n");
			sb.Append($"var subDict = (Dictionary<string, object>)item;\n");
			sb.Append($"return new {{");
			if (allKeys.Any())
			{
				sb.Append("\n");
				foreach (var subKey in allKeys.OrderBy(k => k))
				{
					if (dictList.Any() && dictList.Any(dict => dict.ContainsKey(subKey) && dict[subKey] is Dictionary<string, object> nestedDict))
					{
						var nestedSubDict = dictList.First(dict => dict.ContainsKey(subKey) && dict[subKey] is Dictionary<string, object>)[subKey] as Dictionary<string, object>;
						var nestedKeyMapping = CreateKeyMapping(nestedSubDict.Keys);
						sb.Append($"{innerKeyMapping[subKey]} = {GenerateNestedDictionaryResult(nestedSubDict, subKey, nestedKeyMapping, indentLevel + 1, "subDict")},\n");
					}
					else if (dictList.Any() && dictList.Any(dict => dict.ContainsKey(subKey) && dict[subKey] is object[] innerArr && (innerArr.Any() && innerArr[0] is Dictionary<string, object> || !innerArr.Any())))
					{
						var innerArr = dictList.First(dict => dict.ContainsKey(subKey) && dict[subKey] is object[])[subKey] as object[];
						var innerDictList = innerArr.OfType<Dictionary<string, object>>().ToList();
						var innerInnerKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(innerDictList));
						var innerTemplate = innerDictList.Any() ? GenerateAnonymousTemplate(CreateTemplateDictionary(innerDictList), innerInnerKeyMapping) : "new { }";
						sb.Append($"{innerKeyMapping[subKey]} = subDict.ContainsKey(\"{subKey}\") && subDict[\"{subKey}\"] != null ? ((object[])subDict[\"{subKey}\"]).Select((innerItem, k) =>\n");
						sb.Append("{\n");
						sb.Append($"var innerDict = (Dictionary<string, object>)innerItem;\n");
						sb.Append($"return new {{");
						if (innerInnerKeyMapping.Any())
						{
							sb.Append("\n");
							foreach (var innerKey in innerInnerKeyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
							{
								if (innerDictList.Any() && innerDictList.Any(d => d.ContainsKey(innerKey) && d[innerKey] is Dictionary<string, object>))
								{
									var nestedInnerDict = innerDictList.First(d => d.ContainsKey(innerKey) && d[innerKey] is Dictionary<string, object>)[innerKey] as Dictionary<string, object>;
									var nestedInnerKeyMapping = CreateKeyMapping(nestedInnerDict.Keys);
									sb.Append($"{innerInnerKeyMapping[innerKey]} = {GenerateNestedDictionaryResult(nestedInnerDict, innerKey, nestedInnerKeyMapping, indentLevel + 2, "innerDict")},\n");
								}
								else if (innerDictList.Any() && innerDictList.Any(d => d.ContainsKey(innerKey) && d[innerKey] is object[] innerInnerArr && (innerInnerArr.Any() && innerInnerArr[0] is Dictionary<string, object> || !innerInnerArr.Any())))
								{
									var innerInnerArr = innerDictList.First(d => d.ContainsKey(innerKey) && d[innerKey] is object[])[innerKey] as object[];
									var innerInnerDictList = innerInnerArr.OfType<Dictionary<string, object>>().ToList();
									var innerInnerInnerKeyMapping = CreateKeyMapping(GetAllDictionaryKeys(innerInnerDictList));
									var innerInnerTemplate = innerInnerDictList.Any() ? GenerateAnonymousTemplate(CreateTemplateDictionary(innerInnerDictList), innerInnerInnerKeyMapping) : "new { }";
									sb.Append($"{innerInnerKeyMapping[innerKey]} = innerDict.ContainsKey(\"{innerKey}\") && innerDict[\"{innerKey}\"] != null ? ((object[])innerDict[\"{innerKey}\"]).Select((innerInnerItem, m) =>\n");
									sb.Append("{\n");
									sb.Append($"var innerInnerDict = (Dictionary<string, object>)innerInnerItem;\n");
									sb.Append($"return new {{");
									if (innerInnerInnerKeyMapping.Any())
									{
										sb.Append("\n");
										foreach (var innerInnerKey in innerInnerInnerKeyMapping.OrderBy(kv => kv.Key).Select(kv => kv.Key))
										{
											sb.Append($"{innerInnerInnerKeyMapping[innerInnerKey]} = innerInnerDict.ContainsKey(\"{innerInnerKey}\") ? innerInnerDict[\"{innerInnerKey}\"] : (object)null,\n");
										}
										sb.Length -= 2;
										sb.Append("\n");
									}
									sb.Append("}");
									sb.Append(";\n");
									sb.Append("}).ToArray() : new[] { " + innerInnerTemplate + " },\n");
								}
								else
								{
									sb.Append($"{innerInnerKeyMapping[innerKey]} = innerDict.ContainsKey(\"{innerKey}\") ? innerDict[\"{innerKey}\"] : (object)null,\n");
								}
							}
							if (innerInnerKeyMapping.Count > 0) sb.Length -= 2;
							sb.Append("\n");
						}
						sb.Append("}");
						sb.Append(";\n");
						sb.Append("}).ToArray() : new[] { " + innerTemplate + " },\n");
					}
					else
					{
						sb.Append($"{innerKeyMapping[subKey]} = subDict.ContainsKey(\"{subKey}\") ? subDict[\"{subKey}\"] : (object)null,\n");
					}
				}
				if (allKeys.Count > 0) sb.Length -= 2;
				sb.Append("\n");
			}
			sb.Append("}");
			sb.Append(";\n");
			sb.Append("}).ToArray() : new[] { " + itemTemplate + " })");
		}
		else
		{
			sb.Append($"({dictName}.ContainsKey(\"{key}\") && {dictName}[\"{key}\"] != null ? ((object[]){dictName}[\"{key}\"]).Select(item => item).ToArray() : new object[0])");
		}
		return sb.ToString();
	}
}