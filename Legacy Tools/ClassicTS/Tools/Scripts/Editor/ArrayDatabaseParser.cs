using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ClassicTilestorm.Editor
{
	public class ArrayDatabaseParser : EditorWindow
	{
		// Boolean switch to include compressed_bytes in JSON output
		private static bool includeCompressedBytes = false;
		private static bool boundMaps = true;

		private UnityEngine.Object databasePBF;

		[MenuItem("Tools/Classic Tilestorm/Legacy Database Converter")]
		public static void ShowWindow() => GetWindow<ArrayDatabaseParser>("CT Database Converter");

		private void OnGUI()
		{
			GUILayout.Label("PBF Database Source", EditorStyles.boldLabel);
			databasePBF = EditorGUILayout.ObjectField("database.pbf", databasePBF, typeof(UnityEngine.Object), false);

			GUILayout.Space(10);

			if (databasePBF == null)
			{
				EditorGUILayout.HelpBox("Drag your database.pbf file here first.", MessageType.Warning);
				return;
			}

			if (GUILayout.Button("Convert Database"))
			{
				// Ensure includeCompressedBytes is false when boundMaps is true
				if (boundMaps)
					includeCompressedBytes = false;

				string assetPath = AssetDatabase.GetAssetPath(databasePBF);
				string filePath = Path.GetFullPath(assetPath);
				try
				{
					string jsonOutput = ParseDatabase(filePath);
					string outputPath = filePath.Replace(".pbf", ".json").Trim();
					File.WriteAllText(outputPath, jsonOutput);
					Debug.Log($"Exported to {outputPath}\n{jsonOutput.Substring(0, Mathf.Min(500, jsonOutput.Length))}");
				}
				catch (Exception ex)
				{
					Debug.LogError($"Error parsing file: {ex.Message}");
				}
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

				List<Element> rootElements = ParseNestedElements(reader, null, nSize);
				if (rootElements.Count != nSize)
				{
					Debug.LogWarning($"Expected {nSize} root elements, parsed {rootElements.Count}");
				}

				// Process maps to bound them if enabled
				if (boundMaps)
				{
					MapBoundsHelper.ProcessMaps(rootElements);
				}

				return BuildJson(rootElements);
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
			public string[] BytesValue; // Used for compressed bytes (previously 'bytes')
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
				case 8: return (sbyte)bytes[offset];
				case 16: return BitConverter.ToInt16(bytes, offset);
				case 32: return BitConverter.ToInt32(bytes, offset);
				default:
					Debug.LogError($"Unsupported nBits: {nBits}");
					return 0;
			}
		}

		public static int[] DecompressBytes(Element tileData, Element tilesElement)
		{
			int nCompression = (int)(tileData.Children.Find(e => e.Name == "nCompression")?.NumberValue ?? 0x20);
			int nCompressedLength = (int)(tileData.Children.Find(e => e.Name == "nCompressedLength")?.NumberValue ?? 0);
			int nUncompressedLength = (int)(tileData.Children.Find(e => e.Name == "nUncompressedLength")?.NumberValue ?? 0);
			int nAdjust = (int)(tileData.Children.Find(e => e.Name == "nAdjust")?.NumberValue ?? 0);
			string[] compressedHex = tileData.Children.Find(e => e.Name == "bytes")?.BytesValue;

			int nWidth = tilesElement.Children != null && tilesElement.Children.Count >= 1 ? (int)tilesElement.Children[0].NumberValue : 0;
			int nHeight = tilesElement.Children != null && tilesElement.Children.Count >= 2 ? (int)tilesElement.Children[1].NumberValue : 0;

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
			if (nCompression == 0) nCompression = 0x20;
			int bytesPerValue = nBits >> 3;
			int pos = 0;
			int byteOffset = 0;

			Debug.Log($"Decompressing: nCompression={nCompression}, nBits={nBits}, CompressedLength={nCompressedLength}, Bytes={BitConverter.ToString(compressed)}");

			while (pos < nUncompressedLength && byteOffset < compressed.Length)
			{
				int nVal = GetInteger(compressed, byteOffset, nBits);
				byteOffset += bytesPerValue;
				int nRun = 1;

				if ((nCompression & 1) == 1 && byteOffset < compressed.Length)
				{
					if (nVal < 0)
					{
						nRun = -nVal;
						nVal = GetInteger(compressed, byteOffset, nBits);
						byteOffset += bytesPerValue;
						Debug.Log($"RLE: Run={nRun}, Value={nVal}, ByteOffset={byteOffset}");
					}
					else
					{
						Debug.Log($"Single: Value={nVal}, ByteOffset={byteOffset}");
					}
				}

				for (int i = 0; i < nRun && pos < nUncompressedLength; i++)
				{
					decompressed[pos++] = nVal + nAdjust;
				}
			}

			if (pos != nUncompressedLength)
			{
				Debug.LogError($"Decompression failed: Expected {nUncompressedLength} elements, got {pos}, ByteOffset={byteOffset}");
			}
			else
			{
				Debug.Log($"Decompression succeeded: Filled {pos} elements");
			}

			return decompressed;
		}

		private static string BuildJson(List<Element> rootElements)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("{\n");

			string[] expectedSections = { "maps", "themes", "tiledefs", "buttons", "texture_set" };
			bool firstSection = true;

			foreach (var sectionName in expectedSections)
			{
				Element section = rootElements.Find(e => e.Name == sectionName);
				if (section != null && section.Children != null)
				{
					if (!firstSection) sb.Append(",\n");
					sb.Append($"  \"{sectionName}\": [\n");
					for (int i = 0; i < section.Children.Count; i++)
					{
						var item = section.Children[i];
						sb.Append("    {\n");
						if (sectionName != "tiledefs" && !string.IsNullOrEmpty(item.Name))
						{
							sb.Append($"      \"name\": \"{item.Name}\"");
							if (item.Children != null && item.Children.Count > 0)
								sb.Append(",");
							sb.Append("\n");
						}
						if (item.Children != null)
						{
							for (int j = 0; j < item.Children.Count; j++)
							{
								BuildJsonElement(sb, item.Children[j], 3, item);
								if (j < item.Children.Count - 1) sb.Append(",");
								sb.Append("\n");
							}
						}
						sb.Append("    }");
						if (i < section.Children.Count - 1) sb.Append(",");
						sb.Append("\n");
					}
					sb.Append("  ]");
					firstSection = false;
				}
				else
				{
					Debug.LogWarning($"Section '{sectionName}' not found in root elements!");
					if (!firstSection) sb.Append(",\n");
					sb.Append($"  \"{sectionName}\": []");
					firstSection = false;
				}
			}

			sb.Append("\n}");
			return sb.ToString();
		}

		private static void BuildJsonElement(StringBuilder sb, Element element, int indentLevel, Element parent, bool isArrayElement = false)
		{
			string indent = new string(' ', indentLevel * 2);

			// Skip the original 'bytes' element of type 'char' to avoid duplicate output
			if (element.Type.ToLower() == "char" && element.Name == "bytes")
			{
				return; // Do not output this element directly
			}

			if (!isArrayElement && !string.IsNullOrEmpty(element.Name))
			{
				string elementName = element.Name == "Waypoints" ? "waypoints" : element.Name;
				sb.Append($"{indent}\"{elementName}\": ");
			}

			switch (element.Type.ToLower())
			{
				case "struct":
				case "data":
					if (element.Name == "defs" || element.Name == "tiledefs" || element.Name == "frames" || element.Name.ToLower() == "waypoints")
					{
						sb.Append("[\n");
						if (element.Children != null)
						{
							var childrenToProcess = element.Name.ToLower() == "waypoints"
								? element.Children.Where(c => c.Name.StartsWith("WP")).OrderBy(c => c.Name).ToList()
								: element.Children;

							for (int i = 0; i < childrenToProcess.Count; i++)
							{
								var child = childrenToProcess[i];
								sb.Append($"{indent}  {{");
								if (element.Name != "defs" && element.Name != "tiledefs")
								{
									if (!string.IsNullOrEmpty(child.Name))
									{
										sb.Append($"\n{indent}    \"name\": \"{child.Name}\"");
										if (child.Children != null && child.Children.Count > 0)
											sb.Append(",");
									}
								}
								if (child.Children != null)
								{
									if (element.Name == "defs" || element.Name == "tiledefs" || child.Children.Count > 0)
										sb.Append("\n");
									for (int j = 0; j < child.Children.Count; j++)
									{
										BuildJsonElement(sb, child.Children[j], indentLevel + 2, child);
										if (j < child.Children.Count - 1)
											sb.Append(",");
										sb.Append("\n");
									}
								}
								else
								{
									sb.Append("\n");
								}
								sb.Append($"{indent}  }}");
								if (i < childrenToProcess.Count - 1)
									sb.Append(",");
								sb.Append("\n");
							}
						}
						sb.Append($"{indent}]");
					}
					else
					{
						sb.Append($"\n{indent}");
						sb.Append("{\n");
						if (element.Children != null)
						{
							// List of properties to skip when includeCompressedBytes is false
							var skipProperties = new HashSet<string> { "nReserved", "nUncompressedLength", "nCompression", "nCompressedLength", "nAdjust" };

							// Output children, skipping redundant properties and bytes for nTileIndex
							for (int i = 0; i < element.Children.Count; i++)
							{
								var child = element.Children[i];
								// Skip redundant properties in nTileIndex if includeCompressedBytes is false
								if (element.Name == "nTileIndex" && !includeCompressedBytes && skipProperties.Contains(child.Name))
									continue;
								// Skip bytes element in nTileIndex to avoid duplication (processed in special case below)
								if (element.Name == "nTileIndex" && child.Name == "bytes")
									continue;

								BuildJsonElement(sb, child, indentLevel + 1, element);
								if (i < element.Children.Count - 1 &&
									!(element.Name == "nTileIndex" && !includeCompressedBytes && skipProperties.Contains(element.Children[i + 1].Name)) &&
									!(element.Name == "nTileIndex" && element.Children[i + 1].Name == "bytes"))
									sb.Append(",");
								sb.Append("\n");
							}

							// Special handling for nTileIndex under tiles or mixed
							if (element.Name == "nTileIndex" && parent != null && (parent.Name == "tiles" || parent.Name == "mixed"))
							{
								var compressedBytesElement = element.Children.Find(e => e.Name == "bytes");
								if (includeCompressedBytes && compressedBytesElement?.BytesValue != null)
								{
									sb.Append($"{indent}  \"compressed_bytes\": [");
									for (int j = 0; j < compressedBytesElement.BytesValue.Length; j++)
									{
										sb.Append($"\"{compressedBytesElement.BytesValue[j]}\"");
										if (j < compressedBytesElement.BytesValue.Length - 1)
											sb.Append(", ");
									}
									sb.Append("]");
								}

								// Check for remapped bytes (type array) first, set by MapBoundsHelper
								var remappedBytesElement = element.Children.Find(e => e.Name == "bytes" && e.Type.ToLower() == "array");
								if (remappedBytesElement != null && remappedBytesElement.Children != null)
								{
									sb.Append($"{indent}  \"bytes\": [");
									for (int j = 0; j < remappedBytesElement.Children.Count; j++)
									{
										sb.Append(remappedBytesElement.Children[j].NumberValue.ToString());
										if (j < remappedBytesElement.Children.Count - 1)
											sb.Append(", ");
									}
									sb.Append("]");
								}
								else
								{
									// Fall back to decompressing original bytes if no remapped bytes
									int[] unpacked = DecompressBytes(element, parent);
									if (unpacked.Length > 0)
									{
										sb.Append($"{indent}  \"bytes\": [");
										for (int j = 0; j < unpacked.Length; j++)
										{
											sb.Append(unpacked[j].ToString());
											if (j < unpacked.Length - 1)
												sb.Append(", ");
										}
										sb.Append("]");
									}
								}
							}
						}
						sb.Append($"\n{indent}}}");
					}
					break;
				case "array":
					if (element.Name == "tiles" || element.Name == "mixed")
					{
						sb.Append("{\n");
						if (element.Children != null && element.Children.Count >= 3)
						{
							sb.Append($"{indent}  \"nWidth\": {(int)element.Children[0].NumberValue},\n");
							sb.Append($"{indent}  \"nHeight\": {(int)element.Children[1].NumberValue},\n");
							sb.Append($"{indent}  \"TileData\": ");
							BuildJsonElement(sb, element.Children[2], indentLevel + 1, element, true);
						}
						sb.Append($"\n{indent}}}");
					}
					else if (element.Name == "nTileIndex")
					{
						sb.Append("{\n");
						if (element.Children != null)
						{
							bool first = true;
							for (int i = 0; i < element.Children.Count; i++)
							{
								if (!first) sb.Append(",\n");
								BuildJsonElement(sb, element.Children[i], indentLevel + 1, element);
								first = false;
							}
						}
						sb.Append($"\n{indent}}}");
					}
					else
					{
						sb.Append("[\n");
						if (element.Children != null)
						{
							for (int i = 0; i < element.Children.Count; i++)
							{
								BuildJsonElement(sb, element.Children[i], indentLevel + 1, element, true);
								if (i < element.Children.Count - 1)
									sb.Append(",");
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
							if (i < element.BytesValue.Length - 1)
								sb.Append(", ");
						}
					}
					sb.Append("]");
					break;
			}
		}
	}
}