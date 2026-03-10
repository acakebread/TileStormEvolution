using UnityEngine;

namespace ClassicTilestorm
{
	// Editor-only fake tile that represents one map cell
	public class Cell : ISelectable
	{
		public string type;
		public Vector3 startPosition;
		public Vector3 position;
		public Variant variant;

		public GameObject highlightMesh;

		//public int tile = -1;

		public Cell(IMapEdit iMap, Vector3 pos)
		{
			type = "Cell"; // or leave as base, doesn't matter
			//tile = iMap.VectorToIndex(pos);
			variant = iMap.GetVariantAt(iMap.VectorToIndex(pos));

			var snapped = Map.FullFloorVec(pos);
			startPosition = new Vector3(snapped.x, 0f, snapped.z) + variant.delta;

			variant.delta.y = 0f;//clear the cached delta altitude
			startPosition = position = snapped + variant.delta;
		}

		public void DestroyHighlight()
		{
			EditorSelectionUtil.Destroy(highlightMesh);
			highlightMesh = null;
		}

		//public string TypeName => "Cell";// Optional: give it a nice name in the side panel
	}
}
