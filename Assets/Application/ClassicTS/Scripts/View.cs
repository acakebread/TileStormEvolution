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

		[JsonProperty(Order = 10)]
		public float[] data;

		[JsonIgnore] public Vector3 Position { get => Squatrix.GetPosition(data); set => Rebuild(position: value); }
		[JsonIgnore] public Quaternion Rotation { get => Squatrix.GetRotation(data); set => Rebuild(rotation: value); }
		[JsonIgnore] public float Distance { get => Squatrix.GetDistance(data); set => Rebuild(distance: value); }
		[JsonIgnore] public Vector3 LookAt { get => Squatrix.GetLookAt(data); set => Rebuild(lookAt: value); }

		[JsonIgnore] public Vector3 VSrc { get => Position; set => Rebuild(position: value); }
		[JsonIgnore] public Vector3 VDst { get => LookAt; set => Rebuild(lookAt: value); }

		private void Rebuild(
			Vector3? position = null,
			Quaternion? rotation = null,
			float? distance = null,
			Vector3? lookAt = null)
		{
			Vector3 pos = position ?? Position;
			Quaternion rot = rotation ?? Rotation;
			float dist = distance ?? Distance;

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

					Vector3 currentUp = rot * Vector3.up;
					Vector3 preservedUp = Vector3.ProjectOnPlane(currentUp, newForward);
					if (preservedUp.sqrMagnitude < 0.01f) preservedUp = Vector3.up;
					preservedUp = preservedUp.normalized;

					rot = Quaternion.LookRotation(newForward, preservedUp);
				}
			}

			if (data == null || data.Length != 7) data = new float[7];

			data[0] = pos.x;
			data[1] = pos.y;
			data[2] = pos.z;

			// THIS IS THE ONLY CHANGE: use new Squatrix.Encode
			var encoded = Squatrix.Encode(pos, rot, dist);
			data[3] = encoded[3];
			data[4] = encoded[4];
			data[5] = encoded[5];
			data[6] = encoded[6];
		}
	}
}
//// File: Assets/Scripts/ClassicTilestorm/View.cs
//using UnityEngine;
//using Newtonsoft.Json;
//using MassiveHadronLtd;

//namespace ClassicTilestorm
//{
//	[System.Serializable]
//	public sealed class View : MapAttachment
//	{
//		public View() { type = "View"; }

//		// Single source of truth: 7 floats
//		// [0..2] → position.x, y, z
//		// [3..6] → squaternion (qx, qy, qz, qw) — magnitude = distance
//		[JsonProperty(Order = 10)]
//		public float[] data;

//		// ==================================================================
//		// FULLY READ/WRITE PROPERTIES — ALL MUTATIONS UPDATE `data` INSTANTLY
//		// ==================================================================

//		[JsonIgnore]
//		public Vector3 Position
//		{
//			get => Squatrix.GetPosition(data);
//			set => Rebuild(position: value);
//		}

//		[JsonIgnore]
//		public Quaternion Rotation
//		{
//			get => Squatrix.GetRotation(data);
//			set => Rebuild(rotation: value);
//		}

//		[JsonIgnore]
//		public float Distance
//		{
//			get => Squatrix.GetDistance(data);
//			set => Rebuild(distance: value);
//		}

//		/// <summary>
//		/// Look-at point in world space. Setting this preserves current camera roll.
//		/// </summary>
//		[JsonIgnore]
//		public Vector3 LookAt
//		{
//			get => Squatrix.GetLookAt(data);
//			set => Rebuild(lookAt: value);
//		}

//		// Backward compatibility — fully writable!
//		[JsonIgnore]
//		public Vector3 VSrc
//		{
//			get => Position;
//			set => Rebuild(position: value);
//		}

//		[JsonIgnore]
//		public Vector3 VDst
//		{
//			get => LookAt;
//			set => Rebuild(lookAt: value);
//		}

//		// ==================================================================
//		// SMART REBUILD — preserves roll when LookAt is changed
//		// ==================================================================
//		private void Rebuild(
//			Vector3? position = null,
//			Quaternion? rotation = null,
//			float? distance = null,
//			Vector3? lookAt = null)
//		{
//			// Start from current valid state
//			Vector3 pos = position ?? Position;
//			Quaternion rot = rotation ?? Rotation;
//			float dist = distance ?? Distance;

//			// If LookAt was provided, recompute rotation + distance while PRESERVING ROLL
//			if (lookAt.HasValue)
//			{
//				Vector3 target = lookAt.Value;
//				Vector3 toTarget = target - pos;

//				if (toTarget.sqrMagnitude < 0.0001f)
//				{
//					// Degenerate: same point — keep current rotation and set distance to 0
//					dist = 0f;
//				}
//				else
//				{
//					Vector3 newForward = toTarget.normalized;
//					dist = toTarget.magnitude;

//					// Current up vector in world space
//					Vector3 currentUp = rot * Vector3.up;

//					// Project current up onto plane perpendicular to new forward → preserves roll
//					Vector3 preservedUp = Vector3.ProjectOnPlane(currentUp, newForward);

//					// Fallback if projection collapses (looking straight up/down)
//					if (preservedUp.sqrMagnitude < 0.01f)
//						preservedUp = Vector3.up;

//					preservedUp = preservedUp.normalized;

//					rot = Quaternion.LookRotation(newForward, preservedUp);
//				}
//			}

//			// If only distance changed, keep current rotation (including roll)
//			if (distance.HasValue && rotation == null && lookAt == null)
//			{
//				rot = Rotation; // preserve full orientation
//			}

//			// Ensure data array exists
//			if (data == null || data.Length != 7)
//				data = new float[7];

//			// Write position
//			data[0] = pos.x;
//			data[1] = pos.y;
//			data[2] = pos.z;

//			// Encode rotation + distance into squaternion
//			var qscale = Squaternion.Encode(rot.normalized, dist);

//			data[3] = qscale.x;
//			data[4] = qscale.y;
//			data[5] = qscale.z;
//			data[6] = qscale.w;
//		}
//	}
//}