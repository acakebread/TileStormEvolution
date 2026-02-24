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
		public void SetLight(Vector3 direction, Color color) { }

		public bool HasCommands(RenderPassEvent evt)
		{
			return evt == RenderPassEvent.BeforeRenderingOpaques;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (evt != RenderPassEvent.BeforeRenderingOpaques || models == null)
				return;

			// NO lighting globals here — real Light component handles it
			// Ambient is deliberately disabled for consistency across Editor/WebGL

			// Just draw the models
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


		//public bool HasCommands(RenderPassEvent evt)
		//{
		//	return evt == RenderPassEvent.BeforeRendering || evt == RenderPassEvent.BeforeRenderingOpaques;
		//}

		//public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		//{
		//	if (evt != RenderPassEvent.BeforeRenderingOpaques || models == null)
		//		return;

		//	foreach (var model in models)
		//	{
		//		if (model == null) continue;

		//		foreach (var inst in model.meshInstances)
		//		{
		//			if (inst.mesh == null) continue;

		//			for (int s = 0; s < inst.subMeshCount; s++)
		//			{
		//				var mat = s < inst.materials.Length ? inst.materials[s] : inst.materials[0];
		//				if (mat == null) continue;

		//				// Force Lit shader keywords & properties
		//				// This mimics what URP expects for Lit to "see" light
		//				mat.EnableKeyword("_MAIN_LIGHT_SHADOWS");           // enable main light
		//				mat.EnableKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");   // shadows if needed
		//				mat.EnableKeyword("_ADDITIONAL_LIGHTS");            // allow extra lights if any
		//				mat.EnableKeyword("_ADDITIONAL_LIGHT_SHADOWS");     // extra shadows

		//				// Force basic properties (in case URP didn't bind them)
		//				mat.SetColor("_BaseColor", Color.white);            // fallback color
		//				mat.SetFloat("_Smoothness", 0.5f);                  // prevent black from roughness=1
		//				mat.SetFloat("_Metallic", 0f);                      // non-metallic fallback

		//				// Optional: force a dummy normal map if shader expects it
		//				// mat.SetTexture("_BumpMap", Texture2D.normalTexture);

		//				cmd.DrawMesh(inst.mesh, inst.localToWorld, mat, s, 0);
		//			}
		//		}
		//	}
		//}

		public void Destroy() { }
	}
}