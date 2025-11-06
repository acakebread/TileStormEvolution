using UnityEngine;

public class VertexAllocator : DynamicAllocator
{
	public const int VerticesPerBlock = 2;

	public override int Allocate()
	{
		if (freeBlocks.Count == 0) return -1;
		int id = freeBlocks[freeBlocks.Count - 1];
		freeBlocks.RemoveAt(freeBlocks.Count - 1);
		blockUsed[id] = true;
		return id;
	}

	public override bool Release(int blockId)
	{
		if (blockId < 0 || blockId >= Blocks || !blockUsed[blockId]) return false;
		blockUsed[blockId] = false;
		freeBlocks.Add(blockId);
		return true;
	}
}