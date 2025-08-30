using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class JsonDeserializationExample : MonoBehaviour
{
	void Start()
	{
		string jsonString = @"{
    ""name"": ""John"",
    ""age"": 30,
    ""active"": true,
    ""scores"": [90, 85, 88],
    ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" },
    ""nullField"": null
}

";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				var template = new
				{
					name = "",
					age = 0,
					active = false,
					scores = new int[0],
					address = new
					{
						street = "",
						city = ""
					},
					nullField = (object)null
				};

				var result = new
				{
					name = data.ContainsKey("name") ? data["name"].ToString() : "",
					age = data.ContainsKey("age") ? System.Convert.ToInt32(data["age"]) : 0,
					active = data.ContainsKey("active") ? (bool)data["active"] : false,
					scores = (data.ContainsKey("scores") ? ((object[])data["scores"]).Select(item => System.Convert.ToInt32(item)).ToArray() : new int[0]),
					address = new
					{
						street = data.ContainsKey("address") && ((Dictionary<string, object>)data["address"]).ContainsKey("street") ? ((Dictionary<string, object>)data["address"])["street"].ToString() : "",
						city = data.ContainsKey("address") && ((Dictionary<string, object>)data["address"]).ContainsKey("city") ? ((Dictionary<string, object>)data["address"])["city"].ToString() : ""
					},
					nullField = (object)null
				};

				// Display deserialized values
				Debug.Log($"name: {result.name}"); // John
				Debug.Log($"age: {result.age}"); // 30
				Debug.Log($"active: {result.active}"); // True
				if (result.scores.Length > 0) Debug.Log($"scores[0]: {result.scores[0]}"); // 90
				if (result.scores.Length > 1) Debug.Log($"scores[1]: {result.scores[1]}"); // 85
				if (result.scores.Length > 2) Debug.Log($"scores[2]: {result.scores[2]}"); // 88
				Debug.Log($"address.street: {result.address.street}"); // 123 Main St
				Debug.Log($"address.city: {result.address.city}"); // New York
				Debug.Log($"nullField: {result.nullField}"); // null
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
