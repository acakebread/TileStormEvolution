using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[System.Serializable]
	public sealed class Emitter : MapAttachment
	{
		public Emitter() => type = "Emitter";

		[JsonProperty(Order = 10, NullValueHandling = NullValueHandling.Ignore)]
		public float[] vDir;

		[JsonIgnore]
		public Vector3 Dir
		{
			get => Vector3Serialization.ToVector3(vDir);
			set => vDir = Vector3Serialization.FromVector3(value);
		}
	}
}