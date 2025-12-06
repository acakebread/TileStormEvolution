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

		// The single source of truth: 7 floats
		// [0..2] = position.xyz
		// [3..6] = squaternion (qx,qy,qz,qw) — magnitude = distance
		[JsonProperty(Order = 10)]
		public float[] data;

		// ─────────────────────────────────────────────────────────────────────
		// FULLY READ/WRITE PROPERTIES — ALL MUTATIONS REBUILD DATA
		// ─────────────────────────────────────────────────────────────────────
		[JsonIgnore]
		public Vector3 Position
		{
			get => Squatrix7.GetPosition(data);
			set => Rebuild(position: value);
		}

		[JsonIgnore]
		public Quaternion Rotation
		{
			get => Squatrix7.GetRotation(data);
			set => Rebuild(rotation: value);
		}

		[JsonIgnore]
		public float Distance
		{
			get => Squatrix7.GetDistance(data);
			set => Rebuild(distance: value);
		}

		[JsonIgnore]
		public Vector3 LookAt
		{
			get => Squatrix7.GetLookAt(data);
			set => Rebuild(lookAt: value);
		}

		// Backward compatibility — still fully writable!
		[JsonIgnore]
		public Vector3 VSrc
		{
			get => Position;
			set => Rebuild(position: value);
		}

		[JsonIgnore]
		public Vector3 VDst
		{
			get => LookAt;
			set => Rebuild(lookAt: value);
		}

		// ─────────────────────────────────────────────────────────────────────
		// Smart rebuild — only overwrites what changed, preserves rest when possible
		// ─────────────────────────────────────────────────────────────────────
		private void Rebuild(
			Vector3? position = null,
			Quaternion? rotation = null,
			float? distance = null,
			Vector3? lookAt = null)
		{
			// Start from current known good state
			Vector3 pos = position ?? Position;
			Quaternion rot = rotation ?? Rotation;
			float dist = distance ?? Distance;

			// If LookAt was set, recompute rotation + distance from it
			if (lookAt.HasValue)
			{
				Vector3 dir = lookAt.Value - pos;
				dist = dir.magnitude;
				rot = dist > 0.001f ? Quaternion.LookRotation(dir, Vector3.up) : Quaternion.identity;
			}

			// If only distance changed, preserve rotation direction
			if (distance.HasValue && !rotation.HasValue && !lookAt.HasValue)
			{
				rot = Rotation; // keep current rotation
			}

			// Allocate if needed
			if (data == null || data.Length != 7)
				data = new float[7];

			// Write position
			data[0] = pos.x;
			data[1] = pos.y;
			data[2] = pos.z;

			// Encode rotation + distance into squaternion
			var qscale = Squaternion.Encode(rot.normalized, dist);

			data[3] = qscale.x;
			data[4] = qscale.y;
			data[5] = qscale.z;
			data[6] = qscale.w;
		}
	}
}