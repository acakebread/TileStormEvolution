using UnityEngine;

namespace MassiveHadronLtd
{
	public static class MeshExtensions
	{
		/// <summary>
		/// Returns true if the mesh is *actually* safe to modify at runtime.
		/// </summary>
		public static bool IsRuntimeWritable(this MeshFilter filter)
		{
			if (filter == null || filter.sharedMesh == null)
				return false;

			// Use sharedMesh, not mesh (to avoid Unity cloning it)
			return filter.sharedMesh.isReadable;
		}

		public static bool IsRuntimeWritable(this SkinnedMeshRenderer smr)
		{
			if (smr == null || smr.sharedMesh == null)
				return false;

			return smr.sharedMesh.isReadable;
		}
	}
}