using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public class CommandRenderScene : ICommandBufferProvider
	{
		private CommandRenderModelData[] models;

		public void SetModels(CommandRenderModelData[] data) => models = data;

		// Keep SetLight for future use (icons can call it if we ever want to switch back)
		//public void SetLight(Vector3 direction, Color color) { }

		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			foreach (var model in models)
			{
				if (model == null) continue;

				foreach (var inst in model.meshInstances)
				{
					if (inst.mesh == null) continue;

					for (int s = 0; s < inst.subMeshCount; s++)
					{
						var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
						if (mat == null) continue;

						cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0);
					}
				}
			}
		}

		public void Destroy() { }
	}
}