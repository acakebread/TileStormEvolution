using Newtonsoft.Json;

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

		// Atomic fields — ignored during normal serialization
		[JsonIgnore] public Definition[] definitions;
		[JsonIgnore] public TextureSequence[] textures;
		[JsonIgnore] public string version = "1.0";
		[JsonIgnore] public string author = "Player";
		[JsonIgnore] public string exportedFrom = "ClassicTilestorm";

		public bool ShouldSerializePickups() => Pickups != null && Pickups.nPickupCount > 0;

		[JsonIgnore] public bool IsAtomic => definitions?.Length > 0 || textures?.Length > 0;
	}

	[System.Serializable]
	public class Pickups { public int nPickupCount; }
}