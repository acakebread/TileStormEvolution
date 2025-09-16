//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//public class BeforeRendererPass : ScriptableRenderPass
//{
//	private string passName = "BeforeRendererPass";

//	public BeforeRendererPass()
//	{
//		renderPassEvent = RenderPassEvent.BeforeRendering; // Early in the pipeline for before render
//	}

//	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//	{
//		// Get the camera from RenderingData
//		Camera camera = renderingData.cameraData.camera;

//		// Check for custom before render settings component
//		BeforeRenderSettings beforeRenderSettings = camera.GetComponent<BeforeRenderSettings>();

//		// Create and configure the command buffer
//		CommandBuffer cmd = CommandBufferPool.Get(passName);
//		beforeRenderSettings?.BeforeRender?.Invoke(cmd);

//		// Execute and release the command buffer
//		context.ExecuteCommandBuffer(cmd);
//		CommandBufferPool.Release(cmd);
//	}
//}

//public class AfterRendererPass : ScriptableRenderPass
//{
//	private string passName = "AfterRendererPass";

//	public AfterRendererPass()
//	{
//		renderPassEvent = RenderPassEvent.AfterRendering; // Late in the pipeline for after render
//	}

//	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//	{
//		// Get the camera from RenderingData
//		Camera camera = renderingData.cameraData.camera;

//		// Check for custom before render settings component
//		BeforeRenderSettings renderSettings = camera.GetComponent<BeforeRenderSettings>();

//		// Create and configure the command buffer
//		CommandBuffer cmd = CommandBufferPool.Get(passName);
//		renderSettings?.AfterRender?.Invoke(cmd);

//		// Execute and release the command buffer
//		context.ExecuteCommandBuffer(cmd);
//		CommandBufferPool.Release(cmd);
//	}
//}


//public class BeforeRendererFeature : ScriptableRendererFeature
//{
//	BeforeRendererPass beforeRendererPass;
//	AfterRendererPass afterRendererPass;

//	public override void Create()
//	{
//		beforeRendererPass = new BeforeRendererPass();
//		afterRendererPass = new AfterRendererPass();
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		// Enqueue the pass for all cameras
//		renderer.EnqueuePass(beforeRendererPass);
//		renderer.EnqueuePass(afterRendererPass);
//	}
//}