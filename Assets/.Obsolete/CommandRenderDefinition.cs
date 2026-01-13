using UnityEngine;

namespace MassiveHadronLtd.Render
{
	[System.Serializable]
	public class CommandRenderDefinition
	{
		public Mesh Mesh;
		public Material[] Materials;
		public Matrix4x4 LocalToWorldMatrix;
		public bool IsSkinned;
		public Transform[] Bones; // only needed if skinned

		public CommandRenderDefinition(MeshRenderer mr)
		{
			// CRITICAL FIX: Get mesh from MeshFilter, not MeshRenderer
			MeshFilter filter = mr.GetComponent<MeshFilter>();
			Mesh = filter != null ? filter.sharedMesh : null;

			Materials = mr.sharedMaterials;
			LocalToWorldMatrix = mr.localToWorldMatrix;
			IsSkinned = false;
		}

		public CommandRenderDefinition(SkinnedMeshRenderer smr)
		{
			// SkinnedMeshRenderer DOES have sharedMesh directly (no MeshFilter needed)
			Mesh = smr.sharedMesh;
			Materials = smr.sharedMaterials;
			LocalToWorldMatrix = smr.localToWorldMatrix;
			IsSkinned = true;
			Bones = smr.bones;
		}
	}
}