using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class OBJZInverterHelper
{
	// Structure to hold vertex data
	private struct VertexData
	{
		public Vector3 Position;
		public Vector3? Normal;
		public Vector2? TexCoord;
	}

	// Structure to hold face data
	private struct Face
	{
		public int[] VertexIndices;   // 1-based indices for vertices
		public int[] TexCoordIndices; // 1-based indices for UVs
		public int[] NormalIndices;   // 1-based indices for normals
	}

	// Structure to hold material group
	private struct MaterialGroup
	{
		public string ObjectName;    // For 'o' lines
		public string GroupName;     // For 'g' lines
		public string MaterialName;  // For 'usemtl' lines
		public List<Face> Faces;
	}

	public static bool ProcessOBJFiles(string inputDirectory, string outputDirectory)
	{
		try
		{
			// Ensure directories exist
			if (!Directory.Exists(inputDirectory))
			{
				Debug.LogError($"Input directory {inputDirectory} does not exist.");
				return false;
			}

			if (!Directory.Exists(outputDirectory))
			{
				Directory.CreateDirectory(outputDirectory);
			}

			// Get all .obj files
			string[] objFiles = Directory.GetFiles(inputDirectory, "*.obj", SearchOption.TopDirectoryOnly);
			if (objFiles.Length == 0)
			{
				Debug.LogWarning($"No .obj files found in {inputDirectory}.");
				return false;
			}

			int processedCount = 0;
			foreach (string inputPath in objFiles)
			{
				string fileName = Path.GetFileName(inputPath);
				string outputPath = Path.Combine(outputDirectory, fileName);

				if (ProcessSingleOBJ(inputPath, outputPath))
				{
					processedCount++;
					Debug.Log($"Successfully processed {fileName}");
				}
				else
				{
					Debug.LogError($"Failed to process {fileName}");
				}
			}

			Debug.Log($"Processed {processedCount} of {objFiles.Length} .obj files.");
			return processedCount > 0;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error processing files: {ex.Message}");
			return false;
		}
	}

	private static bool ProcessSingleOBJ(string inputPath, string outputPath)
	{
		try
		{
			// Read the .obj file
			string[] lines = File.ReadAllLines(inputPath);

			List<Vector3> vertices = new List<Vector3>();
			List<Vector3> normals = new List<Vector3>();
			List<Vector2> texCoords = new List<Vector2>();
			List<MaterialGroup> materialGroups = new List<MaterialGroup>();
			MaterialGroup currentGroup = new MaterialGroup { Faces = new List<Face>() };
			string currentObjectName = null;
			string currentGroupName = null;
			string currentMaterial = null;
			string mtllib = null;

			// Parse the .obj file
			foreach (string line in lines)
			{
				string trimmedLine = line.Trim();
				if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
					continue;

				string[] parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0)
					continue;

				switch (parts[0].ToLower())
				{
					case "v":
						if (parts.Length >= 4)
						{
							if (float.TryParse(parts[1], out float x) &&
								float.TryParse(parts[2], out float y) &&
								float.TryParse(parts[3], out float z))
							{
								// Invert Z coordinate
								vertices.Add(new Vector3(x, y, -z));
							}
							else
							{
								Debug.LogWarning($"Invalid vertex format in {inputPath}: {line}");
							}
						}
						break;

					case "vn":
						if (parts.Length >= 4)
						{
							if (float.TryParse(parts[1], out float nx) &&
								float.TryParse(parts[2], out float ny) &&
								float.TryParse(parts[3], out float nz))
							{
								// Invert Z coordinate of normal
								normals.Add(new Vector3(nx, ny, -nz));
							}
							else
							{
								Debug.LogWarning($"Invalid normal format in {inputPath}: {line}");
							}
						}
						break;

					case "vt":
						if (parts.Length >= 3)
						{
							if (float.TryParse(parts[1], out float u) &&
								float.TryParse(parts[2], out float v))
							{
								texCoords.Add(new Vector2(u, v));
							}
							else
							{
								Debug.LogWarning($"Invalid texture coordinate format in {inputPath}: {line}");
							}
						}
						break;

					case "o":
						if (parts.Length >= 2)
						{
							if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
							{
								materialGroups.Add(currentGroup);
							}
							currentGroup = new MaterialGroup
							{
								Faces = new List<Face>(),
								ObjectName = parts[1],
								GroupName = currentGroupName,
								MaterialName = currentMaterial
							};
							currentObjectName = parts[1];
						}
						break;

					case "g":
						if (parts.Length >= 2)
						{
							if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
							{
								materialGroups.Add(currentGroup);
							}
							currentGroup = new MaterialGroup
							{
								Faces = new List<Face>(),
								ObjectName = currentObjectName,
								GroupName = parts[1],
								MaterialName = currentMaterial
							};
							currentGroupName = parts[1];
						}
						break;

					case "usemtl":
						if (parts.Length >= 2)
						{
							if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
							{
								materialGroups.Add(currentGroup);
							}
							currentGroup = new MaterialGroup
							{
								Faces = new List<Face>(),
								ObjectName = currentObjectName,
								GroupName = currentGroupName,
								MaterialName = parts[1]
							};
							currentMaterial = parts[1];
						}
						break;

					case "mtllib":
						mtllib = trimmedLine;
						break;

					case "f":
						if (parts.Length >= 4)
						{
							Face face = new Face
							{
								VertexIndices = new int[parts.Length - 1],
								TexCoordIndices = new int[parts.Length - 1],
								NormalIndices = new int[parts.Length - 1]
							};

							for (int i = 1; i < parts.Length; i++)
							{
								string[] indices = parts[i].Split('/');
								if (indices.Length >= 1 && int.TryParse(indices[0], out int vIndex))
								{
									face.VertexIndices[i - 1] = vIndex;
								}
								if (indices.Length >= 2 && !string.IsNullOrEmpty(indices[1]) && int.TryParse(indices[1], out int vtIndex))
								{
									face.TexCoordIndices[i - 1] = vtIndex;
								}
								if (indices.Length >= 3 && !string.IsNullOrEmpty(indices[2]) && int.TryParse(indices[2], out int vnIndex))
								{
									face.NormalIndices[i - 1] = vnIndex;
								}
							}
							currentGroup.Faces.Add(face);
						}
						else
						{
							Debug.LogWarning($"Invalid face format in {inputPath}: {line}");
						}
						break;
				}
			}

			// Add the last group if it has faces
			if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
			{
				materialGroups.Add(currentGroup);
			}

			// Write the modified .obj file
			using (StreamWriter writer = new StreamWriter(outputPath))
			{
				writer.WriteLine("# Modified by OBJZInverter (Z coordinates inverted, winding order reversed)");
				writer.WriteLine($"# Original file: {Path.GetFileName(inputPath)}");

				// Write mtllib if present
				if (!string.IsNullOrEmpty(mtllib))
				{
					writer.WriteLine(mtllib);
				}

				// Write vertices
				foreach (var vertex in vertices)
				{
					writer.WriteLine($"v {vertex.x:F6} {vertex.y:F6} {vertex.z:F6}");
				}

				// Write texture coordinates
				foreach (var texCoord in texCoords)
				{
					writer.WriteLine($"vt {texCoord.x:F6} {texCoord.y:F6}");
				}

				// Write normals
				foreach (var normal in normals)
				{
					writer.WriteLine($"vn {normal.x:F6} {normal.y:F6} {normal.z:F6}");
				}

				// Write material groups and faces with reversed winding order
				int faceDebugCount = 0;
				foreach (var group in materialGroups)
				{
					if (!string.IsNullOrEmpty(group.ObjectName))
					{
						writer.WriteLine($"o {group.ObjectName}");
					}
					if (!string.IsNullOrEmpty(group.GroupName))
					{
						writer.WriteLine($"g {group.GroupName}");
					}
					if (!string.IsNullOrEmpty(group.MaterialName))
					{
						writer.WriteLine($"usemtl {group.MaterialName}");
					}

					foreach (var face in group.Faces)
					{
						List<string> faceIndices = new List<string>();
						// Reverse the winding order (e.g., 3,2,1 -> 1,2,3)
						for (int i = 0; i < face.VertexIndices.Length; i++)
						{
							int index = face.VertexIndices.Length - 1 - i;
							string indexStr = $"{face.VertexIndices[index]}";
							if (face.TexCoordIndices[index] > 0)
							{
								indexStr += $"/{face.TexCoordIndices[index]}";
							}
							else if (face.NormalIndices[index] > 0)
							{
								indexStr += "/";
							}
							if (face.NormalIndices[index] > 0)
							{
								indexStr += $"/{face.NormalIndices[index]}";
							}
							faceIndices.Add(indexStr);
						}

						// Debug log for the first few faces
						if (faceDebugCount < 5)
						{
							string inputFace = $"f {string.Join(" ", new[] { $"{face.VertexIndices[0]}/{face.TexCoordIndices[0]}/{face.NormalIndices[0]}", $"{face.VertexIndices[1]}/{face.TexCoordIndices[1]}/{face.NormalIndices[1]}", $"{face.VertexIndices[2]}/{face.TexCoordIndices[2]}/{face.NormalIndices[2]}" })}";
							Debug.Log($"File: {Path.GetFileName(inputPath)}, Face {faceDebugCount + 1}: Input: {inputFace}, Output: f {string.Join(" ", faceIndices)}");
							faceDebugCount++;
						}

						writer.WriteLine($"f {string.Join(" ", faceIndices)}");
					}
				}
			}

			return true;
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error processing {inputPath}: {ex.Message}");
			return false;
		}
	}

	// Menu item for Unity Editor with default paths
	[UnityEditor.MenuItem("Tools/Invert OBJ Z Coordinates")]
	public static void RunBatchProcess()
	{
		string inputDir = Path.Combine(Application.dataPath, "InputOBJ");
		string outputDir = Path.Combine(Application.dataPath, "OutputOBJ");
		ProcessOBJFiles(inputDir, outputDir);
	}
}

