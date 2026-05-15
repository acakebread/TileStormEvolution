using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	public static class OBJCleanBruteForce
	{
		// ───────────────────────────────────────────────
		// Define the structs here (was missing in previous test)
		// ───────────────────────────────────────────────
		private struct Face
		{
			public int[] VertexIndices;     // 1-based
			public int[] TexCoordIndices;
			public int[] NormalIndices;
		}

		private struct MaterialGroup
		{
			public string ObjectName;
			public string GroupName;
			public string MaterialName;
			public List<Face> Faces;
		}

		public static bool Clean(
			string inputPath,
			string outputPath = null,
			float tolerance = 0.001f)
		{
			if (string.IsNullOrEmpty(outputPath))
			{
				var dir = Path.GetDirectoryName(inputPath);
				var name = Path.GetFileNameWithoutExtension(inputPath);
				outputPath = Path.Combine(dir, $"{name}_bruteforce.obj");
			}

			try
			{
				Debug.Log($"[BruteForce Clean] START → {Path.GetFileName(inputPath)}");
				Debug.Log($"[BruteForce Clean] Tolerance = {tolerance:F6}");

				string[] lines = File.ReadAllLines(inputPath);

				var vertices = new List<Vector3>();
				var texCoords = new List<Vector2>();
				var normals = new List<Vector3>();
				var groups = new List<MaterialGroup>();
				var currentGroup = new MaterialGroup { Faces = new List<Face>() };

				string mtllib = null;

				foreach (string line in lines)
				{
					string trimmed = line.Trim();
					if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

					string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 0) continue;

					switch (parts[0].ToLowerInvariant())
					{
						case "mtllib": mtllib = trimmed; break;

						case "v":
							if (parts.Length >= 4 &&
								float.TryParse(parts[1], out float vx) &&
								float.TryParse(parts[2], out float vy) &&
								float.TryParse(parts[3], out float vz))
							{
								vertices.Add(new Vector3(vx, vy, vz));
							}
							break;

						case "vt":
							if (parts.Length >= 3 &&
								float.TryParse(parts[1], out float tu) &&
								float.TryParse(parts[2], out float tv))
							{
								texCoords.Add(new Vector2(tu, tv));
							}
							break;

						case "vn":
							if (parts.Length >= 4 &&
								float.TryParse(parts[1], out float nx) &&
								float.TryParse(parts[2], out float ny) &&
								float.TryParse(parts[3], out float nz))
							{
								normals.Add(new Vector3(nx, ny, nz));
							}
							break;

						case "o":
						case "g":
						case "usemtl":
							if (currentGroup.Faces.Count > 0)
								groups.Add(currentGroup);

							currentGroup = new MaterialGroup { Faces = new List<Face>() };

							if (parts.Length > 1)
							{
								if (parts[0] == "o") currentGroup.ObjectName = parts[1];
								if (parts[0] == "g") currentGroup.GroupName = parts[1];
								if (parts[0] == "usemtl") currentGroup.MaterialName = parts[1];
							}
							break;

						case "f":
							if (parts.Length >= 4)
							{
								var face = new Face
								{
									VertexIndices = new int[parts.Length - 1],
									TexCoordIndices = new int[parts.Length - 1],
									NormalIndices = new int[parts.Length - 1]
								};

								for (int i = 1; i < parts.Length; i++)
								{
									string[] idx = parts[i].Split('/');
									if (idx.Length >= 1 && int.TryParse(idx[0], out int vi) && vi != 0)
										face.VertexIndices[i - 1] = vi;
									if (idx.Length >= 2 && !string.IsNullOrEmpty(idx[1]) && int.TryParse(idx[1], out int ti) && ti != 0)
										face.TexCoordIndices[i - 1] = ti;
									if (idx.Length >= 3 && !string.IsNullOrEmpty(idx[2]) && int.TryParse(idx[2], out int ni) && ni != 0)
										face.NormalIndices[i - 1] = ni;
								}
								currentGroup.Faces.Add(face);
							}
							break;
					}
				}

				if (currentGroup.Faces.Count > 0)
					groups.Add(currentGroup);

				Debug.Log($"Parsed vertices: {vertices.Count}");

				if (vertices.Count == 0)
				{
					Debug.LogError("No vertices found — aborting");
					return false;
				}

				// ───────────────────────────────────────────────
				// BRUTE-FORCE WELDING (real distance, no rounding tricks)
				// ───────────────────────────────────────────────
				Debug.Log("Starting brute-force duplicate detection...");

				var cleanedVertices = new List<Vector3>();
				var oldToNew = new int[vertices.Count + 1]; // 1-based

				var mergedInto = new int[vertices.Count];
				for (int i = 0; i < mergedInto.Length; i++) mergedInto[i] = -1;

				int uniqueCount = 0;

				for (int i = 0; i < vertices.Count; i++)
				{
					if (mergedInto[i] != -1) continue;

					var pos = vertices[i];
					cleanedVertices.Add(pos);
					uniqueCount++;
					mergedInto[i] = uniqueCount; // 1-based

					for (int j = i + 1; j < vertices.Count; j++)
					{
						if (mergedInto[j] != -1) continue;
						if (Vector3.Distance(pos, vertices[j]) <= tolerance)
						{
							mergedInto[j] = uniqueCount;
						}
					}
				}

				for (int i = 0; i < vertices.Count; i++)
				{
					oldToNew[i + 1] = mergedInto[i];
				}

				int removed = vertices.Count - cleanedVertices.Count;
				Debug.Log($"Welding complete → kept {cleanedVertices.Count} vertices (removed {removed})");

				// ───────────────────────────────────────────────
				// Remap faces
				// ───────────────────────────────────────────────
				foreach (var group in groups)
				{
					foreach (var face in group.Faces)
					{
						for (int j = 0; j < face.VertexIndices.Length; j++)
						{
							int oldVi = face.VertexIndices[j];
							if (oldVi > 0 && oldVi <= vertices.Count)
							{
								face.VertexIndices[j] = oldToNew[oldVi];
							}
						}
					}
				}

				// ───────────────────────────────────────────────
				// Write result
				// ───────────────────────────────────────────────
				using (var writer = new StreamWriter(outputPath))
				{
					writer.WriteLine("# OBJClean Brute-Force Test");
					writer.WriteLine($"# Tolerance: {tolerance:F6}");
					writer.WriteLine($"# Original verts: {vertices.Count} → after: {cleanedVertices.Count}");

					if (!string.IsNullOrEmpty(mtllib))
						writer.WriteLine(mtllib);

					foreach (var v in cleanedVertices)
						writer.WriteLine($"v {v.x:F6} {v.y:F6} {v.z:F6}");

					foreach (var t in texCoords)
						writer.WriteLine($"vt {t.x:F6} {t.y:F6}");

					foreach (var n in normals)
						writer.WriteLine($"vn {n.x:F6} {n.y:F6} {n.z:F6}");

					foreach (var group in groups)
					{
						if (!string.IsNullOrEmpty(group.ObjectName)) writer.WriteLine($"o {group.ObjectName}");
						if (!string.IsNullOrEmpty(group.GroupName)) writer.WriteLine($"g {group.GroupName}");
						if (!string.IsNullOrEmpty(group.MaterialName)) writer.WriteLine($"usemtl {group.MaterialName}");

						foreach (var face in group.Faces)
						{
							var parts = new List<string>();
							for (int k = 0; k < face.VertexIndices.Length; k++)
							{
								string s = face.VertexIndices[k].ToString();
								if (face.TexCoordIndices[k] != 0)
									s += $"/{face.TexCoordIndices[k]}";
								else if (face.NormalIndices[k] != 0)
									s += "/";
								if (face.NormalIndices[k] != 0)
									s += $"/{face.NormalIndices[k]}";
								parts.Add(s);
							}
							writer.WriteLine($"f {string.Join(" ", parts)}");
						}
					}
				}

				Debug.Log($"Saved cleaned file → {outputPath}");

#if UNITY_EDITOR
				AssetDatabase.Refresh();
#endif
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"Clean failed: {ex.Message}\nStack: {ex.StackTrace}");
				return false;
			}
		}

