using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class JsonDeserializationExample : MonoBehaviour
{
	void Start()
	{
		string jsonString = @"{
            ""deep"": {
                ""a"": [
                {
                    ""b"": {
                        ""c"": [
                        {
                            ""d"": {
                                ""e"": ""value""
                            }
                        }
                        ]
                    }
                },
                {
                    ""b"": {
                        ""c"": []
                    }
                }
                ],
                ""x"": ""\n\t""
            }
        }";
		try
		{
			var data = JsonTocs.FromJson<Dictionary<string, object>>(jsonString);
			if (data != null)
			{
				var template = new
				{
					deep = new
					{
						a = new[] { new
						{
							b = new
							{
								c = new[] { new
								{
									d = new
									{
										e = (object)null
									}
								} }
							}
						} },
						x = (object)null
					}
				};

				var result = new
				{
					deep = (data.ContainsKey("deep") && data["deep"] != null ? new
					{
						a = (data.ContainsKey("deep") && ((Dictionary<string, object>)data["deep"]).ContainsKey("a") ? ((object[])((Dictionary<string, object>)data["deep"])["a"]).Select((item, j) =>
						{
							var subDict = (Dictionary<string, object>)item;
							return new
							{
								b = (subDict.ContainsKey("b") && subDict["b"] != null ? new
								{
									c = (subDict.ContainsKey("b") && ((Dictionary<string, object>)subDict["b"]).ContainsKey("c") ? ((object[])((Dictionary<string, object>)subDict["b"])["c"]).Select((item, j) =>
									{
										var subDict = (Dictionary<string, object>)item;
										return new
										{
											d = (subDict.ContainsKey("d") && subDict["d"] != null ? new
											{
												e = (subDict.ContainsKey("d") && ((Dictionary<string, object>)subDict["d"]).ContainsKey("e") ? ((Dictionary<string, object>)subDict["d"])["e"] : (object)null)
											} : new
											{
												e = (object)null
											})
										};

									}).ToArray() : new[] { new
									{
										d = new
										{
											e = (object)null
										}
									} })
								} : new
								{
									c = new[] { new
									{
										d = new
										{
											e = (object)null
										}
									} }
								})
							};

						}).ToArray() : new[] { new
						{
							b = new
							{
								c = new[] { new
								{
									d = new
									{
										e = (object)null
									}
								} }
							}
						} }),
						x = (data.ContainsKey("deep") && ((Dictionary<string, object>)data["deep"]).ContainsKey("x") ? ((Dictionary<string, object>)data["deep"])["x"] : (object)null)
					} : new
					{
						a = new[] { new
						{
							b = new
							{
								c = new[] { new
								{
									d = new
									{
										e = (object)null
									}
								} }
							}
						} },
						x = (object)null
					})
				};

				// Display deserialized values
				if (result.deep != null && result.deep.a != null) Debug.Log($"deep.a : {string.Join(", ", (object[])result.deep.a)}"); // [...]
				if (result.deep != null && result.deep.x != null) Debug.Log($"deep.x : {result.deep.x}"); //

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

