using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class Waypoint
	{
		public string name;
		public int tile;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public float[] vSrc;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public float[] vDst;

		// CLEAN, READ/WRITE, NEVER SERIALIZED
		[JsonIgnore] public Vector3 VSrc { get => Vector3Serialization.ToVector3(vSrc); set => vSrc = Vector3Serialization.FromVector3(value); }
		[JsonIgnore] public Vector3 VDst { get => Vector3Serialization.ToVector3(vDst); set => vDst = Vector3Serialization.FromVector3(value); }

		public bool IsCamera() => Vector3Serialization.IsValid(vSrc) && Vector3Serialization.IsValid(vDst);
	}
}