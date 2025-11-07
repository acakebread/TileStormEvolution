using UnityEngine;

public abstract class DynamicAllocator : MonoBehaviour
{
	public const int GridSize = 16;
	public const int Blocks = GridSize * GridSize; // 256

	public abstract int Allocate();
	public abstract bool Release(int blockId);
	public abstract bool IsBlockAllocated(int gridX, int gridY);
	public abstract int AllocatedBlockCount { get; }
	public abstract int FreeBlockCount { get; }
}