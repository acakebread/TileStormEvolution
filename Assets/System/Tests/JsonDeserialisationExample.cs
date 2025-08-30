using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class JsonDeserializationExample : MonoBehaviour
{
	void Start()
	{
		string jsonString = @"[
    { ""name"": ""John"", ""age"": 30 },
    { ""name"": ""Jane"", ""age"": 25, ""scores"": [90, 85, 88] }
]
";
		try
		{
			var data = JsonTocs.FromJson<List<object>>(jsonString);
			if (data != null)
			{
				var template = new[] {         new
		{
		  name = "",
		  age = 0,
		  scores = new int[0]
		} };

				var result = ((List<object>)data).Select((item, i) =>
				{
					var dict = (Dictionary<string, object>)item;
					return new
					{
						name = dict.ContainsKey("name") ? dict["name"].ToString() : "",
						age = dict.ContainsKey("age") ? System.Convert.ToInt32(dict["age"]) : 0,
						scores = (dict.ContainsKey("scores") ? ((object[])dict["scores"]).Select(item => System.Convert.ToInt32(item)).ToArray() : new int[0])
					};
				}).ToArray();

				// Display deserialized values
				if (result.Length > 0 && result[0].name != null) Debug.Log($"[0].name: {result[0].name}"); // John
				if (result.Length > 0) Debug.Log($"[0].age: {result[0].age}"); // 30
				if (result.Length > 1 && result[1].name != null) Debug.Log($"[1].name: {result[1].name}"); // Jane
				if (result.Length > 1) Debug.Log($"[1].age: {result[1].age}"); // 25
				if (result.Length > 1 && result[1].scores != null) Debug.Log($"[1].scores: {string.Join(", ", result[1].scores)}"); // [90,85,88]
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
