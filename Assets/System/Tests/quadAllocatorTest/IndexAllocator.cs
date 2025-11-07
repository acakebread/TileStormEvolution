using System.Collections.Generic;
using UnityEngine;

public class IndexAllocator : DynamicAllocator
{
	public const int IndicesPerBlock = 6;
	public const int TotalIndices = Blocks * IndicesPerBlock; // 1536

	[HideInInspector] public int[] indices = new int[TotalIndices];

	private readonly bool[] blockUsed = new bool[Blocks];
	private readonly List<int> freeBlocks = new List<int>(Blocks);

	private void Awake()
	{
		for (int i = 0; i < Blocks; i++)
			freeBlocks.Add(i);

		for (int block = 0; block < Blocks; block++)
		{
			int idx = block * IndicesPerBlock;
			int v = block * 2;
			for (int j = 0; j < IndicesPerBlock; j++)
				indices[idx + j] = v;
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

		int idx = blockId * IndicesPerBlock;
		int v = blockId * 2;
		for (int j = 0; j < IndicesPerBlock; j++)
			indices[idx + j] = v;

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