using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(IndexAllocator))]
[RequireComponent(typeof(VertexAllocator))]
public class QuadStripAllocator : MonoBehaviour
{
	// --------------------------------------------------------------
	// Private fields – set once in Awake (never assigned later)
	// --------------------------------------------------------------
	[SerializeField] private IndexAllocator _indexAllocator;
	[SerializeField] private VertexAllocator _vertexAllocator;

	// --------------------------------------------------------------
	// Public read-only access
	// --------------------------------------------------------------
	public IndexAllocator indexAllocator => _indexAllocator;
	public VertexAllocator vertexAllocator => _vertexAllocator;

	public DynamicAllocator IndexAllocator => _indexAllocator;
	public DynamicAllocator VertexAllocator => _vertexAllocator;

	private readonly List<QuadStrip> activeStrips = new();

	public IReadOnlyList<QuadStrip> ActiveStrips => activeStrips;
	public int ActiveStripCount => activeStrips.Count;

	// --------------------------------------------------------------
	// Awake – guarantee the components are present
	// --------------------------------------------------------------
	private void Awake()
	{
		if (_indexAllocator == null) _indexAllocator = GetComponent<IndexAllocator>();
		if (_vertexAllocator == null) _vertexAllocator = GetComponent<VertexAllocator>();
	}

	// --------------------------------------------------------------
	// Allocate a strip
	// --------------------------------------------------------------
	public QuadStrip AllocateStrip(int numQuads)
	{
		if (numQuads < 1) return null;
		if (_indexAllocator.FreeBlockCount < numQuads) return null;
		if (_vertexAllocator.FreeBlockCount < numQuads + 1) return null;

		var strip = new QuadStrip
		{
			indexBlocks = new List<int>(numQuads),
			vertexBlocks = new List<int>(numQuads + 1)
		};

		// ----- index blocks -----
		for (int i = 0; i < numQuads; i++)
		{
			int idx = _indexAllocator.Allocate();
			if (idx == -1) { ReleaseStrip(strip); return null; }
			strip.indexBlocks.Add(idx);
		}

		// ----- vertex blocks -----
		for (int i = 0; i < numQuads + 1; i++)
		{
			int vtx = _vertexAllocator.Allocate();
			if (vtx == -1) { ReleaseStrip(strip); return null; }
			strip.vertexBlocks.Add(vtx);
		}

		// ----- write *valid* triangle indices -----
		for (int q = 0; q < numQuads; q++)
		{
			int idxBase = strip.indexBlocks[q] * 6;
			int v0 = strip.vertexBlocks[q] * 2;
			int v1 = v0 + 1;
			int v2 = strip.vertexBlocks[q + 1] * 2 + 1;
			int v3 = v2 - 1;

			_indexAllocator.indices[idxBase] = v0;
			_indexAllocator.indices[idxBase + 1] = v1;
			_indexAllocator.indices[idxBase + 2] = v2;
			_indexAllocator.indices[idxBase + 3] = v2;
			_indexAllocator.indices[idxBase + 4] = v3;
			_indexAllocator.indices[idxBase + 5] = v0;
		}

		activeStrips.Add(strip);
		return strip;
	}

	// --------------------------------------------------------------
	// Release a strip
	// --------------------------------------------------------------
	public bool ReleaseStrip(QuadStrip strip)
	{
		if (strip == null || !activeStrips.Contains(strip)) return false;

		// DEGENERATE ALL TRIANGLES
		foreach (int idxBlock in strip.indexBlocks)
		{
			int idxBase = idxBlock * 6;
			int v = idxBlock * 2;  // any vertex
			for (int j = 0; j < 6; j++)
				_indexAllocator.indices[idxBase + j] = v;
		}

		foreach (int idx in strip.indexBlocks) _indexAllocator.Release(idx);
		foreach (int vtx in strip.vertexBlocks) _vertexAllocator.Release(vtx);

		return activeStrips.Remove(strip);
	}
}