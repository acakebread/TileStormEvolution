using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public sealed partial class Pickup : MapAttachment
	{
		public Pickup() { type = "Pickup"; }

		[JsonProperty(Order = 10)] public int pickupType;
		[JsonProperty(Order = 11)] public int amount = 1;
		[JsonProperty(Order = 12)] public bool respawn = false;

		// optional extra fields later
		// [JsonProperty(Order = 13)] public float customDelay;
	}
}