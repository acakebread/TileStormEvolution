using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MassiveHadronLtd
{
	public class OBJJoin
	{
		// Simple directional enum for naming-based joining
		private enum Direction
		{
			Unknown = 0,
			N, S, E, W, NE, NW, SE, SW, Center
		}

		private class MeshData
		{
			// exactly the same fields
			public List<Vector3> Vertices;
			public List<Vector2> TexCoords;
			public List<Vector3> Normals;
			public List<MaterialGroup> Groups;
			public Bounds Bounds;
			public string OriginalFilename;
			public Direction Dir;
			public Vector3 Shift;           // ← this will now be mutable
		}

		private struct Face
		{
			public int[] VertexIndices;   // 1-based
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

		public static bool JoinOBJFiles(
			string inputDirectory,
			string outputDirectory,
			string outputFilename = "CombinedMesh.obj")
		{
			try
			{
				if (!Directory.Exists(inputDirectory))
				{
					Debug.LogError($"Input directory not found: {inputDirectory}");
					return false;
				}

				Directory.CreateDirectory(outputDirectory);

				string[] objFiles = Directory.GetFiles(inputDirectory, "*.obj", SearchOption.TopDirectoryOnly);
				if (objFiles.Length < 2)
				{
					Debug.LogError($"Need at least 2 .obj files to join. Found: {objFiles.Length}");
					return false;
				}

				List<MeshData> pieces = new List<MeshData>();

				// 1. Load all meshes
				foreach (string path in objFiles)
				{
					var data = LoadSingleOBJ(path);
					if (data.Vertices.Count == 0) continue;

					data.OriginalFilename = Path.GetFileNameWithoutExtension(path);
					data.Dir = GuessDirectionFromFilename(data.OriginalFilename);
					pieces.Add(data);
				}

				if (pieces.Count < 2)
				{
					Debug.LogError("No valid meshes loaded.");
					return false;
				}

				// 2. Decide shifts
				ComputeShifts(pieces);

				// 3. Merge everything
				var merged = MergeMeshes(pieces);

				// 4. Write result
				string outPath = Path.Combine(outputDirectory, outputFilename);
				WriteCombinedOBJ(merged, outPath, pieces);

				Debug.Log($"Successfully joined {pieces.Count} meshes → {outPath}");

#if UNITY_EDITOR
				AssetDatabase.Refresh();
#endif
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"Join failed: {e.Message}\n{e.StackTrace}");
				return false;
			}
		}

		// ────────────────────────────────────────────────────────────────
		//  Loading single OBJ (similar to your original script)
		// ────────────────────────────────────────────────────────────────
		private static MeshData LoadSingleOBJ(string path)
		{
			var data = new MeshData
			{
				Vertices = new List<Vector3>(),
				TexCoords = new List<Vector2>(),
				Normals = new List<Vector3>(),
				Groups = new List<MaterialGroup>()
			};

			string[] lines = File.ReadAllLines(path);
			MaterialGroup current = new MaterialGroup { Faces = new List<Face>() };

			foreach (string line in lines)
			{
				string t = line.Trim();
				if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;

				string[] p = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (p.Length == 0) continue;

				switch (p[0].ToLower())
				{
					case "v":
						if (p.Length >= 4 && float.TryParse(p[1], out float x) &&
											float.TryParse(p[2], out float y) &&
											float.TryParse(p[3], out float z))
						{
							data.Vertices.Add(new Vector3(x, y, z));
						}
						break;

					case "vt":
						if (p.Length >= 3 && float.TryParse(p[1], out float u) &&
											float.TryParse(p[2], out float v))
						{
							data.TexCoords.Add(new Vector2(u, v));
						}
						break;

					case "vn":
						if (p.Length >= 4 && float.TryParse(p[1], out float nx) &&
											float.TryParse(p[2], out float ny) &&
											float.TryParse(p[3], out float nz))
						{
							data.Normals.Add(new Vector3(nx, ny, nz));
						}
						break;

					case "o":
					case "g":
					case "usemtl":
						if (current.Faces.Count > 0)
							data.Groups.Add(current);

						current = new MaterialGroup { Faces = new List<Face>() };

						if (p[0].ToLower() == "o" && p.Length > 1) current.ObjectName = p[1];
						if (p[0].ToLower() == "g" && p.Length > 1) current.GroupName = p[1];
						if (p[0].ToLower() == "usemtl" && p.Length > 1) current.MaterialName = p[1];
						break;

					case "f":
						if (p.Length >= 4)
						{
							Face f = new Face
							{
								VertexIndices = new int[p.Length - 1],
								TexCoordIndices = new int[p.Length - 1],
								NormalIndices = new int[p.Length - 1]
							};

							for (int i = 1; i < p.Length; i++)
							{
								string[] idx = p[i].Split('/');
								if (idx.Length >= 1 && int.TryParse(idx[0], out int vi))
									f.VertexIndices[i - 1] = vi;
								if (idx.Length >= 2 && !string.IsNullOrEmpty(idx[1]) && int.TryParse(idx[1], out int vti))
									f.TexCoordIndices[i - 1] = vti;
								if (idx.Length >= 3 && !string.IsNullOrEmpty(idx[2]) && int.TryParse(idx[2], out int vni))
									f.NormalIndices[i - 1] = vni;
							}
							current.Faces.Add(f);
						}
						break;
				}
			}

			if (current.Faces.Count > 0)
				data.Groups.Add(current);

			// Compute bounds
			if (data.Vertices.Count > 0)
			{
				Vector3 min = data.Vertices[0];
				Vector3 max = data.Vertices[0];
				foreach (var v in data.Vertices)
				{
					min = Vector3.Min(min, v);
					max = Vector3.Max(max, v);
				}
				data.Bounds = new Bounds((min + max) * 0.5f, max - min);
			}

			return data;
		}

		// ────────────────────────────────────────────────────────────────
		//  Direction from filename (most reliable method)
		// ────────────────────────────────────────────────────────────────
		private static Direction GuessDirectionFromFilename(string name)
		{
			name = name.ToUpperInvariant();
			if (name.Contains("NORTH") || name.EndsWith("_N") || name.Contains("_N.")) return Direction.N;
			if (name.Contains("SOUTH") || name.EndsWith("_S") || name.Contains("_S.")) return Direction.S;
			if (name.Contains("EAST") || name.EndsWith("_E") || name.Contains("_E.")) return Direction.E;
			if (name.Contains("WEST") || name.EndsWith("_W") || name.Contains("_W.")) return Direction.W;
			if (name.Contains("NE") || name.EndsWith("_NE")) return Direction.NE;
			if (name.Contains("NW") || name.EndsWith("_NW")) return Direction.NW;
			if (name.Contains("SE") || name.EndsWith("_SE")) return Direction.SE;
			if (name.Contains("SW") || name.EndsWith("_SW")) return Direction.SW;
			if (name.Contains("CENTER") || name.Contains("C")) return Direction.Center;
			return Direction.Unknown;
		}

		// ────────────────────────────────────────────────────────────────
		//  Compute how much each piece should be moved
		// ────────────────────────────────────────────────────────────────
		private static void ComputeShifts(List<MeshData> pieces)
		{
			// Strategy 1: Naming convention based (preferred)
			bool usedNaming = false;

			foreach (var p in pieces)
			{
				if (p.Dir != Direction.Unknown)
				{
					usedNaming = true;
					break;
				}
			}

			if (usedNaming)
			{
				foreach (var p in pieces)
				{
					Vector3 shift = Vector3.zero;

					switch (p.Dir)
					{
						case Direction.N: shift = new Vector3(0, 0, p.Bounds.extents.z); break;   // move North piece south
						case Direction.S: shift = new Vector3(0, 0, -p.Bounds.extents.z); break;  // move South piece north
						case Direction.E: shift = new Vector3(-p.Bounds.extents.x, 0, 0); break;
						case Direction.W: shift = new Vector3(p.Bounds.extents.x, 0, 0); break;
						case Direction.NE: shift = new Vector3(-p.Bounds.extents.x, 0, p.Bounds.extents.z); break;
						case Direction.NW: shift = new Vector3(p.Bounds.extents.x, 0, p.Bounds.extents.z); break;
						case Direction.SE: shift = new Vector3(-p.Bounds.extents.x, 0, -p.Bounds.extents.z); break;
						case Direction.SW: shift = new Vector3(p.Bounds.extents.x, 0, -p.Bounds.extents.z); break;
							// Center → no shift
					}

					p.Shift = shift;
				}
			}
			else
			{
				// Fallback: very naive — assume order is N,S or W,E or 2×2 grid in filename order
				// You can improve this later (e.g. compare closest edges)
				Debug.LogWarning("No directional naming detected → using very simple fallback shift logic");

				if (pieces.Count == 2)
				{
					// Guess: first = North, second = South
					pieces[0].Shift = new Vector3(0, 0, pieces[0].Bounds.extents.z);
					pieces[1].Shift = new Vector3(0, 0, -pieces[1].Bounds.extents.z);
				}
				// ... you can extend for 4 pieces etc.
			}

			// Debug print
			foreach (var p in pieces)
			{
				Debug.Log($"Piece {p.OriginalFilename,-12} dir={p.Dir,-6} shift={p.Shift:F3}");
			}
		}

		// ────────────────────────────────────────────────────────────────
		//  Merge all pieces into one big mesh
		// ────────────────────────────────────────────────────────────────
		private static MeshData MergeMeshes(List<MeshData> pieces)
		{
			var merged = new MeshData
			{
				Vertices = new List<Vector3>(),
				TexCoords = new List<Vector2>(),
				Normals = new List<Vector3>(),
				Groups = new List<MaterialGroup>()
			};

			int vertexOffset = 0;
			int texOffset = 0;
			int normalOffset = 0;

			foreach (var piece in pieces)
			{
				// Shift vertices
				for (int i = 0; i < piece.Vertices.Count; i++)
				{
					merged.Vertices.Add(piece.Vertices[i] + piece.Shift);
				}

				merged.TexCoords.AddRange(piece.TexCoords);
				merged.Normals.AddRange(piece.Normals);

				foreach (var group in piece.Groups)
				{
					var newGroup = new MaterialGroup
					{
						ObjectName = group.ObjectName,
						GroupName = group.GroupName,
						MaterialName = group.MaterialName,
						Faces = new List<Face>()
					};

					foreach (var face in group.Faces)
					{
						var nf = new Face
						{
							VertexIndices = (int[])face.VertexIndices.Clone(),
							TexCoordIndices = (int[])face.TexCoordIndices.Clone(),
							NormalIndices = (int[])face.NormalIndices.Clone()
						};

						for (int i = 0; i < nf.VertexIndices.Length; i++)
						{
							if (nf.VertexIndices[i] > 0)
								nf.VertexIndices[i] += vertexOffset;

							if (nf.TexCoordIndices[i] > 0)
								nf.TexCoordIndices[i] += texOffset;

							if (nf.NormalIndices[i] > 0)
								nf.NormalIndices[i] += normalOffset;
						}

						newGroup.Faces.Add(nf);
					}

					merged.Groups.Add(newGroup);
				}

				vertexOffset += piece.Vertices.Count;
				texOffset += piece.TexCoords.Count;
				normalOffset += piece.Normals.Count;
			}

			return merged;
		}

		// ────────────────────────────────────────────────────────────────
		//  Write final combined OBJ
		// ────────────────────────────────────────────────────────────────
		private static void WriteCombinedOBJ(MeshData data, string path, List<MeshData> sources)
		{
			using (var w = new StreamWriter(path))
			{
				w.WriteLine("# Combined OBJ created by OBJJoin");
				w.WriteLine($"# Source files: {string.Join(", ", sources.Select(s => s.OriginalFilename + ".obj"))}");
				w.WriteLine("#");

				foreach (var v in data.Vertices)
					w.WriteLine($"v {v.x:F6} {v.y:F6} {v.z:F6}");

				foreach (var t in data.TexCoords)
					w.WriteLine($"vt {t.x:F6} {t.y:F6}");

				foreach (var n in data.Normals)
					w.WriteLine($"vn {n.x:F6} {n.y:F6} {n.z:F6}");

				foreach (var g in data.Groups)
				{
					if (!string.IsNullOrEmpty(g.ObjectName)) w.WriteLine($"o {g.ObjectName}");
					if (!string.IsNullOrEmpty(g.GroupName)) w.WriteLine($"g {g.GroupName}");
					if (!string.IsNullOrEmpty(g.MaterialName)) w.WriteLine($"usemtl {g.MaterialName}");

					foreach (var f in g.Faces)
					{
						var indices = new List<string>();
						for (int i = 0; i < f.VertexIndices.Length; i++)
						{
							string s = f.VertexIndices[i].ToString();
							if (f.TexCoordIndices[i] > 0)
								s += $"/{f.TexCoordIndices[i]}";
							else if (f.NormalIndices[i] > 0)
								s += "/";
							if (f.NormalIndices[i] > 0)
								s += $"/{f.NormalIndices[i]}";
							indices.Add(s);
						}
						w.WriteLine($"f {string.Join(" ", indices)}");
					}
				}
			}
		}

		// ────────────────────────────────────────────────────────────────
		//  Editor menu items (optional)
		// ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
		[UnityEditor.MenuItem("Tools/OBJ Join - Combine Terrain Pieces")]
		private static void MenuJoin()
		{
			string input = Path.Combine(Application.dataPath, "InputTerrain");
			string output = Path.Combine(Application.dataPath, "OutputCombined");
			JoinOBJFiles(input, output, "Combined_Terrain.obj");
		}
#endif
	}
}