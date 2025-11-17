// ---------------------------------------------------------------
// Waypoint.cs   – THE ONE AND ONLY Waypoint class in the entire project
// ---------------------------------------------------------------
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class Waypoint
	{
		public string name;
		public int nTile;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public float[] vSrc;

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public float[] vDst;

		// --------------------------------------------------------------------
		// Runtime helpers (exactly the same as before)
		// --------------------------------------------------------------------
		public Vector3 GetVSrc() => vSrc != null && vSrc.Length == 3 && IsValid(vSrc)
			? new Vector3(vSrc[0], vSrc[1], vSrc[2])
			: Vector3.zero;

		public Vector3 GetVDst() => vDst != null && vDst.Length == 3 && IsValid(vDst)
			? new Vector3(vDst[0], vDst[1], vDst[2])
			: Vector3.zero;

		public bool IsCamera() => vSrc != null && vSrc.Length == 3 && IsValid(vSrc) &&
								  vDst != null && vDst.Length == 3 && IsValid(vDst);

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
	}
}