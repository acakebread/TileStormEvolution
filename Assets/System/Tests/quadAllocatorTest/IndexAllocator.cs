public class IndexAllocator : DynamicAllocator
{
	public const int IndicesPerBlock = 6;
	public const int TotalIndices = Blocks * IndicesPerBlock;
	public int[] indices = new int[TotalIndices];

	//public override void Initialise()
	//{
	//	base.Initialise();

	//	//for (int block = 0; block < Blocks; block++)
	//	//{
	//	//	int idx = block * IndicesPerBlock;
	//	//	int v = block * 2;
	//	//	for (int j = 0; j < IndicesPerBlock; j++)
	//	//		indices[idx + j] = v;
	//	//}
	//}

	//public override bool Release(int blockId)
	//{
	//	if (!base.Release(blockId)) return false;
 
	//	//int idx = blockId * IndicesPerBlock;
	//	//int v = blockId * 2;
	//	//for (int j = 0; j < IndicesPerBlock; j++)
	//	//	indices[idx + j] = v;

	//	return true;
	//}
}