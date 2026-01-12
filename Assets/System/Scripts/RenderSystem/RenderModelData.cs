using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	public readonly struct MeshInstanceInfo
	{
		public readonly Mesh mesh;
		public readonly Material[] materials;       // per submesh
		public readonly Matrix4x4 localToWorld;
		public readonly int subMeshCount;
		public readonly int layer;                  // optional, usually same

		public MeshInstanceInfo(Mesh mesh, Material[] materials, Matrix4x4 localToWorld, int layer = 0)
		{
			this.mesh = mesh;
			this.materials = materials;
			this.localToWorld = localToWorld;
			this.subMeshCount = mesh != null ? mesh.subMeshCount : 0;
			this.layer = layer;
		}
	}

	public class RenderModelData
	{
		public readonly List<MeshInstanceInfo> meshInstances = new();
		public Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 1f);

		public void Clear()
		{
			meshInstances.Clear();
			bounds = new Bounds(Vector3.zero, Vector3.zero);
		}

		public void AddMeshInstance(Mesh mesh, Material[] materials, Matrix4x4 localToWorld, int layer = 0)
		{
			meshInstances.Add(new MeshInstanceInfo(mesh, materials, localToWorld, layer));
			bounds.Encapsulate(BoundsTransformed(mesh.bounds, localToWorld));
		}

		private static Bounds BoundsTransformed(Bounds localBounds, Matrix4x4 matrix)
		{
			var corners = new Vector3[8];
			int i = 0;

			for (int x = -1; x <= 1; x += 2)
				for (int y = -1; y <= 1; y += 2)
					for (int z = -1; z <= 1; z += 2)
					{
						// Fixed version - using Vector3.Scale
						Vector3 offset = Vector3.Scale(new Vector3(x, y, z), localBounds.extents);
						corners[i++] = matrix.MultiplyPoint(localBounds.center + offset);
					}

			var bounds = new Bounds(corners[0], Vector3.zero);
			for (int j = 1; j < 8; j++)
				bounds.Encapsulate(corners[j]);

			return bounds;
		}
	}
}