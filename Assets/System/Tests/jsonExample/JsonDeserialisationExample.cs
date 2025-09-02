using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class JsonDeserializationExample : MonoBehaviour
{
	void Start()
	{
		string jsonString = @"{""key"":""value""}";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				var template = new
				{
					_key = (object)null
				};

				var result = new
				{
					_key = data.ContainsKey("key") ? data["key"] : (object)null
				};

				// Display deserialized values
				if (result._key != null && result._key != null) Debug.Log($"key : {result._key}"); // value
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