public class OBJZInverter : MonoBehaviour
{
	public string InputPath = "InputOBJ";
	public string OutputPath = "OutputOBJ";

	private void Start()
	{
		if (string.IsNullOrEmpty(InputPath)) InputPath = "InputOBJ";
		if (string.IsNullOrEmpty(OutputPath)) OutputPath = "OutputOBJ";

		// Combine with Application.dataPath to get full paths
		string inputDir = Path.Combine(Application.dataPath, InputPath);
		string outputDir = Path.Combine(Application.dataPath, OutputPath);
		OBJZInverterHelper.ProcessOBJFiles(inputDir, outputDir);
	}
}


//using System;
//using System.Collections.Generic;
//using System.IO;
//using UnityEngine;

//public class OBJZInverterHelper
//{
//	// Structure to hold vertex data
//	private struct VertexData
//	{
//		public Vector3 Position;
//		public Vector3? Normal;
//		public Vector2? TexCoord;
//	}

//	// Structure to hold face data
//	private struct Face
//	{
//		public int[] VertexIndices;   // 1-based indices for vertices
//		public int[] TexCoordIndices; // 1-based indices for UVs
//		public int[] NormalIndices;   // 1-based indices for normals
//	}

//	// Structure to hold material group
//	private struct MaterialGroup
//	{
//		public string ObjectName;    // For 'o' lines
//		public string GroupName;     // For 'g' lines
//		public string MaterialName;  // For 'usemtl' lines
//		public List<Face> Faces;
//	}

