using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class ReflectionCullingPassRG : ScriptableRenderPass
{
	private const string k_PassName = "ReflectionCullingPassRG";

	public ReflectionCullingPassRG()
	{
		renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

			Camera cam = cameraData.camera;
			passData.bufferSettings = cam.GetComponent<CommandBufferSettingsRG>();
			passData.camera = cam; // Set camera in PassData

			// Only execute for Reflection Camera
			if (cam.name != "Reflection Camera")
				return;

			var colorTarget = resourceData.cameraColor;
			var depthTarget = resourceData.cameraDepth;

			if (!colorTarget.IsValid() || !depthTarget.IsValid())
			{
				Debug.LogError($"ReflectionCullingPassRG: Invalid render targets: Color={colorTarget.IsValid()}, Depth={depthTarget.IsValid()}, Camera={cam.name}");
				return;
			}

			builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
			{
				var cmd = context.cmd;
				if (data.bufferSettings != null && data.camera != null)
					data.bufferSettings.ExecuteBeforeRender(cmd, data.camera);
				else
					Debug.LogError($"ReflectionCullingPassRG: Cannot execute; bufferSettings={data.bufferSettings}, camera={data.camera}");
			});
		}
	}

	private class PassData
	{
		public TextureHandle srcColor;
		public CommandBufferSettingsRG bufferSettings;
		public Camera camera;
	}
}

public class BeforeRendererPassRG : ScriptableRenderPass
{
	private const string k_PassName = "BeforeRendererPassRG";

	public BeforeRendererPassRG()
	{
		renderPassEvent = RenderPassEvent.BeforeRendering;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

			Camera cam = cameraData.camera;
			passData.bufferSettings = cam.GetComponent<CommandBufferSettingsRG>();

			var colorTarget = resourceData.activeColorTexture;
			if (!colorTarget.IsValid())
			{
				Debug.LogError($"BeforeRendererPassRG: Invalid color target for Camera={cam.name}");
				return;
			}
			builder.SetRenderAttachment(colorTarget, 0);
			builder.AllowPassCulling(false);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
			{
				var cmd = context.cmd;
				if (data.bufferSettings != null)
					data.bufferSettings.ExecuteBeforeRender(cmd, cam);
			});
		}
	}

	private class PassData
	{
		public TextureHandle srcColor;
		public CommandBufferSettingsRG bufferSettings;
	}
}

public class AfterRendererTransparentsPassRG : ScriptableRenderPass
{
	private const string k_PassName = "AfterRendererTransparentsPassRG";

	public AfterRendererTransparentsPassRG()
	{
		renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

			Camera cam = cameraData.camera;
			passData.bufferSettings = cam.GetComponent<CommandBufferSettingsRG>();

			var colorTarget = resourceData.cameraColor;
			var depthTarget = resourceData.cameraDepth;

			if (!colorTarget.IsValid() || !depthTarget.IsValid())
			{
				Debug.LogError($"AfterRendererTransparentsPassRG: Invalid render targets: Color={colorTarget.IsValid()}, Depth={depthTarget.IsValid()}, Camera={cam.name}");
				return;
			}

			builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
			{
				var cmd = context.cmd;
				if (data.bufferSettings != null)
					data.bufferSettings.ExecuteAfterTransparentRender(cmd, cam);
			});
		}
	}

	private class PassData
	{
		public TextureHandle srcColor;
		public CommandBufferSettingsRG bufferSettings;
	}
}

public class AfterRendererPassRG : ScriptableRenderPass
{
	private const string k_PassName = "AfterRendererPassRG";

	public AfterRendererPassRG()
	{
		renderPassEvent = RenderPassEvent.AfterRendering;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

			Camera cam = cameraData.camera;
			passData.bufferSettings = cam.GetComponent<CommandBufferSettingsRG>();

			var colorTarget = resourceData.cameraColor;
			var depthTarget = resourceData.cameraDepth;

			if (!colorTarget.IsValid() || !depthTarget.IsValid())
			{
				Debug.LogError($"AfterRendererPassRG: Invalid render targets: Color={colorTarget.IsValid()}, Depth={depthTarget.IsValid()}, Camera={cam.name}");
				return;
			}

			builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
			builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
			{
				var cmd = context.cmd;
				if (data.bufferSettings != null)
					data.bufferSettings.ExecuteAfterRender(cmd, cam);
			});
		}
	}

	private class PassData
	{
		public TextureHandle srcColor;
		public CommandBufferSettingsRG bufferSettings;
	}
}