#if UNITY_EDITOR
		[UnityEditor.MenuItem("Tools/Classic Tilestorm/Models/OBJ/Clean - BRUTE FORCE (selected original .obj)")]
		private static void CleanOriginal()
		{
			string path = GetSelectedOBJPath();
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogWarning("No .obj file selected.");
				return;
			}

			if (path.Contains("_clean") || path.Contains("_bruteforce"))
			{
				Debug.LogError("Please select the ORIGINAL file (not a _clean or _bruteforce version)!");
				return;
			}

			Clean(path, tolerance: 0.001f);
		}

		private static string GetSelectedOBJPath()
		{
			var obj = Selection.activeObject;
			if (obj == null) return null;

			string path = AssetDatabase.GetAssetPath(obj);
			if (!path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)) return null;

			return Path.Combine(Application.dataPath, "..", path);
		}
#endif
	}
}

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using UnityEngine;
//#if UNITY_EDITOR
//using UnityEditor;
//#endif

//namespace MassiveHadronLtd
//{
//	public static class OBJClean
//	{
//		// Tolerance for considering two vertices "the same"
//		// For your -0.5..+0.5 pieces, 0.0001–0.001 usually works well
//		private const float DEFAULT_TOLERANCE = 0.5f;

//		private struct VertexKey : IEquatable<VertexKey>
//		{
//			public readonly float X, Y, Z;

