using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class JsonDeserializationExample : MonoBehaviour
{
	void Start()
	{
		string jsonString = @"{
  ""Groups"": [
    {
      ""Items"": [
        {
          ""$type"": ""Document"",
          ""DocumentIndex"": 0,
          ""Title"": ""Test.cs""
        },
        {
          ""$type"": ""Bookmark"",
          ""Name"": ""ST:1:0:{12345678-1234-1234-1234-1234567890ab}""
        }
      ]
    }
  ]
}

";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				var template = new
				{
					Groups = new[] {           new
		  {
			Items = new[] {             new
			{
			  _type = (object)null,
			  DocumentIndex = (object)null,
			  Name = (object)null,
			  Title = (object)null
			} }
		  } }
				};

				var result = new
				{
					Groups = (data.ContainsKey("Groups") ? ((object[])data["Groups"]).Select((item, j) =>
					{
						var subDict = (Dictionary<string, object>)item;
						return new
						{
							Items = subDict.ContainsKey("Items") ? ((object[])subDict["Items"]).Select((innerItem, k) =>
							{
								var innerDict = (Dictionary<string, object>)innerItem;
								return new
								{
									_type = innerDict.ContainsKey("$type") ? innerDict["$type"] : (object)null,
									DocumentIndex = innerDict.ContainsKey("DocumentIndex") ? innerDict["DocumentIndex"] : (object)null,
									Name = innerDict.ContainsKey("Name") ? innerDict["Name"] : (object)null,
									Title = innerDict.ContainsKey("Title") ? innerDict["Title"] : (object)null
								};
							}).ToArray() : new[] {                   new
				  {
					_type = (object)null,
					DocumentIndex = (object)null,
					Name = (object)null,
					Title = (object)null
				  } }
						};
					}).ToArray() : new[] {               new
			  {
				Items = new[] {                 new
				{
				  _type = (object)null,
				  DocumentIndex = (object)null,
				  Name = (object)null,
				  Title = (object)null
				} }
			  } })
				};

				// Display deserialized values
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 0 && result.Groups[0].Items[0]._type != null) Debug.Log($"Groups[0].Items[0].$type : {result.Groups[0].Items[0]._type}"); // "Document"
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 0) Debug.Log($"Groups[0].Items[0].DocumentIndex : {result.Groups[0].Items[0].DocumentIndex}"); // 0
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 0 && result.Groups[0].Items[0].Title != null) Debug.Log($"Groups[0].Items[0].Title : {result.Groups[0].Items[0].Title}"); // "Test.cs"
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 1 && result.Groups[0].Items[1]._type != null) Debug.Log($"Groups[0].Items[1].$type : {result.Groups[0].Items[1]._type}"); // "Bookmark"
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 1 && result.Groups[0].Items[1].Name != null) Debug.Log($"Groups[0].Items[1].Name : {result.Groups[0].Items[1].Name}"); // "ST:1:0:{12345678-1234-1234-1234-1234567890ab}"
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 0 && result.Groups[0].Items[0]._type != null) Debug.Log($"Groups[0].Items[0].$type : {result.Groups[0].Items[0]._type}"); // "Document"
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 0) Debug.Log($"Groups[0].Items[0].DocumentIndex : {result.Groups[0].Items[0].DocumentIndex}"); // 0
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 0 && result.Groups[0].Items[0].Title != null) Debug.Log($"Groups[0].Items[0].Title : {result.Groups[0].Items[0].Title}"); // "Test.cs"
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 1 && result.Groups[0].Items[1]._type != null) Debug.Log($"Groups[0].Items[1].$type : {result.Groups[0].Items[1]._type}"); // "Bookmark"
				if (result.Groups != null && result.Groups.Length > 0 && result.Groups[0].Items != null && result.Groups[0].Items.Length > 1 && result.Groups[0].Items[1].Name != null) Debug.Log($"Groups[0].Items[1].Name : {result.Groups[0].Items[1].Name}"); // "ST:1:0:{12345678-1234-1234-1234-1234567890ab}"
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
