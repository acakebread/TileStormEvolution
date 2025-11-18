namespace ClassicTilestorm
{
	[System.Serializable]
	public class Map
	{
		public string name;
		public string character;
		public string music;
		public string button;
		public int width;
		public int height;

		public Waypoint[] waypoints;
		public string[] table;
		public int[] tiles;
		public int[] mixed;
		public Pickups Pickups;

		public bool ShouldSerializePickups() => Pickups != null && Pickups.nPickupCount > 0;
	}

	[System.Serializable]
	public class Pickups { public int nPickupCount; }
}