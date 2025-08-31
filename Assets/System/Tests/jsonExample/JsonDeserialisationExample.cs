using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO.Pipes;

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
}";
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
			  Title = (object)null
			} }
		  } }
				};

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

		//transform.GetComponent<Anonymous>().TryGetFloat(out myProperty);
		//or if that is not possible because it needs the label, something like
		//transform.GetComponent<Anonymous>().TryGetFloat("myProperty", out myProperty);
		//other functions like
		//transform.GetComponent<Anonymous>().HasFloat("myProperty");
		//or
		//var myProperty = transform.GetComponent<Anonymous>().GetFloat("myProperty");
		//transform.GetComponent<Anonymous>().GetFloat(out myProperty);
		//or
		//transform.GetComponent<Anonymous>().GetFloat("myProperty", out myProperty);
	}
}
