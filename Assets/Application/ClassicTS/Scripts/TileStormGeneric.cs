using UnityEngine;

namespace ClassicTilestorm
{
	public static class TileStormGeneric
	{
		public static Collider AddDefaultTileCollider(GameObject gameObject)
		{
			var collider = gameObject.AddComponent<BoxCollider>();
			collider.size = new Vector3(1f, 0.1f, 1f);
			collider.center = new Vector3(0f, -0.05f, 0f);
			return collider;
		}
	}
}