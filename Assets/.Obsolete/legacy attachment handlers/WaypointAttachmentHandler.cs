namespace ClassicTilestorm
{
	internal class WaypointAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly WaypointAttachmentHandler Instance = new();

		public static Waypoint Create(IMapEdit map, int tile)
		{
			if (map == null) return null;

			// Count existing waypoints to get the next sequential index
			var nextIndex = map.GetAttachments(filterTypes: new[] { typeof(Waypoint) }).Length;
			var waypoint = new Waypoint(nextIndex, tile);
			map.AddAttachment(waypoint);
			return waypoint;
		}
	}
}