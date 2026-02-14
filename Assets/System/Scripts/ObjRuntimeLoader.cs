using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class ObjRuntimeLoader
	{
		public static Mesh LoadFromText(string objText, string meshName = "RuntimeOBJ")
		{
			var verts = new List<Vector3>();
			var norms = new List<Vector3>();
			var uvs = new List<Vector2>();

			var finalVerts = new List<Vector3>();
			var finalNorms = new List<Vector3>();
			var finalUvs = new List<Vector2>();
			var tris = new List<int>();

			var lines = objText.Split('\n');

			foreach (var raw in lines)
			{
				var line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#"))
					continue;

				var parts = line.Split(' ');

				switch (parts[0])
				{
					case "v":
						verts.Add(ParseVec3(parts));
						break;

					case "vn":
						norms.Add(ParseVec3(parts));
						break;

					case "vt":
						uvs.Add(ParseVec2(parts));
						break;

					case "f":
						ParseFace(parts, verts, norms, uvs,
								  finalVerts, finalNorms, finalUvs, tris);
						break;
				}
			}

			Mesh mesh = new Mesh();
			mesh.name = meshName;

			mesh.SetVertices(finalVerts);
			mesh.SetNormals(finalNorms);
			mesh.SetUVs(0, finalUvs);
			mesh.SetTriangles(tris, 0);

			mesh.RecalculateBounds();

			if (finalNorms.Count == 0)
				mesh.RecalculateNormals();

			return mesh;
		}

		static Vector3 ParseVec3(string[] p)
		{
			return new Vector3(
				float.Parse(p[1], CultureInfo.InvariantCulture),
				float.Parse(p[2], CultureInfo.InvariantCulture),
				float.Parse(p[3], CultureInfo.InvariantCulture));
		}

		static Vector2 ParseVec2(string[] p)
		{
			return new Vector2(
				float.Parse(p[1], CultureInfo.InvariantCulture),
				float.Parse(p[2], CultureInfo.InvariantCulture));
		}

		static void ParseFace(
			string[] parts,
			List<Vector3> verts,
			List<Vector3> norms,
			List<Vector2> uvs,
			List<Vector3> outVerts,
			List<Vector3> outNorms,
			List<Vector2> outUvs,
			List<int> tris)
		{
			for (int i = 1; i < parts.Length - 2; i++)
			{
				AddVertex(parts[1], verts, norms, uvs, outVerts, outNorms, outUvs, tris);
				AddVertex(parts[i + 1], verts, norms, uvs, outVerts, outNorms, outUvs, tris);
				AddVertex(parts[i + 2], verts, norms, uvs, outVerts, outNorms, outUvs, tris);
			}
		}

		static void AddVertex(
			string token,
			List<Vector3> verts,
			List<Vector3> norms,
			List<Vector2> uvs,
			List<Vector3> outVerts,
			List<Vector3> outNorms,
			List<Vector2> outUvs,
			List<int> tris)
		{
			var comps = token.Split('/');

			int v = int.Parse(comps[0]) - 1;
			int vt = comps.Length > 1 && comps[1] != "" ? int.Parse(comps[1]) - 1 : -1;
			int vn = comps.Length > 2 ? int.Parse(comps[2]) - 1 : -1;

			outVerts.Add(verts[v]);

			if (vt >= 0 && vt < uvs.Count)
				outUvs.Add(uvs[vt]);
			else
				outUvs.Add(Vector2.zero);

			if (vn >= 0 && vn < norms.Count)
				outNorms.Add(norms[vn]);
			else
				outNorms.Add(Vector3.up);

			tris.Add(outVerts.Count - 1);
		}
	}
}
