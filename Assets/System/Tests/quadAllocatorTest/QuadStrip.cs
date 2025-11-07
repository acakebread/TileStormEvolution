using System.Collections.Generic;

public class QuadStrip
{
	public int numQuads;
	public List<int> indexBlocks;
	public List<int> vertexBlocks;
	public float startTime;
	public float xOffset;
	public float fallSpeed;

	// TEMP FIELDS FOR RENDERING
	[System.NonSerialized] public float tempXLeft;
	[System.NonSerialized] public float tempXRight;
	[System.NonSerialized] public float tempYTop;
	[System.NonSerialized] public float tempYBottom;
}