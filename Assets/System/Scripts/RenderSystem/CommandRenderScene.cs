using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public class CommandRenderScene : ICommandBufferProvider
	{
		private CommandRenderModelData[] _models;

		// These are the only things you should touch from outside
		public Vector3 MainLightDirection { get; set; } = Vector3.forward;
		public Color MainLightColor { get; set; } = Color.white;
		public float MainLightIntensity { get; set; } = 1.0f;

		public Color AmbientColor { get; set; } = new Color(0.25f, 0.25f, 0.3f);
		public float AmbientIntensity { get; set; } = 0.8f;

		public void SetModels(CommandRenderModelData[] data) => _models = data;

		public bool HasCommands(RenderPassEvent evt) => evt == RenderPassEvent.BeforeRenderingOpaques;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (_models == null) return;

			// Force the direction you set (no extra rotation here)
			Vector3 dir = MainLightDirection.normalized;
			cmd.SetGlobalVector("_MainLightPosition", new Vector4(dir.x, dir.y, dir.z, 0f));
			cmd.SetGlobalColor("_MainLightColor", MainLightColor * MainLightIntensity);

			cmd.SetGlobalFloat("_MainLightShadowStrength", 0f);
			//cmd.SetGlobalFloat("_AdditionalLightsCount", 0f);

			cmd.SetGlobalColor("_AmbientLight", AmbientColor * AmbientIntensity);

			// Very weak SH so it doesn't fight the main light
			float sh = AmbientIntensity * 0.5f;
			cmd.SetGlobalVector("unity_SHAr", new Vector4(0.2f, 0.2f, 0.2f, 0f) * sh);
			cmd.SetGlobalVector("unity_SHAg", new Vector4(0.2f, 0.2f, 0.2f, 0f) * sh);
			cmd.SetGlobalVector("unity_SHAb", new Vector4(0.2f, 0.2f, 0.2f, 0f) * sh);
			cmd.SetGlobalVector("unity_SHC", new Vector4(0.15f, 0.15f, 0.15f, 1f) * sh);

			// DRAW
			foreach (var model in _models)
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