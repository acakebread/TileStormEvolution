// File: Assets/Scripts/ClassicTilestorm/View.cs
using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[System.Serializable]
	public sealed class View : MapAttachment
	{
		public View() { type = "View"; }

		// The one and only source of truth: 7 floats
		// [0..2] = position.xyz
		// [3..6] = squaternion (qx, qy, qz, qw) — magnitude = distance
		[JsonProperty(Order = 10)]
		public float[] data;

		// ─────────────────────────────────────────────────────────────────────
		// Public read/write accessors — VSrc and VDst are now SETTABLE
		// Setting either one instantly rebuilds the full 7-float data blob
		// ─────────────────────────────────────────────────────────────────────
		[JsonIgnore] public Vector3 Position => Squatrix7.GetPosition(data);

		[JsonIgnore] public Quaternion Rotation => Squatrix7.GetRotation(data);

		[JsonIgnore] public float Distance => Squatrix7.GetDistance(data);

		[JsonIgnore] public Vector3 LookAt => Squatrix7.GetLookAt(data);

		// Backward compatibility — READ-ONLY for old code that only reads
		// But now also WRITABLE so editors/tools can still do view.VSrc = ...;
		[JsonIgnore]
		public Vector3 VSrc
		{
			get => Position;
			set => RebuildFromSrcAndDst(value, LookAt);
		}

		[JsonIgnore]
		public Vector3 VDst
		{
			get => LookAt;
			set => RebuildFromSrcAndDst(Position, value);
		}

		// ─────────────────────────────────────────────────────────────────────
		// Core rebuild logic — called whenever VSrc or VDst is written to
		// ─────────────────────────────────────────────────────────────────────
		private void RebuildFromSrcAndDst(Vector3 src, Vector3 dst)
		{
			Vector3 dir = dst - src;
			float distance = dir.magnitude;
			Quaternion rotation = distance > 0.001f
				? Quaternion.LookRotation(dir, Vector3.up)
				: Quaternion.identity;

			// Allocate once, reuse if possible
			if (data == null || data.Length != 7)
				data = new float[7];

			data[0] = src.x;
			data[1] = src.y;
			data[2] = src.z;

			var qscale = Squaternion.Encode(rotation, distance);
			data[3] = qscale.x;
			data[4] = qscale.y;
			data[5] = qscale.z;
			data[6] = qscale.w;
		}

		// Optional: ensure data exists even if someone creates a View manually
		public void EnsureValidData()
		{
			if (data == null || data.Length != 7)
			{
				RebuildFromSrcAndDst(Vector3.zero, Vector3.forward * 10f);
			}
		}
	}
}