using Unity.Collections;
using UnityEngine;

public class QuadAllocator : MonoBehaviour
{
	public const int GridSize = 64;
	private const int VertsPerQuad = 4;
	private const int IndicesPerQuad = 6;

	private NativeArray<Vector3> vertices;
	private NativeArray<Color32> colors;
	private NativeArray<Vector2> uvs;
	private NativeArray<ushort> indices;

	private bool[,] used = new bool[GridSize, GridSize];

	public ref NativeArray<Vector3> Vertices => ref vertices;
	public ref NativeArray<Color32> Colors => ref colors;
	public ref NativeArray<Vector2> UVs => ref uvs;
	public ref NativeArray<ushort> Indices => ref indices;

	private void Start() => InitBuffers();
	private void OnDestroy() => DisposeBuffers();

	private void InitBuffers()
	{
		int totalQuads = GridSize * GridSize;
		int totalVerts = totalQuads * VertsPerQuad;
		int totalIndices = totalQuads * IndicesPerQuad;

		vertices = new NativeArray<Vector3>(totalVerts, Allocator.Persistent);
		colors = new NativeArray<Color32>(totalVerts, Allocator.Persistent);
		uvs = new NativeArray<Vector2>(totalVerts, Allocator.Persistent);
		indices = new NativeArray<ushort>(totalIndices, Allocator.Persistent);

		// Degenerate
		for (int i = 0; i < totalVerts; i++)
		{
			vertices[i] = Vector3.zero;
			colors[i] = new Color32(0, 0, 0, 0);
			uvs[i] = Vector2.zero;
		}

		// CCW index buffer
		for (int q = 0; q < totalQuads; q++)
		{
			int v = q * VertsPerQuad;
			int i = q * IndicesPerQuad;
			indices[i + 0] = (ushort)(v + 0);
			indices[i + 1] = (ushort)(v + 2);
			indices[i + 2] = (ushort)(v + 1);
			indices[i + 3] = (ushort)(v + 0);
			indices[i + 4] = (ushort)(v + 3);
			indices[i + 5] = (ushort)(v + 2);
		}
	}

	private void DisposeBuffers()
	{
		if (vertices.IsCreated) vertices.Dispose();
		if (colors.IsCreated) colors.Dispose();
		if (uvs.IsCreated) uvs.Dispose();
		if (indices.IsCreated) indices.Dispose();
	}

	public bool Allocate(int x, int y)
	{
		if (InBounds(x, y) && !used[x, y])
		{
			used[x, y] = true;
			WriteQuad(x, y, true);
			return true;
		}
		return false;
	}

	public void Free(int x, int y)
	{
		if (InBounds(x, y) && used[x, y])
		{
			used[x, y] = false;
			WriteQuad(x, y, false);
		}
	}

	private bool InBounds(int x, int y) =>
		x >= 0 && x < GridSize && y >= 0 && y < GridSize;

	private const float CellSize = 0.12f;
	private const float Gap = 0.02f;
	private const float S = CellSize + Gap;

	private void WriteQuad(int gx, int gy, bool active)
	{
		int q = gy * GridSize + gx;
		int v = q * VertsPerQuad;

		if (active)
		{
			float px = gx * S;
			float py = gy * S;

			vertices[v + 0] = new Vector3(px, py, 0);
			vertices[v + 1] = new Vector3(px + CellSize, py, 0);
			vertices[v + 2] = new Vector3(px + CellSize, py + CellSize, 0);
			vertices[v + 3] = new Vector3(px, py + CellSize, 0);

			// XOR color for debug
			bool xor = (gx & 1) == (gy & 1);
			Color32 col = xor ? new Color32(255, 180, 0, 255) : new Color32(0, 200, 255, 255);
			for (int i = 0; i < 4; i++) colors[v + i] = col;
		}
		else
		{
			for (int i = 0; i < 4; i++)
			{
				vertices[v + i] = Vector3.zero;
				colors[v + i] = new Color32(0, 0, 0, 0);
			}
		}
	}

	public void ApplyToMesh(Mesh mesh)
	{
		mesh.Clear();
		mesh.SetVertices(vertices);
		mesh.SetColors(colors);
		mesh.SetUVs(0, uvs);
		mesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
	}
}