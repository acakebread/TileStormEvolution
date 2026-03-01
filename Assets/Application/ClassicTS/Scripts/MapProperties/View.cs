// File: Assets/Scripts/ClassicTilestorm/View.cs
using UnityEngine;
using Newtonsoft.Json;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[System.Serializable]
	public sealed class View : MapAttachment, ITransformableAttachment
	{
		public View() { type = "View"; }

		[JsonIgnore]
		public override bool HasTransform => true;

		[JsonProperty(Order = 10)]
		public float[] data;

		[JsonIgnore] public Vector3 Position { get => Squatrix.GetPosition(data); set => Rebuild(position: value); }
		[JsonIgnore] public Quaternion Rotation { get => Squatrix.GetRotation(data); set => Rebuild(rotation: value); }
		[JsonIgnore] public float Distance { get => Squatrix.GetDistance(data); set => Rebuild(distance: value); }
		[JsonIgnore] public Vector3 LookAt { get => Squatrix.GetLookAt(data); set => Rebuild(lookAt: value); }
		[JsonIgnore] public float FOV { get => DEFAULT_FOV; set { Debug.Log("ToDo set View::FOV"); } }

		[JsonIgnore] public Vector3 VSrc { get => Position; set => Rebuild(position: value); }
		[JsonIgnore] public Vector3 VDst { get => LookAt; set => Rebuild(lookAt: value); }

		public const float MAX_DISTANCE = 64f;
		public const float DEFAULT_FOV = 20f;//ToDo make View.FOV dynamic property

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

			var encoded = Squatrix.Encode(pos, rot, dist);
			data[3] = encoded[3];
			data[4] = encoded[4];
			data[5] = encoded[5];
			data[6] = encoded[6];
		}
	}
}
