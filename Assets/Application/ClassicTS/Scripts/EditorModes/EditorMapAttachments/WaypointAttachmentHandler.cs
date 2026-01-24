namespace ClassicTilestorm
{
	internal class WaypointAttachmentHandler : IEditorAttachmentHandler
	{
		public static readonly WaypointAttachmentHandler Instance = new();

		public static Waypoint Create(IMap map, int tile)
		{
			if (map == null) return null;

			// Count existing waypoints to get the next sequential index
			int nextIndex = map.GetAttachments(filterTypes: new[] { typeof(Waypoint) }).Length;
			var waypoint = new Waypoint(nextIndex, tile);
			map.AddAttachment(waypoint);
			return waypoint;
		}
	}
}

//namespace ClassicTilestorm
//{
//	internal class WaypointAttachmentHandler : IEditorAttachmentHandler
//	{
//		public static readonly WaypointAttachmentHandler Instance = new();

//		public static Waypoint Create(IMap map, int tile)
//		{
//			if (map == null) return null;
//			var index = map.WaypointAttachments?.Length ?? 0;
//			var waypoint = new Waypoint(index, tile);
//			map.AddAttachment(waypoint);
//			return waypoint;
//		}
//	}
//}