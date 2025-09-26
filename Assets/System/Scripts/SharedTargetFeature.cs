using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

public class SharedTargetFeature : ScriptableRendererFeature
{
	[System.Serializable]
	public class Settings
	{
		public List<Renderer> secondaryRenderers = new List<Renderer>();
		public List<Material> overrideMaterials = new List<Material>();
	}

	public Settings settings = new Settings();
	private SharedTargetPass _pass;

	class SharedTargetPass : ScriptableRenderPass
	{
		private Settings _settings;

		public SharedTargetPass(Settings settings)
		{
			_settings = settings;
			renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (_settings.secondaryRenderers.Count == 0)
				return;

			var resources = frameData.Get<UniversalResourceData>();

			using (var builder = renderGraph.AddUnsafePass<PassData>("SecondaryObjectsIntoMain", out var passData))
			{
				passData.renderers = _settings.secondaryRenderers;
				passData.overrideMaterials = _settings.overrideMaterials;

				builder.UseTexture(resources.activeColorTexture, AccessFlags.ReadWrite);
				builder.UseTexture(resources.activeDepthTexture, AccessFlags.ReadWrite);

				builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
				{
					// Set main camera RT
					ctx.cmd.SetRenderTarget(resources.activeColorTexture, resources.activeDepthTexture);

					for (int i = 0; i < data.renderers.Count; i++)
					{
						Renderer r = data.renderers[i];
						if (r == null) continue;

						Material mat = (i < data.overrideMaterials.Count) ? data.overrideMaterials[i] : null;
						if (mat != null)
							ctx.cmd.DrawRenderer(r, mat);
						else
							ctx.cmd.DrawRenderer(r, r.sharedMaterial);
					}
				});
			}
		}

		class PassData
		{
			public List<Renderer> renderers;
			public List<Material> overrideMaterials;
		}
	}

	public override void Create()
	{
		_pass = new SharedTargetPass(settings);
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		renderer.EnqueuePass(_pass);
	}
}


//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine.Rendering.RenderGraphModule;

//public class SharedTargetFeature : ScriptableRendererFeature
//{
//	[System.Serializable]
//	public class Settings
//	{
//		public Camera secondaryCamera; // assign at runtime
//	}

//	public Settings settings = new Settings();
//	private SharedTargetPass _pass;

//	class SharedTargetPass : ScriptableRenderPass
//	{
//		private Settings _settings;

//		public SharedTargetPass(Settings settings)
//		{
//			_settings = settings;
//			renderPassEvent = RenderPassEvent.BeforeRendering;
//		}

//		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//		{
//			Camera secondary = _settings.secondaryCamera;
//			if (secondary == null)
//				return; // camera not assigned yet

//			var resources = frameData.Get<UniversalResourceData>();

//			using (var builder = renderGraph.AddUnsafePass<PassData>("SecondaryCameraIntoMain", out var passData))
//			{
//				passData.secondaryCamera = secondary;

//				// Bind main camera RTs
//				builder.UseTexture(resources.activeColorTexture, AccessFlags.ReadWrite);
//				builder.UseTexture(resources.activeDepthTexture, AccessFlags.ReadWrite);

//				builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
//				{
//					if (data.secondaryCamera != null)
//					{
//						// Render the secondary camera into the main camera RT
//						//ctx.cmd.RenderCamera(data.secondaryCamera);//this doesn't compile!!!!!

//						// Use cmd to set render target
//						ctx.cmd.SetRenderTarget(resources.activeColorTexture);
//						ctx.cmd.ClearRenderTarget(true, true, Color.clear);

//						// Render the secondary camera
//						ctx.cmd.DrawRenderer(data.secondaryCamera.GetComponent<Renderer>(), data.secondaryCamera.GetComponent<Material>());

//					}
//				});
//			}
//		}

//		class PassData
//		{
//			public Camera secondaryCamera;
//		}
//	}

//	public override void Create()
//	{
//		_pass = new SharedTargetPass(settings);
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		renderer.EnqueuePass(_pass);
//	}
//}



//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine.Rendering.RenderGraphModule;

//public class SharedTargetFeature : ScriptableRendererFeature
//{
//	class SharedTargetPass : ScriptableRenderPass
//	{
//		private Camera _secondaryCamera;

//		Settings _settings;

//		public SharedTargetPass(Settings settings)
//		{
//			_settings = settings;
//			renderPassEvent = RenderPassEvent.BeforeRendering;
//		}

//		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
//		{
//			//var camData = frameData.Get<UniversalCameraData>();
//			//if (camData.camera != _secondaryCamera)
//			//	return;

//			if (_settings.secondaryCamera == null)
//				return; // camera not assigned yet

//			var resources = frameData.Get<UniversalResourceData>();

//			// Create unsafe pass
//			using (var builder = renderGraph.AddUnsafePass<PassData>("SecondaryCameraIntoMain", out var passData))
//			{
//				//passData.secondaryCamera = _secondaryCamera;
//				passData.secondaryCamera = _settings.secondaryCamera;

//				// Bind main camera’s RTs
//				builder.UseTexture(resources.activeColorTexture, AccessFlags.ReadWrite);
//				builder.UseTexture(resources.activeDepthTexture, AccessFlags.ReadWrite);

//				// Execute: render secondary camera
//				builder.SetRenderFunc(
//					(PassData data, UnsafeGraphContext ctx) =>
//					{
//						if (data.secondaryCamera != null)
//						{
//							// Use cmd to set render target
//							ctx.cmd.SetRenderTarget(resources.activeColorTexture);
//							ctx.cmd.ClearRenderTarget(true, true, Color.clear);

//							// Render the secondary camera
//							ctx.cmd.DrawRenderer(data.secondaryCamera.GetComponent<Renderer>(), data.secondaryCamera.GetComponent<Material>());
//						}
//					});
//			}
//		}

//		class PassData
//		{
//			public Camera secondaryCamera;
//		}
//	}

//	[System.Serializable]
//	public class Settings
//	{
//		public Camera secondaryCamera;
//	}

//	public Settings settings = new Settings();
//	private SharedTargetPass _pass;

//	public override void Create()
//	{
//		//_pass = new SharedTargetPass(settings.secondaryCamera);
//		_pass = new SharedTargetPass(settings);
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		renderer.EnqueuePass(_pass);
//	}
//}
