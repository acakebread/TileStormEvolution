// QuadStripAllocator.cs
using System.Collections.Generic;
using UnityEngine;

public class QuadStrip
{
	public List<int> indexBlocks;
	public List<int> vertexBlocks;
}

public class QuadStripAllocator
{
	public const int IndicesPerBlock = 6;
	public const int VerticesPerBlock = 2;

	public int MaxIndexBlocks => _indexBlockAllocator.MaxBlocks;
	public int MaxVertexBlocks => _vertexBlockAllocator.MaxBlocks;

	private readonly List<int> _indices = new();
	private readonly List<Vector3> _vertices = new();
	private readonly List<Color> _colors = new();
	private readonly List<Vector2> _uv = new();

	private readonly DynamicAllocator _indexBlockAllocator;
	private readonly DynamicAllocator _vertexBlockAllocator;
	private readonly List<QuadStrip> activeStrips = new();

	public IReadOnlyList<QuadStrip> ActiveStrips => activeStrips;
	public int ActiveStripCount => activeStrips.Count;

	public IReadOnlyList<int> Indices => _indices;
	public IReadOnlyList<Vector3> Vertices => _vertices;
	public IReadOnlyList<Color> Colors => _colors;
	public IReadOnlyList<Vector2> UV => _uv;

	internal List<int> MutableIndices => _indices;
	internal List<Vector3> MutableVertices => _vertices;
	internal List<Color> MutableColors => _colors;
	internal List<Vector2> MutableUV => _uv;

	public int IndexBlockAvailable => _indexBlockAllocator.AvailableBlockCount;
	public int IndexBlockAllocated => _indexBlockAllocator.AllocatedBlockCount;
	public int VertexBlockAvailable => _vertexBlockAllocator.AvailableBlockCount;
	public int VertexBlockAllocated => _vertexBlockAllocator.AllocatedBlockCount;

	// Optional: expose high-water marks
	public int IndexHighWater => _indexBlockAllocator.HighWaterMark;
	public int VertexHighWater => _vertexBlockAllocator.HighWaterMark;

	public QuadStripAllocator(int maxIndexBlocks, int maxVertexBlocks)
	{
		_indexBlockAllocator = new DynamicAllocator(maxIndexBlocks);
		_vertexBlockAllocator = new DynamicAllocator(maxVertexBlocks);
	}

	public void SetMaxIndexBlocks(int newMax)
	{
		_indexBlockAllocator.SetMaxBlocks(newMax);
	}

	public void SetMaxVertexBlocks(int newMax)
	{
		_vertexBlockAllocator.SetMaxBlocks(newMax);
	}

	private void EnsureIndexCapacity(int required)
	{
		while (_indices.Count < required) _indices.Add(0);
	}

	private void EnsureVertexCapacity(int required)
	{
		while (_vertices.Count < required) _vertices.Add(Vector3.zero);
		while (_colors.Count < required) _colors.Add(Color.white);
		while (_uv.Count < required) _uv.Add(Vector2.zero);
	}

	public QuadStrip AllocateStrip(int numQuads)
	{
		if (numQuads < 1) return null;
		if (_indexBlockAllocator.AvailableBlockCount < numQuads) return null;
		if (_vertexBlockAllocator.AvailableBlockCount < numQuads + 1) return null;

		var strip = new QuadStrip
		{
			indexBlocks = new List<int>(numQuads),
			vertexBlocks = new List<int>(numQuads + 1)
		};

		// Allocate index blocks
		for (int i = 0; i < numQuads; i++)
		{
			int idx = _indexBlockAllocator.Allocate();
			if (idx == -1) { ReleaseStrip(strip); return null; }
			strip.indexBlocks.Add(idx);
			EnsureIndexCapacity((idx + 1) * IndicesPerBlock);
		}

		// Allocate vertex blocks
		for (int i = 0; i < numQuads + 1; i++)
		{
			int vtx = _vertexBlockAllocator.Allocate();
			if (vtx == -1) { ReleaseStrip(strip); return null; }
			strip.vertexBlocks.Add(vtx);
			EnsureVertexCapacity((vtx + 1) * VerticesPerBlock);
		}

		// Fill index buffer
		for (int q = 0; q < numQuads; q++)
		{
			int idxBase = strip.indexBlocks[q] * IndicesPerBlock;
			int v0 = strip.vertexBlocks[q] * VerticesPerBlock;
			int v1 = v0 + 1;
			int v2 = strip.vertexBlocks[q + 1] * VerticesPerBlock + 1;
			int v3 = v2 - 1;

			_indices[idxBase] = v0;
			_indices[idxBase + 1] = v1;
			_indices[idxBase + 2] = v2;
			_indices[idxBase + 3] = v2;
			_indices[idxBase + 4] = v3;
			_indices[idxBase + 5] = v0;
		}

		activeStrips.Add(strip);
		return strip;
	}

	public bool ReleaseStrip(QuadStrip strip)
	{
		if (strip == null || !activeStrips.Contains(strip)) return false;

		// Clear index references (optional, for safety)
		foreach (int idxBlock in strip.indexBlocks)
		{
			int idxBase = idxBlock * IndicesPerBlock;
			for (int j = 0; j < IndicesPerBlock; j++)
				_indices[idxBase + j] = 0;
		}

		// Release blocks
		foreach (int idx in strip.indexBlocks) _indexBlockAllocator.Release(idx);
		foreach (int vtx in strip.vertexBlocks) _vertexBlockAllocator.Release(vtx);

		bool removed = activeStrips.Remove(strip);

		// Optional: full reset when empty
		if (activeStrips.Count == 0)
		{
			_indexBlockAllocator.Clear();
			_vertexBlockAllocator.Clear();
		}

		return removed;
	}
}