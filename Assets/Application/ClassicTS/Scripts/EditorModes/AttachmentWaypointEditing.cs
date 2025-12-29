using UnityEngine;

namespace ClassicTilestorm
{
	public static class AttachmentWaypointEditing
	{
		public static void OnSelectionChanged(IMapManager mapManager, Camera camera) { }
		public static void OnGizmoInput(IMapManager mapManager, Camera camera) { }
		public static void OnDragInput(IMapManager mapManager) { }

		public static Waypoint CreateWaypoint(IMapManager mapManager, int tile)
		{
			if (null == mapManager) return null;
			return null;
		}
	}
}