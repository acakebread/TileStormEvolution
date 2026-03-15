using UnityEngine;

namespace ClassicTilestorm
{
	// Editor-only fake tile that represents one map cell
	public partial class Cell : ISelectable
	{
		public string type;
		public Variant variant;
		public Vector3 origin;
		public Vector3 position;

		public GameObject originalMesh;
		public GameObject highlightMesh;

		public Cell(IMapEdit iMap, Vector3 pos)
		{
			type = TypeName;
			variant = iMap.GetVariantAt(iMap.VectorToIndex(pos));

			var snapped = Map.FullFloorVec(pos);
			variant.delta.y = 0f;//clear the cached delta altitude
			origin = position = snapped + variant.delta;
		}

		public string TypeName => "Cell";// Optional: give it a nice name in the side panel
	}
}
