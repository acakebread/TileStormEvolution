using System.Collections.Generic;
using UnityEngine;

public class VertexAllocator : DynamicAllocator
{
	public const int VerticesPerBlock = 2;
	public const int TotalVertices = Blocks * VerticesPerBlock; // 512

	[HideInInspector] public Vector3[] vertices = new Vector3[TotalVertices];
	[HideInInspector] public Color[] colors = new Color[TotalVertices];
	[HideInInspector] public Vector2[] uv = new Vector2[TotalVertices]; // ADDED

	private readonly bool[] blockUsed = new bool[Blocks];
	private readonly List<int> freeBlocks = new List<int>(Blocks);

	private void Awake()
	{
		for (int i = 0; i < Blocks; i++)
			freeBlocks.Add(i);

		for (int i = 0; i < TotalVertices; i++)
		{
			vertices[i] = Vector3.zero;
			colors[i] = Color.clear;
			uv[i] = Vector2.zero; // ADDED
		}
	}

	public override int Allocate()
	{
		if (freeBlocks.Count == 0) return -1;
		int blockId = freeBlocks[freeBlocks.Count - 1];
		freeBlocks.RemoveAt(freeBlocks.Count - 1);
		blockUsed[blockId] = true;
		return blockId;
	}

	public override bool Release(int blockId)
	{
		if (blockId < 0 || blockId >= Blocks || !blockUsed[blockId]) return false;
		blockUsed[blockId] = false;
		freeBlocks.Add(blockId);

		int vIdx = blockId * VerticesPerBlock;
		vertices[vIdx] = Vector3.zero;
		vertices[vIdx + 1] = Vector3.zero;
		colors[vIdx] = Color.clear;
		colors[vIdx + 1] = Color.clear;
		uv[vIdx] = Vector2.zero;     // ADDED
		uv[vIdx + 1] = Vector2.zero; // ADDED

		return true;
	}

	public override bool IsBlockAllocated(int gridX, int gridY)
	{
		int blockId = gridY * GridSize + gridX;
		return blockId < Blocks && blockUsed[blockId];
	}

	public override int AllocatedBlockCount => Blocks - freeBlocks.Count;
	public override int FreeBlockCount => freeBlocks.Count;
}