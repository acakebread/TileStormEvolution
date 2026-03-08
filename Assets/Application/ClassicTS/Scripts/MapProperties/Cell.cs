using UnityEngine;

namespace ClassicTilestorm
{
	// Editor-only fake tile that represents one map cell
	public class Cell : ISelectable
	{
		public string type;
		public string name;
		public int tile = -1;
		public Vector3 position;
		public Variant variant;
		public Vector3 startPosition;

		public Cell(IMapEdit iMap, Vector3 pos)
		{
			type = "Cell"; // or leave as base, doesn't matter
			tile = iMap.VectorToIndex(pos);
			variant = iMap.GetVariantAt(tile);
			variant.delta.y = 0f;//clear the cached delta altitude
			startPosition = position = Map.FullFloorVec(pos) + variant.delta;
		}

		//public string TypeName => "Cell";// Optional: give it a nice name in the side panel
	}
}
