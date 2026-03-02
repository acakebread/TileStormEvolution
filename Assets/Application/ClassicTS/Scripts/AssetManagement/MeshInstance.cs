using UnityEngine;

namespace ClassicTilestorm
{
	internal class MeshInstance
	{
		private Mesh mesh;
		private Material[] unlitMats;
		private object matrix;

		public MeshInstance(Mesh mesh, Material[] unlitMats, object matrix)
		{
			this.mesh = mesh;
			this.unlitMats = unlitMats;
			this.matrix = matrix;
		}
	}
}