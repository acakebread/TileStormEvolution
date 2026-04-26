using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class EnsureDepthTexturePass : ScriptableRendererFeature
{
	class CustomRenderPass : ScriptableRenderPass
	{
		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			// Access UniversalCameraData from ContextContainer
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
			// Ensure depth texture is allocated
			cameraData.cameraTargetDescriptor.depthBufferBits = 32; // Request depth buffer
			cameraData.requiresDepthTexture = true;
		}
	}

	CustomRenderPass m_ScriptablePass;

	public override void Create()
	{
		m_ScriptablePass = new CustomRenderPass();
		m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		renderer.EnqueuePass(m_ScriptablePass);
	}
}