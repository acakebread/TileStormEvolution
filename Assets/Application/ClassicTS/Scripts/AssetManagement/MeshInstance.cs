using UnityEngine;

namespace ClassicTilestorm
{
	internal class MeshInstance
	{
		private readonly Mesh mesh;
		private readonly Material[] unlitMats;
		private readonly object matrix;

		public MeshInstance(Mesh mesh, Material[] unlitMats, object matrix)
		{
			this.mesh = mesh;
			this.unlitMats = unlitMats;
			this.matrix = matrix;
		}
	}
}