[CreateAssetMenu(menuName = "Rendering/CommandBufferFeatureRG")]
public class CommandBufferFeatureRG : ScriptableRendererFeature
{
	ReflectionCullingPassRG reflectionCullingPass;
	BeforeRendererPassRG beforePass;
	AfterRendererTransparentsPassRG afterTransparentPass;
	AfterRendererPassRG afterPass;

	public override void Create()
	{
		reflectionCullingPass = new ReflectionCullingPassRG();
		beforePass = new BeforeRendererPassRG();
		afterTransparentPass = new AfterRendererTransparentsPassRG();
		afterPass = new AfterRendererPassRG();
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		renderer.EnqueuePass(reflectionCullingPass);
		// renderer.EnqueuePass(beforePass); // Keep commented out
		renderer.EnqueuePass(afterTransparentPass);
		renderer.EnqueuePass(afterPass);
	}

	protected override void Dispose(bool disposing)
	{
	}
}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine.Rendering.RenderGraphModule;

//public class BeforeRendererPassRG : ScriptableRenderPass
//{
//	private const string k_PassName = "BeforeRendererPassRG";
//	private static int executionCount = 0;

//	public BeforeRendererPassRG()
//	{
//		renderPassEvent = RenderPassEvent.BeforeRendering;
//	}

//	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//	{
//		using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
//		{
//			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
//			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

//			Camera cam = cameraData.camera;
//			passData.bufferSettings = cam.GetComponent<CommandBufferSettingsRG>();

//			// ✅ Bind the camera color target to avoid render errors
//			var colorTarget = resourceData.activeColorTexture;
//			builder.SetRenderAttachment(colorTarget, 0); // 0 = color attachment index

//			builder.AllowPassCulling(false);

//			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
//			{
//				var cmd = context.cmd;
//				if (data.bufferSettings != null)
//					data.bufferSettings.ExecuteBeforeRender(cmd, cam);
//			});
//		}
//	}

//	public override void OnFinishCameraStackRendering(CommandBuffer cmd)
//	{
//		Debug.Log($"BeforeRendererPassRG: Finished camera stack rendering, Total Executions={executionCount}");
//		executionCount = 0;
//	}

//	private class PassData
//	{
//		public TextureHandle srcColor;
//		public CommandBufferSettingsRG bufferSettings;
//	}
//}

//public class AfterRendererPassRG : ScriptableRenderPass
//{
//	private const string k_PassName = "AfterRendererPassRG";

//	public AfterRendererPassRG()
//	{
//		renderPassEvent = RenderPassEvent.AfterRendering;
//	}

//	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//	{
//		using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_PassName, out var passData))
//		{
//			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
//			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

//			Camera cam = cameraData.camera;
//			passData.bufferSettings = cam.GetComponent<CommandBufferSettingsRG>();

//			// ✅ Bind the camera color target to avoid render errors
//			var colorTarget = resourceData.activeColorTexture;
//			builder.SetRenderAttachment(colorTarget, 0); // 0 = color attachment index

//			builder.AllowPassCulling(false);

//			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
//			{
//				var cmd = context.cmd;
//				if (data.bufferSettings != null)
//					data.bufferSettings.ExecuteAfterRender(cmd, cam);
//			});
//		}
//	}

//	private class PassData
//	{
//		public TextureHandle srcColor;
//		public CommandBufferSettingsRG bufferSettings;
//	}
//}

//[CreateAssetMenu(menuName = "Rendering/CommandBufferFeatureRG")]
//public class CommandBufferFeatureRG : ScriptableRendererFeature
//{
//	BeforeRendererPassRG beforePass;
//	AfterRendererPassRG afterPass;

//	public override void Create()
//	{
//		beforePass = new BeforeRendererPassRG();
//		afterPass = new AfterRendererPassRG();

//		// Optionally set different events here if you want them placed differently
//		// beforePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques; etc.
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		// Enqueue these passes for all cameras
//		renderer.EnqueuePass(beforePass);
//		renderer.EnqueuePass(afterPass);
//	}

//	// Clean‑up if needed
//	protected override void Dispose(bool disposing)
//	{
//		// If you create any materials or other disposable resources in this Feature you should clean them here
//	}
//}
