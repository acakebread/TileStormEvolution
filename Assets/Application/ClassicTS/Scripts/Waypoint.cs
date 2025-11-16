// ---------------------------------------------------------------
// Waypoint.cs   (CORRECTED: with dedicated tile-space fields)
// ---------------------------------------------------------------
using UnityEngine;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class Waypoint
	{
		public string name;
		public int nTile;

		// Original world-space vectors (from JSON, with tile_origin applied on read)
		private Vector3? vSrc;
		private Vector3? vDst;

		// NEW: Tile-space vectors (computed in MapManager.SetupWaypoints)
		private Vector3? vSrcTileSpace;
		private Vector3? vDstTileSpace;

		// --- ORIGINAL: World-space with tile_origin (legacy) ---
		public Vector3 GetVSrc() => vSrc.HasValue && IsValid(vSrc.Value)
			? vSrc.Value + MapManager.tile_origin
			: Vector3.zero;

		public Vector3 GetVDst() => vDst.HasValue && IsValid(vDst.Value)
			? vDst.Value + MapManager.tile_origin
			: Vector3.zero;

		//// --- NEW: Tile-space (relative to tile center, no origin) ---
		//public Vector3 GetVSrcTileSpace() => vSrcTileSpace.HasValue && IsValid(vSrcTileSpace.Value)
		//	? vSrcTileSpace.Value
		//	: Vector3.zero;

		//public Vector3 GetVDstTileSpace() => vDstTileSpace.HasValue && IsValid(vDstTileSpace.Value)
		//	? vDstTileSpace.Value
		//	: Vector3.zero;

		// Camera check uses original world vectors
		public bool IsCamera() => vSrc.HasValue && vDst.HasValue &&
								 IsValid(vSrc.Value) && IsValid(vDst.Value);

		private static bool IsValid(Vector3 v)
		{
			return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
				   !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
				   !float.IsNaN(v.z) && !float.IsInfinity(v.z);
		}

		public void SetVSrc(Vector3 vec)
		{
			vSrc = IsValid(vec) ? (Vector3?)vec : null;
		}

		public void SetVDst(Vector3 vec)
		{
			vDst = IsValid(vec) ? (Vector3?)vec : null;
		}

		// --- NEW: Internal setter for tile-space (called by MapManager) ---
		internal void SetVSrcTileSpace(Vector3 vec)
		{
			vSrcTileSpace = IsValid(vec) ? (Vector3?)vec : null;
		}

		internal void SetVDstTileSpace(Vector3 vec)
		{
			vDstTileSpace = IsValid(vec) ? (Vector3?)vec : null;
		}

		// --- Conversion from DTO ---
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

			// Note: tile-space not set here — done in MapManager.SetupWaypoints
			return wp;
		}

		// --- Back to DTO (only serializes original vSrc/vDst) ---
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