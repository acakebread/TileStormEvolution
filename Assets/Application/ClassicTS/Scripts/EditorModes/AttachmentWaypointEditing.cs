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
			var index = null != mapManager.CurrentMap.waypoints ? mapManager.CurrentMap.waypoints.Length : 0;
			var waypoint = new Waypoint(index, tile);
			mapManager.AddAttachment(waypoint);
			return waypoint;
		}
	}
}