// File: Assets/Scripts/ClassicTilestorm/Emitter.cs
using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[System.Serializable]
	public sealed class Emitter : MapAttachment
	{
		public Emitter() => type = "Emitter";

		// Single source of truth: 7 floats (Squatrix format)
		// [0..2] → position.x, y, z
		// [3..6] → squaternion (qx, qy, qz, qw) — magnitude = distance
		[JsonProperty(Order = 10)]
		public float[] data;

		// ==================================================================
		// FULLY READ/WRITE PROPERTIES — ALL MUTATIONS UPDATE `data` INSTANTLY
		// ==================================================================

		[JsonIgnore]
		public Vector3 Position
		{
			get => Squatrix.GetPosition(data);
			set => Rebuild(position: value);
		}

		[JsonIgnore]
		public Quaternion Rotation
		{
			get => Squatrix.GetRotation(data);
			set => Rebuild(rotation: value);
		}

		[JsonIgnore]
		public float Distance
		{
			get => Squatrix.GetDistance(data);
			set => Rebuild(distance: value);
		}

		/// <summary>
		/// Look-at point in world space. Setting this preserves current roll.
		/// </summary>
		[JsonIgnore]
		public Vector3 LookAt
		{
			get => Squatrix.GetLookAt(data);
			set => Rebuild(lookAt: value);
		}

		// Direction vector (forward) — convenience property
		[JsonIgnore]
		public Vector3 Direction
		{
			get => Rotation * Vector3.forward;
			set => Rebuild(rotation: Quaternion.LookRotation(value));
		}

		// ==================================================================
		// SMART REBUILD — preserves roll when LookAt is changed
		// ==================================================================
		private void Rebuild(
			Vector3? position = null,
			Quaternion? rotation = null,
			float? distance = null,
			Vector3? lookAt = null)
		{
			// Start from current valid state
			Vector3 pos = position ?? Position;
			Quaternion rot = rotation ?? Rotation;
			float dist = distance ?? Distance;

			// If LookAt was provided, recompute rotation + distance while PRESERVING ROLL
			if (lookAt.HasValue)
			{
				Vector3 target = lookAt.Value;
				Vector3 toTarget = target - pos;

				if (toTarget.sqrMagnitude < 0.0001f)
				{
					dist = 0f;
				}
				else
				{
					Vector3 newForward = toTarget.normalized;
					dist = toTarget.magnitude;

					// Preserve roll: project current up vector onto plane perpendicular to new forward
					Vector3 currentUp = rot * Vector3.up;
					Vector3 preservedUp = Vector3.ProjectOnPlane(currentUp, newForward);

					if (preservedUp.sqrMagnitude < 0.01f)
						preservedUp = Vector3.up;
					else
						preservedUp = preservedUp.normalized;

					rot = Quaternion.LookRotation(newForward, preservedUp);
				}
			}

			// Allocate or reuse the data array
			if (data == null || data.Length != 7)
				data = new float[7];

			// Write position
			data[0] = pos.x;
			data[1] = pos.y;
			data[2] = pos.z;

			var encoded = Squatrix.Encode(pos, rot, dist);
			data[3] = encoded[3];
			data[4] = encoded[4];
			data[5] = encoded[5];
			data[6] = encoded[6];
		}

		// Ensure valid data when the object is deserialized or enabled
		public void OnEnable()
		{
			if (data == null || data.Length != 7)
			{
				// Default: 10 units in front of origin, looking toward +Z
				Rebuild(position: Vector3.zero, rotation: Quaternion.identity, distance: 10f);
			}
		}
	}
}