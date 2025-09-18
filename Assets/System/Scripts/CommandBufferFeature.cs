using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CommandBufferPass : ScriptableRenderPass
{
	private readonly string passName;
	private readonly CommandBufferSettings.RenderPassMode mode;

	public CommandBufferPass(CommandBufferSettings.RenderPassMode mode)
	{
		this.mode = mode;
		this.passName = $"CommandBufferPass_{mode}";
		switch (mode)
		{
			case CommandBufferSettings.RenderPassMode.BeforeRendering:
				renderPassEvent = RenderPassEvent.BeforeRendering;
				break;
			case CommandBufferSettings.RenderPassMode.BeforeRenderingOpaques:
				renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
				break;
			case CommandBufferSettings.RenderPassMode.AfterRenderingTransparents:
				renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
				break;
			case CommandBufferSettings.RenderPassMode.AfterRendering:
				renderPassEvent = RenderPassEvent.AfterRendering;
				break;
			default:
				Debug.LogError($"CommandBufferPass: Invalid mode {mode}");
				renderPassEvent = RenderPassEvent.AfterRendering;
				break;
		}
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

			var colorTarget = mode == CommandBufferSettings.RenderPassMode.BeforeRendering
				? resourceData.activeColorTexture
				: resourceData.cameraColor;
			var depthTarget = resourceData.cameraDepth;

			if (!colorTarget.IsValid() || (mode != CommandBufferSettings.RenderPassMode.BeforeRendering && !depthTarget.IsValid()))
			{
				Debug.LogError($"{passName}: Invalid render targets: Color={colorTarget.IsValid()}, Depth={depthTarget.IsValid()}, Camera={cam.name}");
				return;
			}

			builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
			if (mode != CommandBufferSettings.RenderPassMode.BeforeRendering)
				builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
			{
				// Execute registered commands
				data.bufferSettings.ExecuteCommands(mode, context.cmd, data.camera);
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
		beforeRenderingPass = new CommandBufferPass(CommandBufferSettings.RenderPassMode.BeforeRendering);
		beforeRenderingOpaquesPass = new CommandBufferPass(CommandBufferSettings.RenderPassMode.BeforeRenderingOpaques);
		afterRenderingTransparentsPass = new CommandBufferPass(CommandBufferSettings.RenderPassMode.AfterRenderingTransparents);
		afterRenderingPass = new CommandBufferPass(CommandBufferSettings.RenderPassMode.AfterRendering);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		var cam = renderingData.cameraData.camera;
		var settings = cam.GetComponent<CommandBufferSettings>();
		if (settings == null)
			return;

		if (settings.HasCommands(CommandBufferSettings.RenderPassMode.BeforeRendering))
			renderer.EnqueuePass(beforeRenderingPass);

		if (settings.HasCommands(CommandBufferSettings.RenderPassMode.BeforeRenderingOpaques))
			renderer.EnqueuePass(beforeRenderingOpaquesPass);

		if (settings.HasCommands(CommandBufferSettings.RenderPassMode.AfterRenderingTransparents))
			renderer.EnqueuePass(afterRenderingTransparentsPass);

		if (settings.HasCommands(CommandBufferSettings.RenderPassMode.AfterRendering))
			renderer.EnqueuePass(afterRenderingPass);
	}

	protected override void Dispose(bool disposing)
	{
	}
}