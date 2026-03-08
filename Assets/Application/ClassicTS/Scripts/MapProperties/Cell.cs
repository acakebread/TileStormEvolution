using UnityEngine;

namespace ClassicTilestorm
{
	// Editor-only fake attachment that represents one map cell
	public class Cell : ISelectable
	{
		public string type;
		public string name;
		public int tile = -1;
		public Vector3 position;
		public Variant variant(IMapEdit iMap) => iMap.GetVariantAt(tile);
		public Vector3 startPosition(IMapEdit iMap) => iMap.IndexToVector(tile) + variant(iMap).delta;

		public Cell(int tileIndex)
		{
			type = "Cell"; // or leave as base, doesn't matter
			tile = tileIndex;
			position = Vector3.zero;
		}

		// Optional: give it a nice name in the side panel
		//public string TypeName => "Cell";
	}
}
