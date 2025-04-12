using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class DatabaseParser : MonoBehaviour
{
	void Start()
	{
		string filePath = Application.streamingAssetsPath + "/database.bin";
		try
		{
			string jsonOutput = ParseDatabase(filePath);
			string outputPath = Application.persistentDataPath + "/output.json";
			File.WriteAllText(outputPath, jsonOutput);
			Debug.Log($"Exported to {outputPath}\n{jsonOutput}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error parsing file: {ex.Message}");
		}
	}

	public static string ParseDatabase(string filePath)
	{
		using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
		using (var reader = new BinaryReader(stream, Encoding.UTF8))
		{
			float version = reader.ReadSingle();
			Debug.Log($"Version: {version}");

			int nSize = reader.ReadInt32();
			Debug.Log($"Root element count: {nSize}, Position={reader.BaseStream.Position}");

			Element root = new Element
			{
				Type = "struct",
				Name = "",
				Children = ParseNestedElements(reader, null, nSize)
			};

			return BuildJson(root);
		}
	}

	[Serializable]
	public class Element
	{
		public string Type;
		public string Name;
		public List<Element> Children;
		public string StringValue;
		public float NumberValue;
		public string[] BytesValue;
	}

	private static Element ParseElement(BinaryReader reader, List<Element> siblings)
	{
		string type = ReadString(reader);
		string name = ReadString(reader);
		Debug.Log($"Parsing: Type={type}, Name={name}, Position={reader.BaseStream.Position}");

		Element element = new Element { Type = type, Name = name };

		switch (type.ToLower())
		{
			case "struct":
			case "array":
			case "data":
				element.Children = ParseNestedElements(reader, element);
				break;
			case "string":
				element.StringValue = ReadString(reader);
				break;
			case "bool":
				element.NumberValue = reader.ReadBoolean() ? 1.0f : 0.0f;
				break;
			case "float":
				element.NumberValue = reader.ReadSingle();
				break;
			case "int":
				element.NumberValue = reader.ReadInt32();
				break;
			case "char":
				break;
			default:
				throw new Exception($"Unknown type: {type}");
		}

		return element;
	}

	private static List<Element> ParseNestedElements(BinaryReader reader, Element parent, int? nSize = null)
	{
		int count = nSize ?? reader.ReadInt32();
		Debug.Log($"Nested elements count: {count}, Position={reader.BaseStream.Position}");
		List<Element> children = new List<Element>(count);

		for (int i = 0; i < count; i++)
		{
			if (reader.BaseStream.Position >= reader.BaseStream.Length)
				throw new Exception($"Unexpected end of file at {i}/{count}");
			Element child = ParseElement(reader, children);
			children.Add(child);
		}

		foreach (var child in children)
		{
			if (child.Type.ToLower() == "char")
			{
				child.BytesValue = ParseCharData(reader, children);
			}
		}

		return children;
	}

	private static string[] ParseCharData(BinaryReader reader, List<Element> siblings)
	{
		int nCompression = (int)(siblings.Find(e => e.Name == "nCompression")?.NumberValue ?? 0x20);
		int nCompressedLength = (int)(siblings.Find(e => e.Name == "nCompressedLength")?.NumberValue ?? 0);
		int nBits = nCompression & 0xF8;
		int byteLength = nCompressedLength * (nBits >> 3);

		Debug.Log($"Char data: nCompression={nCompression}, nCompressedLength={nCompressedLength}, nBits={nBits}, Calculated Length={byteLength}");

		if (byteLength <= 0 || reader.BaseStream.Position + byteLength > reader.BaseStream.Length)
		{
			Debug.LogWarning($"Invalid char length: {byteLength}, using remaining bytes");
			byteLength = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
		}

		byte[] bytes = reader.ReadBytes(byteLength);
		Debug.Log($"Char bytes: Length={byteLength}, Data={BitConverter.ToString(bytes)}");
		return Array.ConvertAll(bytes, b => b.ToString("X2"));
	}

	private static string ReadString(BinaryReader reader)
	{
		var chars = new List<char>();
		char c;
		while ((c = reader.ReadChar()) != '\0')
			chars.Add(c);
		return new string(chars.ToArray());
	}

	private static int GetInteger(byte[] bytes, int offset, int nBits)
	{
		switch (nBits)
		{
			case 8: return (sbyte)bytes[offset]; // Signed 8-bit
			case 16: return BitConverter.ToInt16(bytes, offset);
			case 32: return BitConverter.ToInt32(bytes, offset);
			default:
				Debug.LogError($"Unsupported nBits: {nBits}");
				return 0;
		}
	}

	private static int[] DecompressBytes(Element nTileIndexElement, Element tilesElement)
	{
		int nCompression = (int)(nTileIndexElement.Children.Find(e => e.Name == "nCompression")?.NumberValue ?? 0x20);
		int nCompressedLength = (int)(nTileIndexElement.Children.Find(e => e.Name == "nCompressedLength")?.NumberValue ?? 0);
		int nUncompressedLength = (int)(nTileIndexElement.Children.Find(e => e.Name == "nUncompressedLength")?.NumberValue ?? 0);
		int nAdjust = (int)(nTileIndexElement.Children.Find(e => e.Name == "nAdjust")?.NumberValue ?? 0);
		string[] compressedHex = nTileIndexElement.Children.Find(e => e.Name == "bytes")?.BytesValue;
		int nWidth = (int)(tilesElement.Children.Find(e => e.Name == "nWidth")?.NumberValue ?? 0);
		int nHeight = (int)(tilesElement.Children.Find(e => e.Name == "nHeight")?.NumberValue ?? 0);

		if (compressedHex == null || nWidth == 0 || nHeight == 0)
		{
			Debug.LogError("Missing required fields for decompression");
			return new int[0];
		}

		byte[] compressed = Array.ConvertAll(compressedHex, s => Convert.ToByte(s, 16));
		int[] decompressed = new int[nUncompressedLength];
		int expectedSize = nWidth * nHeight;

		if (nUncompressedLength != expectedSize)
		{
			Debug.LogWarning($"Uncompressed length {nUncompressedLength} does not match expected size {expectedSize}");
		}

		int nBits = nCompression & 0xF8;
		if (nCompression == 0) nCompression = 0x20; // No compression default
		int bytesPerValue = nBits >> 3;
		int pos = 0; // Output position
		int byteOffset = 0; // Input position

		while (pos < nUncompressedLength && byteOffset < compressed.Length)
		{
			int nVal = GetInteger(compressed, byteOffset, nBits);
			byteOffset += bytesPerValue;
			int nRun = 1;

			if ((nCompression & 1) == 1 && byteOffset < compressed.Length) // RLE enabled
			{
				if (nVal < 0)
				{
					nRun = -nVal;
					nVal = GetInteger(compressed, byteOffset, nBits);
					byteOffset += bytesPerValue;
				}
			}

			for (int i = 0; i < nRun && pos < nUncompressedLength; i++)
			{
				decompressed[pos++] = nVal + nAdjust;
			}
		}

		if (pos != nUncompressedLength)
		{
			Debug.LogError($"Decompression failed: Expected {nUncompressedLength} elements, got {pos}");
		}

		return decompressed;
	}

	private static string BuildJson(Element element)
	{
		StringBuilder sb = new StringBuilder();
		BuildJsonElement(sb, element, 0, null);
		return sb.ToString();
	}

	private static void BuildJsonElement(StringBuilder sb, Element element, int indentLevel, Element parent, bool isArrayElement = false)
	{
		string indent = new string(' ', indentLevel * 2);

		if (!isArrayElement && !string.IsNullOrEmpty(element.Name))
		{
			sb.Append($"{indent}\"{element.Name}\": ");
		}

		switch (element.Type.ToLower())
		{
			case "struct":
			case "data":
				if (element.Name == "defs" || element.Name == "tiledefs")
				{
					sb.Append("[\n");
					if (element.Children != null)
					{
						for (int i = 0; i < element.Children.Count; i++)
						{
							BuildJsonElement(sb, element.Children[i], indentLevel + 1, element, true);
							if (i < element.Children.Count - 1) sb.Append(",");
							sb.Append("\n");
						}
					}
					sb.Append($"{indent}]");
				}
				else
				{
					sb.Append("{\n");
					if (element.Children != null)
					{
						for (int i = 0; i < element.Children.Count; i++)
						{
							BuildJsonElement(sb, element.Children[i], indentLevel + 1, element);
							if (i < element.Children.Count - 1) sb.Append(",");
							sb.Append("\n");
						}
						// Add unpacked bytes for "nTileIndex" under "tiles" or "mixed"
						if (element.Name == "nTileIndex" && parent != null && (parent.Name == "tiles" || parent.Name == "mixed"))
						{
							int[] unpacked = DecompressBytes(element, parent);
							if (unpacked.Length > 0)
							{
								sb.Append($",\n{indent}  \"unpacked_bytes\": [");
								for (int j = 0; j < unpacked.Length; j++)
								{
									sb.Append(unpacked[j].ToString());
									if (j < unpacked.Length - 1) sb.Append(", ");
								}
								sb.Append("]\n");
							}
						}
					}
					sb.Append($"{indent}}}");
				}
				break;
			case "array":
				if (element.Name == "tiles" || element.Name == "mixed" || element.Name == "nTileIndex")
				{
					sb.Append("{\n");
					if (element.Children != null)
					{
						for (int i = 0; i < element.Children.Count; i++)
						{
							BuildJsonElement(sb, element.Children[i], indentLevel + 1, element);
							if (i < element.Children.Count - 1) sb.Append(",");
							sb.Append("\n");
						}
					}
					sb.Append($"{indent}}}");
				}
				else
				{
					sb.Append("[\n");
					if (element.Children != null)
					{
						for (int i = 0; i < element.Children.Count; i++)
						{
							BuildJsonElement(sb, element.Children[i], indentLevel + 1, element, true);
							if (i < element.Children.Count - 1) sb.Append(",");
							sb.Append("\n");
						}
					}
					sb.Append($"{indent}]");
				}
				break;
			case "string":
				sb.Append($"\"{element.StringValue}\"");
				break;
			case "bool":
				sb.Append(element.NumberValue > 0 ? "true" : "false");
				break;
			case "float":
				sb.Append(element.NumberValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
				break;
			case "int":
				sb.Append((int)element.NumberValue);
				break;
			case "char":
				sb.Append("[");
				if (element.BytesValue != null)
				{
					for (int i = 0; i < element.BytesValue.Length; i++)
					{
						sb.Append($"\"{element.BytesValue[i]}\"");
						if (i < element.BytesValue.Length - 1) sb.Append(", ");
					}
				}
				sb.Append("]");
				break;
		}
	}
}


//          "unpacked_bytes": [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 23, 9, 0, 12, 23, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 23, 9, 0, 0, 12, 23, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 29, 6, 6, 6, 25, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 18, 6, 6, 6, 6, 28, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 2, 1, 0, 3, 10, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 10, 1, 0, 0, 3, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 23, 9, 10, 12, 23, 9, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 15, 15, 10, 27, 15, 4, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 15, 24, 19, 24, 15, 1, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 15, 24, 19, 24, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 15, 24, 19, 24, 15, 0, 0, 0, 12, 23, 9, 0, 0, 12, 10, 23, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 18, 6, 26, 6, 25, 0, 0, 0, 8, 15, 15, 15, 15, 15, 10, 15, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 13, 10, 13, 10, 0, 0, 0, 3, 15, 24, 24, 24, 24, 19, 15, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 23, 9, 10, 11, 10, 11, 10, 12, 23, 9, 0, 15, 24, 24, 24, 24, 19, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 18, 6, 14, 6, 14, 6, 14, 6, 25, 4, 0, 15, 24, 24, 24, 24, 19, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 10, 13, 10, 13, 10, 13, 10, 13, 10, 1, 0, 15, 24, 24, 24, 24, 19, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 23, 9, 10, 11, 10, 11, 10, 11, 10, 11, 10, 0, 12, 22, 21, 20, 21, 20, 19, 15, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 18, 6, 14, 6, 14, 6, 14, 6, 14, 6, 14, 6, 6, 6, 16, 17, 16, 17, 16, 15, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 10, 13, 10, 13, 10, 13, 10, 13, 10, 13, 10, 0, 8, 15, 15, 15, 15, 15, 15, 15, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 10, 11, 10, 11, 10, 11, 10, 11, 10, 11, 10, 9, 3, 2, 1, 0, 0, 0, 3, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 7, 6, 14, 6, 14, 6, 14, 6, 14, 6, 5, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 2, 1, 10, 13, 10, 13, 10, 13, 10, 3, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 10, 11, 10, 11, 10, 11, 10, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 7, 6, 14, 6, 14, 6, 5, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 2, 1, 10, 13, 10, 3, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 12, 10, 11, 10, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 7, 6, 5, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 2, 2, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]


//using UnityEngine;
//using System;
//using System.IO;
//using System.Text;
//using System.Collections.Generic;

//public class DatabaseParser : MonoBehaviour
//{
//	void Start()
//	{
//		string filePath = Application.streamingAssetsPath + "/database.bin";
//		try
//		{
//			string jsonOutput = ParseDatabase(filePath);
//			string outputPath = Application.persistentDataPath + "/output.json";
//			File.WriteAllText(outputPath, jsonOutput);
//			Debug.Log($"Exported to {outputPath}\n{jsonOutput}");
//		}
//		catch (Exception ex)
//		{
//			Debug.LogError($"Error parsing file: {ex.Message}");
//		}
//	}

//	public static string ParseDatabase(string filePath)
//	{
//		using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
//		using (var reader = new BinaryReader(stream, Encoding.UTF8))
//		{
//			float version = reader.ReadSingle();
//			Debug.Log($"Version: {version}");

//			int nSize = reader.ReadInt32();
//			Debug.Log($"Root element count: {nSize}, Position={reader.BaseStream.Position}");

//			Element root = new Element
//			{
//				Type = "struct",
//				Name = "",
//				Children = ParseNestedElements(reader, null, nSize)
//			};

//			return BuildJson(root);
//		}
//	}

//	[Serializable]
//	public class Element
//	{
//		public string Type;
//		public string Name;
//		public List<Element> Children;
//		public string StringValue;
//		public float NumberValue;
//		public string[] BytesValue;
//	}

//	private static Element ParseElement(BinaryReader reader, List<Element> siblings)
//	{
//		string type = ReadString(reader);
//		string name = ReadString(reader);
//		Debug.Log($"Parsing: Type={type}, Name={name}, Position={reader.BaseStream.Position}");

//		Element element = new Element { Type = type, Name = name };

//		switch (type.ToLower())
//		{
//			case "struct":
//			case "array":
//			case "data":
//				element.Children = ParseNestedElements(reader, element);
//				break;
//			case "string":
//				element.StringValue = ReadString(reader);
//				break;
//			case "bool":
//				element.NumberValue = reader.ReadBoolean() ? 1.0f : 0.0f;
//				break;
//			case "float":
//				element.NumberValue = reader.ReadSingle();
//				break;
//			case "int":
//				element.NumberValue = reader.ReadInt32();
//				break;
//			case "char":
//				break;
//			default:
//				throw new Exception($"Unknown type: {type}");
//		}

//		return element;
//	}

//	private static List<Element> ParseNestedElements(BinaryReader reader, Element parent, int? nSize = null)
//	{
//		int count = nSize ?? reader.ReadInt32();
//		Debug.Log($"Nested elements count: {count}, Position={reader.BaseStream.Position}");
//		List<Element> children = new List<Element>(count);

//		for (int i = 0; i < count; i++)
//		{
//			if (reader.BaseStream.Position >= reader.BaseStream.Length)
//				throw new Exception($"Unexpected end of file at {i}/{count}");
//			Element child = ParseElement(reader, children);
//			children.Add(child);
//		}

//		foreach (var child in children)
//		{
//			if (child.Type.ToLower() == "char")
//			{
//				child.BytesValue = ParseCharData(reader, children);
//			}
//		}

//		return children;
//	}

//	private static string[] ParseCharData(BinaryReader reader, List<Element> siblings)
//	{
//		int nCompression = (int)(siblings.Find(e => e.Name == "nCompression")?.NumberValue ?? 0x20);
//		int nCompressedLength = (int)(siblings.Find(e => e.Name == "nCompressedLength")?.NumberValue ?? 0);
//		int nBits = nCompression & 0xF8;
//		int byteLength = nCompressedLength * (nBits >> 3);

//		Debug.Log($"Char data: nCompression={nCompression}, nCompressedLength={nCompressedLength}, nBits={nBits}, Calculated Length={byteLength}");

//		if (byteLength <= 0 || reader.BaseStream.Position + byteLength > reader.BaseStream.Length)
//		{
//			Debug.LogWarning($"Invalid char length: {byteLength}, using remaining bytes");
//			byteLength = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
//		}

//		byte[] bytes = reader.ReadBytes(byteLength);
//		Debug.Log($"Char bytes: Length={byteLength}, Data={BitConverter.ToString(bytes)}");
//		return Array.ConvertAll(bytes, b => b.ToString("X2"));
//	}

//	private static string ReadString(BinaryReader reader)
//	{
//		var chars = new List<char>();
//		char c;
//		while ((c = reader.ReadChar()) != '\0')
//			chars.Add(c);
//		return new string(chars.ToArray());
//	}

//	private static string BuildJson(Element element)
//	{
//		StringBuilder sb = new StringBuilder();
//		BuildJsonElement(sb, element, 0);
//		return sb.ToString();
//	}

//	private static void BuildJsonElement(StringBuilder sb, Element element, int indentLevel, bool isArrayElement = false)
//	{
//		string indent = new string(' ', indentLevel * 2);

//		if (!isArrayElement && !string.IsNullOrEmpty(element.Name))
//		{
//			sb.Append($"{indent}\"{element.Name}\": ");
//		}

//		switch (element.Type.ToLower())
//		{
//			case "struct":
//			case "data":
//				if (element.Name == "defs" || element.Name == "tiledefs") // Treat "defs" and "tiledefs" as arrays
//				{
//					sb.Append("[\n");
//					if (element.Children != null)
//					{
//						for (int i = 0; i < element.Children.Count; i++)
//						{
//							BuildJsonElement(sb, element.Children[i], indentLevel + 1, true);
//							if (i < element.Children.Count - 1) sb.Append(",");
//							sb.Append("\n");
//						}
//					}
//					sb.Append($"{indent}]");
//				}
//				else // Other structs remain objects
//				{
//					sb.Append("{\n");
//					if (element.Children != null)
//					{
//						for (int i = 0; i < element.Children.Count; i++)
//						{
//							BuildJsonElement(sb, element.Children[i], indentLevel + 1);
//							if (i < element.Children.Count - 1) sb.Append(",");
//							sb.Append("\n");
//						}
//					}
//					sb.Append($"{indent}}}");
//				}
//				break;
//			case "array":
//				if (element.Name == "tiles" || element.Name == "mixed" || element.Name == "nTileIndex")
//				{
//					sb.Append("{\n");
//					if (element.Children != null)
//					{
//						for (int i = 0; i < element.Children.Count; i++)
//						{
//							BuildJsonElement(sb, element.Children[i], indentLevel + 1);
//							if (i < element.Children.Count - 1) sb.Append(",");
//							sb.Append("\n");
//						}
//					}
//					sb.Append($"{indent}}}");
//				}
//				else
//				{
//					sb.Append("[\n");
//					if (element.Children != null)
//					{
//						for (int i = 0; i < element.Children.Count; i++)
//						{
//							BuildJsonElement(sb, element.Children[i], indentLevel + 1, true);
//							if (i < element.Children.Count - 1) sb.Append(",");
//							sb.Append("\n");
//						}
//					}
//					sb.Append($"{indent}]");
//				}
//				break;
//			case "string":
//				sb.Append($"\"{element.StringValue}\"");
//				break;
//			case "bool":
//				sb.Append(element.NumberValue > 0 ? "true" : "false");
//				break;
//			case "float":
//				sb.Append(element.NumberValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
//				break;
//			case "int":
//				sb.Append((int)element.NumberValue);
//				break;
//			case "char":
//				sb.Append("[");
//				if (element.BytesValue != null)
//				{
//					for (int i = 0; i < element.BytesValue.Length; i++)
//					{
//						sb.Append($"\"{element.BytesValue[i]}\"");
//						if (i < element.BytesValue.Length - 1) sb.Append(", ");
//					}
//				}
//				sb.Append("]");
//				break;
//		}
//	}
//}


//using UnityEngine;
//using System;
//using System.IO;
//using System.Text;
//using System.Collections.Generic;

//public class TSParser : MonoBehaviour
//{
//	void Start()
//	{
//		string filePath = Application.streamingAssetsPath + "/database.bin";
//		try
//		{
//			string jsonOutput = ParseDatabase(filePath);
//			string outputPath = Application.persistentDataPath + "/output.json";
//			File.WriteAllText(outputPath, jsonOutput);
//			Debug.Log($"Exported to {outputPath}\n{jsonOutput}");
//		}
//		catch (Exception ex)
//		{
//			Debug.LogError($"Error parsing file: {ex.Message}");
//		}
//	}

//	public static string ParseDatabase(string filePath)
//	{
//		using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
//		using (var reader = new BinaryReader(stream, Encoding.UTF8))
//		{
//			float version = reader.ReadSingle();
//			Debug.Log($"Version: {version}");

//			int nSize = reader.ReadInt32();
//			Debug.Log($"Root element count: {nSize}, Position={reader.BaseStream.Position}");

//			Element root = new Element
//			{
//				Type = "struct",
//				Name = "",
//				Children = ParseNestedElements(reader, null, nSize)
//			};

//			return BuildJson(root);
//		}
//	}

//	[Serializable]
//	public class Element
//	{
//		public string Type;
//		public string Name;
//		public List<Element> Children;
//		public string StringValue;
//		public float NumberValue;
//		public string[] BytesValue;
//	}

//	private static Element ParseElement(BinaryReader reader, List<Element> siblings)
//	{
//		string type = ReadString(reader);
//		string name = ReadString(reader);
//		Debug.Log($"Parsing: Type={type}, Name={name}, Position={reader.BaseStream.Position}");

//		Element element = new Element { Type = type, Name = name };

//		switch (type.ToLower())
//		{
//			case "struct":
//			case "array":
//			case "data":
//				element.Children = ParseNestedElements(reader, element);
//				break;
//			case "string":
//				element.StringValue = ReadString(reader);
//				break;
//			case "bool":
//				element.NumberValue = reader.ReadBoolean() ? 1.0f : 0.0f;
//				break;
//			case "float":
//				element.NumberValue = reader.ReadSingle();
//				break;
//			case "int":
//				element.NumberValue = reader.ReadInt32();
//				break;
//			case "char":
//				break;
//			default:
//				throw new Exception($"Unknown type: {type}");
//		}

//		return element;
//	}

//	private static List<Element> ParseNestedElements(BinaryReader reader, Element parent, int? nSize = null)
//	{
//		int count = nSize ?? reader.ReadInt32();
//		Debug.Log($"Nested elements count: {count}, Position={reader.BaseStream.Position}");
//		List<Element> children = new List<Element>(count);

//		for (int i = 0; i < count; i++)
//		{
//			if (reader.BaseStream.Position >= reader.BaseStream.Length)
//				throw new Exception($"Unexpected end of file at {i}/{count}");
//			Element child = ParseElement(reader, children);
//			children.Add(child);
//		}

//		foreach (var child in children)
//		{
//			if (child.Type.ToLower() == "char")
//			{
//				child.BytesValue = ParseCharData(reader, children);
//			}
//		}

//		return children;
//	}

//	private static string[] ParseCharData(BinaryReader reader, List<Element> siblings)
//	{
//		int nCompression = (int)(siblings.Find(e => e.Name == "nCompression")?.NumberValue ?? 0x20);
//		int nCompressedLength = (int)(siblings.Find(e => e.Name == "nCompressedLength")?.NumberValue ?? 0);
//		int nBits = (int)nCompression & 0xF8;
//		int byteLength = nCompressedLength * (nBits >> 3);

//		Debug.Log($"Char data: nCompression={nCompression}, nCompressedLength={nCompressedLength}, nBits={nBits}, Calculated Length={byteLength}");

//		if (byteLength <= 0 || reader.BaseStream.Position + byteLength > reader.BaseStream.Length)
//		{
//			Debug.LogWarning($"Invalid char length: {byteLength}, using remaining bytes");
//			byteLength = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
//		}

//		byte[] bytes = reader.ReadBytes(byteLength);
//		Debug.Log($"Char bytes: Length={byteLength}, Data={BitConverter.ToString(bytes)}");
//		return Array.ConvertAll(bytes, b => b.ToString("X2"));
//	}

//	private static string ReadString(BinaryReader reader)
//	{
//		var chars = new List<char>();
//		char c;
//		while ((c = reader.ReadChar()) != '\0')
//			chars.Add(c);
//		return new string(chars.ToArray());
//	}

//	private static string BuildJson(Element element)
//	{
//		StringBuilder sb = new StringBuilder();
//		BuildJsonElement(sb, element, 0);
//		return sb.ToString();
//	}

//	private static void BuildJsonElement(StringBuilder sb, Element element, int indentLevel, bool isArrayElement = false)
//	{
//		string indent = new string(' ', indentLevel * 2);

//		// Only append the name as a key if it's not an array element and has a name
//		if (!isArrayElement && !string.IsNullOrEmpty(element.Name))
//		{
//			sb.Append($"{indent}\"{element.Name}\": ");
//		}

//		switch (element.Type.ToLower())
//		{
//			case "struct":
//			case "data":
//				sb.Append("{\n");
//				if (element.Children != null)
//				{
//					for (int i = 0; i < element.Children.Count; i++)
//					{
//						BuildJsonElement(sb, element.Children[i], indentLevel + 1);
//						if (i < element.Children.Count - 1) sb.Append(",");
//						sb.Append("\n");
//					}
//				}
//				sb.Append($"{indent}}}");
//				break;
//			case "array":
//				sb.Append("[\n");
//				if (element.Children != null)
//				{
//					if (element.Name == "tiles" || element.Name == "mixed" || element.Name == "nTileIndex")
//					{
//						sb.Length -= 2; // Remove "[\n"
//						sb.Append("{\n");
//						for (int i = 0; i < element.Children.Count; i++)
//						{
//							BuildJsonElement(sb, element.Children[i], indentLevel + 1);
//							if (i < element.Children.Count - 1) sb.Append(",");
//							sb.Append("\n");
//						}
//						sb.Append($"{indent}}}");
//					}
//					else
//					{
//						for (int i = 0; i < element.Children.Count; i++)
//						{
//							BuildJsonElement(sb, element.Children[i], indentLevel + 1, true);
//							if (i < element.Children.Count - 1) sb.Append(",");
//							sb.Append("\n");
//						}
//						sb.Append($"{indent}]");
//					}
//				}
//				else
//				{
//					sb.Append($"{indent}]");
//				}
//				break;
//			case "string":
//				sb.Append($"\"{element.StringValue}\"");
//				break;
//			case "bool":
//				sb.Append(element.NumberValue > 0 ? "true" : "false");
//				break;
//			case "float":
//				sb.Append(element.NumberValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
//				break;
//			case "int":
//				sb.Append((int)element.NumberValue);
//				break;
//			case "char":
//				sb.Append("[");
//				if (element.BytesValue != null)
//				{
//					for (int i = 0; i < element.BytesValue.Length; i++)
//					{
//						sb.Append($"\"{element.BytesValue[i]}\"");
//						if (i < element.BytesValue.Length - 1) sb.Append(", ");
//					}
//				}
//				sb.Append("]");
//				break;
//		}
//	}
//}