public abstract class DynamicAllocator
{
	public const int GridSize = 32;
	public const int Blocks = GridSize * GridSize;

	public abstract void Initialise();
	public abstract int Allocate();
	public abstract bool Release(int blockId);
	public abstract bool IsBlockAllocated(int gridX, int gridY);
	public abstract int AllocatedBlockCount { get; }
	public abstract int FreeBlockCount { get; }
}