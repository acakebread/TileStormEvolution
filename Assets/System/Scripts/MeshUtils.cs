using UnityEngine;

namespace MassiveHadronLtd
{
	public static class MeshUtils
	{
		public static Mesh GenerateQuadXZ(float size = 1f, float uv_scale = 1f, string name = "Quad (default)")
		{
			var half = size;
			var mesh = new Mesh
			{
				name = "PreviewGroundMesh",
				vertices = new[] { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half), },
				triangles = new[] { 0, 1, 2, 0, 2, 3 },
				uv = new[] { new Vector2(0, 0), new Vector2(0, uv_scale), new Vector2(uv_scale, uv_scale), new Vector2(uv_scale, 0) }
			};

			mesh.RecalculateNormals();
			return mesh;
		}
	}
}