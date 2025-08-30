using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class JsonToCsTest : MonoBehaviour
{
	[SerializeField, TextArea(3, 10)] private string jsonInput = @"{
        ""name"": ""John"",
        ""age"": 30,
        ""address"": { ""street"": ""123 Main St"", ""city"": ""New York"" }
    }";

	[SerializeField, TextArea(5, 20), Header("C# Deserialization Code (Read-Only)")]
	private string cSharpRepresentation = "";
	public string CSharpRepresentation => cSharpRepresentation;
	private object Data { get; set; }

	public void OnValidate()
	{
		Data = null;
		cSharpRepresentation = "No data deserialized";

		if (string.IsNullOrEmpty(jsonInput))
		{
			return;
		}

		try
		{
			Data = JsonTocs.FromJson<object>(jsonInput);
			if (Data != null)
			{
				cSharpRepresentation = jsontocs_usagegenerator.GenerateDeserializationCode(Data, jsonInput);
			}
			else
			{
				cSharpRepresentation = "Error: Failed to deserialize JSON - null result";
			}
		}
		catch (System.Exception e)
		{
			cSharpRepresentation = $"Error: Failed to deserialize JSON - {e.Message}";
			Data = null;
		}
	}

	void Start()
	{
		OnValidate();
		if (Data != null)
		{
			Debug.Log($"Deserialized data: {JsonTocs.ToJson(Data)}");
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(JsonToCsTest))]
	private class JsonToCsTestEditor : Editor
	{
		private Vector2 inputScrollPosition;
		private Vector2 outputScrollPosition;

		public override void OnInspectorGUI()
		{
			JsonToCsTest test = (JsonToCsTest)target;

			EditorGUILayout.LabelField("JSON Input", EditorStyles.boldLabel);
			EditorGUI.BeginChangeCheck();
			inputScrollPosition = EditorGUILayout.BeginScrollView(inputScrollPosition, GUILayout.Height(100));
			string jsonInput = EditorGUILayout.TextArea(test.jsonInput, GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(test, "Change JSON Input");
				// Update the serialized field and mark the object as dirty
				SerializedObject serializedObject = new SerializedObject(test);
				serializedObject.FindProperty("jsonInput").stringValue = jsonInput;
				serializedObject.ApplyModifiedProperties();
				EditorUtility.SetDirty(test);
			}

			EditorGUILayout.LabelField("C# Deserialization Code (Read-Only)", EditorStyles.boldLabel);
			outputScrollPosition = EditorGUILayout.BeginScrollView(outputScrollPosition, GUILayout.Height(200));
			EditorGUILayout.TextArea(test.CSharpRepresentation, GUILayout.ExpandHeight(true));
			EditorGUILayout.EndScrollView();

			if (GUILayout.Button("Copy Script"))
			{
				GUIUtility.systemCopyBuffer = test.CSharpRepresentation;
				Debug.Log("Deserialization script copied to clipboard");
			}
		}
	}
#endif
}