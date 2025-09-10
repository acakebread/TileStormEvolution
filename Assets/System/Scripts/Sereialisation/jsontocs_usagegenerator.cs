using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class jsontocs_usagegenerator
{
	public static string GenerateDeserializationCode(object data, string jsonString)
	{
		var sb = new StringBuilder();

		// ----- Header -----
		sb.AppendLine("using System;");
		sb.AppendLine("using System.Collections.Generic;");
		sb.AppendLine("using System.Linq;");
		sb.AppendLine("using UnityEngine;");
		sb.AppendLine();
		sb.AppendLine("public class JsonDeserializationExample : MonoBehaviour");
		sb.AppendLine("{");
		sb.AppendLine("    static class J");
		sb.AppendLine("    {");
		sb.AppendLine("        public static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;");
		sb.AppendLine("        public static object[] A(object o) => o as object[];");
		sb.AppendLine("        public static object G(Dictionary<string, object> d, string k) => (d != null && d.ContainsKey(k)) ? d[k] : null;");
		sb.AppendLine("    }");
		sb.AppendLine();
		sb.AppendLine("    void Start()");
		sb.AppendLine("    {");
		sb.AppendLine($"        string jsonString = @\"{EscapeJson(jsonString)}\";");
		sb.AppendLine("        try");
		sb.AppendLine("        {");
		string rootType = data is object[]? "object[]" : "Dictionary<string, object>";
		sb.AppendLine($"            var data = JsonTocs.FromJson<{rootType}>(jsonString);");
		sb.AppendLine("            if (data != null)");
		sb.AppendLine("            {");

		// ----- TEMPLATE -----
		sb.Append("                var template = ");
		sb.Append(BuildTemplate(data));
		sb.AppendLine(";");

		// ----- RESULT -----
		sb.Append("                var result = ");
		sb.Append(BuildValue(data, "data", 0));
		sb.AppendLine(";");

		// ----- LOGS -----
		var logs = new StringBuilder();
		int uid = 0;
		GenerateLogs(logs, data, "result", "", 16, ref uid);
		sb.Append(logs.ToString());

		sb.AppendLine("            }");
		sb.AppendLine("            else");
		sb.AppendLine("            {");
		sb.AppendLine("                Debug.LogError(\"Failed to deserialize JSON\");");
		sb.AppendLine("            }");
		sb.AppendLine("        }");
		sb.AppendLine("        catch (System.Exception e)");
		sb.AppendLine("        {");
		sb.AppendLine("            Debug.LogError($\"Deserialization error: {e.Message}\");");
		sb.AppendLine("        }");
		sb.AppendLine("    }");
		sb.AppendLine("}");

		return sb.ToString();
	}

	// ---------------- TEMPLATE BUILDER ----------------
	private static string BuildTemplate(object node)
	{
		if (node is IDictionary<string, object> dict)
		{
			var used = new HashSet<string>(StringComparer.Ordinal);
			var props = new List<string>();
			foreach (var kv in dict.OrderBy(k => k.Key))
			{
				string name = "_" + UniqueSanitized(kv.Key, used);
				props.Add($"{name} = {BuildTemplate(kv.Value)}");
			}
			return $"new {{ {string.Join(", ", props)} }}";
		}
		if (node is object[] arr)
		{
			object sample = arr.FirstOrDefault();
			string elem = sample != null ? BuildTemplate(sample) : "(object)null";
			return $"new[] {{ {elem} }}";
		}
		return "(object)null";
	}

	// ---------------- VALUE BUILDER ----------------
	private static string BuildValue(object node, string expr, int depth)
	{
		if (node is IDictionary<string, object> dict)
		{
			var used = new HashSet<string>(StringComparer.Ordinal);
			var props = new List<string>();
			foreach (var kv in dict.OrderBy(k => k.Key))
			{
				string key = kv.Key;
				string safe = "_" + UniqueSanitized(key, used);
				string get = $"J.G({expr}, \"{EscapeStringLiteral(key)}\")";

				if (kv.Value is IDictionary<string, object>)
				{
					props.Add($"{safe} = {BuildValue(kv.Value, $"J.D({get})", depth + 1)}");
				}
				else if (kv.Value is object[] arr)
				{
					string item = $"item{depth}";
					string idx = $"i{depth}";
					object sample = arr.FirstOrDefault();
					string elemExpr = sample != null
						? BuildValue(sample, $"J.D({item})", depth + 1)
						: item;
					string defaultElem = sample != null
						? BuildTemplate(sample)
						: "(object)null";

					props.Add($"{safe} = (J.A({get}) != null ? J.A({get}).Select(({item}, {idx}) => {elemExpr}).ToArray() : new[] {{ {defaultElem} }})");
				}
				else
				{
					props.Add($"{safe} = {get}");
				}
			}
			return $"new {{ {string.Join(", ", props)} }}";
		}
		else if (node is object[] arr)
		{
			string item = $"item{depth}";
			string idx = $"i{depth}";
			object sample = arr.FirstOrDefault();
			string elemExpr = sample != null
				? BuildValue(sample, $"J.D({item})", depth + 1)
				: item;
			string defaultElem = sample != null
				? BuildTemplate(sample)
				: "(object)null";

			return $"(J.A({expr}) != null ? J.A({expr}).Select(({item}, {idx}) => {elemExpr}).ToArray() : new[] {{ {defaultElem} }})";
		}
		else
		{
			return expr;
		}
	}

	// ---------------- LOG BUILDER ----------------
	private static void GenerateLogs(StringBuilder sb, object node, string varExpr, string path, int indent, ref int uid)
	{
		string tabs = new string(' ', indent);

		if (node is IDictionary<string, object> dict)
		{
			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var kv in dict.OrderBy(k => k.Key))
			{
				string safe = "_" + UniqueSanitized(kv.Key, used);
				string child = $"{varExpr}.{safe}";
				string newPath = string.IsNullOrEmpty(path) ? kv.Key : path + "." + kv.Key;

				if (kv.Value is IDictionary<string, object> nestedDict)
				{
					GenerateLogs(sb, nestedDict, child, newPath, indent, ref uid);
				}
				else if (kv.Value is object[] nestedArr)
				{
					string idx = "i" + (uid++);
					sb.AppendLine($"{tabs}if ({child} != null) Debug.Log($\"{EscapePath(newPath)} (array length): {{{child}.Length}}\");");
					sb.AppendLine($"{tabs}if ({child} != null)");
					sb.AppendLine($"{tabs}{{");
					sb.AppendLine($"{tabs}    for (int {idx} = 0; {idx} < {child}.Length; {idx}++)");
					sb.AppendLine($"{tabs}    {{");
					string nestedElemExpr = $"{child}[{idx}]";
					string nestedElemPath = $"{newPath}[{{{idx}}}]";
					object nestedSample = nestedArr.FirstOrDefault();
					if (nestedSample != null)
					{
						if (nestedSample is IDictionary<string, object> nestedDictSample)
						{
							var nestedUsed = new HashSet<string>(StringComparer.Ordinal);
							string anonExpr = BuildValue(nestedDictSample, $"J.D({nestedElemExpr})", uid);
							sb.AppendLine($"{tabs}        var element{idx} = {anonExpr};");
							GenerateLogs(sb, nestedDictSample, $"element{idx}", nestedElemPath, indent + 8, ref uid);
						}
						else if (nestedSample is object[])
						{
							sb.AppendLine($"{tabs}        var element{idx} = {nestedElemExpr};");
							GenerateLogs(sb, nestedSample, $"element{idx}", nestedElemPath, indent + 8, ref uid);
						}
						else
						{
							sb.AppendLine($"{tabs}        if ({nestedElemExpr} != null) Debug.Log($\"{EscapePath(nestedElemPath)} : {{{nestedElemExpr}}}\");");
						}
					}
					sb.AppendLine($"{tabs}    }}");
					sb.AppendLine($"{tabs}}}");
				}
				else
				{
					sb.AppendLine($"{tabs}if ({child} != null) Debug.Log($\"{EscapePath(newPath)} : {{{child}}}\");");
				}
			}
		}
		else if (node is object[] arr)
		{
			sb.AppendLine($"{tabs}if ({varExpr} != null) Debug.Log($\"{EscapePath(path)} (array length): {{{varExpr}.Length}}\");");

			string idx = "i" + (uid++);
			sb.AppendLine($"{tabs}if ({varExpr} != null)");
			sb.AppendLine($"{tabs}{{");
			sb.AppendLine($"{tabs}    for (int {idx} = 0; {idx} < {varExpr}.Length; {idx}++)");
			sb.AppendLine($"{tabs}    {{");

			string elemExpr = $"{varExpr}[{idx}]";
			string elemPath = $"{path}[{{{idx}}}]";
			object sample = arr.FirstOrDefault();
			if (sample != null)
			{
				if (sample is IDictionary<string, object> dictSample)
				{
					string anonExpr = BuildValue(dictSample, $"J.D({elemExpr})", uid);
					sb.AppendLine($"{tabs}        var element{idx} = {anonExpr};");
					GenerateLogs(sb, dictSample, $"element{idx}", elemPath, indent + 8, ref uid);
				}
				else if (sample is object[])
				{
					sb.AppendLine($"{tabs}        var element{idx} = {elemExpr};");
					GenerateLogs(sb, sample, $"element{idx}", elemPath, indent + 8, ref uid);
				}
				else
				{
					sb.AppendLine($"{tabs}        if ({elemExpr} != null) Debug.Log($\"{EscapePath(elemPath)} : {{{elemExpr}}}\");");
				}
			}

			sb.AppendLine($"{tabs}    }}");
			sb.AppendLine($"{tabs}}}");
		}
	}

	// ---------------- Utilities ----------------
	private static string EscapeJson(string json) => (json ?? "").Replace("\"", "\"\"");
	private static string EscapeStringLiteral(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
	private static string EscapePath(string s) => (s ?? "").Replace("{", "{{").Replace("}", "}}");

	private static string Sanitize(string key)
	{
		if (string.IsNullOrEmpty(key)) return "_";
		var sb = new StringBuilder();
		bool first = true;
		foreach (char c in key)
		{
			if (first && char.IsDigit(c)) sb.Append('_');
			sb.Append(char.IsLetterOrDigit(c) ? c : '_');
			first = false;
		}
		return sb.ToString();
	}

	private static string UniqueSanitized(string key, HashSet<string> used)
	{
		string baseName = Sanitize(key);
		string name = baseName;
		int n = 1;
		while (!used.Add(name)) name = baseName + "_" + n++;
		return name;
	}
}