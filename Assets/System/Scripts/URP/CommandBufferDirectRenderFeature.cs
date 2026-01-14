using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public interface IDirectCommandProvider
	{
		TestCommandCamera testCommandCamera { get; }
		bool HasCommands(RenderPassEvent evt);
		void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, TestCommandCamera camera);
	}

	[CreateAssetMenu(menuName = "Rendering/CommandBufferDirectRenderFeature")]
	public class CommandBufferDirectRenderFeature : ScriptableRendererFeature
	{
		// We cache passes per event so we don't recreate them constantly
		private readonly Dictionary<RenderPassEvent, DirectCommandBufferPass> passes = new();

		public override void Create()
		{
			// Optional: you could pre-create common ones here if you want
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			var camera = renderingData.cameraData.camera;
			if (camera == null) return;

			var provider = camera.GetComponent<IDirectCommandProvider>();
			if (provider == null) return;

			// Which events do we care about? 
			// You can expand this list later
			var eventsToInject = new[]
			{
				RenderPassEvent.BeforeRendering,
                // RenderPassEvent.AfterRenderingOpaques,
                // RenderPassEvent.BeforeRenderingPostProcessing,
                // etc.
            };

			foreach (var evt in eventsToInject)
			{
				if (provider.HasCommands(evt))
				{
					if (!passes.TryGetValue(evt, out var pass))
					{
						pass = new DirectCommandBufferPass(evt);
						passes[evt] = pass;
					}

					renderer.EnqueuePass(pass);
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			passes.Clear();
			base.Dispose(disposing);
		}

		// ────────────────────────────────────────────────────────────────
		//                      The actual render pass
		// ────────────────────────────────────────────────────────────────
		private class DirectCommandBufferPass : ScriptableRenderPass
		{
			private readonly string passName;

			public DirectCommandBufferPass(RenderPassEvent evt)
			{
				renderPassEvent = evt;
				passName = $"DirectCmd_{evt}";
				profilingSampler = new ProfilingSampler(passName);
			}

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (!frameData.Contains<UniversalResourceData>() ||
					!frameData.Contains<UniversalCameraData>())
					return;

				var resources = frameData.Get<UniversalResourceData>();
				var camData = frameData.Get<UniversalCameraData>();
				var camera = camData.camera;

				if (camera == null) return;

				var provider = camera.GetComponent<IDirectCommandProvider>();
				if (provider == null || !provider.HasCommands(renderPassEvent))
					return;

				// ───────────────────────────────────────────────
				// Try to get a meaningful TestCommandCamera
				// Order of preference:
				// 1. Component on the same object (most explicit control)
				// 2. Fallback to something derived from real camera
				// 3. Minimal safe default (worst case)
				// ───────────────────────────────────────────────
				//TestCommandCamera commandCam = null;

				var commandCam = provider.testCommandCamera;

				using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
				{
					passData.provider = provider;
					passData.commandCamera = commandCam;
					passData.renderPassEvent = renderPassEvent;

					builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
					builder.AllowPassCulling(false);
					builder.AllowGlobalStateModification(true);

					builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
					{
						if (data.provider != null)
						{
							// Pass the camera we prepared (or the one the provider might override internally)
							data.provider.ExecuteCommands(
								data.renderPassEvent,
								ctx.cmd,
								data.commandCamera);
						}
					});
				}
			}

			private class PassData
			{
				public IDirectCommandProvider provider;
				public TestCommandCamera commandCamera;
				public RenderPassEvent renderPassEvent;
			}
		}
	}
}