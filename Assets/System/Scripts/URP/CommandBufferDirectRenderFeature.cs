using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public interface IDirectCommandProvider
	{
		bool HasCommands(RenderPassEvent evt);
		void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera);
	}

	public class DirectCommandBufferPass : ScriptableRenderPass
	{
		private string passNameStr;

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

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameStr, out var passData))
			{
				passData.camera = cam;
				passData.provider = provider;

				var colorTarget = resourceData.cameraColor;
				var depthTarget = resourceData.cameraDepth;

				if (!colorTarget.IsValid())
				{
					Debug.LogError($"{passNameStr}: Invalid color target for Camera={cam.name}");
					return;
				}

				if (renderPassEvent != RenderPassEvent.BeforeRendering && !depthTarget.IsValid())
				{
					Debug.LogError($"{passNameStr}: Invalid depth target for Camera={cam.name}");
					return;
				}

				builder.SetRenderAttachment(colorTarget, 0, AccessFlags.Write);
				if (renderPassEvent != RenderPassEvent.BeforeRendering)
					builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);

				builder.AllowPassCulling(false);
				builder.AllowGlobalStateModification(true);

				builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
				{
					data.provider.ExecuteCommands(renderPassEvent, context.cmd, data.camera);
				});
			}
		}

		private class PassData
		{
			public IDirectCommandProvider provider;
			public Camera camera;
		}
	}

	[CreateAssetMenu(menuName = "Rendering/CommandBufferDirectRenderFeature")]
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
				if (!provider.HasCommands(evt))
					continue;

				if (!renderPasses.ContainsKey(evt))
					renderPasses[evt] = new DirectCommandBufferPass(evt);

				renderer.EnqueuePass(renderPasses[evt]);
			}
		}

		protected override void Dispose(bool disposing)
		{
			renderPasses.Clear();
		}
	}
}
