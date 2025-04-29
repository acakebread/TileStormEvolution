using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine; // For Debug.Log

public class WWMToOBJConverter
{
	// Structures mirroring the C++ code
	private struct Attribute
	{
		public uint AttribID;
		public uint FaceStart;
		public uint FaceCount;
	}

	private struct WWMaterial
	{
		public string TextureName;
	}

	// Generic vertex struct to hold all possible components
	private struct Vertex
	{
		public Vector3 Position;
		public Vector3? Normal;
		public Color32? Color;
		public Vector2? TexCoord;
		public Vector3? Tangent;
		public Vector3? Binormal;
	}

	public static bool ConvertWWMToOBJ(string inputWWMPath, string outputOBJPath)
	{
		try
		{
			// Read the entire .wwm file
			byte[] fileData = File.ReadAllBytes(inputWWMPath);
			int offset = 0;

			// Read number of parts (little-endian, no reverse needed on little-endian systems)
			uint numParts = BitConverter.ToUInt32(fileData, offset);
			offset += 4;

			// Debug: Print raw bytes and numParts
			Debug.Log($"First 4 bytes: {fileData[0]:X2} {fileData[1]:X2} {fileData[2]:X2} {fileData[3]:X2}");
			Debug.Log($"numParts: {numParts}, offset: {offset}");

			if (numParts != 1)
			{
				Debug.LogError($"Only one mesh part is supported. Found {numParts} parts.");
				return false;
			}

			// Read vertex format
			uint vertexFormat = BitConverter.ToUInt32(fileData, offset);
			offset += 4;
			Debug.Log($"Vertex format: 0x{vertexFormat:X}, offset: {offset}");

			// Vertex format flags
			const uint V_VERTEX_FORMAT_POS = 0x0001;
			const uint V_VERTEX_FORMAT_NORMAL = 0x0002;
			const uint V_VERTEX_FORMAT_COLOUR = 0x0004;
			const uint V_VERTEX_FORMAT_TANGENT = 0x0008;
			const uint V_VERTEX_FORMAT_BINORMAL = 0x0010;
			const uint V_VERTEX_FORMAT_TEX1 = 0x0100;

			// Check for required position flag
			if ((vertexFormat & V_VERTEX_FORMAT_POS) == 0)
			{
				Debug.LogError("Vertex format must include V_VERTEX_FORMAT_POS.");
				return false;
			}

			// Calculate vertex size based on flags
			uint vertexSize = 0;
			if ((vertexFormat & V_VERTEX_FORMAT_POS) != 0) vertexSize += 12; // 3 floats
			if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0) vertexSize += 12; // 3 floats
			if ((vertexFormat & V_VERTEX_FORMAT_COLOUR) != 0) vertexSize += 4; // 1 uint
			if ((vertexFormat & V_VERTEX_FORMAT_TANGENT) != 0) vertexSize += 12; // 3 floats
			if ((vertexFormat & V_VERTEX_FORMAT_BINORMAL) != 0) vertexSize += 12; // 3 floats
			if ((vertexFormat & V_VERTEX_FORMAT_TEX1) != 0) vertexSize += 8; // 2 floats

			// Read vertex size from file (for validation)
			uint fileVertexSize = BitConverter.ToUInt32(fileData, offset);
			offset += 4;
			Debug.Log($"Calculated vertexSize: {vertexSize}, fileVertexSize: {fileVertexSize}, offset: {offset}");
			if (fileVertexSize != vertexSize)
			{
				Debug.LogError($"Vertex size mismatch. Expected {vertexSize}, found {fileVertexSize}.");
				return false;
			}

			// Read number of vertices, indices, and attributes
			uint numVertices = BitConverter.ToUInt32(fileData, offset);
			offset += 4;
			Debug.Log($"numVertices: {numVertices}, offset: {offset}");

			uint numIndices = BitConverter.ToUInt32(fileData, offset);
			offset += 4;
			Debug.Log($"numIndices: {numIndices}, offset: {offset}");

			uint numAttributes = BitConverter.ToUInt32(fileData, offset);
			offset += 4;
			Debug.Log($"numAttributes: {numAttributes}, offset: {offset}");

			// Read vertices dynamically based on format
			List<Vertex> vertices = new List<Vertex>();
			for (uint i = 0; i < numVertices; i++)
			{
				Vertex vertex = new Vertex();
				int vertexOffset = offset;

				// Read position (required)
				vertex.Position = new Vector3(
					BitConverter.ToSingle(fileData, vertexOffset),
					BitConverter.ToSingle(fileData, vertexOffset + 4),
					BitConverter.ToSingle(fileData, vertexOffset + 8)
				);
				vertexOffset += 12;

				// Read normal (optional)
				if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0)
				{
					vertex.Normal = new Vector3(
						BitConverter.ToSingle(fileData, vertexOffset),
						BitConverter.ToSingle(fileData, vertexOffset + 4),
						BitConverter.ToSingle(fileData, vertexOffset + 8)
					);
					vertexOffset += 12;
				}

				// Read color (optional)
				if ((vertexFormat & V_VERTEX_FORMAT_COLOUR) != 0)
				{
					uint color = BitConverter.ToUInt32(fileData, vertexOffset);
					vertex.Color = new Color32(
						(byte)((color >> 16) & 0xFF), // R
						(byte)((color >> 8) & 0xFF),  // G
						(byte)(color & 0xFF),         // B
						(byte)((color >> 24) & 0xFF)  // A
					);
					vertexOffset += 4;
				}

				// Read tangent (optional)
				if ((vertexFormat & V_VERTEX_FORMAT_TANGENT) != 0)
				{
					vertex.Tangent = new Vector3(
						BitConverter.ToSingle(fileData, vertexOffset),
						BitConverter.ToSingle(fileData, vertexOffset + 4),
						BitConverter.ToSingle(fileData, vertexOffset + 8)
					);
					vertexOffset += 12;
				}

				// Read binormal (optional)
				if ((vertexFormat & V_VERTEX_FORMAT_BINORMAL) != 0)
				{
					vertex.Binormal = new Vector3(
						BitConverter.ToSingle(fileData, vertexOffset),
						BitConverter.ToSingle(fileData, vertexOffset + 4),
						BitConverter.ToSingle(fileData, vertexOffset + 8)
					);
					vertexOffset += 12;
				}

				// Read UVs (optional)
				if ((vertexFormat & V_VERTEX_FORMAT_TEX1) != 0)
				{
					float u = BitConverter.ToSingle(fileData, vertexOffset);
					float v = BitConverter.ToSingle(fileData, vertexOffset + 4);
					// Flip V coordinate to match Unity's UV space (optional, comment out if not needed)
					vertex.TexCoord = new Vector2(u, 1f - v);
					vertexOffset += 8;

					// Debug: Log first few UVs
					if (i < 5)
					{
						Debug.Log($"Vertex {i} UV: ({u:F6}, {v:F6}) -> ({vertex.TexCoord.Value.x:F6}, {vertex.TexCoord.Value.y:F6})");
					}
				}

				vertices.Add(vertex);
				offset += (int)vertexSize;
			}
			Debug.Log($"After vertices, offset: {offset}");

