// ---------------------------------------------------------------
// Map.cs   – THE ONE AND ONLY Map class used everywhere
// ---------------------------------------------------------------
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class Map
	{
		public string name;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string szEggbotCostume;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string szMusic;

		public Pickups Pickups;

		// Needed so Json.NET doesn’t serialize empty Pickups objects
		public bool ShouldSerializePickups() => Pickups != null && Pickups.nPickupCount > 0;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string szButtonID;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public Waypoint[] waypoints;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string[] defs;

		public int nWidth;
		public int nHeight;
		public int[] tiles;
		public int[] mixed;
	}

	// Pickups is only used inside Map – keep it here to avoid another file
	[System.Serializable]
	public class Pickups
	{
		public int nPickupCount;
	}
}