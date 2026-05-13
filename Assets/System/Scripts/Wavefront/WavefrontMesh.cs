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
		private struct FaceVertex
		{
			public int vertexIndex;
			public int uvIndex;
			public int normalIndex;
		}

		public string name = "Mesh";
		public string materialLibrary = "";   // mtllib line
		public string materialName = "";      // usemtl line

		public List<Vector3> vertices = new List<Vector3>();
		public List<Vector3> normals = new List<Vector3>();
		public List<Vector2> uvs = new List<Vector2>();
		public List<int> triangles = new List<int>();
		private readonly List<FaceVertex> faceVertices = new List<FaceVertex>();

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
			FromObjText(text, Path.GetFileNameWithoutExtension(objPath));
		}

		public void FromObjText(string objText, string meshName = "LoadedOBJ")
		{
			name = meshName;
			vertices.Clear();
			normals.Clear();
			uvs.Clear();
			triangles.Clear();
			faceVertices.Clear();
			materialLibrary = "";
			materialName = "";

			var lines = objText.Split('\n');

			foreach (var raw in lines)
			{
				var line = raw.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

				var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0) continue;

				switch (parts[0].ToLowerInvariant())
				{
					case "mtllib":
						if (parts.Length > 1) materialLibrary = string.Join(" ", parts, 1, parts.Length - 1);
						break;

					case "usemtl":
						if (parts.Length > 1) materialName = string.Join(" ", parts, 1, parts.Length - 1);
						break;

					case "v":
						vertices.Add(ParseVec3(parts));
						break;

					case "vn":
						normals.Add(ParseVec3(parts));
						break;

					case "vt":
						uvs.Add(ParseVec2(parts));
						break;

					case "f":
						ParseFace(parts);
						break;
				}
			}

			Debug.Log($"[WavefrontMesh] Loaded '{name}' | usemtl='{materialName}' | mtllib='{materialLibrary}'");
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

			if (faceVertices.Count > 0)
			{
				var unityVertices = new List<Vector3>(faceVertices.Count);
				var unityUvs = new List<Vector2>(faceVertices.Count);
				var unityNormals = new List<Vector3>(faceVertices.Count);
				var remap = new Dictionary<string, int>();
				var rebuiltTriangles = new List<int>(triangles.Count);

				for (int i = 0; i < faceVertices.Count; i++)
				{
					var fv = faceVertices[i];
					string key = $"{fv.vertexIndex}|{fv.uvIndex}|{fv.normalIndex}";

					if (!remap.TryGetValue(key, out int newIndex))
					{
						newIndex = unityVertices.Count;
						remap[key] = newIndex;

						unityVertices.Add(GetVertex(vertices, fv.vertexIndex));
						unityUvs.Add(GetUv(uvs, fv.uvIndex));
						unityNormals.Add(GetNormal(normals, fv.normalIndex));
					}

					rebuiltTriangles.Add(newIndex);
				}

				mesh.SetVertices(unityVertices);
				mesh.SetTriangles(rebuiltTriangles, 0);
				mesh.SetUVs(0, unityUvs);

				if (unityNormals.Count > 0 && HasAnyNormalIndex(faceVertices))
					mesh.SetNormals(unityNormals);
				else
					mesh.RecalculateNormals();
			}
			else
			{
				mesh.SetVertices(vertices);
				mesh.SetTriangles(triangles, 0);
				mesh.SetUVs(0, uvs);

				if (normals.Count > 0)
					mesh.SetNormals(normals);
				else
					mesh.RecalculateNormals();
			}

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

			foreach (var v in vertices)
				sb.AppendLine($"v {v.x:F6} {v.y:F6} {v.z:F6}");

			foreach (var uv in uvs)
				sb.AppendLine($"vt {uv.x:F6} {uv.y:F6}");

			foreach (var n in normals)
				sb.AppendLine($"vn {n.x:F6} {n.y:F6} {n.z:F6}");

			sb.AppendLine();

			if (faceVertices.Count > 0)
			{
				for (int i = 0; i < faceVertices.Count; i += 3)
				{
					sb.AppendLine($"f {FormatFaceVertex(faceVertices[i])} {FormatFaceVertex(faceVertices[i + 1])} {FormatFaceVertex(faceVertices[i + 2])}");
				}
			}
			else
			{
				for (int i = 0; i < triangles.Count; i += 3)
				{
					int a = triangles[i] + 1;
					int b = triangles[i + 1] + 1;
					int c = triangles[i + 2] + 1;

					sb.AppendLine($"f {a} {b} {c}");
				}
			}

			File.WriteAllText(filePath, sb.ToString());
			Debug.Log($"✅ Exported Wavefront OBJ: {filePath}");
		}

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
			var face = new List<FaceVertex>();
			for (int i = 1; i < parts.Length; i++)
			{
				if (string.IsNullOrEmpty(parts[i])) continue;
				var comps = parts[i].Split('/');
				int v = comps.Length > 0 && !string.IsNullOrEmpty(comps[0]) ? ParseObjIndex(comps[0], vertices.Count) : -1;
				int vt = comps.Length > 1 && !string.IsNullOrEmpty(comps[1]) ? ParseObjIndex(comps[1], uvs.Count) : -1;
				int vn = comps.Length > 2 && !string.IsNullOrEmpty(comps[2]) ? ParseObjIndex(comps[2], normals.Count) : -1;

				if (v >= 0)
					face.Add(new FaceVertex { vertexIndex = v, uvIndex = vt, normalIndex = vn });
			}

			for (int i = 1; i < face.Count - 1; i++)
			{
				AddFaceVertex(face[0]);
				AddFaceVertex(face[i]);
				AddFaceVertex(face[i + 1]);
			}
		}

		private void AddFaceVertex(FaceVertex faceVertex)
		{
			faceVertices.Add(faceVertex);
			triangles.Add(faceVertices.Count - 1);
		}

		private static int ParseObjIndex(string token, int count)
		{
			if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
				return -1;

			return index > 0 ? index - 1 : count + index;
		}

		private static Vector3 GetVertex(List<Vector3> source, int index)
		{
			return index >= 0 && index < source.Count ? source[index] : Vector3.zero;
		}

		private static Vector2 GetUv(List<Vector2> source, int index)
		{
			return index >= 0 && index < source.Count ? source[index] : Vector2.zero;
		}

		private static Vector3 GetNormal(List<Vector3> source, int index)
		{
			return index >= 0 && index < source.Count ? source[index] : Vector3.zero;
		}

		private static bool HasAnyNormalIndex(List<FaceVertex> faceVertices)
		{
			for (int i = 0; i < faceVertices.Count; i++)
			{
				if (faceVertices[i].normalIndex >= 0)
					return true;
			}
			return false;
		}

		private static string FormatFaceVertex(FaceVertex faceVertex)
		{
			int v = faceVertex.vertexIndex + 1;
			bool hasUv = faceVertex.uvIndex >= 0;
			bool hasNormal = faceVertex.normalIndex >= 0;

			if (hasUv && hasNormal)
				return $"{v}/{faceVertex.uvIndex + 1}/{faceVertex.normalIndex + 1}";

			if (hasUv)
				return $"{v}/{faceVertex.uvIndex + 1}";

			if (hasNormal)
				return $"{v}//{faceVertex.normalIndex + 1}";

			return v.ToString(CultureInfo.InvariantCulture);
		}
	}
}
