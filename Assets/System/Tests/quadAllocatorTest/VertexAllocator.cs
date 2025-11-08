using UnityEngine;

public class VertexAllocator : DynamicAllocator
{
	public const int VerticesPerBlock = 2;
	public const int TotalVertices = Blocks * VerticesPerBlock;

	public Vector3[] vertices = new Vector3[TotalVertices];
	public Color[] colors = new Color[TotalVertices];
	public Vector2[] uv = new Vector2[TotalVertices];

	//public override void Initialise()
	//{
	//	base.Initialise();

	//	//for (int i = 0; i < TotalVertices; i++)
	//	//{
	//	//	vertices[i] = Vector3.zero;
	//	//	colors[i] = Color.clear;
	//	//	uv[i] = Vector2.zero;
	//	//}
	//}

	//public override bool Release(int blockId)
	//{
	//	if (!base.Release(blockId)) return false;

	//	//int vIdx = blockId * VerticesPerBlock;
	//	//vertices[vIdx] = Vector3.zero;
	//	//vertices[vIdx + 1] = Vector3.zero;
	//	//colors[vIdx] = Color.clear;
	//	//colors[vIdx + 1] = Color.clear;
	//	//uv[vIdx] = Vector2.zero;
	//	//uv[vIdx + 1] = Vector2.zero;

	//	return true;
	//}
}