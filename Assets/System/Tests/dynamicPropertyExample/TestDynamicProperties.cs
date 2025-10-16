using MassiveHadronLtd;
using UnityEngine;

public class TestDynamicProperties : MonoBehaviour
{
	void Start()
	{
		var dynamicProps = GetComponent<DynamicProperties>();
		if (dynamicProps != null)
		{
			// Add properties programmatically
			dynamicProps.AddFloat("myfloat", 10.5f); // Ensure myfloat is added
			dynamicProps.AddString("Name", "Player");

			// Enumerate all properties
			Debug.Log("All Properties:");
			foreach (var prop in dynamicProps.Properties)
			{
				switch (prop.Type)
				{
					case "float":
						Debug.Log($"Float: {prop.Name} = {prop.FloatValue}");
						break;
					case "int":
						Debug.Log($"Int: {prop.Name} = {prop.IntValue}");
						break;
					case "string":
						Debug.Log($"String: {prop.Name} = {prop.StringValue}");
						break;
					case "bool":
						Debug.Log($"Bool: {prop.Name} = {prop.BoolValue}");
						break;
				}
			}

			// Access a property
			if (dynamicProps.TryGetFloat("myfloat", out float myfloat))
			{
				Debug.Log($"myfloat: {myfloat}");
			}
			else
			{
				Debug.LogWarning("Failed to get myfloat.");
			}

			if (dynamicProps.TryGetString("Name", out string name))
			{
				Debug.Log($"Name: {name}");
			}
		}
	}

	void Update()
	{
		var dynamicProps = GetComponent<DynamicProperties>();
		if (dynamicProps != null)
		{
			// Safely access myfloat
			if (dynamicProps.TryGetFloat("myfloat", out float myfloat))
			{
				Debug.Log($"Update: myfloat = {myfloat}");
			}
			else
			{
				Debug.LogWarning("Update: myfloat not found.");
			}
		}
	}
}