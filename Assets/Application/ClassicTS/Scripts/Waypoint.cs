using UnityEngine;

namespace ClassicTilestorm
{
	public class Waypoint
	{
		public string name;
		public int nTile;
		private float[] vSrc;
		private float[] vDst;

		public Vector3 GetVSrc() => vSrc != null && vSrc.Length == 3 && IsValid(vSrc)
			? new Vector3(vSrc[0], vSrc[1], vSrc[2])
			: Vector3.zero;

		public Vector3 GetVDst() => vDst != null && vDst.Length == 3 && IsValid(vDst)
			? new Vector3(vDst[0], vDst[1], vDst[2])
			: Vector3.zero;

		public bool IsCamera() => IsValid(vSrc) && IsValid(vDst);

		private static bool IsValid(float[] v)
		{
			return v != null &&
				   !float.IsNaN(v[0]) && !float.IsInfinity(v[0]) &&
				   !float.IsNaN(v[1]) && !float.IsInfinity(v[1]) &&
				   !float.IsNaN(v[2]) && !float.IsInfinity(v[2]);
		}

		public void SetVSrc(Vector3 vec)
		{
			vSrc = vec == Vector3.zero ? null : new[] { vec.x, vec.y, vec.z };
		}

		public void SetVDst(Vector3 vec)
		{
			vDst = vec == Vector3.zero ? null : new[] { vec.x, vec.y, vec.z };
		}

		// Conversion from serialized waypoint
		public static Waypoint FromSerialized(DatabaseSerializer.Waypoint serialized)
		{
			if (serialized == null) return null;
			return new Waypoint
			{
				name = serialized.name,
				nTile = serialized.nTile,
				vSrc = serialized.vSrc,
				vDst = serialized.vDst
			};
		}

		// Conversion to serialized waypoint
		public DatabaseSerializer.Waypoint ToSerialized()
		{
			return new DatabaseSerializer.Waypoint
			{
				name = name,
				nTile = nTile,
				vSrc = vSrc,
				vDst = vDst
			};
		}
	}
}