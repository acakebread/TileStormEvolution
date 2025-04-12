using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SerializableXStruct
{
	public string type;
	public string name;
	public string stringValue; // For "string" type
	public bool boolValue;    // For "bool" type
	public float floatValue;  // For "float" type
	public int intValue;      // For "int" type
	public string charValue;  // For "char" type (base64-encoded)
	public List<SerializableXStruct> members; // For "struct", "array", "data"

	public SerializableXStruct()
	{
		members = new List<SerializableXStruct>();
	}
}