			// Read indices
			List<uint> indices = new List<uint>();
			for (uint i = 0; i < numIndices; i++)
			{
				if (offset + 2 > fileData.Length)
				{
					Debug.LogError($"Index data out of range at offset {offset}. Expected {numIndices} indices.");
					return false;
				}
				ushort index = BitConverter.ToUInt16(fileData, offset);
				indices.Add(index);
				offset += 2;
			}
			Debug.Log($"After indices, offset: {offset}");

			// Read attributes
			List<Attribute> attributes = new List<Attribute>();
			for (uint i = 0; i < numAttributes; i++)
			{
				if (offset + 12 > fileData.Length)
				{
					Debug.LogError($"Attribute data out of range at offset {offset}. Expected {numAttributes} attributes.");
					return false;
				}
				Attribute attr;
				attr.AttribID = BitConverter.ToUInt32(fileData, offset);
				attr.FaceStart = BitConverter.ToUInt32(fileData, offset + 4);
				attr.FaceCount = BitConverter.ToUInt32(fileData, offset + 8);
				attributes.Add(attr);
				offset += 12;
				Debug.Log($"Attribute {i}: AttribID={attr.AttribID}, FaceStart={attr.FaceStart}, FaceCount={attr.FaceCount}");
			}
			Debug.Log($"After attributes, offset: {offset}");

			// Read materials (with fallback for missing data)
			uint numMaterials = 0;
			List<WWMaterial> materials = new List<WWMaterial>();
			if (offset + 4 <= fileData.Length)
			{
				numMaterials = BitConverter.ToUInt32(fileData, offset);
				offset += 4;
				Debug.Log($"numMaterials: {numMaterials}, offset: {offset}");

				for (uint i = 0; i < numMaterials; i++)
				{
					if (offset + 100 > fileData.Length)
					{
						Debug.LogWarning($"Material data out of range at offset {offset}. Skipping materials.");
						materials.Clear();
						numMaterials = 0;
						break;
					}
					WWMaterial mat;
					byte[] nameBytes = new byte[100]; // MAX_TEXNAME_LENGTH
					Array.Copy(fileData, offset, nameBytes, 0, 100);
					mat.TextureName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
					materials.Add(mat);
					offset += 100;
				}
			}
			else
			{
				Debug.LogWarning($"No material data available at offset {offset}. Proceeding without materials.");
			}

