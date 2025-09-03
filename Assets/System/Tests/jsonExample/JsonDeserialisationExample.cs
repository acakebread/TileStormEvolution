using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class JsonDeserializationExample : MonoBehaviour
{
	static class J
	{
		public static Dictionary<string, object> D(object o) => o as Dictionary<string, object>;
		public static object[] A(object o) => o as object[];
		public static object G(Dictionary<string, object> d, string k) => (d != null && d.ContainsKey(k)) ? d[k] : null;
	}

	void Start()
	{
		string jsonString = @"[{""$type"": ""Item"", ""id"": 1}, {""$type"": ""Item"", ""id"": 2}]";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				var template = new[] { new { __type = (object)null, _id = (object)null } };
				var result = (J.A(data) != null ? J.A(data).Select((item0, i0) => new { __type = J.G(J.D(item0), "$type"), _id = J.G(J.D(item0), "id") }).ToArray() : new[] { new { __type = (object)null, _id = (object)null } });
				if (result != null) Debug.Log($" (array length): {result.Length}");
				if (result != null)
				{
					for (int i0 = 0; i0 < result.Length; i0++)
					{
						if (result[i0].__type != null) Debug.Log($"[{{i0}}].$type : {result[i0].__type}");
						if (result[i0]._id != null) Debug.Log($"[{{i0}}].id : {result[i0]._id}");
					}
				}
			}
			else
			{
				Debug.LogError("Failed to deserialize JSON");
			}
		}
		catch (System.Exception e)
		{
			Debug.LogError($"Deserialization error: {e.Message}");
		}
	}
}
