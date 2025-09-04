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
		string jsonString = @"{
    ""team"": ""Alpha"",
    ""members"": [
        { ""name"": ""John"", ""roles"": [""Leader"", ""Developer""] },
        { ""name"": ""Jane"", ""roles"": [""Tester""], ""details"": { ""level"": 3, ""active"": true } }
    ]
}";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				var template = new { _members = new[] { new { _name = (object)null, _roles = new[] { (object)null } } }, _team = (object)null };
				var result = new { _members = (J.A(J.G(data, "members")) != null ? J.A(J.G(data, "members")).Select((item0, i0) => new { _name = J.G(J.D(item0), "name"), _roles = (J.A(J.G(J.D(item0), "roles")) != null ? J.A(J.G(J.D(item0), "roles")).Select((item1, i1) => J.D(item1)).ToArray() : new[] { (object)null }) }).ToArray() : new[] { new { _name = (object)null, _roles = new[] { (object)null } } }), _team = J.G(data, "team") };
				if (result._members != null) Debug.Log($"members (array length): {result._members.Length}");
				if (result._members != null)
				{
					for (int i0 = 0; i0 < result._members.Length; i0++)
					{
						if (result._members[i0]._name != null) Debug.Log($"members[{{i0}}].name : {result._members[i0]._name}");
						if (result._members[i0]._roles != null) Debug.Log($"members[{{i0}}].roles (array length): {result._members[i0]._roles.Length}");
						if (result._members[i0]._roles != null)
						{
							for (int i1 = 0; i1 < result._members[i0]._roles.Length; i1++)
							{
								if (result._members[i0]._roles[i1] != null) Debug.Log($"members[{{i0}}].roles[{{i1}}] : {result._members[i0]._roles[i1]}");
							}
						}
					}
				}
				if (result._team != null) Debug.Log($"team : {result._team}");
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
