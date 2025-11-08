using System.Collections.Generic;
using UnityEngine;

public class QuadStrip
{
	public List<int> indexBlocks;
	public List<int> vertexBlocks;
}

public class QuadStripAllocator
{
	// ==================================================================
	// Configuration (can be made dynamic later)
	// ==================================================================
	public const int IndicesPerBlock = 6;
	public const int VerticesPerBlock = 2;
	public const int TotalBlocks = DynamicAllocator.Blocks;

	public const int TotalIndices = TotalBlocks * IndicesPerBlock;
	public const int TotalVertices = TotalBlocks * VerticesPerBlock;

	// ==================================================================
	// Allocators (now internal, using DynamicAllocator directly)
	// ==================================================================
	private readonly DynamicAllocator _indexBlockAllocator = new();
	private readonly DynamicAllocator _vertexBlockAllocator = new();

	// ==================================================================
	// Buffers
	// ==================================================================
	public int[] indices = new int[TotalIndices];
	public Vector3[] vertices = new Vector3[TotalVertices];
	public Color[] colors = new Color[TotalVertices];
	public Vector2[] uv = new Vector2[TotalVertices];

	// ==================================================================
	// Public read-only access
	// ==================================================================
	public int IndexBlockFreeCount => _indexBlockAllocator.FreeBlockCount;
	public int IndexBlockAllocatedCount => _indexBlockAllocator.AllocatedBlockCount;

	public int VertexBlockFreeCount => _vertexBlockAllocator.FreeBlockCount;
	public int VertexBlockAllocatedCount => _vertexBlockAllocator.AllocatedBlockCount;

	private readonly List<QuadStrip> activeStrips = new();
	public IReadOnlyList<QuadStrip> ActiveStrips => activeStrips;
	public int ActiveStripCount => activeStrips.Count;

	// ==================================================================
	// Allocate a strip
	// ==================================================================
	public QuadStrip AllocateStrip(int numQuads)
	{
		if (numQuads < 1) return null;
		if (_indexBlockAllocator.FreeBlockCount < numQuads) return null;
		if (_vertexBlockAllocator.FreeBlockCount < numQuads + 1) return null;

		var strip = new QuadStrip
		{
			indexBlocks = new List<int>(numQuads),
			vertexBlocks = new List<int>(numQuads + 1)
		};

		// ----- Allocate index blocks -----
		for (int i = 0; i < numQuads; i++)
		{
			int idx = _indexBlockAllocator.Allocate();
			if (idx == -1) { ReleaseStrip(strip); return null; }
			strip.indexBlocks.Add(idx);
		}

		// ----- Allocate vertex blocks -----
		for (int i = 0; i < numQuads + 1; i++)
		{
			int vtx = _vertexBlockAllocator.Allocate();
			if (vtx == -1) { ReleaseStrip(strip); return null; }
			strip.vertexBlocks.Add(vtx);
		}

		// ----- Write valid triangle indices -----
		for (int q = 0; q < numQuads; q++)
		{
			int idxBase = strip.indexBlocks[q] * IndicesPerBlock;
			int v0 = strip.vertexBlocks[q] * VerticesPerBlock;       // left
			int v1 = v0 + 1;                                         // right
			int v2 = strip.vertexBlocks[q + 1] * VerticesPerBlock + 1; // right next
			int v3 = v2 - 1;                                         // left next

			indices[idxBase] = v0;
			indices[idxBase + 1] = v1;
			indices[idxBase + 2] = v2;
			indices[idxBase + 3] = v2;
			indices[idxBase + 4] = v3;
			indices[idxBase + 5] = v0;
		}

		activeStrips.Add(strip);
		return strip;
	}

	// ==================================================================
	// Release a strip
	// ==================================================================
	public bool ReleaseStrip(QuadStrip strip)
	{
		if (strip == null || !activeStrips.Contains(strip)) return false;

		// ----- Degenerate all triangles -----
		foreach (int idxBlock in strip.indexBlocks)
		{
			int idxBase = idxBlock * IndicesPerBlock;
			int v = idxBlock * VerticesPerBlock; // any valid vertex index in this block
			for (int j = 0; j < IndicesPerBlock; j++)
				indices[idxBase + j] = v;
		}

		// ----- Release blocks -----
		foreach (int idx in strip.indexBlocks)
			_indexBlockAllocator.Release(idx);
		foreach (int vtx in strip.vertexBlocks)
			_vertexBlockAllocator.Release(vtx);

		return activeStrips.Remove(strip);
	}
}