//	public static bool ProcessOBJFiles(string inputDirectory, string outputDirectory)
//	{
//		try
//		{
//			// Ensure directories exist
//			if (!Directory.Exists(inputDirectory))
//			{
//				Debug.LogError($"Input directory {inputDirectory} does not exist.");
//				return false;
//			}

//			if (!Directory.Exists(outputDirectory))
//			{
//				Directory.CreateDirectory(outputDirectory);
//			}

//			// Get all .obj files
//			string[] objFiles = Directory.GetFiles(inputDirectory, "*.obj", SearchOption.TopDirectoryOnly);
//			if (objFiles.Length == 0)
//			{
//				Debug.LogWarning($"No .obj files found in {inputDirectory}.");
//				return false;
//			}

//			int processedCount = 0;
//			foreach (string inputPath in objFiles)
//			{
//				string fileName = Path.GetFileName(inputPath);
//				string outputPath = Path.Combine(outputDirectory, fileName);

//				if (ProcessSingleOBJ(inputPath, outputPath))
//				{
//					processedCount++;
//					Debug.Log($"Successfully processed {fileName}");
//				}
//				else
//				{
//					Debug.LogError($"Failed to process {fileName}");
//				}
//			}

//			Debug.Log($"Processed {processedCount} of {objFiles.Length} .obj files.");
//			return processedCount > 0;
//		}
//		catch (Exception ex)
//		{
//			Debug.LogError($"Error processing files: {ex.Message}");
//			return false;
//		}
//	}

//	private static bool ProcessSingleOBJ(string inputPath, string outputPath)
//	{
//		try
//		{
//			// Read the .obj file
//			string[] lines = File.ReadAllLines(inputPath);

//			List<Vector3> vertices = new List<Vector3>();
//			List<Vector3> normals = new List<Vector3>();
//			List<Vector2> texCoords = new List<Vector2>();
//			List<MaterialGroup> materialGroups = new List<MaterialGroup>();
//			MaterialGroup currentGroup = new MaterialGroup { Faces = new List<Face>() };
//			string currentObjectName = null;
//			string currentGroupName = null;
//			string currentMaterial = null;
//			string mtllib = null;

