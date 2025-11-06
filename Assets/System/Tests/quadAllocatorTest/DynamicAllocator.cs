using UnityEngine;

public abstract class DynamicAllocator : MonoBehaviour
{
	public const int Blocks = 256;                 // 16×16

	protected readonly bool[] blockUsed = new bool[Blocks];
	protected readonly System.Collections.Generic.List<int> freeBlocks =
		new System.Collections.Generic.List<int>(Blocks);

	protected virtual void Awake()
	{
		for (int i = 0; i < Blocks; i++) freeBlocks.Add(i);
	}

	public abstract int Allocate();
	public abstract bool Release(int blockId);

	public bool IsBlockAllocated(int gridX, int gridY)
	{
		int id = gridY * 16 + gridX;
		return id < Blocks && blockUsed[id];
	}

	public int AllocatedBlockCount => Blocks - freeBlocks.Count;
	public int FreeBlockCount => freeBlocks.Count;
}