//			public VertexKey(Vector3 v, float tolerance)
//			{
//				X = Mathf.Round(v.x / tolerance) * tolerance;
//				Y = Mathf.Round(v.y / tolerance) * tolerance;
//				Z = Mathf.Round(v.z / tolerance) * tolerance;
//			}

//			public bool Equals(VertexKey other)
//			{
//				return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
//			}

//			public override bool Equals(object obj) => obj is VertexKey other && Equals(other);

//			public override int GetHashCode() => HashCode.Combine(X, Y, Z);
//		}

//		private struct Face
//		{
//			public int[] VertexIndices;     // will be remapped
//			public int[] TexCoordIndices;
//			public int[] NormalIndices;
//		}

//		private struct MaterialGroup
//		{
//			public string ObjectName;
//			public string GroupName;
//			public string MaterialName;
//			public List<Face> Faces;
//		}

//		public static bool Clean(
//			string inputPath,
//			string outputPath = null,
//			float tolerance = DEFAULT_TOLERANCE,
//			bool normalizeWinding = false)  // optional: flip face winding if needed
//		{
//			if (string.IsNullOrEmpty(outputPath))
//			{
//				var dir = Path.GetDirectoryName(inputPath);
//				var name = Path.GetFileNameWithoutExtension(inputPath);
//				outputPath = Path.Combine(dir, $"{name}_clean.obj");
//			}

//			try
//			{
//				// ───────────────────────────────────────────────
//				// 1. Parse original OBJ
//				// ───────────────────────────────────────────────
//				string[] lines = File.ReadAllLines(inputPath);

//				var vertices = new List<Vector3>();
//				var texCoords = new List<Vector2>();
//				var normals = new List<Vector3>();
//				var groups = new List<MaterialGroup>();
//				var currentGroup = new MaterialGroup { Faces = new List<Face>() };

//				string mtllib = null;

//				foreach (string line in lines)
//				{
//					string trimmed = line.Trim();
//					if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

//					string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
//					if (parts.Length == 0) continue;

//					switch (parts[0].ToLowerInvariant())
//					{
//						case "mtllib":
//							mtllib = trimmed;
//							break;

//						case "v":
//							if (parts.Length >= 4 &&
//								float.TryParse(parts[1], out float vx) &&
//								float.TryParse(parts[2], out float vy) &&
//								float.TryParse(parts[3], out float vz))
//							{
//								vertices.Add(new Vector3(vx, vy, vz));
//							}
//							break;

