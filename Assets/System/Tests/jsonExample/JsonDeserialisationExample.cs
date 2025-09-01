using UnityEngine;
using System.Collections.Generic;

public class JsonDeserializationExample : MonoBehaviour
{
	void Start()
	{
		string jsonString = @"{""@metadata"": ""info"", ""#count"": 5, ""user-name"": ""alice""}";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				var template = new
				{
					_count = (object)null,
					_metadata = (object)null,
					user_name = (object)null
				};

				var result = new
				{
					_count = data.ContainsKey("#count") ? data["#count"] : (object)null,
					_metadata = data.ContainsKey("@metadata") ? data["@metadata"] : (object)null,
					user_name = data.ContainsKey("user-name") ? data["user-name"] : (object)null
				};

				// Display deserialized values
				if (true) Debug.Log($"#count : {result._count}"); // 5
				if (result._metadata != null) Debug.Log($"@metadata : {result._metadata}"); // "info"
				if (result.user_name != null) Debug.Log($"user-name : {result.user_name}"); // "alice"
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

