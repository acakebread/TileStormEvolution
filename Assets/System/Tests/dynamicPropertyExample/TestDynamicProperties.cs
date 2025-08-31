//using UnityEngine;

//public class TestDynamicProperties : MonoBehaviour
//{
//	void Start()
//	{
//		var dynamicProps = GetComponent<DynamicProperties>();
//		if (dynamicProps != null)
//		{
//			// Add properties programmatically
//			dynamicProps.AddFloat("Speed", 10.5f);
//			dynamicProps.AddString("Name", "Player");

//			// Enumerate all properties
//			Debug.Log("All Properties:");
//			foreach (var prop in dynamicProps.Properties)
//			{
//				switch (prop.Type)
//				{
//					case PropertyType.Float:
//						Debug.Log($"Float: {prop.Name} = {prop.FloatValue}");
//						break;
//					case PropertyType.Int:
//						Debug.Log($"Int: {prop.Name} = {prop.IntValue}");
//						break;
//					case PropertyType.String:
//						Debug.Log($"String: {prop.Name} = {prop.StringValue}");
//						break;
//					case PropertyType.Bool:
//						Debug.Log($"Bool: {prop.Name} = {prop.BoolValue}");
//						break;
//				}
//			}

//			// Access a property
//			if (dynamicProps.TryGetFloat("Speed", out float speed))
//			{
//				Debug.Log($"Speed: {speed}");
//			}

//			if (dynamicProps.TryGetString("Name", out string name))
//			{
//				Debug.Log($"Name: {name}");
//			}
//		}
//	}
//}