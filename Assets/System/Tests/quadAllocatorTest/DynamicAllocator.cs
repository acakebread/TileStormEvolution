using System.Collections.Generic;

public abstract class DynamicAllocator
{
	public const int Blocks = 1024;//ToDo make this dynamic

	protected List<int> freeBlocks = new List<int>(Blocks);
	protected readonly bool[] blockUsed = new bool[Blocks];//this is used for debug and it needs to be moved out to the test class

	public virtual void Initialise()
	{
		for (int i = 0; i < Blocks; i++)
			freeBlocks.Add(i);
		freeBlocks.Reverse();//unnecessary but makes allocations look prettier in debug
	}

	public virtual bool Release(int blockId)
	{
		if (blockId < 0 || blockId >= Blocks || !blockUsed[blockId]) return false;
		blockUsed[blockId] = false;
		freeBlocks.Add(blockId);
		return true;
	}

	public bool IsBlockAllocated(int blockId) => blockId < Blocks && blockUsed[blockId];//this is used for debug and it needs to be moved out to the test class

	public int Allocate()
	{
		if (freeBlocks.Count == 0) return -1;
		int blockId = freeBlocks[freeBlocks.Count - 1];
		freeBlocks.RemoveAt(freeBlocks.Count - 1);
		blockUsed[blockId] = true;
		return blockId;
	}
	public int FreeBlockCount => freeBlocks.Count;
	public int AllocatedBlockCount => Blocks - freeBlocks.Count;
}