//						case "vt":
//							if (parts.Length >= 3 &&
//								float.TryParse(parts[1], out float tu) &&
//								float.TryParse(parts[2], out float tv))
//							{
//								texCoords.Add(new Vector2(tu, tv));
//							}
//							break;

//						case "vn":
//							if (parts.Length >= 4 &&
//								float.TryParse(parts[1], out float nx) &&
//								float.TryParse(parts[2], out float ny) &&
//								float.TryParse(parts[3], out float nz))
//							{
//								normals.Add(new Vector3(nx, ny, nz));
//							}
//							break;

//						case "o":
//						case "g":
//						case "usemtl":
//							if (currentGroup.Faces.Count > 0)
//								groups.Add(currentGroup);

//							currentGroup = new MaterialGroup { Faces = new List<Face>() };

//							if (parts[0].ToLower() == "o" && parts.Length > 1) currentGroup.ObjectName = parts[1];
//							if (parts[0].ToLower() == "g" && parts.Length > 1) currentGroup.GroupName = parts[1];
//							if (parts[0].ToLower() == "usemtl" && parts.Length > 1) currentGroup.MaterialName = parts[1];
//							break;

//						case "f":
//							if (parts.Length >= 4)
//							{
//								var face = new Face
//								{
//									VertexIndices = new int[parts.Length - 1],
//									TexCoordIndices = new int[parts.Length - 1],
//									NormalIndices = new int[parts.Length - 1]
//								};

//								for (int i = 1; i < parts.Length; i++)
//								{
//									string[] idx = parts[i].Split('/');
//									if (idx.Length >= 1 && int.TryParse(idx[0], out int vi) && vi != 0)
//										face.VertexIndices[i - 1] = vi;
//									if (idx.Length >= 2 && !string.IsNullOrEmpty(idx[1]) && int.TryParse(idx[1], out int ti) && ti != 0)
//										face.TexCoordIndices[i - 1] = ti;
//									if (idx.Length >= 3 && !string.IsNullOrEmpty(idx[2]) && int.TryParse(idx[2], out int ni) && ni != 0)
//										face.NormalIndices[i - 1] = ni;
//								}
//								currentGroup.Faces.Add(face);
//							}
//							break;
//					}
//				}

//				if (currentGroup.Faces.Count > 0)
//					groups.Add(currentGroup);

//				if (vertices.Count == 0)
//				{
//					Debug.LogWarning("No vertices found in OBJ.");
//					return false;
//				}

//				// ───────────────────────────────────────────────
//				// 2. Build vertex welding map
//				// ───────────────────────────────────────────────
//				var positionToNewIndex = new Dictionary<VertexKey, int>();
//				var oldToNew = new int[vertices.Count + 1]; // 1-based

//				var cleanedVertices = new List<Vector3>();

//				for (int i = 0; i < vertices.Count; i++)
//				{
//					var key = new VertexKey(vertices[i], tolerance);

//					if (!positionToNewIndex.TryGetValue(key, out int newIdx))
//					{
//						newIdx = cleanedVertices.Count;
//						cleanedVertices.Add(vertices[i]);
//						positionToNewIndex[key] = newIdx;
//					}

//					oldToNew[i + 1] = newIdx + 1; // keep 1-based for OBJ
//				}

//				// ───────────────────────────────────────────────
//				// 3. Remap all faces
//				// ───────────────────────────────────────────────
//				foreach (var group in groups)
//				{
//					foreach (var face in group.Faces)
//					{
//						for (int j = 0; j < face.VertexIndices.Length; j++)
//						{
//							int oldVi = face.VertexIndices[j];
//							if (oldVi > 0 && oldVi <= vertices.Count)
//								face.VertexIndices[j] = oldToNew[oldVi];

//							// UV & normals are **not** welded (common practice)
//							// If you want to weld them too → more complex logic needed
//						}

