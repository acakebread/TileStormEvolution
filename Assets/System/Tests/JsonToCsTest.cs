using UnityEngine;

[RequireComponent(typeof(JsonInspectorUtility))]
public class JsonToCsTest : MonoBehaviour
{
	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
        ""name"": ""John"",
        ""age"": 30,
        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
    }";

	private JsonInspectorUtility utility;

	void OnValidate()
	{
		// Get or add JsonInspectorUtility
		if (utility == null)
		{
			utility = GetComponent<JsonInspectorUtility>();
		}
		// Set JSON input
		utility.SetJsonInput(jsonInput);
	}
}
