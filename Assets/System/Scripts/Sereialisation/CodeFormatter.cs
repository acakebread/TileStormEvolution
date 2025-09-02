using System;
using System.Text;

public static class CodeFormatter
{
	public static string FormatCode(string code)
	{
		if (string.IsNullOrWhiteSpace(code))
			return string.Empty;

		var sb = new StringBuilder();
		var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
		int indentLevel = 0;
		string indentString = "    "; // 4 spaces per indent

		foreach (var rawLine in lines)
		{
			string line = rawLine.Trim();
			if (line.Length == 0)
			{
				sb.AppendLine();
				continue;
			}
			if (line.StartsWith("}"))
				indentLevel = Math.Max(indentLevel - 1, 0);
			sb.Append(new string(' ', indentLevel * indentString.Length));
			sb.AppendLine(line);
			if (line.EndsWith("{"))
				indentLevel++;
		}
		return sb.ToString();
	}
}