//						// Optional: reverse winding (sometimes needed after joins)
//						if (normalizeWinding)
//						{
//							Array.Reverse(face.VertexIndices);
//							Array.Reverse(face.TexCoordIndices);
//							Array.Reverse(face.NormalIndices);
//						}
//					}
//				}

//				// ───────────────────────────────────────────────
//				// 4. Write cleaned OBJ
//				// ───────────────────────────────────────────────
//				using (var writer = new StreamWriter(outputPath))
//				{
//					writer.WriteLine("# Cleaned by OBJClean - MassiveHadronLtd");
//					writer.WriteLine($"# Tolerance: {tolerance:F6}");
//					writer.WriteLine($"# Original: {Path.GetFileName(inputPath)}");
//					writer.WriteLine($"# Vertices before: {vertices.Count} → after: {cleanedVertices.Count}");

//					if (!string.IsNullOrEmpty(mtllib))
//						writer.WriteLine(mtllib);

//					foreach (var v in cleanedVertices)
//						writer.WriteLine($"v {v.x:F6} {v.y:F6} {v.z:F6}");

//					foreach (var t in texCoords)
//						writer.WriteLine($"vt {t.x:F6} {t.y:F6}");

//					foreach (var n in normals)
//						writer.WriteLine($"vn {n.x:F6} {n.y:F6} {n.z:F6}");

//					foreach (var group in groups)
//					{
//						if (!string.IsNullOrEmpty(group.ObjectName))
//							writer.WriteLine($"o {group.ObjectName}");
//						if (!string.IsNullOrEmpty(group.GroupName))
//							writer.WriteLine($"g {group.GroupName}");
//						if (!string.IsNullOrEmpty(group.MaterialName))
//							writer.WriteLine($"usemtl {group.MaterialName}");

//						foreach (var face in group.Faces)
//						{
//							var parts = new List<string>();
//							for (int k = 0; k < face.VertexIndices.Length; k++)
//							{
//								string s = face.VertexIndices[k].ToString();

//								if (face.TexCoordIndices[k] != 0)
//									s += $"/{face.TexCoordIndices[k]}";
//								else if (face.NormalIndices[k] != 0)
//									s += "/";

//								if (face.NormalIndices[k] != 0)
//									s += $"/{face.NormalIndices[k]}";

//								parts.Add(s);
//							}
//							writer.WriteLine($"f {string.Join(" ", parts)}");
//						}
//					}
//				}

//				Debug.Log($"Cleaned OBJ saved → {outputPath}  (vertices reduced from {vertices.Count} to {cleanedVertices.Count})");

//#if UNITY_EDITOR
//				AssetDatabase.Refresh();
//#endif
//				return true;
//			}
//			catch (Exception ex)
//			{
//				Debug.LogError($"OBJClean failed: {ex.Message}");
//				return false;
//			}
//		}

//		// ────────────────────────────────────────────────────────────────
//		//  Convenience overloads / editor helpers
//		// ────────────────────────────────────────────────────────────────
//#if UNITY_EDITOR
//		[UnityEditor.MenuItem("Tools/OBJ Clean - Weld Duplicates (selected .obj)")]
//		private static void CleanSelected()
//		{
//			string path = GetSelectedOBJPath();
//			if (string.IsNullOrEmpty(path))
//			{
//				Debug.LogWarning("No .obj file selected.");
//				return;
//			}

//			Clean(path, tolerance: 0.0002f);
//		}

//		private static string GetSelectedOBJPath()
//		{
//			var obj = Selection.activeObject;
//			if (obj == null) return null;

//			string path = AssetDatabase.GetAssetPath(obj);
//			if (string.IsNullOrEmpty(path) || !path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
//				return null;

//			return Path.Combine(Application.dataPath, "..", path);
//		}
//#endif

//		// Example batch usage
//		public static void CleanInFolder(string inputFolder, float tolerance = DEFAULT_TOLERANCE)
//		{
//			if (!Directory.Exists(inputFolder)) return;

//			foreach (var file in Directory.GetFiles(inputFolder, "*.obj"))
//			{
//				Clean(file, tolerance: tolerance);
//			}
//		}
//	}
//}
