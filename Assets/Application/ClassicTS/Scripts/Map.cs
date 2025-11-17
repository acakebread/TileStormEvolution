// Map.cs — CLEAN OUTPUT ONLY + FULL BACKWARD COMPATIBILITY
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class Map
	{
		// NEW CLEAN PUBLIC FIELDS — these are the ones that get serialized
		public string name;
		public string character;
		public string music;
		public string button;
		public int width;
		public int height;

		public Waypoint[] waypoints;
		public string[] defs;
		public int[] tiles;
		public int[] mixed;
		public Pickups Pickups;

		public bool ShouldSerializePickups() => Pickups != null && Pickups.nPickupCount > 0;

		// BACKWARD COMPATIBILITY: Read old field names from JSON, but NEVER write them
		[JsonProperty("szEggbotCostume")] private string LegacyEggbot { set => character = value; }
		[JsonProperty("szMusic")] private string LegacyMusic { set => music = value; }
		[JsonProperty("szButtonID")] private string LegacyButton { set => button = value; }
		[JsonProperty("nWidth")] private int LegacyWidth { set => width = value; }
		[JsonProperty("nHeight")] private int LegacyHeight { set => height = value; }

		// Optional: Keep old public names working in C# code (with warning)
		[JsonIgnore, System.Obsolete("Use 'character' instead", false)]
		public string szEggbotCostume { get => character; set => character = value; }

		[JsonIgnore, System.Obsolete("Use 'music' instead", false)]
		public string szMusic { get => music; set => music = value; }

		[JsonIgnore, System.Obsolete("Use 'button' instead", false)]
		public string szButtonID { get => button; set => button = value; }

		[JsonIgnore, System.Obsolete("Use 'width' instead", false)]
		public int nWidth { get => width; set => width = value; }

		[JsonIgnore, System.Obsolete("Use 'height' instead", false)]
		public int nHeight { get => height; set => height = value; }
	}

	[System.Serializable]
	public class Pickups
	{
		public int nPickupCount;
	}
}