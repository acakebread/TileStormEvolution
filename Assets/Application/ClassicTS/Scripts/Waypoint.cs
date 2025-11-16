// ---------------------------------------------------------------
// Waypoint.cs   (runtime class – NO Newtonsoft, NO float[])
// ---------------------------------------------------------------
using UnityEngine;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class Waypoint
	{
		public string name;
		public int nTile;

		// ----------------------------------------------------------------
		// Internal storage – nullable Vector3 (serialised as float[3] by DTO)
		// ----------------------------------------------------------------
		private Vector3? vSrc;
		private Vector3? vDst;

		// ----------------------------------------------------------------
		// Public read‑only access – returns zero if the vector is missing
		// ----------------------------------------------------------------
		public Vector3 GetVSrc() => vSrc.HasValue && IsValid(vSrc.Value)
			? vSrc.Value + MapManager.tile_origin//temporary adjustment until the values are adjusted to 'tile space'
			: Vector3.zero;

		public Vector3 GetVDst() => vDst.HasValue && IsValid(vDst.Value)
			? vDst.Value + MapManager.tile_origin//temporary adjustment until the values are adjusted to 'tile space'
			: Vector3.zero;

		// ----------------------------------------------------------------
		// Camera waypoint test – both vectors must be present & valid
		// ----------------------------------------------------------------
		public bool IsCamera() => vSrc.HasValue && vDst.HasValue &&
								 IsValid(vSrc.Value) && IsValid(vDst.Value);

		// ----------------------------------------------------------------
		// Validation – NaN / Infinity guard
		// ----------------------------------------------------------------
		private static bool IsValid(Vector3 v)
		{
			return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
				   !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
				   !float.IsNaN(v.z) && !float.IsInfinity(v.z);
		}

		// ----------------------------------------------------------------
		// Mutators – null = “no camera data”
		// ----------------------------------------------------------------
		public void SetVSrc(Vector3 vec)
		{
			vSrc = IsValid(vec) ? (Vector3?)vec : null;
		}

		public void SetVDst(Vector3 vec)
		{
			vDst = IsValid(vec) ? (Vector3?)vec : null;
		}

		// ----------------------------------------------------------------
		// Just‑in‑time conversion from the serializer DTO
		// ----------------------------------------------------------------
		public static Waypoint FromSerialized(DatabaseSerializer.Waypoint ser)
		{
			if (ser == null) return null;

			var wp = new Waypoint
			{
				name = ser.name,
				nTile = ser.nTile
			};

			if (ser.vSrc != null && ser.vSrc.Length == 3)
				wp.vSrc = new Vector3(ser.vSrc[0], ser.vSrc[1], ser.vSrc[2]);

			if (ser.vDst != null && ser.vDst.Length == 3)
				wp.vDst = new Vector3(ser.vDst[0], ser.vDst[1], ser.vDst[2]);

			return wp;
		}

		// ----------------------------------------------------------------
		// Just‑in‑time conversion back to the serializer DTO
		// ----------------------------------------------------------------
		public DatabaseSerializer.Waypoint ToSerialized()
		{
			return new DatabaseSerializer.Waypoint
			{
				name = name,
				nTile = nTile,
				vSrc = vSrc.HasValue ? new[] { vSrc.Value.x, vSrc.Value.y, vSrc.Value.z } : null,
				vDst = vDst.HasValue ? new[] { vDst.Value.x, vDst.Value.y, vDst.Value.z } : null
			};
		}
	}
}