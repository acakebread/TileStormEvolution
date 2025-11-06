// ---------------------------------------------------------------
//  QuadGridTest.cs
//  Drop this on any GameObject – it creates the allocator,
//  fills the mesh, and draws it in Scene/Game view.
// ---------------------------------------------------------------
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class QuadGridTest : MonoBehaviour
{
	private const int GridSize = 64;   // total grid
	private const int TestSize = 8;    // visible test block
	private const float CellSize = 0.12f;
	private const float Gap = 0.02f;
	private const float S = CellSize + Gap;

	private Mesh mesh;

	private void Awake()
	{
		// ---- material ------------------------------------------------
		var mat = new Material(Shader.Find("Unlit/Color"));
		mat.color = Color.white;
		GetComponent<MeshRenderer>().sharedMaterial = mat;

		// ---- mesh ----------------------------------------------------
		mesh = new Mesh { name = "QuadGridTest" };
		mesh.MarkDynamic();
		GetComponent<MeshFilter>().sharedMesh = mesh;

		BuildGrid();
	}

	private void BuildGrid()
	{
		int vertsPerQuad = 4;
		int trisPerQuad = 6;
		int totalVerts = GridSize * GridSize * vertsPerQuad;
		int totalTris = GridSize * GridSize * trisPerQuad;

		Vector3[] v = new Vector3[totalVerts];
		Color[] c = new Color[totalVerts];
		int[] t = new int[totalTris];

		int vi = 0, ti = 0;

		for (int y = 0; y < GridSize; y++)
			for (int x = 0; x < GridSize; x++)
			{
				float px = x * S;
				float py = y * S;

				// ---- four corners (BL, BR, TR, TL) ----
				v[vi + 0] = new Vector3(px, py, 0); // BL
				v[vi + 1] = new Vector3(px + CellSize, py, 0); // BR
				v[vi + 2] = new Vector3(px + CellSize, py + CellSize, 0); // TR
				v[vi + 3] = new Vector3(px, py + CellSize, 0); // TL

				// ---- colour ------------------------------------------------
				bool inTest = x < TestSize && y < TestSize;
				bool xor = (x & 1) == (y & 1);
				Color col = inTest
					? (xor ? new Color(1f, 0.7f, 0f) : new Color(0f, 0.8f, 1f)) // orange / cyan
					: new Color(0.1f, 0.1f, 0.5f);                               // dim blue

				for (int i = 0; i < 4; i++) c[vi + i] = col;

				// ---- **counter-clockwise** triangles (0,2,1) & (0,3,2) ----
				t[ti++] = vi + 0; t[ti++] = vi + 2; t[ti++] = vi + 1;
				t[ti++] = vi + 0; t[ti++] = vi + 3; t[ti++] = vi + 2;

				vi += vertsPerQuad;
			}

		mesh.Clear();
		mesh.vertices = v;
		mesh.colors = c;
		mesh.triangles = t;
		mesh.RecalculateBounds();
	}

	// optional wireframe in Scene view
	private void OnDrawGizmos()
	{
		if (mesh == null) return;
		Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
		for (int i = 0; i < mesh.vertexCount; i += 4)
		{
			Vector3 a = mesh.vertices[i];
			Vector3 b = mesh.vertices[i + 1];
			Vector3 c = mesh.vertices[i + 2];
			Vector3 d = mesh.vertices[i + 3];
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
		}
	}
}