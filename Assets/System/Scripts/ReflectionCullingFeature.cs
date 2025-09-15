using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ReflectionCullingPass : ScriptableRenderPass
{
	public ReflectionCullingPass()
	{
		renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
	}

	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
	{
		var cmd = CommandBufferPool.Get("CullingPass");
		if (renderingData.cameraData.camera.name == "Reflection Camera")
		{
			cmd.SetInvertCulling(true);
			cmd.SetGlobalFloat("_TestCommandBuffer", 1.0f);
		}
		else
		{
			cmd.SetInvertCulling(false);
			cmd.SetGlobalFloat("_TestCommandBuffer", 0.0f);
		}
		context.ExecuteCommandBuffer(cmd);
		CommandBufferPool.Release(cmd);
	}
}

public class ReflectionCullingFeature : ScriptableRendererFeature
{
	private ReflectionCullingPass pass;

	public override void Create()
	{
		pass = new ReflectionCullingPass();
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		renderer.EnqueuePass(pass);
	}
}