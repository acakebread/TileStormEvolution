using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CommandBufferPass : ScriptableRenderPass
{
	private readonly string passName;

	public CommandBufferPass(RenderPassEvent renderEvent)
	{
		this.passName = $"CommandBufferPass_{renderEvent}";
		renderPassEvent = renderEvent; // Directly assign the provided RenderPassEvent
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

		Camera cam = cameraData.camera;
		CommandBufferSettings bufferSettings = cam.GetComponent<CommandBufferSettings>();

		// Skip cameras without CommandBufferSettings
		if (bufferSettings == null)
			return;

		using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
		{
			passData.camera = cam;
			passData.bufferSettings = bufferSettings;

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
				// Execute registered commands
				data.bufferSettings.ExecuteCommands(renderPassEvent, context.cmd, data.camera);
			});
		}
	}

	private class PassData
	{
		public CommandBufferSettings bufferSettings;
		public Camera camera;
	}
}

[CreateAssetMenu(menuName = "Rendering/CommandBufferFeature")]
public class CommandBufferFeature : ScriptableRendererFeature
{
	private CommandBufferPass beforeRenderingPass;
	private CommandBufferPass beforeRenderingOpaquesPass;
	private CommandBufferPass afterRenderingTransparentsPass;
	private CommandBufferPass afterRenderingPass;

	public override void Create()
	{
		beforeRenderingPass = new CommandBufferPass(RenderPassEvent.BeforeRendering);
		beforeRenderingOpaquesPass = new CommandBufferPass(RenderPassEvent.BeforeRenderingOpaques);
		afterRenderingTransparentsPass = new CommandBufferPass(RenderPassEvent.AfterRenderingTransparents);
		afterRenderingPass = new CommandBufferPass(RenderPassEvent.AfterRendering);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		var cam = renderingData.cameraData.camera;
		var settings = cam.GetComponent<CommandBufferSettings>();
		if (settings == null)
			return;

		if (settings.HasCommands(RenderPassEvent.BeforeRendering))
			renderer.EnqueuePass(beforeRenderingPass);

		if (settings.HasCommands(RenderPassEvent.BeforeRenderingOpaques))
			renderer.EnqueuePass(beforeRenderingOpaquesPass);

		if (settings.HasCommands(RenderPassEvent.AfterRenderingTransparents))
			renderer.EnqueuePass(afterRenderingTransparentsPass);

		if (settings.HasCommands(RenderPassEvent.AfterRendering))
			renderer.EnqueuePass(afterRenderingPass);
	}

	protected override void Dispose(bool disposing)
	{
	}
}