			// Write OBJ file
			using (StreamWriter writer = new StreamWriter(outputOBJPath))
			{
				writer.WriteLine("# Converted from .wwm to .obj by WWMToOBJConverter");

				// Write vertices
				foreach (var vertex in vertices)
				{
					writer.WriteLine($"v {vertex.Position.x:F6} {vertex.Position.y:F6} {vertex.Position.z:F6}");
				}

				// Write UVs (if present)
				if ((vertexFormat & V_VERTEX_FORMAT_TEX1) != 0)
				{
					foreach (var vertex in vertices)
					{
						writer.WriteLine($"vt {vertex.TexCoord.Value.x:F6} {vertex.TexCoord.Value.y:F6}");
					}
				}

				// Write normals (if present)
				if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0)
				{
					foreach (var vertex in vertices)
					{
						writer.WriteLine($"vn {vertex.Normal.Value.x:F6} {vertex.Normal.Value.y:F6} {vertex.Normal.Value.z:F6}");
					}
				}

				// Write faces grouped by material, converting triangle strips to triangles
				for (int attrIdx = 0; attrIdx < attributes.Count; attrIdx++)
				{
					var attr = attributes[attrIdx];
					if (numMaterials > 0 && attr.AttribID < materials.Count)
					{
						writer.WriteLine($"g Material_{attr.AttribID}");
						writer.WriteLine($"usemtl {materials[(int)attr.AttribID].TextureName}");
					}
					else
					{
						writer.WriteLine($"g Material_{attrIdx}");
					}

					// Process triangle strip
					uint startIndex = attr.FaceStart;
					uint endIndex = startIndex + attr.FaceCount + 2;
					if (endIndex > indices.Count)
					{
						Debug.LogWarning($"Attribute {attrIdx} requests {attr.FaceCount} triangles, but only {indices.Count - startIndex} indices available. Clamping.");
						endIndex = (uint)indices.Count;
					}

					// Convert strip to triangles
					for (uint i = startIndex; i + 2 < endIndex; i++)
					{
						uint v1 = indices[(int)i] + 1;     // OBJ indices are 1-based
						uint v2 = indices[(int)i + 1] + 1;
						uint v3 = indices[(int)i + 2] + 1;

						// Skip degenerate triangles
						if (v1 == v2 || v2 == v3 || v1 == v3)
							continue;

						// Alternate winding order for triangle strips
						if (i % 2 == 0)
						{
							// Normal order: v1, v2, v3
							if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0 && (vertexFormat & V_VERTEX_FORMAT_TEX1) != 0)
								writer.WriteLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
							else if ((vertexFormat & V_VERTEX_FORMAT_TEX1) != 0)
								writer.WriteLine($"f {v1}/{v1} {v2}/{v2} {v3}/{v3}");
							else if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0)
								writer.WriteLine($"f {v1}//{v1} {v2}//{v2} {v3}//{v3}");
							else
								writer.WriteLine($"f {v1} {v2} {v3}");
						}
						else
						{
							// Reversed order: v1, v3, v2 to maintain consistent winding
							if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0 && (vertexFormat & V_VERTEX_FORMAT_TEX1) != 0)
								writer.WriteLine($"f {v1}/{v1}/{v1} {v3}/{v3}/{v3} {v2}/{v2}/{v2}");
							else if ((vertexFormat & V_VERTEX_FORMAT_TEX1) != 0)
								writer.WriteLine($"f {v1}/{v1} {v3}/{v3} {v2}/{v2}");
							else if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0)
								writer.WriteLine($"f {v1}//{v1} {v3}//{v3} {v2}//{v2}");
							else
								writer.WriteLine($"f {v1} {v3} {v2}");
						}
					}
				}
			}

			Debug.Log($"Successfully converted {inputWWMPath} to {outputOBJPath}");
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error converting file: {ex.Message}");
			return false;
		}
	}

	// Helper method for float byte reversal (FIX_LE_FP32)
	private static float FixLEFloat(byte[] data, int offset)
	{
		// Read float directly, as file and system are both little-endian
		return BitConverter.ToSingle(data, offset);
		// Uncomment below to test byte reversal if needed
		// byte[] floatBytes = new byte[4];
		// Array.Copy(data, offset, floatBytes, 0, 4);
		// Array.Reverse(floatBytes);
		// return BitConverter.ToSingle(floatBytes, 0);
	}

	//// Example usage (can be called from a Unity script)
	//[UnityEngine.RuntimeInitializeOnLoadMethod]
	//public static void TestConversion()
	//{
	//	string inputPath = "Assets/Meshes/input.wwm";
	//	string outputPath = "Assets/Meshes/output.obj";
	//	ConvertWWMToOBJ(inputPath, outputPath);
	//}
}