using UnityEngine;

namespace ClassicTilestorm
{
	// Editor-only fake tile that represents one map cell
	public class Cell : ISelectable
	{
		public string type;
		public Vector3 origin;
		public Vector3 position;
		public Variant variant;

		public GameObject originalMesh;
		public GameObject highlightMesh;

		public Cell(IMapEdit iMap, Vector3 pos)
		{
			type = "Cell"; // or leave as base, doesn't matter
			variant = iMap.GetVariantAt(iMap.VectorToIndex(pos));

			var snapped = Map.FullFloorVec(pos);
			variant.delta.y = 0f;//clear the cached delta altitude
			origin = position = snapped + variant.delta;

			//originalMesh = iMap.GetTile(pos).gameObject;

			//variant.delta.y = 0f;//clear the cached delta altitude
			//origin = position = (variant.HasNav ? Map.FullFloorVec(pos) : Map.HalfFloorVec(pos)) - variant.delta;
			//variant.delta = Vector3.zero;//clear the cached delta
		}

		public void DestroyHighlight()
		{
			EditorSelectionUtil.Destroy(highlightMesh);
			highlightMesh = null;
		}

		//public string TypeName => "Cell";// Optional: give it a nice name in the side panel
	}
}
