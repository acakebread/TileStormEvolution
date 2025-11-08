using System.Collections.Generic;

public class DynamicAllocator
{
	public const int Blocks = 1024;               //ToDo make this dynamic

	protected List<int> freeBlocks = new List<int>(Blocks);

	public DynamicAllocator()
	{
		for (int i = 0; i < Blocks; i++)
			freeBlocks.Add(i);
		freeBlocks.Reverse();                     //unnecessary but makes allocations look prettier in debug
	}

	public virtual bool Release(int blockId)
	{
		if (blockId < 0 || blockId >= Blocks)
			return false;

		if (freeBlocks.Contains(blockId))
			return false; // already released

		freeBlocks.Add(blockId);
		return true;
	}

	public int Allocate()
	{
		if (freeBlocks.Count == 0) return -1;
		int blockId = freeBlocks[freeBlocks.Count - 1];
		freeBlocks.RemoveAt(freeBlocks.Count - 1);
		return blockId;
	}

	public int FreeBlockCount => freeBlocks.Count;
	public int AllocatedBlockCount => Blocks - freeBlocks.Count;
}