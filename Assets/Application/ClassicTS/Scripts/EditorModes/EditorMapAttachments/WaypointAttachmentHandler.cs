namespace ClassicTilestorm
{
	internal class WaypointAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly WaypointAttachmentHandler Instance = new();

		public static Waypoint Create(IMapManager mapManager, int tile)
		{
			if (mapManager == null) return null;
			var index = mapManager.CurrentMap.waypointAttachments?.Length ?? 0;
			var waypoint = new Waypoint(index, tile);
			mapManager.CurrentMap.AddAttachment(waypoint);
			return waypoint;
		}
	}
}