//			// Parse the .obj file
//			foreach (string line in lines)
//			{
//				string trimmedLine = line.Trim();
//				if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
//					continue;

//				string[] parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
//				if (parts.Length == 0)
//					continue;

//				switch (parts[0].ToLower())
//				{
//					case "v":
//						if (parts.Length >= 4)
//						{
//							if (float.TryParse(parts[1], out float x) &&
//								float.TryParse(parts[2], out float y) &&
//								float.TryParse(parts[3], out float z))
//							{
//								// Invert Z coordinate
//								vertices.Add(new Vector3(x, y, -z));
//							}
//							else
//							{
//								Debug.LogWarning($"Invalid vertex format in {inputPath}: {line}");
//							}
//						}
//						break;

//					case "vn":
//						if (parts.Length >= 4)
//						{
//							if (float.TryParse(parts[1], out float nx) &&
//								float.TryParse(parts[2], out float ny) &&
//								float.TryParse(parts[3], out float nz))
//							{
//								// Invert Z coordinate of normal
//								normals.Add(new Vector3(nx, ny, -nz));
//							}
//							else
//							{
//								Debug.LogWarning($"Invalid normal format in {inputPath}: {line}");
//							}
//						}
//						break;

//					case "vt":
//						if (parts.Length >= 3)
//						{
//							if (float.TryParse(parts[1], out float u) &&
//								float.TryParse(parts[2], out float v))
//							{
//								texCoords.Add(new Vector2(u, v));
//							}
//							else
//							{
//								Debug.LogWarning($"Invalid texture coordinate format in {inputPath}: {line}");
//							}
//						}
//						break;

//					case "o":
//						if (parts.Length >= 2)
//						{
//							if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
//							{
//								materialGroups.Add(currentGroup);
//							}
//							currentGroup = new MaterialGroup
//							{
//								Faces = new List<Face>(),
//								ObjectName = parts[1],
//								GroupName = currentGroupName,
//								MaterialName = currentMaterial
//							};
//							currentObjectName = parts[1];
//						}
//						break;

//					case "g":
//						if (parts.Length >= 2)
//						{
//							if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
//							{
//								materialGroups.Add(currentGroup);
//							}
//							currentGroup = new MaterialGroup
//							{
//								Faces = new List<Face>(),
//								ObjectName = currentObjectName,
//								GroupName = parts[1],
//								MaterialName = currentMaterial
//							};
//							currentGroupName = parts[1];
//						}
//						break;

//					case "usemtl":
//						if (parts.Length >= 2)
//						{
//							if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
//							{
//								materialGroups.Add(currentGroup);
//							}
//							currentGroup = new MaterialGroup
//							{
//								Faces = new List<Face>(),
//								ObjectName = currentObjectName,
//								GroupName = currentGroupName,
//								MaterialName = parts[1]
//							};
//							currentMaterial = parts[1];
//						}
//						break;

//					case "mtllib":
//						mtllib = trimmedLine;
//						break;

//					case "f":
//						if (parts.Length >= 4)
//						{
//							Face face = new Face
//							{
//								VertexIndices = new int[parts.Length - 1],
//								TexCoordIndices = new int[parts.Length - 1],
//								NormalIndices = new int[parts.Length - 1]
//							};

//							for (int i = 1; i < parts.Length; i++)
//							{
//								string[] indices = parts[i].Split('/');
//								if (indices.Length >= 1 && int.TryParse(indices[0], out int vIndex))
//								{
//									face.VertexIndices[i - 1] = vIndex;
//								}
//								if (indices.Length >= 2 && !string.IsNullOrEmpty(indices[1]) && int.TryParse(indices[1], out int vtIndex))
//								{
//									face.TexCoordIndices[i - 1] = vtIndex;
//								}
//								if (indices.Length >= 3 && !string.IsNullOrEmpty(indices[2]) && int.TryParse(indices[2], out int vnIndex))
//								{
//									face.NormalIndices[i - 1] = vnIndex;
//								}
//							}
//							currentGroup.Faces.Add(face);
//						}
//						else
//						{
//							Debug.LogWarning($"Invalid face format in {inputPath}: {line}");
//						}
//						break;
//				}
//			}

//			// Add the last group if it has faces
//			if (currentGroup.Faces.Count > 0 || currentGroup.MaterialName != null || currentGroup.ObjectName != null)
//			{
//				materialGroups.Add(currentGroup);
//			}

