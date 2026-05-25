using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public interface IDirectCommandProvider
	{
		bool HasCommands(RenderPassEvent evt);
		bool RequiresColorTexture(RenderPassEvent evt);
		void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera);
	}

	public class DirectCommandBufferPass : ScriptableRenderPass
	{
		private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
		private readonly string passNameStr;

		public DirectCommandBufferPass(RenderPassEvent renderEvent)
		{
			passNameStr = $"DirectCommandBufferPass_{renderEvent}";
			renderPassEvent = renderEvent;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

			Camera cam = cameraData.camera;
			IDirectCommandProvider provider = cam.GetComponent<IDirectCommandProvider>();

			if (provider == null || !provider.HasCommands(renderPassEvent))
				return;

			TextureHandle sourceColor = TextureHandle.nullHandle;
			if (provider.RequiresColorTexture(renderPassEvent))
			{
				var sourceDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
				sourceDesc.name = $"{passNameStr}_SourceColor";
				sourceDesc.clearBuffer = false;

				sourceColor = renderGraph.CreateTexture(sourceDesc);
				renderGraph.AddBlitPass(resourceData.activeColorTexture, sourceColor, Vector2.one, Vector2.zero, passName: $"{passNameStr}_CopySource");
			}

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameStr, out var passData))
			{
				passData.camera = cam;
				passData.provider = provider;
				passData.colorSource = sourceColor;

				var colorTarget = resourceData.activeColorTexture;
				var depthTarget = resourceData.cameraDepth;

				if (provider.RequiresColorTexture(renderPassEvent))
				{
					builder.UseTexture(passData.colorSource, AccessFlags.Read);
				}

				builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
				if (renderPassEvent != RenderPassEvent.BeforeRendering)
					builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);

				builder.AllowPassCulling(false);
				builder.AllowGlobalStateModification(true);

				builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
				{
					if (data.colorSource.IsValid())
						context.cmd.SetGlobalTexture(BlitTextureId, data.colorSource);

					data.provider.ExecuteCommands(renderPassEvent, context.cmd, data.camera);
				});
			}
		}

		private class PassData
		{
			public IDirectCommandProvider provider;
			public Camera camera;
			public TextureHandle colorSource;
		}
	}

	[CreateAssetMenu(menuName = "Rendering/CommandBufferDirectFeature")]
	public class CommandBufferDirectRenderFeature : ScriptableRendererFeature
	{
		private Dictionary<RenderPassEvent, DirectCommandBufferPass> renderPasses;

		public override void Create()
		{
			renderPasses = new Dictionary<RenderPassEvent, DirectCommandBufferPass>();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			var cam = renderingData.cameraData.camera;

			var provider = cam.GetComponent<IDirectCommandProvider>();
			if (provider == null)
				return;

			foreach (RenderPassEvent evt in System.Enum.GetValues(typeof(RenderPassEvent)))
			{
				if (provider.HasCommands(evt))
				{
					if (!renderPasses.ContainsKey(evt))
						renderPasses[evt] = new DirectCommandBufferPass(evt);

					renderer.EnqueuePass(renderPasses[evt]);
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			renderPasses?.Clear();
		}
	}
}
