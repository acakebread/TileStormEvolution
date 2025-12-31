using UnityEngine;

namespace ClassicTilestorm
{
	public static class AttachmentWaypointEditing
	{
		public static void OnSelectionChanged(IMapManager mapManager, Camera camera, MapAttachment[] selection) { }
		public static void OnGizmoInput(IMapManager mapManager, Camera camera, MapAttachment[] selection) { }
		public static void OnDragInput(IMapManager mapManager, MapAttachment[] selection) { }

		public static Waypoint Create(IMapManager mapManager, int tile)
		{
			if (null == mapManager) return null;
			var index = null != mapManager.CurrentMap.waypoints ? mapManager.CurrentMap.waypoints.Length : 0;
			var waypoint = new Waypoint(index, tile);
			mapManager.AddAttachment(waypoint);
			return waypoint;
		}
	}
}