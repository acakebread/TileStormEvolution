using System.Collections.Generic;
using UnityEngine;

public class JsonDeserializationExample : MonoBehaviour
{
	void Start()
	{
		string jsonString = @"{
        ""name"": ""John"",
        ""age"": 30,
        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
}";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				// Anonymous type template
				var template = new
				{
					name = "",
					age = 0,
					address = new
					{
						street = "",
						city = ""
					}
				};

				// Create anonymous type with deserialized values
				var result = new
				{
					name = data["name"].ToString(),
					age = data["age"] is double ? (int)(double)data["age"] : data["age"] is long ? (int)(long)data["age"] : (int)data["age"],
					address = new
					{
						street = ((Dictionary<string, object>)data["address"])["street"].ToString(),
						city = ((Dictionary<string, object>)data["address"])["city"].ToString()
					}
				};

				// Type-safe access example
				Debug.Log($"name: {result.name}"); // John
				Debug.Log($"age: {result.age}"); // 30
				Debug.Log($"street: {result.address.street}"); // 123 Main St
				Debug.Log($"city: {result.address.city}"); // New York
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
