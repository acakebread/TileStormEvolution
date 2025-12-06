using MassiveHadronLtd;
using Newtonsoft.Json;
using UnityEngine;

namespace ClassicTilestorm
{
	[System.Serializable]
	public sealed class Viewpoint : MapAttachment
    {
		public Viewpoint() { type = "Viewpoint"; }

		[JsonProperty(Order = 10, NullValueHandling = NullValueHandling.Ignore)]
		public float[] vSrc;

		[JsonProperty(Order = 11, NullValueHandling = NullValueHandling.Ignore)]
		public float[] vDst;

		//ToDo other parameters like FOV and autofocus etc.. 'orientation' will replace vSrc and vDst at some point but it's tricky because vSrc -> vDst also provides distance - Vector3.up is assumed

		// CLEAN, READ/WRITE, NEVER SERIALIZED
		[JsonIgnore] public Vector3 VSrc { get => Vector3Serialization.ToVector3(vSrc); set => vSrc = Vector3Serialization.FromVector3(value); }
		[JsonIgnore] public Vector3 VDst { get => Vector3Serialization.ToVector3(vDst); set => vDst = Vector3Serialization.FromVector3(value); }
	}
}