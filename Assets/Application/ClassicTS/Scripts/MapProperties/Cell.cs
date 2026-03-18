using UnityEngine;

namespace ClassicTilestorm
{
	// Editor-only fake tile that represents one map cell
	public partial class Cell
	{
		public string type;
		public Variant variant;
		public Vector3 origin;
		private Vector3 offset;
		public Vector3 position 
		{ 
			get => origin + offset;
			set => offset = value - origin;
		}

		public GameObject originalMesh;
		public GameObject highlightMesh;

		public Cell(IMapEdit iMap, Vector3 pos)
		{
			type = TypeName;
			variant = iMap.GetVariantAt(iMap.VectorToIndex(pos));

			var snapped = Map.FullFloorVec(pos);
			variant.delta.y = 0f;//clear the cached delta altitude
			origin = snapped + variant.delta;
			offset = Vector3.zero;
		}

		public string TypeName => "Cell";// Optional: give it a nice name in the side panel

		public void ApplyDelta(EditorController controller, Vector3 delta, bool global = false)
		{
			if (global)
				origin += delta;
			else
				position += delta;
			Update(controller);
		}
	}
}