//			// Write the modified .obj file
//			using (StreamWriter writer = new StreamWriter(outputPath))
//			{
//				writer.WriteLine("# Modified by OBJZInverter (Z coordinates inverted, winding order reversed)");
//				writer.WriteLine($"# Original file: {Path.GetFileName(inputPath)}");

//				// Write mtllib if present
//				if (!string.IsNullOrEmpty(mtllib))
//				{
//					writer.WriteLine(mtllib);
//				}

//				// Write vertices
//				foreach (var vertex in vertices)
//				{
//					writer.WriteLine($"v {vertex.x:F6} {vertex.y:F6} {vertex.z:F6}");
//				}

//				// Write texture coordinates
//				foreach (var texCoord in texCoords)
//				{
//					writer.WriteLine($"vt {texCoord.x:F6} {texCoord.y:F6}");
//				}

//				// Write normals
//				foreach (var normal in normals)
//				{
//					writer.WriteLine($"vn {normal.x:F6} {normal.y:F6} {normal.z:F6}");
//				}

//				// Write material groups and faces with reversed winding order
//				int faceDebugCount = 0;
//				foreach (var group in materialGroups)
//				{
//					if (!string.IsNullOrEmpty(group.ObjectName))
//					{
//						writer.WriteLine($"o {group.ObjectName}");
//					}
//					if (!string.IsNullOrEmpty(group.GroupName))
//					{
//						writer.WriteLine($"g {group.GroupName}");
//					}
//					if (!string.IsNullOrEmpty(group.MaterialName))
//					{
//						writer.WriteLine($"usemtl {group.MaterialName}");
//					}

//					foreach (var face in group.Faces)
//					{
//						List<string> faceIndices = new List<string>();
//						// Reverse the winding order (e.g., 3,2,1 -> 1,2,3)
//						for (int i = 0; i < face.VertexIndices.Length; i++)
//						{
//							int index = face.VertexIndices.Length - 1 - i;
//							string indexStr = $"{face.VertexIndices[index]}";
//							if (face.TexCoordIndices[index] > 0)
//							{
//								indexStr += $"/{face.TexCoordIndices[index]}";
//							}
//							else if (face.NormalIndices[index] > 0)
//							{
//								indexStr += "/";
//							}
//							if (face.NormalIndices[index] > 0)
//							{
//								indexStr += $"/{face.NormalIndices[index]}";
//							}
//							faceIndices.Add(indexStr);
//						}

//						// Debug log for the first few faces
//						if (faceDebugCount < 5)
//						{
//							string inputFace = $"f {string.Join(" ", new[] { $"{face.VertexIndices[0]}/{face.TexCoordIndices[0]}/{face.NormalIndices[0]}", $"{face.VertexIndices[1]}/{face.TexCoordIndices[1]}/{face.NormalIndices[1]}", $"{face.VertexIndices[2]}/{face.TexCoordIndices[2]}/{face.NormalIndices[2]}" })}";
//							Debug.Log($"File: {Path.GetFileName(inputPath)}, Face {faceDebugCount + 1}: Input: {inputFace}, Output: f {string.Join(" ", faceIndices)}");
//							faceDebugCount++;
//						}

//						writer.WriteLine($"f {string.Join(" ", faceIndices)}");
//					}
//				}
//			}

//			return true;
//		}
//		catch (Exception ex)
//		{
//			Debug.LogError($"Error processing {inputPath}: {ex.Message}");
//			return false;
//		}
//	}

//	// Menu item for Unity Editor with default paths
//	[UnityEditor.MenuItem("Tools/Invert OBJ Z Coordinates")]
//	public static void RunBatchProcess()
//	{
//		string inputDir = Path.Combine(Application.dataPath, "InputOBJ");
//		string outputDir = Path.Combine(Application.dataPath, "OutputOBJ");
//		ProcessOBJFiles(inputDir, outputDir);
//	}
//}

//public class OBJZInverter : MonoBehaviour
//{
//	public string InputPath = "InputOBJ";
//	public string OutputPath = "OutputOBJ";

//	private void Start()
//	{
//		if (string.IsNullOrEmpty(InputPath)) InputPath = "InputOBJ";
//		if (string.IsNullOrEmpty(OutputPath)) OutputPath = "OutputOBJ";

//		// Combine with Application.dataPath to get full paths
//		string inputDir = Path.Combine(Application.dataPath, InputPath);
//		string outputDir = Path.Combine(Application.dataPath, OutputPath);
//		OBJZInverterHelper.ProcessOBJFiles(inputDir, outputDir);
//	}
//}
