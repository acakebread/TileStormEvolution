using System;
using System.Collections.Generic;
using System.Text;

namespace GrokParser
{
	public class XStruct
	{
		public string Type { get; set; } // e.g., "struct", "array", "string", "int", etc.
		public string Name { get; set; } // e.g., "maps", "defs", "szTheme"
		public byte[] Data { get; set; } // Raw data for primitives (string, bool, float, int, char)
		public List<XStruct> Members { get; set; } // Child structs for "struct", "array", "data"

		public XStruct()
		{
			Members = new List<XStruct>();
		}

		// Helper to get data as a specific type
		public T GetData<T>()
		{
			if (Data == null) return default;

			if (typeof(T) == typeof(string))
			{
				return (T)(object)Encoding.UTF8.GetString(Data).TrimEnd('\0');
			}
			else if (typeof(T) == typeof(bool))
			{
				return (T)(object)(Data[0] != 0);
			}
			else if (typeof(T) == typeof(float))
			{
				return (T)(object)BitConverter.ToSingle(Data, 0);
			}
			else if (typeof(T) == typeof(int))
			{
				return (T)(object)BitConverter.ToInt32(Data, 0);
			}
			else if (typeof(T) == typeof(byte[]))
			{
				return (T)(object)Data;
			}
			throw new InvalidOperationException($"Unsupported data type: {typeof(T)}");
		}

		// Find a member by name
		public XStruct Find(string name)
		{
			foreach (var member in Members)
			{
				if (string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return member;
				}
			}
			return null;
		}
	}
}