//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//public class ReflectionCullingPass : ScriptableRenderPass
//{
//	public ReflectionCullingPass()
//	{
//		renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
//	}

//	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//	{
//		var cmd = CommandBufferPool.Get("CullingPass");
//		if (renderingData.cameraData.camera.name == "Reflection Camera")
//		{
//			cmd.SetInvertCulling(true);
//		}
//		else
//		{
//			cmd.SetInvertCulling(false);
//		}
//		context.ExecuteCommandBuffer(cmd);
//		CommandBufferPool.Release(cmd);
//	}
//}

//public class ReflectionCullingFeature : ScriptableRendererFeature
//{
//	private ReflectionCullingPass pass;

//	public override void Create()
//	{
//		pass = new ReflectionCullingPass();
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		renderer.EnqueuePass(pass);
//	}
//}



//////public class ReflectionCullingPassPreRender : ScriptableRenderPass
//////{
//////	public ReflectionCullingPassPreRender()
//////	{
//////		renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
//////	}

//////	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//////	{
//////		var cmd = CommandBufferPool.Get("CullingPass");
//////		cmd.SetInvertCulling(true);
//////		context.ExecuteCommandBuffer(cmd);
//////		CommandBufferPool.Release(cmd);
//////	}
//////}

//////public class ReflectionCullingPassPostRender : ScriptableRenderPass
//////{
//////	public ReflectionCullingPassPostRender()
//////	{
//////		renderPassEvent = RenderPassEvent.AfterRendering;
//////	}

//////	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//////	{
//////		var cmd = CommandBufferPool.Get("CullingPass");
//////		cmd.SetInvertCulling(false);
//////		context.ExecuteCommandBuffer(cmd);
//////		CommandBufferPool.Release(cmd);
//////	}
//////}

////public class ReflectionCullingFeature : ScriptableRendererFeature
////{
////	//private ReflectionCullingPassPreRender passPreRender;
////	//private ReflectionCullingPassPostRender passPostRender;

////	public override void Create()
////	{
////		//passPreRender = new ReflectionCullingPassPreRender();
////		//passPostRender = new ReflectionCullingPassPostRender();
////	}

////	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
////	{
////		//renderer.EnqueuePass(passPreRender);
////		//renderer.EnqueuePass(passPostRender);
////	}
////}