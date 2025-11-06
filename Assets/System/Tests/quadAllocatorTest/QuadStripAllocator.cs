using System.Collections.Generic;
using UnityEngine;

public class QuadStripAllocator : MonoBehaviour
{
	[SerializeField] private IndexAllocator indexAllocator;
	[SerializeField] private VertexAllocator vertexAllocator;

	private readonly List<QuadStrip> activeStrips = new();

	public IndexAllocator IndexAllocator => indexAllocator;
	public VertexAllocator VertexAllocator => vertexAllocator;

	private void Awake()
	{
		if (!indexAllocator) indexAllocator = GetComponentInChildren<IndexAllocator>();
		if (!vertexAllocator) vertexAllocator = GetComponentInChildren<VertexAllocator>();
	}

	public QuadStrip AllocateStrip(int numQuads)
	{
		if (numQuads < 1) return null;

		// Check availability BEFORE any allocation
		if (indexAllocator.FreeBlockCount < numQuads) return null;
		if (vertexAllocator.FreeBlockCount < numQuads + 1) return null;

		var strip = new QuadStrip
		{
			numQuads = numQuads,
			indexBlocks = new List<int>(),
			vertexBlocks = new List<int>()
		};

		// Allocate indices
		for (int i = 0; i < numQuads; i++)
		{
			int idx = indexAllocator.Allocate();
			if (idx == -1) { ReleaseStrip(strip); return null; }
			strip.indexBlocks.Add(idx);
		}

		// Allocate vertices
		for (int i = 0; i < numQuads + 1; i++)
		{
			int vtx = vertexAllocator.Allocate();
			if (vtx == -1) { ReleaseStrip(strip); return null; }
			strip.vertexBlocks.Add(vtx);
		}

		activeStrips.Add(strip);
		return strip;
	}

	public bool ReleaseStrip(QuadStrip strip)
	{
		if (strip == null || !activeStrips.Contains(strip)) return false;

		foreach (int idx in strip.indexBlocks)
			indexAllocator.Release(idx);

		foreach (int vtx in strip.vertexBlocks)
			vertexAllocator.Release(vtx);

		return activeStrips.Remove(strip);
	}

	public int ActiveStripCount => activeStrips.Count;
}