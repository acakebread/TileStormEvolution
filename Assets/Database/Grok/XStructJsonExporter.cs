using GrokParser;
using System;
using UnityEngine;

public static class XStructJsonExporter
{
	public static string ToJson(XStruct xstruct)
	{
		var serializable = ConvertToSerializable(xstruct);
		return JsonUtility.ToJson(serializable, true); // Pretty print
	}

	private static SerializableXStruct ConvertToSerializable(XStruct xstruct)
	{
		var result = new SerializableXStruct
		{
			type = xstruct.Type,
			name = xstruct.Name
		};

		if (xstruct.Type == "struct" || xstruct.Type == "array" || xstruct.Type == "data")
		{
			foreach (var member in xstruct.Members)
			{
				result.members.Add(ConvertToSerializable(member));
			}
		}
		else if (xstruct.Type == "string")
		{
			result.stringValue = xstruct.GetData<string>();
		}
		else if (xstruct.Type == "bool")
		{
			result.boolValue = xstruct.GetData<bool>();
		}
		else if (xstruct.Type == "float")
		{
			result.floatValue = xstruct.GetData<float>();
		}
		else if (xstruct.Type == "int")
		{
			result.intValue = xstruct.GetData<int>();
		}
		else if (xstruct.Type == "char")
		{
			result.charValue = Convert.ToBase64String(xstruct.GetData<byte[]>());
		}

		return result;
	}
}