using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public sealed class CommandRenderScene : MonoBehaviour, ICommandBufferProvider
	{
		private static readonly int MainLightPositionID = Shader.PropertyToID("_MainLightPosition");
		private static readonly int MainLightColorID = Shader.PropertyToID("_MainLightColor");

		private CommandRenderModelData[] models;

		[SerializeField] private Vector3 lightDirection = new Vector3(0.5f, 1f, -0.3f).normalized;
		[SerializeField] private Color lightColor = new Color(0.75f, 0.75f, 0.75f);

		public void SetModels(CommandRenderModelData[] data) => models = data;

		public void SetLight(Vector3 direction, Color color)
		{
			lightDirection = direction.normalized;
			lightColor = color;
		}

		// ─────────────── ICommandBufferProvider ───────────────

		public bool HasCommands(RenderPassEvent evt)
		{
			return evt == RenderPassEvent.BeforeRenderingOpaques;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (evt != RenderPassEvent.BeforeRenderingOpaques || models == null)
				return;

			Vector4 pos = new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 1f);
			cmd.SetGlobalVector(MainLightPositionID, pos);
			cmd.SetGlobalVector(MainLightColorID, lightColor.linear);

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

		//none of this is currently needed but I have a nagging feeling that we may want to let the scene know about the cameras at a future point so leave commented
		//[SerializeField] private CommandRenderCamera renderCamera;
		//// Only change: type is now CommandRenderCamera (not MonoBehaviour anymore)
		//public CommandRenderCamera Camera => renderCamera;

		//// In Initialize:
		//public void Initialize(CommandRenderCamera camera)
		//{
		//	renderCamera = camera ?? throw new System.ArgumentNullException(nameof(camera));
		//	camera.AssignCommandProvider(this);
		//}
	}
}