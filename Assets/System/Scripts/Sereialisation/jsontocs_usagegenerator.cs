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
			foreach (var kv in dict.OrderBy(k => k.Key))
			{
				string safe = "_" + Sanitize(kv.Key);
				string child = $"{varExpr}.{safe}";
				string newPath = string.IsNullOrEmpty(path) ? kv.Key : path + "." + kv.Key;

				if (kv.Value is IDictionary<string, object> || kv.Value is object[])
				{
					GenerateLogs(sb, kv.Value, child, newPath, indent, ref uid);
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
			if (sample is IDictionary<string, object> || sample is object[])
			{
				GenerateLogs(sb, sample, elemExpr, elemPath, indent + 8, ref uid);
			}
			else
			{
				sb.AppendLine($"{tabs}        if ({elemExpr} != null) Debug.Log($\"{EscapePath(elemPath)} : {{{elemExpr}}}\");");
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

//using System;
 //using System.Collections.Generic;
 //using System.Linq;
 //using System.Text;

//public static class jsontocs_usagegenerator
//{
//	public static string GenerateDeserializationCode(object data, string jsonString)
//	{
//		var sb = new StringBuilder();

//		// ----- Header -----
//		sb.AppendLine("using System;");
//		sb.AppendLine("using System.Collections.Generic;");
//		sb.AppendLine("using System.Linq;");
//		sb.AppendLine("using UnityEngine;");
//		sb.AppendLine();
//		sb.AppendLine("public class JsonDeserializationExample : MonoBehaviour");
//		sb.AppendLine("{");
//		sb.AppendLine("    static class J");
//		sb.AppendLine("    {");
//		sb.AppendLine("        public static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;");
//		sb.AppendLine("        public static object[] A(object o) => o as object[];");
//		sb.AppendLine("        public static object G(Dictionary<string, object> d, string k) => (d != null && d.ContainsKey(k)) ? d[k] : null;");
//		sb.AppendLine("    }");
//		sb.AppendLine();
//		sb.AppendLine("    void Start()");
//		sb.AppendLine("    {");
//		sb.AppendLine($"        string jsonString = @\"{EscapeJson(jsonString)}\";");
//		sb.AppendLine("        try");
//		sb.AppendLine("        {");
//		sb.AppendLine("            var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);");
//		sb.AppendLine("            if (data != null)");
//		sb.AppendLine("            {");

//		// ----- TEMPLATE -----
//		sb.Append("                var template = ");
//		sb.Append(BuildTemplate(data));
//		sb.AppendLine(";");

//		// ----- RESULT -----
//		sb.Append("                var result = ");
//		sb.Append(BuildValue(data, "data", 0));
//		sb.AppendLine(";");

//		// ----- LOGS -----
//		var logs = new StringBuilder();
//		int uid = 0;
//		GenerateLogs(logs, data, "result", "", 16, ref uid);
//		sb.Append(logs.ToString());

//		sb.AppendLine("            }");
//		sb.AppendLine("            else");
//		sb.AppendLine("            {");
//		sb.AppendLine("                Debug.LogError(\"Failed to deserialize JSON\");");
//		sb.AppendLine("            }");
//		sb.AppendLine("        }");
//		sb.AppendLine("        catch (System.Exception e)");
//		sb.AppendLine("        {");
//		sb.AppendLine("            Debug.LogError($\"Deserialization error: {e.Message}\");");
//		sb.AppendLine("        }");
//		sb.AppendLine("    }");
//		sb.AppendLine("}");

//		return sb.ToString();
//	}

//	// ---------------- TEMPLATE BUILDER ----------------
//	private static string BuildTemplate(object node)
//	{
//		if (node is IDictionary<string, object> dict)
//		{
//			var used = new HashSet<string>(StringComparer.Ordinal);
//			var props = new List<string>();
//			foreach (var kv in dict.OrderBy(k => k.Key))
//			{
//				string name = "_" + UniqueSanitized(kv.Key, used);
//				props.Add($"{name} = {BuildTemplate(kv.Value)}");
//			}
//			return $"new {{ {string.Join(", ", props)} }}";
//		}
//		if (node is object[] arr)
//		{
//			object sample = arr.FirstOrDefault();
//			string elem = sample != null ? BuildTemplate(sample) : "(object)null";
//			return $"new[] {{ {elem} }}";
//		}
//		return "(object)null";
//	}

//	// ---------------- VALUE BUILDER ----------------
//	private static string BuildValue(object node, string expr, int depth)
//	{
//		if (node is IDictionary<string, object> dict)
//		{
//			var used = new HashSet<string>(StringComparer.Ordinal);
//			var props = new List<string>();
//			foreach (var kv in dict.OrderBy(k => k.Key))
//			{
//				string key = kv.Key;
//				string safe = "_" + UniqueSanitized(key, used);
//				string get = $"J.G({expr}, \"{EscapeStringLiteral(key)}\")";

//				if (kv.Value is IDictionary<string, object>)
//				{
//					props.Add($"{safe} = {BuildValue(kv.Value, $"J.D({get})", depth + 1)}");
//				}
//				else if (kv.Value is object[] arr)
//				{
//					string item = $"item{depth}";
//					string idx = $"i{depth}";
//					object sample = arr.FirstOrDefault();
//					string elemExpr = sample != null
//						? BuildValue(sample, $"J.D({item})", depth + 1)
//						: item;
//					string defaultElem = sample != null
//						? BuildTemplate(sample)
//						: "(object)null";

//					props.Add($"{safe} = (J.A({get}) != null ? J.A({get}).Select(({item}, {idx}) => {elemExpr}).ToArray() : new[] {{ {defaultElem} }})");
//				}
//				else
//				{
//					props.Add($"{safe} = {get}");
//				}
//			}
//			return $"new {{ {string.Join(", ", props)} }}";
//		}
//		else if (node is object[] arr)
//		{
//			string item = $"item{depth}";
//			string idx = $"i{depth}";
//			object sample = arr.FirstOrDefault();
//			string elemExpr = sample != null
//				? BuildValue(sample, $"J.D({item})", depth + 1)
//				: item;
//			string defaultElem = sample != null
//				? BuildTemplate(sample)
//				: "(object)null";

//			return $"(J.A({expr}) != null ? J.A({expr}).Select(({item}, {idx}) => {elemExpr}).ToArray() : new[] {{ {defaultElem} }})";
//		}
//		else
//		{
//			return expr;
//		}
//	}

//	// ---------------- LOG BUILDER ----------------
//	private static void GenerateLogs(StringBuilder sb, object node, string varExpr, string path, int indent, ref int uid)
//	{
//		string tabs = new string(' ', indent);

//		if (node is IDictionary<string, object> dict)
//		{
//			foreach (var kv in dict.OrderBy(k => k.Key))
//			{
//				string safe = "_" + Sanitize(kv.Key);
//				string child = $"{varExpr}.{safe}";
//				string newPath = string.IsNullOrEmpty(path) ? kv.Key : path + "." + kv.Key;

//				if (kv.Value is IDictionary<string, object> || kv.Value is object[])
//				{
//					GenerateLogs(sb, kv.Value, child, newPath, indent, ref uid);
//				}
//				else
//				{
//					sb.AppendLine($"{tabs}if ({child} != null) Debug.Log($\"{EscapePath(newPath)} : {{{child}}}\");");
//				}
//			}
//		}
//		else if (node is object[] arr)
//		{
//			sb.AppendLine($"{tabs}if ({varExpr} != null) Debug.Log($\"{EscapePath(path)} (array length): {{{varExpr}.Length}}\");");

//			string idx = "i" + (uid++);
//			sb.AppendLine($"{tabs}if ({varExpr} != null)");
//			sb.AppendLine($"{tabs}{{");
//			sb.AppendLine($"{tabs}    for (int {idx} = 0; {idx} < {varExpr}.Length; {idx}++)");
//			sb.AppendLine($"{tabs}    {{");

//			string elemExpr = $"{varExpr}[{idx}]";
//			string elemPath = $"{path}[{{{idx}}}]";
//			object sample = arr.FirstOrDefault();
//			if (sample is IDictionary<string, object> || sample is object[])
//			{
//				GenerateLogs(sb, sample, elemExpr, elemPath, indent + 8, ref uid);
//			}
//			else
//			{
//				sb.AppendLine($"{tabs}        if ({elemExpr} != null) Debug.Log($\"{EscapePath(elemPath)} : {{{elemExpr}}}\");");
//			}

//			sb.AppendLine($"{tabs}    }}");
//			sb.AppendLine($"{tabs}}}");
//		}
//	}

//	// ---------------- Utilities ----------------
//	private static string EscapeJson(string json) => (json ?? "").Replace("\"", "\"\"");
//	private static string EscapeStringLiteral(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
//	private static string EscapePath(string s) => (s ?? "").Replace("{", "{{").Replace("}", "}}");

//	private static string Sanitize(string key)
//	{
//		if (string.IsNullOrEmpty(key)) return "_";
//		var sb = new StringBuilder();
//		bool first = true;
//		foreach (char c in key)
//		{
//			if (first && char.IsDigit(c)) sb.Append('_');
//			sb.Append(char.IsLetterOrDigit(c) ? c : '_');
//			first = false;
//		}
//		return sb.ToString();
//	}

//	private static string UniqueSanitized(string key, HashSet<string> used)
//	{
//		string baseName = Sanitize(key);
//		string name = baseName;
//		int n = 1;
//		while (!used.Add(name)) name = baseName + "_" + n++;
//		return name;
//	}
//}



//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.RegularExpressions;

//public static class jsontocs_usagegenerator
//{
//	public static string GenerateDeserializationCode(object data, string jsonString)
//	{
//		var sb = new StringBuilder();

//		// Header
//		sb.AppendLine("using System;");
//		sb.AppendLine("using System.Collections.Generic;");
//		sb.AppendLine("using System.Linq;");
//		sb.AppendLine("using UnityEngine;");
//		sb.AppendLine();
//		sb.AppendLine("public class JsonDeserializationExample : MonoBehaviour");
//		sb.AppendLine("{");
//		// Helper class embedded into the generated script
//		sb.AppendLine("    static class J");
//		sb.AppendLine("    {");
//		sb.AppendLine("        public static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;");
//		sb.AppendLine("        public static object[] A(object o) => o as object[];");
//		sb.AppendLine("        public static object G(Dictionary<string, object> d, string k) => (d != null && d.ContainsKey(k)) ? d[k] : null;");
//		sb.AppendLine("    }");
//		sb.AppendLine();
//		sb.AppendLine("    void Start()");
//		sb.AppendLine("    {");
//		sb.AppendLine($"        string jsonString = @\"{EscapeJson(jsonString)}\";");
//		sb.AppendLine("        try");
//		sb.AppendLine("        {");
//		sb.AppendLine("            var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);");
//		sb.AppendLine("            if (data != null)");
//		sb.AppendLine("            {");

//		// TEMPLATE
//		sb.Append("                var template = ");
//		sb.Append(BuildTemplate(data, 16));
//		sb.AppendLine(";");

//		// RESULT
//		sb.Append("                var result = ");
//		sb.Append(BuildValue(data, "data", 16));
//		sb.AppendLine(";");

//		// LOGS
//		var logs = new StringBuilder();
//		GenerateLogs(logs, data, "result", "", 16, new List<string>());
//		sb.Append(logs.ToString());

//		sb.AppendLine("            }");
//		sb.AppendLine("            else");
//		sb.AppendLine("            {");
//		sb.AppendLine("                Debug.LogError(\"Failed to deserialize JSON\");");
//		sb.AppendLine("            }");
//		sb.AppendLine("        }");
//		sb.AppendLine("        catch (System.Exception e)");
//		sb.AppendLine("        {");
//		sb.AppendLine("            Debug.LogError($\"Deserialization error: {e.Message}\");");
//		sb.AppendLine("        }");
//		sb.AppendLine("    }");
//		sb.AppendLine("}");

//		return sb.ToString();
//	}

//	// -------- Template builder (anonymous shape with (object)null leaves) --------
//	private static string BuildTemplate(object node, int indent)
//	{
//		if (node is IDictionary<string, object> dict)
//		{
//			var used = new HashSet<string>(StringComparer.Ordinal);
//			var props = new List<string>();
//			foreach (var kv in dict.OrderBy(kv => kv.Key))
//			{
//				string name = SanitizeKey(kv.Key, used);
//				props.Add($"{name} = {BuildTemplate(kv.Value, indent + 4)}");
//			}
//			return $"new {{ {string.Join(", ", props)} }}";
//		}
//		if (node is object[] arr)
//		{
//			// Create a template based on all elements to capture the most comprehensive structure
//			var sampleDict = CreateTemplateDictionary(arr.OfType<Dictionary<string, object>>());
//			string elem = sampleDict != null ? BuildTemplate(sampleDict, indent + 4) : "(object)null";
//			return $"new[] {{ {elem} }}";
//		}
//		return "(object)null";
//	}

//	// -------- Value builder (uses helper J.* to safely index & cast) --------
//	private static string BuildValue(object node, string exprDict, int depth)
//	{
//		if (node is IDictionary<string, object> dict)
//		{
//			var used = new HashSet<string>(StringComparer.Ordinal);
//			var props = new List<string>();
//			foreach (var kv in dict.OrderBy(kv => kv.Key))
//			{
//				string key = kv.Key;
//				string safe = SanitizeKey(key, used);
//				string get = $"J.G({exprDict}, \"{EscapeStringLiteral(key)}\")";

//				if (kv.Value is IDictionary<string, object>)
//				{
//					props.Add($"{safe} = {BuildValue(kv.Value, $"J.D({get})", depth)}");
//				}
//				else if (kv.Value is object[] arr)
//				{
//					string item = $"item{depth}";
//					string idx = $"i{depth}";
//					string arrExpr = $"J.A({get})";
//					var sampleDict = CreateTemplateDictionary(arr.OfType<Dictionary<string, object>>());
//					string elemExpr = sampleDict != null ? BuildValue(sampleDict, $"J.D({item})", depth + 1) : $"{item}";
//					string defaultElem = sampleDict != null ? BuildTemplate(sampleDict, depth + 1) : "(object)null";
//					props.Add($"{safe} = ({arrExpr} != null ? {arrExpr}.Select(({item}, {idx}) => {elemExpr}).ToArray() : new[] {{ {defaultElem} }})");
//				}
//				else
//				{
//					props.Add($"{safe} = {get}");
//				}
//			}
//			return $"new {{ {string.Join(", ", props)} }}";
//		}
//		else if (node is object[] arr)
//		{
//			string item = $"item{depth}";
//			string idx = $"i{depth}";
//			string arrExpr = $"J.A({exprDict})";
//			var sampleDict = CreateTemplateDictionary(arr.OfType<Dictionary<string, object>>());
//			string elemExpr = sampleDict != null ? BuildValue(sampleDict, $"J.D({item})", depth + 1) : $"{item}";
//			string defaultElem = sampleDict != null ? BuildTemplate(sampleDict, depth + 1) : "(object)null";
//			return $"({arrExpr} != null ? {arrExpr}.Select(({item}, {idx}) => {elemExpr}).ToArray() : new[] {{ {defaultElem} }})";
//		}
//		else
//		{
//			return exprDict;
//		}
//	}

//	// -------- Logs: emit safe, deep Debug.Log with comments for all elements --------
//	private static void GenerateLogs(StringBuilder sb, object node, string varExpr, string path, int indent, List<string> checks)
//	{
//		string tabs = new string(' ', indent);

//		if (node is IDictionary<string, object> dict)
//		{
//			foreach (var kv in dict.OrderBy(kv => kv.Key))
//			{
//				string safe = SanitizeKey(kv.Key, new HashSet<string>());
//				string childExpr = $"{varExpr}.{safe}";
//				string newPath = string.IsNullOrEmpty(path) ? kv.Key : $"{path}.{kv.Key}";

//				if (kv.Value is IDictionary<string, object> || kv.Value is object[])
//				{
//					GenerateLogs(sb, kv.Value, childExpr, newPath, indent, checks);
//				}
//				else
//				{
//					string comment = EscapeCommentValue(kv.Value);
//					sb.AppendLine($"{tabs}if ({childExpr} != null) Debug.Log($\"{EscapeInterpolatedString(newPath)} : {{{childExpr}}}\"); // {comment}");
//				}
//			}
//		}
//		else if (node is object[] arr)
//		{
//			sb.AppendLine($"{tabs}if ({varExpr} != null) Debug.Log($\"{EscapeInterpolatedString(path)} (array length): {{{varExpr}.Length}}\"); // Array length");

//			for (int i = 0; i < arr.Length; i++)
//			{
//				string idxCheck = $"{varExpr} != null && {varExpr}.Length > {i}";
//				string elemExpr = $"{varExpr}[{i}]";
//				string elemPath = $"{path}[{i}]";
//				var newChecks = new List<string>(checks) { idxCheck };

//				if (arr[i] is IDictionary<string, object> || arr[i] is object[])
//				{
//					sb.AppendLine($"{tabs}if ({string.Join(" && ", newChecks)}) {{");
//					GenerateLogs(sb, arr[i], elemExpr, elemPath, indent + 4, newChecks);
//					sb.AppendLine($"{tabs}}}");
//				}
//				else
//				{
//					string comment = EscapeCommentValue(arr[i]);
//					sb.AppendLine($"{tabs}if ({string.Join(" && ", newChecks)}) Debug.Log($\"{EscapeInterpolatedString(elemPath)} : {{{elemExpr}}}\"); // {comment}");
//				}
//			}
//		}
//	}

//	// -------- Utilities --------
//	private static string EscapeJson(string json) => (json ?? "").Replace("\"", "\"\"");
//	private static string EscapeStringLiteral(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
//	private static string EscapeInterpolatedString(string s) => (s ?? "").Replace("{", "{{").Replace("}", "}}");
//	private static string EscapeCommentValue(object value)
//	{
//		if (value == null) return "null";
//		string result = value.ToString().Replace("\n", "_").Replace("\r", "_").Replace("\"", "_").Replace("'", "_");
//		return Regex.Replace(result, @"[\t\\]+", "_");
//	}

//	private static string SanitizeKey(string key, HashSet<string> used)
//	{
//		if (string.IsNullOrEmpty(key)) return "_empty_" + (used.Count + 1);
//		var sb = new StringBuilder();
//		bool isFirst = true;
//		foreach (char c in key)
//		{
//			if (isFirst && char.IsDigit(c))
//				sb.Append('_'); // Prefix underscore for leading digits
//			sb.Append(char.IsLetterOrDigit(c) ? c : '_');
//			isFirst = false;
//		}
//		string baseName = sb.ToString();
//		string name = baseName;
//		int n = 1;
//		while (!used.Add(name))
//			name = baseName + "_" + n++;
//		return name;
//	}

//	private static Dictionary<string, object> CreateTemplateDictionary(IEnumerable<Dictionary<string, object>> dicts)
//	{
//		var template = new Dictionary<string, object>();
//		var allKeys = dicts.SelectMany(d => d.Keys).Distinct().OrderBy(k => k);
//		foreach (var key in allKeys)
//		{
//			var values = dicts.Where(d => d.ContainsKey(key)).Select(d => d[key]).ToList();
//			if (values.Any())
//			{
//				var firstNonNull = values.FirstOrDefault(v => v != null);
//				if (firstNonNull is Dictionary<string, object> nestedDict)
//				{
//					template[key] = nestedDict;
//				}
//				else if (firstNonNull is object[] arr && arr.Any())
//				{
//					var subDicts = arr.OfType<Dictionary<string, object>>().ToList();
//					template[key] = subDicts.Any() ? CreateTemplateDictionary(subDicts) : new object[0];
//				}
//				else
//				{
//					template[key] = null;
//				}
//			}
//			else
//			{
//				template[key] = null;
//			}
//		}
//		return template.Count > 0 ? template : null;
//	}
//}