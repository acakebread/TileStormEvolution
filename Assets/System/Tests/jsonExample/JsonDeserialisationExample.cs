//using System;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

//public class JsonDeserializationExample : MonoBehaviour
//{
//	void Start()
//	{
//		string jsonString = @"{
//            ""DocumentGroupContainers"": [
//            {
//                ""DocumentGroups"": [
//                {
//                    ""Children"": [
//                    {
//                        ""EditorCaption"": """"
//                    },
//                    {
//                        ""WhenOpened"": ""2025-01-21T12:56:43.65Z""
//                    }
//                    ]
//                }
//                ]
//            }
//            ]
//        }";

//		try
//		{
//			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
//			if (data != null)
//			{
//				var result = new
//				{
//					_DocumentGroupContainers =
//						data.ContainsKey("DocumentGroupContainers")
//						? ((object[])data["DocumentGroupContainers"]).Select(item =>
//						{
//							var subDict = (Dictionary<string, object>)item;
//							return new
//							{
//								_DocumentGroups = subDict.ContainsKey("DocumentGroups")
//									? ((object[])subDict["DocumentGroups"]).Select(innerItem =>
//									{
//										var innerDict = (Dictionary<string, object>)innerItem;
//										return new
//										{
//											_Children = innerDict.ContainsKey("Children")
//												? innerDict["Children"] // always object
//												: (object)null
//										};
//									}).ToArray()
//									: Array.Empty<object>() // consistent type
//							};
//						}).ToArray()
//						: Array.Empty<object>()
//				};

//				// Example debug output
//				Debug.Log("Deserialization succeeded!");
//				var firstChild = ((object[])((dynamic)result._DocumentGroupContainers)[0]._DocumentGroups[0]._Children)[0];
//				Debug.Log($"First child: {firstChild}");
//			}
//			else
//			{
//				Debug.LogError("Failed to deserialize JSON");
//			}
//		}
//		catch (Exception e)
//		{
//			Debug.LogError($"Deserialization error: {e.Message}");
//		}
//	}
//}
