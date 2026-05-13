using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MassiveHadronLtd
{
	[Serializable]
	public class WavefrontMesh
	{
		public string name = "Mesh";
		public string materialName = "";                    // NEW: For usemtl
		public List<Vector3> vertices = new List<Vector3>();
		public List<Vector3> normals = new List<Vector3>();
		public List<Vector2> uvs = new List<Vector2>();
		public List<int> triangles = new List<int>();

		public WavefrontMesh() { }

		public WavefrontMesh(string objPath)
		{
			FromObjFile(objPath);
		}

		public void FromObjFile(string objPath)
		{
			if (!File.Exists(objPath))
			{
				Debug.LogError($"OBJ file not found: {objPath}");
				return;
			}

			string text = File.ReadAllText(objPath);
			FromObjText(text);
		}

		public void FromObjText(string objText, string meshName = "LoadedOBJ")
		{
			name = meshName;
			vertices.Clear();
			normals.Clear();
			uvs.Clear();
			triangles.Clear();
			materialName = "";

			var lines = objText.Split('\n');

			foreach (var raw in lines)
			{
				var line = raw.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

				var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				switch (parts[0])
				{
					case "v": vertices.Add(ParseVec3(parts)); break;
					case "vn": normals.Add(ParseVec3(parts)); break;
					case "vt": uvs.Add(ParseVec2(parts)); break;
					case "usemtl":
						if (parts.Length > 1) materialName = parts[1];
						break;
					case "f":
						ParseFace(parts);
						break;
				}
			}
		}

		public void FromUnityMesh(Mesh unityMesh, string meshName = null, string matName = "")
		{
			if (unityMesh == null) return;

			name = meshName ?? unityMesh.name ?? "ExportedMesh";
			materialName = matName;

			vertices = new List<Vector3>(unityMesh.vertices);
			normals = unityMesh.normals != null ? new List<Vector3>(unityMesh.normals) : new List<Vector3>();
			uvs = unityMesh.uv != null ? new List<Vector2>(unityMesh.uv) : new List<Vector2>();
			triangles = new List<int>(unityMesh.triangles);
		}

		public Mesh ToUnityMesh()
		{
			Mesh mesh = new Mesh { name = name };

			mesh.SetVertices(vertices);
			mesh.SetTriangles(triangles, 0);
			mesh.SetUVs(0, uvs);

			if (normals.Count > 0)
				mesh.SetNormals(normals);
			else
				mesh.RecalculateNormals();

			mesh.RecalculateBounds();
			mesh.Optimize();

			return mesh;
		}

		public void ExportToObj(string filePath, string mtlFileName = "")
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine($"# Wavefront OBJ exported by WavefrontMesh");
			sb.AppendLine($"# Mesh: {name}");
			if (!string.IsNullOrEmpty(mtlFileName))
			{
				sb.AppendLine($"mtllib {mtlFileName}");
			}
			sb.AppendLine();

			if (!string.IsNullOrEmpty(materialName))
				sb.AppendLine($"usemtl {materialName}");

			// Vertices
			foreach (var v in vertices)
				sb.AppendLine($"v {v.x:F6} {v.y:F6} {v.z:F6}");

			foreach (var uv in uvs)
				sb.AppendLine($"vt {uv.x:F6} {uv.y:F6}");

			foreach (var n in normals)
				sb.AppendLine($"vn {n.x:F6} {n.y:F6} {n.z:F6}");

			sb.AppendLine();

			// Faces
			for (int i = 0; i < triangles.Count; i += 3)
			{
				int a = triangles[i] + 1;
				int b = triangles[i + 1] + 1;
				int c = triangles[i + 2] + 1;

				if (normals.Count > 0 && uvs.Count > 0)
					sb.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
				else if (uvs.Count > 0)
					sb.AppendLine($"f {a}/{a} {b}/{b} {c}/{c}");
				else if (normals.Count > 0)
					sb.AppendLine($"f {a}//{a} {b}//{b} {c}//{c}");
				else
					sb.AppendLine($"f {a} {b} {c}");
			}

			File.WriteAllText(filePath, sb.ToString());
			Debug.Log($"✅ Exported Wavefront OBJ: {filePath}");
		}

		// ... (ParseVec3, ParseVec2, ParseFace, AddVertex methods remain the same as previous version)
		private static Vector3 ParseVec3(string[] p)
		{
			return new Vector3(
				float.Parse(p[1], CultureInfo.InvariantCulture),
				float.Parse(p[2], CultureInfo.InvariantCulture),
				float.Parse(p[3], CultureInfo.InvariantCulture));
		}

		private static Vector2 ParseVec2(string[] p)
		{
			return new Vector2(
				float.Parse(p[1], CultureInfo.InvariantCulture),
				float.Parse(p[2], CultureInfo.InvariantCulture));
		}

		private void ParseFace(string[] parts)
		{
			var indices = new List<int>();
			for (int i = 1; i < parts.Length; i++)
			{
				if (string.IsNullOrEmpty(parts[i])) continue;
				indices.Add(GetVertexIndex(parts[i]));
			}

			for (int i = 1; i < indices.Count - 1; i++)
			{
				triangles.Add(indices[0]);
				triangles.Add(indices[i]);
				triangles.Add(indices[i + 1]);
			}
		}

		private int GetVertexIndex(string token)
		{
			var comps = token.Split('/');
			return int.Parse(comps[0]) - 1;
		}
	}
}