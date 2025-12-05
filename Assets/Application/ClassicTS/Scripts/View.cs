// File: Assets/Application/ClassicTS/Scripts/View.cs
using UnityEngine;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[System.Serializable]
	public sealed class View : MapAttachment
	{
		public View() { type = "View"; }

		[JsonProperty(Order = 10, NullValueHandling = NullValueHandling.Ignore)]
		public float[] position;

		[JsonProperty(Order = 11, NullValueHandling = NullValueHandling.Ignore)]
		public float[] qscale;

		// Legacy support — old format
		[JsonProperty("vSrc", Order = 100, NullValueHandling = NullValueHandling.Ignore)]
		private float[] legacyVSrc;

		[JsonProperty("vDst", Order = 101, NullValueHandling = NullValueHandling.Ignore)]
		private float[] legacyVDst;

		// Public accessors
		[JsonIgnore] public Vector3 Position => Vector3Serialization.ToVector3(position);

		[JsonIgnore]
		public Quaternion Rotation
		{
			get
			{
				if (qscale != null && qscale.Length == 4)
				{
					var v4 = Vector4Serialization.ToVector4(qscale);
					if (Squaternion.Decode(v4, out Quaternion q, out _))
						return q;
				}
				return Quaternion.identity;
			}
		}

		[JsonIgnore]
		public float Distance
		{
			get
			{
				if (qscale != null && qscale.Length == 4)
				{
					var v4 = Vector4Serialization.ToVector4(qscale);
					if (Squaternion.Decode(v4, out _, out float s))
						return Mathf.Abs(s);
				}
				return 10f;
			}
		}

		[JsonIgnore] public Vector3 LookAt => Position + Rotation * Vector3.forward * Distance;

		// Backward compatibility — old code keeps working
		[JsonIgnore] public Vector3 VSrc => Position;
		[JsonIgnore] public Vector3 VDst => LookAt;

		[OnDeserialized]
		private void OnDeserialized(StreamingContext context)
		{
			if ((position == null || position.Length != 3 || qscale == null || qscale.Length != 4) &&
				legacyVSrc != null && legacyVDst != null &&
				legacyVSrc.Length == 3 && legacyVDst.Length == 3)
			{
				Vector3 src = new Vector3(legacyVSrc[0], legacyVSrc[1], legacyVSrc[2]);
				Vector3 dst = new Vector3(legacyVDst[0], legacyVDst[1], legacyVDst[2]);

				Vector3 forward = dst - src;
				float distance = forward.magnitude;
				Quaternion rot = distance > 0.001f
					? Quaternion.LookRotation(forward, Vector3.up)
					: Quaternion.identity;

				position = new[] { src.x, src.y, src.z };
				qscale = Vector4Serialization.FromVector4(Squaternion.Encode(rot, distance));

				// Clean up legacy data
				legacyVSrc = null;
				legacyVDst = null;
			}
		}
	}
}