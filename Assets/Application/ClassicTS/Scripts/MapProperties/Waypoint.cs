namespace ClassicTilestorm
{
	// Editor-only fake attachment that represents one waypoint
	public class Waypoint : MapAttachment
	{
		public int waypointIndex; // which position in the waypoints array this represents

		public Waypoint(int WPindex, int tileIndex)
		{
			type = "Waypoint"; // or leave as base, doesn't matter
			tile = tileIndex;
			waypointIndex = WPindex;
		}

		// Optional: give it a nice name in the side panel
		public override string TypeName => "Waypoint";
	}
}
