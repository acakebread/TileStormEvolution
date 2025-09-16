//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//public class CullingRenderPass : ScriptableRenderPass
//{
//	private string passName = "CullingPass";

//	public CullingRenderPass()
//	{
//		renderPassEvent = RenderPassEvent.BeforeRendering; // Early in the pipeline for culling
//	}

//	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//	{
//		// Get the camera from RenderingData
//		Camera camera = renderingData.cameraData.camera;

//		// Check for custom culling settings component
//		CameraCullingSettings cullingSettings = camera.GetComponent<CameraCullingSettings>();
//		bool invertCulling = cullingSettings != null && cullingSettings.invertCulling;

//		// Create and configure the command buffer
//		CommandBuffer cmd = CommandBufferPool.Get(passName);
//		cmd.SetInvertCulling(invertCulling); // Set based on component or default to false

//		// Log for debugging
//		Debug.Log($"Camera: {camera.name}, InvertCulling: {invertCulling}");

//		// Execute and release the command buffer
//		context.ExecuteCommandBuffer(cmd);
//		CommandBufferPool.Release(cmd);
//	}
//}

//public class CullingRendererFeature : ScriptableRendererFeature
//{
//	CullingRenderPass cullingPass;

//	public override void Create()
//	{
//		cullingPass = new CullingRenderPass();
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		// Enqueue the pass for all cameras
//		renderer.EnqueuePass(cullingPass);
//	}
//}