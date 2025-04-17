using MassiveHadron;
using System;
using UnityEngine;

public class XStructTest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    { 
		var myData = Resources.Load<TextAsset>("database");
		var binaryData = myData.bytes;


		var data = myData.bytes;

		byte[] strippedData = new byte[data.Length - 4];
		Array.Copy(data, 4, strippedData, 0, strippedData.Length);



		var xstruct = new XStruct();
		var db = xstruct.InitFromStream(strippedData);
        Debug.Log("loaded?");
	}
}
