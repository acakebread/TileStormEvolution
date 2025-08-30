using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections;

public static class JsonTocs
{
	private static Dictionary<string, Type> _typeRegistry = new Dictionary<string, Type>();

	public static void RegisterType(string jsonKey, Type type)
	{
		_typeRegistry[jsonKey] = type;
	}

	public static T FromJson<T>(string json) where T : class
	{
		XString str = new XString(ref json);
		var result = ReadObject(ref str);
		return ConvertToType<T>(result);
	}

	public static string ToJson(object src)
	{
		string dst = "";
		if (null == src) return dst + "null";
		Type type = src.GetType();
		switch (Type.GetTypeCode(type))
		{
			case TypeCode.Byte:
			case TypeCode.SByte:
			case TypeCode.UInt16:
			case TypeCode.UInt32:
			case TypeCode.UInt64:
			case TypeCode.Int16:
			case TypeCode.Int32:
			case TypeCode.Int64:
			case TypeCode.Decimal:
			case TypeCode.Double:
			case TypeCode.Single:
				return dst + src.ToString();

			case TypeCode.Boolean:
				return dst + src.ToString().ToLower();

			case TypeCode.String:
				return dst + "\"" + Regex.Replace((string)src, "[\"\\\\]", @"\$0") + "\"";

			case TypeCode.DateTime:
				return dst + "\"" + ((DateTime)src).ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") + "\"";
		}

		string writeAsArray(IEnumerable enumerable) =>
			"[" + String.Join(",", enumerable.Cast<object>().Select(o => ToJson(o))) + "]";

		string writeAsDictionary(IDictionary<string, object> dict) =>
			"{" + String.Join(",", dict.Select(p => "\"" + p.Key + "\":" + ToJson(p.Value))) + "}";

		if (type.IsArray || type.IsGenericType(typeof(List<>)) || type.IsGenericType(typeof(HashSet<>)))
			return dst + writeAsArray(src as IEnumerable);

		if (type.IsGenericType(typeof(Dictionary<,>)))
		{
			IEnumerable<DictionaryEntry> asDict() { foreach (DictionaryEntry p in src as IDictionary) yield return p; }
			return dst + writeAsDictionary(asDict().ToDictionary(k => k.Key.ToString(), v => v.Value));
		}

		PropertyInfo[] propInfo = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
		if (propInfo.Length > 0)
			return dst + writeAsDictionary(propInfo.ToDictionary(p => p.Name, p => p.GetValue(src)));

		FieldInfo[] fieldInfo = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
		if (fieldInfo.Length > 0)
			return dst + writeAsDictionary(fieldInfo.ToDictionary(f => f.Name, f => f.GetValue(src)));

		Debug.LogWarning("Source object type is not supported: " + type);
		return dst;
	}

	class XString : IEnumerable<char>
	{
		int pos = 0;
		string str;
		public XString(ref string src) => str = src;

		public char this[int i] => str[pos + i];
		public int Length => str.Length - pos;

		public string Substring(int startIndex, int length) => str.Substring(pos + startIndex, length);
		public XString Substring(int startIndex)
		{
			pos += startIndex;
			return this;
		}
		public XString TrimStart(params char[] trimChars)
		{
			for (; pos < str.Length && trimChars.Any(c => c == str[pos]); pos++) ;
			return this;
		}

		public bool StartsWith(string src) => str.IndexOf(src, pos, src.Length) == pos;
		public int IndexOf(string c) => str.IndexOf(c, pos) - pos;

		public IEnumerator<char> GetEnumerator() { for (int i = pos; i < str.Length; i++) yield return str[i]; }
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		public override string ToString() => Substring(0, Length);
	}

	static readonly char[] WHITESPACE = { ' ', '\r', '\n', '\t', '\uFEFF', '\u0009' };

	static object ReadNull(ref XString src)
	{
		src = src.Substring("null".Length);
		return null;
	}

	static object ReadBool(ref XString src)
	{
		bool result = src.StartsWith("true");
		src = src.Substring(result ? "true".Length : "false".Length);
		return result;
	}

	static object ReadString(ref XString src)
	{
		int pos = 1;
		while (pos < src.Length && (src[pos] != '"' || src[pos - 1] == '\\')) pos++;
		string result = Regex.Unescape(src.Substring(1, pos - 1));
		src = src.Substring(pos + 1);
		return result;
	}

	static bool IsNumber(XString src)
	{
		int n = 0;
		if (src[n] == '-') n++;
		if (n < src.Length && src[n] == '.') n++;
		return n < src.Length && Char.IsDigit(src[n]);
	}

