using GrokParser;
using System;
using System.IO;
using UnityEngine;

public class XStructConverter : MonoBehaviour
{
	void Start()
	{
		// Sample hex data (replace with full hex string)
		//string hexData = "0x00, 0x00, 0x80, 0x40, 0x05, 0x00, 0x00, 0x00, 0x73, 0x74, 0x72, 0x75, 0x63, 0x74, 0x00, 0x6D, ..."; // Truncated

		try
		{
			// Convert hex to bytes
			//byte[] data = HexStringToByteArray(hexData);

			var myData = Resources.Load<TextAsset>("database");
			var binaryData = myData.bytes;


			var data = myData.bytes;


			// Parse binary data
			XStruct xstruct = XStructParser.LoadBinary(data);

			// Export to JSON
			string json = XStructJsonExporter.ToJson(xstruct);

			// Save to file
			File.WriteAllText(Application.persistentDataPath + "/output.json", json);
			Debug.Log($"Successfully exported to {Application.persistentDataPath}/output.json");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error: {ex.Message}");
		}
	}

	public static byte[] HexStringToByteArray(string hex)
	{
		hex = hex.Replace("0x", "").Replace(",", "").Trim();
		if (hex.Length % 2 != 0)
			throw new ArgumentException("Hex string length must be even.");

		byte[] bytes = new byte[hex.Length / 2];
		for (int i = 0; i < hex.Length; i += 2)
		{
			bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
		}
		return bytes;
	}
}