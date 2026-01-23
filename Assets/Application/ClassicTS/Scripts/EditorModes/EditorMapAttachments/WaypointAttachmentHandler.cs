namespace ClassicTilestorm
{
	internal class WaypointAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly WaypointAttachmentHandler Instance = new();

		public static Waypoint Create(IMap mapManager, int tile)
		{
			if (mapManager == null) return null;
			var index = mapManager.waypointAttachments?.Length ?? 0;
			var waypoint = new Waypoint(index, tile);
			mapManager.AddAttachment(waypoint);
			return waypoint;
		}
	}
}