	static readonly char[] NUM = "-+.eE0123456789".ToCharArray();
	static object ReadNumber(ref XString src)
	{
		string num = new String(src.TakeWhile(c => NUM.Contains(c)).ToArray());
		src = src.Substring(num.Length);
		if (!num.Contains(".") && Int64.TryParse(num, out long LONG)) return LONG;
		return double.TryParse(num, out double DOUBLE) ? DOUBLE : double.MaxValue;
	}

	static object ReadArray(ref XString src)
	{
		List<object> list = new List<object>();
		src = src.Substring(src.IndexOf("[") + 1);
		while (src[0] != ']')
		{
			object v = ReadObject(ref src);
			list.Add(v);
			src = src.TrimStart(WHITESPACE);
			if (src.StartsWith(",")) src = src.Substring(1);
		}
		src = src.Substring(src.IndexOf("]") + 1);

		if (list.Count <= 0) return new object[0];
		return list.ToArray();
	}

	static object ReadObject(ref XString src)
	{
		src = src.TrimStart(WHITESPACE);
		if (src.Length <= 0) return null;

		if (src[0] == '[') return ReadArray(ref src);

		if (src[0] == '{')
		{
			var dict = new Dictionary<string, object>();
			src = src.Substring(src.IndexOf("{") + 1);
			while (src.Length > 0 && src[0] != '}')
			{
				object name = ReadObject(ref src);
				if (!(name is string)) break;
				string key = (string)name;
				src = src.Substring(src.IndexOf(":") + 1);
				dict[key] = ReadObject(ref src);
				src = src.TrimStart(WHITESPACE);
				if (src.StartsWith(",")) src = src.Substring(1);
			}
			src = src.Substring(src.IndexOf("}") + 1);
			return dict;
		}

		if (src[0] == '\"') return ReadString(ref src);
		if (src.StartsWith("null")) return ReadNull(ref src);
		if (src.StartsWith("true") || src.StartsWith("false")) return ReadBool(ref src);
		if (IsNumber(src)) return ReadNumber(ref src);

		throw new ArgumentException("jsontocs encountered unknown type: " + src.Substring(-20, 20) + "^" + src.Substring(0, 64));
	}

	static T ConvertToType<T>(object value) where T : class
	{
		if (value == null) return null;
		if (value is T typedValue) return typedValue;

		// Handle arrays -> List<T>
		if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
		{
			Type elementType = typeof(T).GetGenericArguments()[0];
			var srcList = (value as IEnumerable<object>) ?? (value as object[])?.Cast<object>();
			if (srcList == null) return null;

			var listInstance = (IList)Activator.CreateInstance(typeof(T));
			foreach (var item in srcList)
				listInstance.Add(ConvertValue(elementType, item));
			return listInstance as T;
		}

		if (value is IDictionary<string, object> dict)
		{
			if (typeof(T) == typeof(Dictionary<string, object>))
				return dict as T;

			T instance = Activator.CreateInstance<T>();
			foreach (var pair in dict)
			{
				PropertyInfo prop = typeof(T).GetProperty(pair.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
				if (prop != null && prop.CanWrite)
				{
					object convertedValue = ConvertValue(prop.PropertyType, pair.Value);
					prop.SetValue(instance, convertedValue);
				}
			}
			return instance;
		}

		return value as T;
	}

	static object ConvertValue(Type targetType, object value)
	{
		if (value == null) return null;
		if (targetType.IsInstanceOfType(value)) return value;

		if (targetType.IsArray)
		{
			var elementType = targetType.GetElementType();
			var list = ((IEnumerable<object>)value).ToList();
			var array = Array.CreateInstance(elementType, list.Count);
			for (int i = 0; i < list.Count; i++)
				array.SetValue(ConvertValue(elementType, list[i]), i);
			return array;
		}

		if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
		{
			Type elementType = targetType.GetGenericArguments()[0];
			var listInstance = (IList)Activator.CreateInstance(targetType);
			foreach (var item in (IEnumerable<object>)value)
				listInstance.Add(ConvertValue(elementType, item));
			return listInstance;
		}

		if (value is IDictionary<string, object> dict && !targetType.IsGenericType(typeof(Dictionary<,>)))
		{
			var instance = Activator.CreateInstance(targetType);
			foreach (var pair in dict)
			{
				PropertyInfo prop = targetType.GetProperty(pair.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
				if (prop != null && prop.CanWrite)
					prop.SetValue(instance, ConvertValue(prop.PropertyType, pair.Value));
			}
			return instance;
		}

		if (value is long longValue && targetType == typeof(int))
			return (int)longValue;
		if (value is double doubleValue && targetType == typeof(float))
			return (float)doubleValue;

		return Convert.ChangeType(value, targetType);
	}
}

static class TypeEx
{
	public static bool IsGenericType(this Type type, Type compare) =>
		type.IsGenericType && type.GetGenericTypeDefinition() == compare;
}
