using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine; // For Debug.Log

public class WWMToDOTXConverter
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

	public static bool ConvertWWMToDOTX(string inputWWMPath, string outputDOTXPath)
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

				// Debug: Log first few vertices
				if (i < 5)
				{
					Debug.Log($"Vertex {i}: ({vertex.Position.x:F6}, {vertex.Position.y:F6}, {vertex.Position.z:F6})");
				}

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
					// Flip V coordinate to match Unity's UV space
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

			// Skip materials for simplicity
			Debug.LogWarning($"Skipping material data to simplify .x output. Using default material.");
			List<WWMaterial> materials = new List<WWMaterial>();
			WWMaterial defaultMat = new WWMaterial { TextureName = "default.png" };
			materials.Add(defaultMat);
			uint numMaterials = 1;

			// Write .x file (text format matching working example)
			using (StreamWriter writer = new StreamWriter(outputDOTXPath))
			{
				// Write template definitions
				writer.WriteLine("xof 0303txt 0032");
				writer.WriteLine("template ColorRGBA {");
				writer.WriteLine(" <35ff44e0-6c7c-11cf-8f52-0040333594a3>");
				writer.WriteLine(" FLOAT red;");
				writer.WriteLine(" FLOAT green;");
				writer.WriteLine(" FLOAT blue;");
				writer.WriteLine(" FLOAT alpha;");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template ColorRGB {");
				writer.WriteLine(" <d3e16e81-7835-11cf-8f52-0040333594a3>");
				writer.WriteLine(" FLOAT red;");
				writer.WriteLine(" FLOAT green;");
				writer.WriteLine(" FLOAT blue;");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template Material {");
				writer.WriteLine(" <3d82ab4d-62da-11cf-ab39-0020af71e433>");
				writer.WriteLine(" ColorRGBA faceColor;");
				writer.WriteLine(" FLOAT power;");
				writer.WriteLine(" ColorRGB specularColor;");
				writer.WriteLine(" ColorRGB emissiveColor;");
				writer.WriteLine(" [...]");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template TextureFilename {");
				writer.WriteLine(" <a42790e1-7810-11cf-8f52-0040333594a3>");
				writer.WriteLine(" STRING filename;");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template Frame {");
				writer.WriteLine(" <3d82ab46-62da-11cf-ab39-0020af71e433>");
				writer.WriteLine(" [...]");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template Matrix4x4 {");
				writer.WriteLine(" <f6f23f45-7686-11cf-8f52-0040333594a3>");
				writer.WriteLine(" array FLOAT matrix[16];");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template FrameTransformMatrix {");
				writer.WriteLine(" <f6f23f41-7686-11cf-8f52-0040333594a3>");
				writer.WriteLine(" Matrix4x4 frameMatrix;");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template Vector {");
				writer.WriteLine(" <3d82ab5e-62da-11cf-ab39-0020af71e433>");
				writer.WriteLine(" FLOAT x;");
				writer.WriteLine(" FLOAT y;");
				writer.WriteLine(" FLOAT z;");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template MeshFace {");
				writer.WriteLine(" <3d82ab5f-62da-11cf-ab39-0020af71e433>");
				writer.WriteLine(" DWORD nFaceVertexIndices;");
				writer.WriteLine(" array DWORD faceVertexIndices[nFaceVertexIndices];");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template Mesh {");
				writer.WriteLine(" <3d82ab44-62da-11cf-ab39-0020af71e433>");
				writer.WriteLine(" DWORD nVertices;");
				writer.WriteLine(" array Vector vertices[nVertices];");
				writer.WriteLine(" DWORD nFaces;");
				writer.WriteLine(" array MeshFace faces[nFaces];");
				writer.WriteLine(" [...]");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template MeshNormals {");
				writer.WriteLine(" <f6f23f43-7686-11cf-8f52-0040333594a3>");
				writer.WriteLine(" DWORD nNormals;");
				writer.WriteLine(" array Vector normals[nNormals];");
				writer.WriteLine(" DWORD nFaceNormals;");
				writer.WriteLine(" array MeshFace faceNormals[nFaceNormals];");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template MeshMaterialList {");
				writer.WriteLine(" <f6f23f42-7686-11cf-8f52-0040333594a3>");
				writer.WriteLine(" DWORD nMaterials;");
				writer.WriteLine(" DWORD nFaceIndexes;");
				writer.WriteLine(" array DWORD faceIndexes[nFaceIndexes];");
				writer.WriteLine(" [Material <3d82ab4d-62da-11cf-ab39-0020af71e433>]");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template Coords2d {");
				writer.WriteLine(" <f6f23f44-7686-11cf-8f52-0040333594a3>");
				writer.WriteLine(" FLOAT u;");
				writer.WriteLine(" FLOAT v;");
				writer.WriteLine("}");
				writer.WriteLine("");
				writer.WriteLine("template MeshTextureCoords {");
				writer.WriteLine(" <f6f23f40-7686-11cf-8f52-0040333594a3>");
				writer.WriteLine(" DWORD nTextureCoords;");
				writer.WriteLine(" array Coords2d textureCoords[nTextureCoords];");
				writer.WriteLine("}");
				writer.WriteLine("");

				// Define material at root level
				writer.WriteLine("Material DefaultMaterial {");
				writer.WriteLine(" 1.000000;1.000000;1.000000;1.000000;;");
				writer.WriteLine(" 3.200000;");
				writer.WriteLine(" 0.000000;0.000000;0.000000;;");
				writer.WriteLine(" 0.000000;0.000000;0.000000;;");
				writer.WriteLine($" TextureFilename {{ \"{materials[0].TextureName}\"; }}");
				writer.WriteLine("}");
				writer.WriteLine("");

				// Write frame hierarchy
				writer.WriteLine("Frame MeshFrame {");
				writer.WriteLine(" FrameTransformMatrix {");
				writer.WriteLine("  1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000;;");
				writer.WriteLine(" }");
				writer.WriteLine("");
				writer.WriteLine(" Frame {");
				writer.WriteLine("  FrameTransformMatrix {");
				writer.WriteLine("   1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000,0.000000,0.000000,0.000000,0.000000,1.000000;;");
				writer.WriteLine("  }");
				writer.WriteLine("");

				// Write mesh
				writer.WriteLine("  Mesh {");
				writer.WriteLine($"   {vertices.Count};");
				for (int i = 0; i < vertices.Count; i++)
				{
					var v = vertices[i];
					writer.WriteLine($"   {v.Position.x:F6};{v.Position.y:F6};{v.Position.z:F6};{(i < vertices.Count - 1 ? "," : ";")}");
				}
				writer.WriteLine("");

				// Calculate faces from triangle strips
				List<string> faceLines = new List<string>();
				List<string> normalFaceLines = new List<string>();
				uint totalFaces = 0;
				foreach (var attr in attributes)
				{
					uint startIndex = attr.FaceStart;
					uint endIndex = startIndex + attr.FaceCount + 2;
					if (endIndex > indices.Count)
					{
						Debug.LogWarning($"Attribute {attr.AttribID} requests {attr.FaceCount} triangles, but only {indices.Count - startIndex} indices available. Clamping.");
						endIndex = (uint)indices.Count;
					}

					// Convert strip to triangles
					for (uint i = startIndex; i + 2 < endIndex; i++)
					{
						uint v1 = indices[(int)i];
						uint v2 = indices[(int)i + 1];
						uint v3 = indices[(int)i + 2];

						// Skip degenerate triangles
						if (v1 == v2 || v2 == v3 || v1 == v3)
							continue;

						// Validate indices
						if (v1 >= numVertices || v2 >= numVertices || v3 >= numVertices)
						{
							Debug.LogError($"Invalid face indices: {v1},{v2},{v3} (max {numVertices - 1})");
							continue;
						}

						// Match OBJ winding order (0-based for .x)
						string faceLine;
						if (i % 2 == 0)
						{
							faceLine = $"   3;{v1},{v2},{v3};,";
						}
						else
						{
							faceLine = $"   3;{v1},{v3},{v2};,";
						}
						faceLines.Add(faceLine);
						// For normals, use same indices (per-vertex normals)
						normalFaceLines.Add(faceLine);
						totalFaces++;

						// Debug: Log all faces to check indices
						Debug.Log($"Face {totalFaces}: {faceLine}");
					}
				}

				// Write faces
				writer.WriteLine($"   {totalFaces};");
				for (int i = 0; i < faceLines.Count; i++)
				{
					if (i == faceLines.Count - 1)
					{
						// Last face: replace trailing ",;" with ";;"
						writer.WriteLine(faceLines[i].Replace(";,", ";;"));
					}
					else
					{
						writer.WriteLine(faceLines[i]);
					}
				}
				writer.WriteLine("");

				// Write normals (if present)
				if ((vertexFormat & V_VERTEX_FORMAT_NORMAL) != 0)
				{
					writer.WriteLine("   MeshNormals {");
					writer.WriteLine($"    {vertices.Count};");
					for (int i = 0; i < vertices.Count; i++)
					{
						var n = vertices[i].Normal.Value;
						writer.WriteLine($"    {n.x:F6};{n.y:F6};{n.z:F6};{(i < vertices.Count - 1 ? "," : ";")}");
					}
					writer.WriteLine($"    {totalFaces};");
					for (int i = 0; i < normalFaceLines.Count; i++)
					{
						if (i == normalFaceLines.Count - 1)
						{
							writer.WriteLine(normalFaceLines[i].Replace(";,", ";;"));
						}
						else
						{
							writer.WriteLine(normalFaceLines[i]);
						}
					}
					writer.WriteLine("   }");
					writer.WriteLine("");
				}

				// Write UVs (if present)
				if ((vertexFormat & V_VERTEX_FORMAT_TEX1) != 0)
				{
					writer.WriteLine("   MeshTextureCoords {");
					writer.WriteLine($"    {vertices.Count};");
					for (int i = 0; i < vertices.Count; i++)
					{
						var t = vertices[i].TexCoord.Value;
						writer.WriteLine($"    {t.x:F6};{t.y:F6};{(i < vertices.Count - 1 ? "," : ";")}");
					}
					writer.WriteLine("   }");
					writer.WriteLine("");
				}

				// Write MeshMaterialList
				writer.WriteLine("   MeshMaterialList {");
				writer.WriteLine($"    {numMaterials};");
				writer.WriteLine($"    {totalFaces};");
				for (int i = 0; i < totalFaces; i++)
				{
					writer.WriteLine($"    0{(i < totalFaces - 1 ? "," : ";")}");
				}
				writer.WriteLine("    { DefaultMaterial }");
				writer.WriteLine("   }");

				writer.WriteLine("  }");
				writer.WriteLine(" }");
				writer.WriteLine("}");
		
				Debug.Log($"Successfully converted {inputWWMPath} to {outputDOTXPath} with {totalFaces} faces and {numMaterials} material(s).");
			}

			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error converting file: {ex.Message}");
			return false;
		}
	}

	//// Example usage (can be called from a Unity script)
	//[UnityEngine.RuntimeInitializeOnLoadMethod]
	//public static void TestConversion()
	//{
	//	string inputPath = "Assets/Meshes/input.wwm";
	//	string outputPath = "Assets/Meshes/output.x";
	//	ConvertWWMToDOTX(inputPath, outputPath);
	//}
}
