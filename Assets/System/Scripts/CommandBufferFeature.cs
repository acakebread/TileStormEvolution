using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

public class CommandBufferPass : ScriptableRenderPass
{
	private new string passName;

	public CommandBufferPass(RenderPassEvent renderEvent)
	{
		passName = $"CommandBufferPass_{renderEvent}";
		renderPassEvent = renderEvent;
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

		Camera cam = cameraData.camera;
		ICommandBufferProvider provider = cam.GetComponent<ICommandBufferProvider>();

		if (provider == null || !provider.HasCommands(renderPassEvent))
		{
			Debug.Log($"Skipping CommandBufferPass for {cam.name} at {renderPassEvent}: No provider or commands");
			return;
		}

		using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
		{
			passData.camera = cam;
			passData.provider = provider;

			var colorTarget = renderPassEvent == RenderPassEvent.BeforeRendering
				? resourceData.activeColorTexture
				: resourceData.cameraColor;
			var depthTarget = resourceData.cameraDepth;

			if (!colorTarget.IsValid() || (renderPassEvent != RenderPassEvent.BeforeRendering && !depthTarget.IsValid()))
			{
				Debug.LogError($"{passName}: Invalid render targets: Color={colorTarget.IsValid()}, Depth={depthTarget.IsValid()}, Camera={cam.name}");
				return;
			}

			builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
			if (renderPassEvent != RenderPassEvent.BeforeRendering)
				builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
			{
				//Debug.Log($"Executing CommandBufferPass for {data.camera.name} at {renderPassEvent}");
				data.provider.ExecuteCommands(renderPassEvent, context.cmd, data.camera);
			});
		}
	}

	private class PassData
	{
		public ICommandBufferProvider provider;
		public Camera camera;
	}
}

[CreateAssetMenu(menuName = "Rendering/CommandBufferFeature")]
public class CommandBufferFeature : ScriptableRendererFeature
{
	private Dictionary<RenderPassEvent, CommandBufferPass> renderPasses;

	public override void Create()
	{
		renderPasses = new Dictionary<RenderPassEvent, CommandBufferPass>();
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		var cam = renderingData.cameraData.camera;
		var provider = cam.GetComponent<ICommandBufferProvider>();
		if (provider == null)
		{
			Debug.Log($"No ICommandBufferProvider found for {cam.name}");
			return;
		}

		foreach (RenderPassEvent evt in System.Enum.GetValues(typeof(RenderPassEvent)))
		{
			if (provider.HasCommands(evt))
			{
				if (!renderPasses.ContainsKey(evt))
				{
					renderPasses[evt] = new CommandBufferPass(evt);
				}
				renderer.EnqueuePass(renderPasses[evt]);
			}
		}
	}

	protected override void Dispose(bool disposing)
	{
		renderPasses.Clear();
	}
}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine.Rendering.RenderGraphModule;
//using System.Collections.Generic;

//public class CommandBufferPass : ScriptableRenderPass
//{
//	private new string passName;

//	public CommandBufferPass(RenderPassEvent renderEvent)
//	{
//		this.passName = $"CommandBufferPass_{renderEvent}";
//		renderPassEvent = renderEvent; // Directly assign the provided RenderPassEvent
//	}

//	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//	{
//		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
//		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

//		Camera cam = cameraData.camera;
//		CommandBufferSettings bufferSettings = cam.GetComponent<CommandBufferSettings>();

//		// Skip cameras without CommandBufferSettings
//		if (bufferSettings == null)
//			return;

//		using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
//		{
//			passData.camera = cam;
//			passData.bufferSettings = bufferSettings;

//			var colorTarget = renderPassEvent == RenderPassEvent.BeforeRendering
//				? resourceData.activeColorTexture
//				: resourceData.cameraColor;
//			var depthTarget = resourceData.cameraDepth;

//			if (!colorTarget.IsValid() || (renderPassEvent != RenderPassEvent.BeforeRendering && !depthTarget.IsValid()))
//			{
//				Debug.LogError($"{passName}: Invalid render targets: Color={colorTarget.IsValid()}, Depth={depthTarget.IsValid()}, Camera={cam.name}");
//				return;
//			}

//			builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
//			if (renderPassEvent != RenderPassEvent.BeforeRendering)
//				builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
//			builder.AllowPassCulling(false);
//			builder.AllowGlobalStateModification(true);

//			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
//			{
//				// Execute registered commands
//				data.bufferSettings.ExecuteCommands(renderPassEvent, context.cmd, data.camera);
//			});
//		}
//	}

//	private class PassData
//	{
//		public CommandBufferSettings bufferSettings;
//		public Camera camera;
//	}
//}

//[CreateAssetMenu(menuName = "Rendering/CommandBufferFeature")]
//public class CommandBufferFeature : ScriptableRendererFeature
//{
//	private Dictionary<RenderPassEvent, CommandBufferPass> renderPasses;

//	public override void Create()
//	{
//		renderPasses = new Dictionary<RenderPassEvent, CommandBufferPass>();
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		var cam = renderingData.cameraData.camera;
//		var settings = cam.GetComponent<CommandBufferSettings>();
//		if (settings == null)
//			return;

//		// Iterate through all possible RenderPassEvent values
//		foreach (RenderPassEvent evt in System.Enum.GetValues(typeof(RenderPassEvent)))
//		{
//			if (settings.HasCommands(evt))
//			{
//				// Create a CommandBufferPass if it doesn't exist
//				if (!renderPasses.ContainsKey(evt))
//				{
//					renderPasses[evt] = new CommandBufferPass(evt);
//				}
//				renderer.EnqueuePass(renderPasses[evt]);
//			}
//		}
//	}

//	protected override void Dispose(bool disposing)
//	{
//		renderPasses.Clear();
//	}
//}