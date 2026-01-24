namespace ClassicTilestorm
{
	internal class WaypointAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly WaypointAttachmentHandler Instance = new();

		public static Waypoint Create(IMap map, int tile)
		{
			if (map == null) return null;
			var index = map.WaypointAttachments?.Length ?? 0;
			var waypoint = new Waypoint(index, tile);
			map.AddAttachment(waypoint);
			return waypoint;
		}
	}
}