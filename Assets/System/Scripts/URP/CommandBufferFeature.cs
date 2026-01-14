using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public interface ICommandBufferProvider
	{
		bool HasCommands(RenderPassEvent evt);
		void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera);
	}

	public class CommandBufferPass : ScriptableRenderPass
	{
		private string passNameStr;

		public CommandBufferPass(RenderPassEvent renderEvent)
		{
			passNameStr = $"CommandBufferPass_{renderEvent}";
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

			using (var builder = renderGraph.AddRasterRenderPass<PassData>(passNameStr, out var passData))
			{
				passData.camera = cam;
				passData.provider = provider;

				var colorTarget = resourceData.cameraColor; // Use cameraColor consistently
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
				//Debug.Log($"No ICommandBufferProvider found for {cam.name}");
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
					//Debug.Log($"Enqueued CommandBufferPass for {cam.name} at {evt}");
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			renderPasses.Clear();
		}
	}
}