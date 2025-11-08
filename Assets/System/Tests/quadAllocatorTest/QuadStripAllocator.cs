using System.Collections.Generic;

public class QuadStripAllocator
{
	// --------------------------------------------------------------
	// Private fields – set once in Awake (never assigned later)
	// --------------------------------------------------------------
	private IndexAllocator _indexAllocator = new();
	private VertexAllocator _vertexAllocator = new();

	// --------------------------------------------------------------
	// Public read-only access
	// --------------------------------------------------------------
	public IndexAllocator IndexAllocator => _indexAllocator;
	public VertexAllocator VertexAllocator => _vertexAllocator;

	private readonly List<QuadStrip> activeStrips = new();

	public IReadOnlyList<QuadStrip> ActiveStrips => activeStrips;
	public int ActiveStripCount => activeStrips.Count;


	public void Initialise()
	{
		_indexAllocator.Initialise();
		_vertexAllocator.Initialise();
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