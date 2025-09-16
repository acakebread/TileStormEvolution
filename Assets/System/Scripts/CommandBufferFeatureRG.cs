using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CommandBufferPassRG : ScriptableRenderPass
{
	private readonly string passName;
	private readonly CommandBufferSettingsRG.RenderPassMode mode;

	public CommandBufferPassRG(CommandBufferSettingsRG.RenderPassMode mode)
	{
		this.mode = mode;
		this.passName = $"CommandBufferPassRG_{mode}";
		switch (mode)
		{
			case CommandBufferSettingsRG.RenderPassMode.BeforeRendering:
				renderPassEvent = RenderPassEvent.BeforeRendering;
				break;
			case CommandBufferSettingsRG.RenderPassMode.BeforeRenderingOpaques:
				renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
				break;
			case CommandBufferSettingsRG.RenderPassMode.AfterRenderingTransparents:
				renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
				break;
			case CommandBufferSettingsRG.RenderPassMode.AfterRendering:
				renderPassEvent = RenderPassEvent.AfterRendering;
				break;
			default:
				Debug.LogError($"CommandBufferPassRG: Invalid mode {mode}");
				renderPassEvent = RenderPassEvent.AfterRendering;
				break;
		}
	}

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

		Camera cam = cameraData.camera;
		CommandBufferSettingsRG bufferSettings = cam.GetComponent<CommandBufferSettingsRG>();

		// Skip cameras without CommandBufferSettingsRG
		if (bufferSettings == null)
			return;

		using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
		{
			passData.camera = cam;
			passData.bufferSettings = bufferSettings;

			var colorTarget = mode == CommandBufferSettingsRG.RenderPassMode.BeforeRendering
				? resourceData.activeColorTexture
				: resourceData.cameraColor;
			var depthTarget = resourceData.cameraDepth;

			if (!colorTarget.IsValid() || (mode != CommandBufferSettingsRG.RenderPassMode.BeforeRendering && !depthTarget.IsValid()))
			{
				Debug.LogError($"{passName}: Invalid render targets: Color={colorTarget.IsValid()}, Depth={depthTarget.IsValid()}, Camera={cam.name}");
				return;
			}

			builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
			if (mode != CommandBufferSettingsRG.RenderPassMode.BeforeRendering)
				builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
			{
				data.bufferSettings.ExecuteCommands(mode, context.cmd, data.camera);
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

[CreateAssetMenu(menuName = "Rendering/CommandBufferFeatureRG")]
public class CommandBufferFeatureRG : ScriptableRendererFeature
{
	private CommandBufferPassRG beforeRenderingPass;
	private CommandBufferPassRG beforeRenderingOpaquesPass;
	private CommandBufferPassRG afterRenderingTransparentsPass;
	private CommandBufferPassRG afterRenderingPass;

	public override void Create()
	{
		beforeRenderingPass = new CommandBufferPassRG(CommandBufferSettingsRG.RenderPassMode.BeforeRendering);
		beforeRenderingOpaquesPass = new CommandBufferPassRG(CommandBufferSettingsRG.RenderPassMode.BeforeRenderingOpaques);
		afterRenderingTransparentsPass = new CommandBufferPassRG(CommandBufferSettingsRG.RenderPassMode.AfterRenderingTransparents);
		afterRenderingPass = new CommandBufferPassRG(CommandBufferSettingsRG.RenderPassMode.AfterRendering);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		renderer.EnqueuePass(beforeRenderingOpaquesPass); // For ReflectionCamera SetInvertCulling
		renderer.EnqueuePass(afterRenderingTransparentsPass); // For future use
		renderer.EnqueuePass(afterRenderingPass); // For DimOverlay and SetInvertCulling reset
												  // beforeRenderingPass disabled unless needed
	}

	protected override void Dispose(bool disposing)
	{
	}
}